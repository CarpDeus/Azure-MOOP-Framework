using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;


using Finsel.AzureCommands;
using System.Collections.Specialized;

using System.Xml;

namespace MOOPWorkerRole
{
  public class WorkerRole : RoleEntryPoint
  {

    string blobConfig = string.Empty;
    public string blobWebConfig
    {
      get
      {
        return blobConfig;
      }
      set
      {

        {
          blobConfig = value;
        }
      }
    }

    int sleepDuration = 30000;
    string azureAccount = string.Empty;
    string azureEndpoint = string.Empty;
    string azureSharedKey = string.Empty;
    string queueTypeOfCall = string.Empty;
    string message = string.Empty;

    string queueName = string.Empty;
    string messageID = string.Empty;
    string popReceipt = string.Empty;
    string qParameters = string.Empty;
    public override void Run()
    {
      // This is a sample worker implementation. Replace with your logic.
      Trace.WriteLine("AzureArchitectWorker entry point called", "Information");

      while (true)
      {
        Finsel.AzureCommands.AzureQueueStorage aqs = new Finsel.AzureCommands.AzureQueueStorage(azureAccount, string.Format("http://{0}.blob.core.windows.net", azureAccount), azureSharedKey, "SharedKey");
        azureResults ar = aqs.Messages(cmdType.get, queueName, string.Empty, string.Empty, string.Empty);
        if (ar.Succeeded)
        {
          System.Xml.XmlDocument xdoc = new System.Xml.XmlDocument();
          xdoc.LoadXml(ar.Body);
          System.Xml.XmlNodeList nodes = xdoc.SelectNodes("//QueueMessage");
          if (nodes.Count != 0)
          {
            foreach (System.Xml.XmlNode node in nodes)
            {
              string popReceipt = string.Empty;
              string messageData = string.Empty;
              string messageID = node.SelectSingleNode("MessageId").InnerText;
              if (node.SelectNodes("//PopReceipt").Count > 0)
                popReceipt = node.SelectSingleNode("PopReceipt").InnerText;
              if (node.SelectNodes("//MessageText").Count > 0)
                messageData = node.SelectSingleNode("MessageText").InnerText;

              if (messageData != string.Empty)
              {

                string queryName = string.Empty;

                NameValueCollection htKeys = new NameValueCollection();

                foreach (string messageDetail in messageData.Split("\r\n".ToCharArray()))
                {
                  if (messageDetail.Contains(":"))
                    htKeys.Add(messageDetail.Substring(0, messageDetail.IndexOf(":")), messageDetail.Substring(messageDetail.IndexOf(":") + 1));
                  else
                    if (messageDetail != string.Empty)
                      queryName = messageDetail;
                }
                quoteSearchLoader qls = new quoteSearchLoader();

                if (htKeys.Count == 0 || queryName.ToLower() == "optimize")
                  aqs.Messages(cmdType.delete, queueName, string.Empty, string.Format("popreceipt={0}", popReceipt), messageID);
                if (queryName.ToLower() == "optimize")
                {
                  qls.optimizeIndex(htKeys[0]);
                }
                else
                {
                  qls.processRequest(queryName, htKeys);
                }
                if (htKeys.Count != 0)
                  aqs.Messages(cmdType.delete, queueName, string.Empty, string.Format("popreceipt={0}", popReceipt), messageID);
              }
              else
                aqs.Messages(cmdType.delete, queueName, string.Empty, string.Format("popreceipt={0}", popReceipt), messageID);

            }
          }
        }

        fbUserProcessor fbup = new fbUserProcessor(azureAccount, azureSharedKey, string.Format("http://{0}.queue.core.windows.net/", azureAccount), new Utility().ResolveDataConnection("sqlAzureConnection"));
        fbup.ProcessMessage("fbprocessing");
        Thread.Sleep(sleepDuration);

      }
    }

    public override bool OnStart()
    {
      // Set the maximum number of concurrent connections 
      ServicePointManager.DefaultConnectionLimit = 12;

      DiagnosticMonitor.Start("DiagnosticsConnectionString");
      TimeSpan tsOneMinute = TimeSpan.FromMinutes(30);
      DiagnosticMonitorConfiguration dmc = DiagnosticMonitor.GetDefaultInitialConfiguration();
      // Transfer logs to storage every minute
      dmc.Logs.ScheduledTransferPeriod = tsOneMinute;

      // Transfer verbose, critical, etc. logs
      dmc.Logs.ScheduledTransferLogLevelFilter = LogLevel.Information;

      // Start up the diagnostic manager with the given configuration
      DiagnosticMonitor.Start("DiagnosticsConnectionString", dmc);

      // For information on handling configuration changes
      // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
      RoleEnvironment.Changing += RoleEnvironmentChanging;

      try
      {
        sleepDuration = Convert.ToInt32(RoleEnvironment.GetConfigurationSettingValue("SleepDuration"));

        XmlDocument xdoc = new XmlDocument();
        xdoc.LoadXml(new Utility().getConfigXML());
        XmlNode xNode = xdoc.SelectSingleNode(string.Format("//blobData[@name='default']"));

        azureAccount = xNode.SelectSingleNode("Setting[@name='account']").Attributes["value"].Value;
        azureEndpoint = xNode.SelectSingleNode("Setting[@name='endpoint']").Attributes["value"].Value;
        azureSharedKey = xNode.SelectSingleNode("Setting[@name='accountSharedKey']").Attributes["value"].Value;

        //azureAccount = RoleEnvironment.GetConfigurationSettingValue("AccountName");
        //azureEndpoint = RoleEnvironment.GetConfigurationSettingValue("AccountName");
        //azureSharedKey = RoleEnvironment.GetConfigurationSettingValue("AccountSharedKey");
        queueName = RoleEnvironment.GetConfigurationSettingValue("queueName");
      }
      catch (Exception ex)
      {
        Trace.WriteLine(ex.ToString(), "Information");
      }
      return base.OnStart();
    }

    private void ProcessInformation(string messageText)
    {
      Trace.WriteLine(messageText, "Information");
    }

    private void ProcessErrors(Exception ex)
    {
      Trace.WriteLine(ex.ToString(), "Information");
    }

    private void RoleEnvironmentChanging(object sender, RoleEnvironmentChangingEventArgs e)
    {
      // If a configuration setting is changing
      if (e.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange))
      {
        // Set e.Cancel to true to restart this role instance
        e.Cancel = true;
      }
    }
  }
}
