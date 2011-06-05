using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using System.Diagnostics;

using Finsel.AzureCommands;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.IO;
using Amundsen.Utilities;
using System.Data.SqlClient;

namespace MOOPWorkerRole
{
  public class fbUserProcessor
  {

    /// <summary>
    ///  Declare a delegate that takes a single string parameter and has no return type.
    /// </summary>
    /// <param name="message">Message to display</param>
    /// <param name="LogOnly">Whether this message should show up in the log only</param>
    public delegate void LogHandler(string message, bool LogOnly);

    /// <summary>
    ///  Declare a delegate that takes a single string parameter and has no return type.
    /// </summary>
    /// <param name="ex">The Exception</param>
    public delegate void ErrorHandler(Exception ex);


    /// <summary>
    /// Log handler for notifications to the client
    /// </summary>
    private LogHandler mvLogHandler;

    /// <summary>
    /// Log handler for notifications to the client
    /// </summary>
    private ErrorHandler mvErrorHandler;

    /// <summary>
    /// Default notifycaller is not log only
    /// </summary>
    /// <param name="msg">Message to display</param>
    void NotifyCaller(string msg)
    {
      if (mvLogHandler != null)
        mvLogHandler(msg, false);
      Trace.TraceInformation(msg);

    }

    /// <summary>
    /// Default notifyError is not log only
    /// </summary>
    /// <param name="msg">Message to display</param>
    void NotifyError(Exception ex)
    {
      if (mvErrorHandler != null)
        mvErrorHandler(ex);

      Trace.WriteLine(ex.ToString());
    }


    string queueName = string.Empty;
    string azureAppName = string.Empty;
    string azureSharedKey = string.Empty;
    string azureEndPoint = string.Empty;
    string azureSQLConnection = string.Empty;

    private void LoadFriendList(string UserID, string XMLData)
    {
      SqlCommand cmd = new SqlCommand("dbo.AddUserRelationships", new SqlConnection(azureSQLConnection));
      cmd.CommandType = System.Data.CommandType.StoredProcedure;
      cmd.Parameters.Add("@UserID", System.Data.SqlDbType.NVarChar, 255);
      cmd.Parameters.Add("@FriendXML", System.Data.SqlDbType.Xml);
      cmd.Parameters["@UserID"].Value = UserID;
      cmd.Parameters["@FriendXML"].Value = XMLData;
      cmd.Connection.Open();
      cmd.ExecuteNonQuery();
      cmd.Connection.Close();
    }

    private void UpdateUserDataSocialInformation(string UserID, string XMLData)
    {
      SqlCommand cmd = new SqlCommand("dbo.UpdateUserDataSocialInformation", new SqlConnection(azureSQLConnection));
      cmd.CommandType = System.Data.CommandType.StoredProcedure;
      cmd.Parameters.Add("@UserID", System.Data.SqlDbType.NVarChar, 255);
      cmd.Parameters.Add("@FriendXML", System.Data.SqlDbType.Xml);
      cmd.Parameters["@UserID"].Value = UserID;
      cmd.Parameters["@FriendXML"].Value = XMLData;
      cmd.Connection.Open();
      cmd.ExecuteNonQuery();
      cmd.Connection.Close();
    }

    private void LoadUserInformation(string UserID, string XMLData)
    {
    }

    public fbUserProcessor(string AppName, string SharedKey, string EndPoint, string sqlConnectionString)
    {
      azureAppName = AppName;
      azureSharedKey = SharedKey;
      azureEndPoint = EndPoint;
      azureSQLConnection = sqlConnectionString;
    }

    public void ProcessMessage(string fbQueue)
    {
      AzureQueueStorage aqs = new AzureQueueStorage(azureAppName, string.Format("http://{0}.queue.core.windows.net", azureAppName), azureSharedKey, "SharedKey");
      azureResults ar = aqs.Messages(cmdType.get, fbQueue, "", "", "");
      string messageID = string.Empty;
      string popReceipt = string.Empty;
      string message = string.Empty;

      while (ar.Succeeded)
      {
        if (ar.Body != null)
        {
          System.Xml.XmlDocument xdoc = new System.Xml.XmlDocument();
          xdoc.LoadXml(ar.Body);
          System.Xml.XmlNodeList nodes = xdoc.SelectNodes("//QueueMessage");
          if (nodes.Count != 0)
          {
            foreach (System.Xml.XmlNode node in nodes)
            {
              messageID = node.SelectSingleNode("MessageId").InnerText;
              if (node.SelectNodes("//PopReceipt").Count > 0)
                popReceipt = node.SelectSingleNode("PopReceipt").InnerText;
              if (node.SelectNodes("//MessageText").Count > 0)
                message = node.SelectSingleNode("MessageText").InnerText;

              xdoc.LoadXml(message);
              nodes = xdoc.SelectNodes("//fbGetUserInfo");
              foreach (XmlNode n in nodes)
              {
                string fbUserID = n.Attributes["fbUserID"].Value;
                Trace.WriteLine(string.Format("Processing fb information for user {0}", fbUserID), "Information");
                string fbOAuthToken = n.Attributes["fbOAuthToken"].Value;
                string fbauthExpires = n.Attributes["authExpires"].Value;
                if (fbUserID.StartsWith("fb"))
                  fbUserID = fbUserID.Substring(2);
                if (Convert.ToDateTime(fbauthExpires) > DateTime.UtcNow)
                {
                  string graphURL = string.Format("https://graph.facebook.com/{0}/friends/?access_token={1}", fbUserID, fbOAuthToken);
                  HttpClient ht = new HttpClient();
                  string graphInfo = ht.Execute(graphURL, "GET");
                  graphInfo = "{\"?xml\": {\"@version\": \"1.0\",\"@standalone\": \"no\"},\"root\": {" + graphInfo.Substring(1);// +"}}";
                  XmlDocument doc = (XmlDocument)JsonConvert.DeserializeXmlNode(graphInfo);
                  LoadFriendList("fb" + fbUserID, doc.InnerXml);
                  graphURL = string.Format("https://graph.facebook.com/{0}/?access_token={1}", fbUserID, fbOAuthToken);
                  graphInfo = ht.Execute(graphURL, "GET");
                  graphInfo = "{\"?xml\": {\"@version\": \"1.0\",\"@standalone\": \"no\"},\"root\": {" + graphInfo.Substring(1);// +"}}";
                  doc = (XmlDocument)JsonConvert.DeserializeXmlNode(graphInfo);
                  UpdateUserDataSocialInformation("fb" + fbUserID, doc.InnerXml);
                }
                ar = aqs.Messages(cmdType.delete, fbQueue, "", string.Format("popreceipt={0}", popReceipt), messageID);
              }
              ar = aqs.Messages(cmdType.get, fbQueue, "", "", "");
            }
          }
          else break;
        }
      }
    }
  }
}
