using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Protocol.Text;
using Microsoft.WindowsAzure.ServiceRuntime;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

public static partial class WindowsAzureMemcachedHelpers
{
    public static MemcachedClient CreateDefaultClient(string memcachedRoleName, string memcachedEndpointName)
    {
        return new MemcachedClient(new WindowsAzureServerPool(memcachedRoleName, memcachedEndpointName), new DefaultKeyTransformer(), new DefaultTranscoder());
    }
}
public class WindowsAzureServerPool : IServerPool, IDisposable
{
    private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(WindowsAzureServerPool));

    private Dictionary<string, IMemcachedNode> nodes; // keys are Windows Azure instance IDs

    private IMemcachedClientConfiguration configuration;
    private IOperationFactory factory;
    private IMemcachedNodeLocator nodeLocator;

    private object PollSync = new Object(); // used to lock around modifying the list of nodes
    private Timer pollingTimer;
    private const int pollingInterval = 30000; // 30 seconds

    private bool isDisposed;
    private string memcachedRoleName, memcachedEndpointName;

    public WindowsAzureServerPool() : this("MemcachedRole", "Memcached") { }

    public WindowsAzureServerPool(string memcachedRoleName, string memcachedEndpointName) :
        this(memcachedRoleName, memcachedEndpointName, ConfigurationManager.GetSection("enyim.com/memcached") as MemcachedClientSection, new TextOperationFactory()) { }

    public WindowsAzureServerPool(string memcachedRoleName, string memcachedEndpointName, IMemcachedClientConfiguration configuration, IOperationFactory opFactory)
    {
        this.memcachedRoleName = memcachedRoleName;
        this.memcachedEndpointName = memcachedEndpointName;
        configuration = configuration ?? new MemcachedClientConfiguration();
        if (opFactory == null) throw new ArgumentNullException("opFactory");

        nodes = new Dictionary<string, IMemcachedNode>();

        this.configuration = configuration;
        this.factory = opFactory;
    }

    ~WindowsAzureServerPool()
    {
        try { ((IDisposable)this).Dispose(); }
        catch { }
    }

    private void Poll(object state)
    {
        lock (PollSync) // pattern taken from DefaultNodeLocator, presumably to avoid two threads from colliding in here
        {
            var instances = RoleEnvironment.Roles[memcachedRoleName].Instances.ToList(); // potential memcached servers according to Windows Azure

            var changed = false;

            foreach (var instance in instances)
            {
                if (!nodes.ContainsKey(instance.Id)) // new server
                {
                    var endpoint = instance.InstanceEndpoints[memcachedEndpointName].IPEndpoint;

                    // if and only if server's alive (accepts socket connection)
                    using (var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                    {
                        try
                        {
                            socket.Connect(endpoint);
                            if (socket.Connected)
                            {
                                socket.Disconnect(false);

                                // add the server
                                nodes[instance.Id] = new MemcachedNode(endpoint, this.configuration.SocketPool);
                                nodes[instance.Id].Failed += Poll;
                                changed = true;
                            }
                        }
                        catch (SocketException) { }
                    }
                }
            }

            foreach (var key in nodes.Keys.ToList())
            {
                // prune dead nodes (either found to be dead by trying to use them or by dropping off Windows Azure's list)
                if (!nodes[key].IsAlive || !instances.Any(i => i.Id == key))
                {
                    nodes[key].Failed -= Poll;
                    nodes.Remove(key);
                    changed = true;
                }
            }

            if (changed)
            {
                // Note: Enyim documentation says it's important to always use the same order (for consistent hashing)
                nodeLocator.Initialize(nodes.OrderBy(p => p.Key).Select(p => p.Value).ToList());
            }
        }
    }

    public IMemcachedNode Locate(string key) { return nodeLocator.Locate(key); }
    public IOperationFactory OperationFactory { get { return factory; } }
    public IEnumerable<IMemcachedNode> GetWorkingNodes() { return nodeLocator.GetWorkingNodes(); }

    public void Start()
    {
        nodeLocator = configuration.CreateNodeLocator();
        nodes = new Dictionary<string, IMemcachedNode>();
        nodeLocator.Initialize(new List<IMemcachedNode>());
        Poll(null); // seed it once before returning from Start()
        pollingTimer = new Timer(Poll, null, pollingInterval, pollingInterval);
    }

    // Largely taken from DefaultNodeLocator
    void IDisposable.Dispose()
    {
        GC.SuppressFinalize(this);

        lock (PollSync)
        {
            if (isDisposed) return;

            isDisposed = true;

            var nd = nodeLocator as IDisposable;
            if (nd != null)
            {
                try { nd.Dispose(); }
                catch (Exception e) { if (log.IsErrorEnabled) log.Error(e); }
            }

            nodeLocator = null;

            foreach (var node in nodes.Values)
            {
                try { node.Dispose(); }
                catch (Exception e) { if (log.IsErrorEnabled) log.Error(e); }
            }

            // stop the timer
            if (pollingTimer != null)
                using (pollingTimer)
                    pollingTimer.Change(Timeout.Infinite, Timeout.Infinite);

            nodes = null;
            pollingTimer = null;
        }
    }
}