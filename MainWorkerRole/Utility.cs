﻿using System;
using System.Data;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Xml.Linq;

using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Text;
using System.Data.SqlClient;
using Microsoft.WindowsAzure.ServiceRuntime;
using Finsel.AzureCommands;
using System.Xml;
using Enyim.Caching;


namespace MainWorkerRole
{
  public class Utility
  {

    static MemcachedClient client = WindowsAzureMemcachedHelpers.CreateDefaultClient("MainWorkerRole", "memcacheWorker");

    public Utility()
    { }



    public string getConfigXML()
    {

      string retVal = client.Get("config") as string;
        if (retVal == null || retVal == string.Empty )
        {

          string diagnosticsConnection = RoleEnvironment.GetConfigurationSettingValue("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString");
            string azureAccount = string.Empty;
            string azureEndpoint = string.Empty;
            string azureSharedKey = string.Empty;
            string defaultEndpointsProtocol = string.Empty;
            string configBlobContainer = RoleEnvironment.GetConfigurationSettingValue("configurationContainer");
            foreach (string item in diagnosticsConnection.Split(";".ToCharArray()))
            {
                string[] parsedItem = item.Split("=".ToCharArray());
                switch (parsedItem[0])
                {
                    case "AccountKey": azureSharedKey = parsedItem[1];
                        for (int i = 2; i < parsedItem.Length; i++)
                            azureSharedKey += "=";
                        break;
                    case "AccountName": azureAccount = parsedItem[1]; break;
                    case "DefaultEndpointsProtocol": defaultEndpointsProtocol = parsedItem[1]; break;
                    case "Endpoint": azureEndpoint = parsedItem[1]; break;
                    default: break;
                }
            }

            if (azureEndpoint == string.Empty)
                azureEndpoint = string.Format("{0}://{1}.blob.core.windows.net/", defaultEndpointsProtocol, azureAccount);

            byte[] xmlFragment = null;
            Finsel.AzureCommands.AzureBlobStorage abs = new Finsel.AzureCommands.AzureBlobStorage(azureAccount, azureEndpoint, azureSharedKey, "SharedKey");
            azureResults ar = new azureResults();
            xmlFragment = abs.GetBlob(configBlobContainer, "configuration.xml", "", ref ar);
            System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
            retVal = enc.GetString(xmlFragment);
            client.Store(Enyim.Caching.Memcached.StoreMode.Set, "config", retVal, new TimeSpan(0,10,0));
        }
      return retVal;
    }

    public string ResolveDataConnection(string connectionName)
    {
      string retVal = string.Empty;
      XmlDocument xdoc = new XmlDocument();
      xdoc.LoadXml(getConfigXML());
      XmlNode xNode = xdoc.SelectSingleNode(string.Format("//connectionStrings/Setting[@name='{0}']", connectionName));
      if (xNode != null)
        retVal = xNode.Attributes["value"].Value;
      return retVal;
    }
  }
}
