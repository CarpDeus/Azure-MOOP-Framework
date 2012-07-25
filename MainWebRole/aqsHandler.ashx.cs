using System;
using System.Collections;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Web.Services.Protocols;
using System.Xml.Linq;

using System.Xml;
using Finsel.AzureCommands;
using System.Text;

namespace MOOPFramework
{
  /// <summary>
  /// Summary description for $codebehindclassname$
  /// </summary>
  [WebService(Namespace = "http://tempuri.org/")]
  [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
  public class aqsHandler : IHttpHandler
  {

    public void ProcessRequest(HttpContext context)
    {
      string accountName = string.Empty;
      string sharedKey = string.Empty;
      string queueTypeOfCall = string.Empty;
      string message = string.Empty;

      string queueName = string.Empty;
      string messageID = string.Empty;
      string popReceipt = string.Empty;
      string qParameters = string.Empty;


      if (context.Request.Params.AllKeys.Contains("accountname"))
      {
        accountName = context.Request.Params["accountname"].ToString();
      }
      if (context.Request.Params.AllKeys.Contains("sharedkey"))
      {
        sharedKey = context.Request.Params["sharedkey"].ToString().Replace(" ", "+");
      }
      if (context.Request.Params.AllKeys.Contains("queuetypeofcall"))
      {
        queueTypeOfCall = context.Request.Params["queuetypeofcall"].ToString();
      }
      if (context.Request.Params.AllKeys.Contains("queuename"))
      {
        queueName = context.Request.Params["queuename"].ToString();
      }
      if (context.Request.Params.AllKeys.Contains("messageid"))
      {
        messageID = context.Request.Params["messageid"].ToString();
      }
      if (context.Request.Params.AllKeys.Contains("popreceipt"))
      {
        popReceipt = context.Request.Params["popreceipt"].ToString();
      }
      if (context.Request.Params.AllKeys.Contains("message"))
      {
        message = context.Request.Params["message"].ToString();
      }
      string retVal = string.Empty;
      string retValType = string.Empty;
      retValType = "text/xml";

      Hashtable ht = new Hashtable();

      foreach (string key in context.Request.Params.AllKeys)
        {
          if (key.StartsWith("x-ms-meta-"))
            if (ht.ContainsKey(key))
              ht[key] = string.Format("{0},{1}", ht[key].ToString(), context.Request.Params[key].ToString());
            else
              ht.Add(key, context.Request.Params[key].ToString());
        }
      
      azureResults ar = new azureResults();
      Finsel.AzureCommands.AzureQueueStorage aqs = new Finsel.AzureCommands.AzureQueueStorage(accountName,
        string.Format("http://{0}.blob.core.windows.net", accountName), sharedKey,
        "SharedKey");

      switch (queueTypeOfCall.ToLower())
      {
        case "addqueuemetadata":
          ar = aqs.MetaData(cmdType.put, queueName, ht);
          retVal = processAzureResults(ar);
          break;
        case "clearqueue":
           ar = aqs.Messages(cmdType.delete, queueName, "", "", "");
           retVal = processAzureResults(ar);
           break;
        case "createmessage":
           ar = aqs.Messages(cmdType.post, queueName, message, "", "");
           retVal = processAzureResults(ar);
           break;
        case "createqueue":
          ht.Add("x-ms-meta-CreatedBy", "Finsel.AzureCommands");
          ar = aqs.Queues(cmdType.put, queueName,qParameters , ht);
          retVal = processAzureResults(ar);
          break;
        case "deletemessage":
          qParameters = string.Format("popreceipt={0}", popReceipt);
          ar = aqs.Messages(cmdType.delete, queueName,"",qParameters , messageID);
          retVal = processAzureResults(ar);
          break;
        case "deletequeue":
          ar = aqs.Queues(cmdType.delete, queueName,qParameters, new Hashtable());
          retVal = processAzureResults(ar);
          break;
        case "deletequeuemetadata":
          ar = aqs.MetaData(cmdType.get, queueName, null);
          retVal = processAzureResults(ar);
          break;
        case "displayqueuemetadata":
          ar = aqs.MetaData(cmdType.get, queueName, null);
          retVal = processAzureResults(ar);
          break;
        case "getmessage":
          ar = aqs.Messages(cmdType.get, queueName, "", qParameters, messageID);
          retVal = processAzureResults(ar);
          break;
        case "getqueuelist":
          ar = aqs.GetQueueList(qParameters);
          retVal = processAzureResults(ar);
          break;
        case "peekmessage":
          ar = aqs.Messages(cmdType.get, queueName, "", "peekonly=true", messageID);
          retVal = processAzureResults(ar);
          break;
        
        default:
          retVal = @"<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN' 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd'>
<html xmlns='http://www.w3.org/1999/xhtml' >
<head>
    <title>Finsel Azure Queue Handler Form</title>
</head>
<body>
<form action='aqsHandler.ashx' method='post'>
<table border='1'>
<tr>
<td>Account</td><td><input name='accountname' maxlength='100' /></td>
</tr><tr>
<td>Shared Key</td><td><input name='sharedkey' maxlength='100' /></td>
</tr><tr>
<td>Container Name</td><td><input name='queuename' maxlength='100' /></td>
</tr><tr>
<td>Blob Name</td><td><input name='messageid' maxlength='1240' /></td>
</tr><tr>
<td>Parameters</td><td><input name='parameters' maxlength='1240' /></td>
</tr><tr>
<td>Type of call</td><td>
<select name='queuetypeofcall'>
<option>AddQueueMetadata</option>
    <option value='CreateQueue'>CreateContainer</option>
    <option value='DeleteMessage'>DeleteBlob</option>
    <option value='DeleteQueue'>DeleteContainer</option>
    <option value='DeleteQueueMetadata'>DeleteContainerMetadata</option>
    <option value='DisplayQueueMetadata'>DisplayContainerMetadata</option>
    <option value='GetBlob'>GetMessage</option>
    <option value='GetQueueList'>GetContainerList</option>
</td>
</tr><tr>
<td colspan='2'><input type='submit' /></td>
</tr>
</table>
</form>
</body>
</html>";
          retValType = "text/html";
         
          break;
      }

      context.Response.ContentType = retValType;
      context.Response.Write(retVal);
    }

    private string processAzureResults(azureResults ar)
    {
      StringBuilder retVal = new StringBuilder();
      retVal.Append("<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n");
      retVal.AppendFormat("<azureResults Succeeded=\"{0}\" StatusCode=\"{1}\" >", ar.Succeeded, ar.StatusCode);
      retVal.AppendFormat("<Url>{0}</Url>", System.Web.HttpUtility.HtmlEncode(ar.Url));
      retVal.AppendFormat("<CanonicalResource>{0}</CanonicalResource>", System.Web.HttpUtility.HtmlEncode(ar.CanonicalResource));
      retVal.AppendFormat("<Headers>");
      if (ar.Headers != null)
        if (ar.Headers.Keys.Count > 0)
        {
          foreach (string key in ar.Headers.Keys)
          {
            retVal.AppendFormat("<Header><Key>{0}</Key><Value>{1}</Value></Header>", key, System.Web.HttpUtility.HtmlEncode(ar.Headers[key]));
          }
        }
      retVal.AppendFormat("</Headers>");
      retVal.AppendFormat("<body>{0}</body>", System.Web.HttpUtility.HtmlEncode(ar.Body));
      retVal.Append("</azureResults>");
      return retVal.ToString();
    }


    public bool IsReusable
    {
      get
      {
        return false;
      }
    }
  }
}
