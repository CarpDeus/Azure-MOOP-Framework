using System;
using System.Collections;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Web.Services.Protocols;
using System.Xml.Linq;
using Finsel.AzureCommands;
using System.Text;

namespace MOOP_WebRole
{
  /// <summary>
  /// Summary description for $codebehindclassname$
  /// </summary>
  [WebService(Namespace = "http://tempuri.org/")]
  [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
  public class atsHandler : IHttpHandler
  {

    public void ProcessRequest(HttpContext context)
    {
      string accountName = string.Empty;
      string sharedKey = string.Empty;
      string tableTypeOfCall = string.Empty;

      string tableName = string.Empty;
      string partitionKey = string.Empty;
      string rowKey = string.Empty;
      string docData = string.Empty;
      string tParameters = string.Empty;


      if (context.Request.Params.AllKeys.Contains("accountname"))
      {
        accountName = context.Request.Params["accountname"].ToString();
      }
      if (context.Request.Params.AllKeys.Contains("sharedkey"))
      {
        sharedKey = context.Request.Params["sharedkey"].ToString().Replace(" ", "+");
      }
      if (context.Request.Params.AllKeys.Contains("tabletypeofcall"))
      {
        tableTypeOfCall = context.Request.Params["tabletypeofcall"].ToString();
      }
      if (context.Request.Params.AllKeys.Contains("tablename"))
      {
        tableName = context.Request.Params["tablename"].ToString();
      }
      if (context.Request.Params.AllKeys.Contains("partitionkey"))
      {
        partitionKey = context.Request.Params["partitionkey"].ToString();
      }
      if (context.Request.Params.AllKeys.Contains("rowkey"))
      {
        rowKey = context.Request.Params["rowkey"].ToString();
      }
      if (context.Request.Params.AllKeys.Contains("docdata"))
      {
        docData = context.Request.Params["docdata"].ToString();
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
      Finsel.AzureCommands.AzureTableStorage ats = new Finsel.AzureCommands.AzureTableStorage(accountName,
        string.Format("http://{0}.blob.core.windows.net", accountName), sharedKey,
        "SharedKey");

      switch (tableTypeOfCall.ToLower())
      {
        case "createtable": ar = ats.Tables(cmdType.post, tableName); retVal = processAzureResults(ar); break;
        case "deleteentity": ar = ats.Entities(cmdType.delete, tableName, partitionKey, rowKey, "", ""); retVal = processAzureResults(ar); break;
        case "deletetable": ar = ats.Tables(cmdType.delete, tableName); retVal = processAzureResults(ar); break;
        case "insertentity]": ar = ats.Entities(cmdType.post, tableName, partitionKey, rowKey, docData, ""); retVal = processAzureResults(ar); break;
        case "mergeentity": ar = ats.Entities(cmdType.merge, tableName, partitionKey, rowKey, docData, ""); retVal = processAzureResults(ar); break;
        case "queryentities": ar = ats.Entities(cmdType.get, tableName, partitionKey, rowKey, "", ""); retVal = processAzureResults(ar); break;
        case "querytables": ar = ats.Tables(cmdType.get, ""); retVal = processAzureResults(ar); break;
        case "updateentity": ar = ats.Entities(cmdType.put, tableName, partitionKey, rowKey, docData, ""); retVal = processAzureResults(ar); break;
        default:
          retVal = @"<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN' 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd'>
<html xmlns='http://www.w3.org/1999/xhtml' >
<head>
    <title>Finsel Azure Tables Handler Form</title>
</head>
<body>
<form action='atsHandler.ashx' method='post'>
<table border='1'>
<tr>
<td>Account</td><td><input name='accountname' maxlength='100' /></td>
</tr><tr>
<td>Shared Key</td><td><input name='sharedkey' maxlength='100' /></td>
</tr><tr>
<td>Table Name</td><td><input name='tablename' maxlength='100' /></td>
</tr><tr>
<td>Partition Key</td><td><input name='partitionkey' maxlength='100' /></td>
</tr><tr>
<td>Row key</td><td><input name='rowkey' maxlength='100' /></td>
</tr><tr>
<td>Document Data</td><td><input name='docdata' Width='398px' Height='92px' TextMode='MultiLine' /></td>
</tr><tr>
<td>Parameters</td><td><input name='parameters' maxlength='1240' /></td>
</tr><tr>
<td>Type of call</td><td>
<select name='tabletypeofcall'>
<option>CreateTable</option>
<option>DeleteEntity</option>
<option>DeleteTable</option>
<option>InsertEntity]</option>
<option>MergeEntity</option>
<option>QueryEntities</option>
<option>QueryTables</option>
<option>UpdateEntity</option>
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
      retVal.AppendFormat("<Url><![CDATA[{0}]]></Url>", ar.Url);
      retVal.AppendFormat("<CanonicalResource><![CDATA[{0}]]></CanonicalResource>", ar.CanonicalResource);
      retVal.AppendFormat("<Headers>");
      if (ar.Headers != null)
        if (ar.Headers.Keys.Count > 0)
        {
          foreach (string key in ar.Headers.Keys)
          {
            retVal.AppendFormat("<Header><Key>{0}</Key><Value><![CDATA[{1}]]></Value></Header>", key, ar.Headers[key]);
          }
        }
      retVal.AppendFormat("</Headers>");
      retVal.AppendFormat("<body><![CDATA[{0}]]></body>", ar.Body);
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
