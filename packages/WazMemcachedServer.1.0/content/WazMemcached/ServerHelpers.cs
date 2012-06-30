using Microsoft.WindowsAzure.ServiceRuntime;
using System;
using System.Diagnostics;
using System.IO;

public static partial class WindowsAzureMemcachedHelpers
{
    public static Process StartMemcached(string endpointName, int maxMemory)
    {
        return StartMemcached(RoleEnvironment.CurrentRoleInstance.InstanceEndpoints[endpointName].IPEndpoint.Port, maxMemory);
    }

    public static Process StartMemcached(int port, int maxMemory)
    {
        var path = Path.Combine(
            Directory.Exists(Environment.ExpandEnvironmentVariables(@"%RoleRoot%\approot\bin"))
                ? Environment.ExpandEnvironmentVariables(@"%RoleRoot%\approot\bin\memcached") // web role
                : Environment.ExpandEnvironmentVariables(@"%RoleRoot%\approot\memcached"),    // worker role
            Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"));
        return Process.Start(new ProcessStartInfo(
            Path.Combine(path, "memcached.exe"),
            string.Format("-p {0} -m {1}", port, maxMemory))
            {
                WorkingDirectory = path
            });
    }
}