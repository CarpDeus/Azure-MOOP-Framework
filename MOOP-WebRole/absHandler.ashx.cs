using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;


using System.Xml;
using Finsel.AzureCommands;
using System.Text;

using System.Collections;

namespace MOOP_WebRole
{
  /// <summary>
  /// Summary description for absHandler
  /// </summary>
  public class absHandler : IHttpHandler
  {

    public void ProcessRequest(HttpContext context)
    {
      string accountName = string.Empty;
      string sharedKey = string.Empty;
      string blobTypeOfCall = string.Empty;

      string containerName = string.Empty;
      string blobName = string.Empty;
      string leaseAction = string.Empty;
      string leaseID = string.Empty;
      string eTag = string.Empty;
      string mimicDirectories = string.Empty;
      string includeSnapshots = string.Empty;
      string includeUncommitted = string.Empty;


      if (context.Request.Params.AllKeys.Contains("accountname"))
        accountName = context.Request.Params["accountname"].ToString();
      if (context.Request.Params.AllKeys.Contains("sharedkey"))
        sharedKey = context.Request.Params["sharedkey"].ToString().Replace(" ", "+");
      if (context.Request.Params.AllKeys.Contains("blobtypeofcall"))
        blobTypeOfCall = context.Request.Params["blobtypeofcall"].ToString();
      if (context.Request.Params.AllKeys.Contains("containername"))
        containerName = context.Request.Params["containername"].ToString();
      if (context.Request.Params.AllKeys.Contains("blobname"))
        blobName = context.Request.Params["blobname"].ToString();
      if (context.Request.Params.AllKeys.Contains("leaseaction"))
        leaseAction = context.Request.Params["leaseaction"].ToString().ToLower();
      if (context.Request.Params.AllKeys.Contains("leaseid"))
        leaseID = context.Request.Params["leaseid"].ToString();
      if (context.Request.Params.AllKeys.Contains("etag"))
        eTag = context.Request.Params["etag"].ToString();
      if (context.Request.Params.AllKeys.Contains("mimicdirectories"))
        mimicDirectories = "1";
      if (context.Request.Params.AllKeys.Contains("includesnapshots"))
        includeSnapshots = "1";
      if (context.Request.Params.AllKeys.Contains("includeuncommitted"))
        includeUncommitted = "1";


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
      Finsel.AzureCommands.AzureBlobStorage abs = new Finsel.AzureCommands.AzureBlobStorage(accountName,
        string.Format("http://{0}.blob.core.windows.net", accountName), sharedKey,
        "SharedKey");

      switch (blobTypeOfCall.ToLower())
      {
        case "addblobmetadata":
          ar = abs.MetaData(cmdType.put, containerName, blobName, ht);
          retVal = processAzureResults(ar);
          break;
        case "addcontainermetadata":
          ar = abs.MetaData(cmdType.put, containerName, "", ht);
          retVal = processAzureResults(ar);
          break;
        case "copyblob":
        //ar=abs.CopyBlob(
        case "createcontainer":
          ht.Add("x-ms-meta-CreatedBy", "Finsel.AzureCommands");
          ar = abs.Containers(cmdType.put, containerName, ht);
          retVal = processAzureResults(ar);
          break;
        case "deleteblob":
          ar = abs.DeleteBlob(containerName, blobName);
          retVal = processAzureResults(ar);
          break;
        case "deletecontainer":
          ar = abs.Containers(cmdType.delete, containerName, new Hashtable());
          retVal = processAzureResults(ar);
          break;
        case "deleteblobmetadata":
          ar = abs.MetaData(cmdType.put, containerName, blobName, null);
          retVal = processAzureResults(ar);
          break;
        case "deletecontainermetadata":
          ar = abs.MetaData(cmdType.put, containerName, "", null);
          retVal = processAzureResults(ar);
          break;
        case "displayblobmetadata":
          ar = abs.MetaData(cmdType.get, containerName, blobName, null);
          retVal = processAzureResults(ar);
          break;
        case "displaycontainermetadata":
          ar = abs.MetaData(cmdType.get, containerName, "", null);
          retVal = processAzureResults(ar);
          break;
        case "getblob":
          break;
        case "getbloblist":
          string parameterList = string.Empty;
          if (mimicDirectories == "1")
            if (blobName == string.Empty)
              parameterList = "delimiter=/";
            else
              parameterList = string.Format("prefix={0}&delimiter={1}", (blobName == string.Empty ? "/" : blobName), "/");
          if (includeSnapshots == "1")
          {
            if (parameterList != string.Empty)
              parameterList = string.Format("{0}&include=snapshots", parameterList);
            else parameterList = "include=snapshots";
          }
          if (includeUncommitted == "1")
          {
            if (parameterList != string.Empty)
              parameterList = string.Format("{0}&include=uncommittedblobs", parameterList);
            else parameterList = "include=uncommittedblobs";
          }
          ar = abs.GetBlobList(containerName, parameterList);
          retVal = processAzureResults(ar);
          break;
        case "getcontainerlist":
          ar = abs.GetContainerList(containerName);
          retVal = processAzureResults(ar);
          break;
        case "leaseblob":
          AzureBlobStorage.BlobLease bl;
          switch (leaseAction)
          {
            case "acquirelease": bl = AzureBlobStorage.BlobLease.acquireLease; break;
            case "breaklease": bl = AzureBlobStorage.BlobLease.breakLease; break;
            case "releaselease": bl = AzureBlobStorage.BlobLease.releaseLease; break;
            case "renewlease": bl = AzureBlobStorage.BlobLease.renewLease; break;
            default: bl = AzureBlobStorage.BlobLease.acquireLease; break;
          }

          ar = abs.LeaseBlob(containerName, blobName, bl, leaseID);
          retVal = processAzureResults(ar);
          break;
        case "makecontainerprivate":
          ar = abs.SetContainerAccess(containerName, false);
          retVal = processAzureResults(ar);
          break;
        case "makecontainerpublic":
          ar = abs.SetContainerAccess(containerName, true);
          retVal = processAzureResults(ar);
          break;
        case "snapshotblob":
          ar = abs.SnapshotBlob(containerName, blobName);
          retVal = processAzureResults(ar);
          break;
        case "verifyblob":
          ar = abs.CheckBlobCache(containerName, blobName, eTag);
          retVal = processAzureResults(ar);
          break;
        default:
          retVal = @"<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN' 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd'>
<html xmlns='http://www.w3.org/1999/xhtml' >
<head>
    <title>Azure Architect Azure Blob Storage Handler Form</title>
</head>
<body>
<form action='absHandler.ashx' method='post'>
<table border='1'>
<tr>
<td>Account</td><td><input name='accountname' maxlength='100' /></td>
</tr><tr>
<td>Shared Key</td><td><input name='sharedkey' maxlength='100' /></td>
</tr><tr>
<td>Container Name</td><td><input name='containername' maxlength='100' /></td>
</tr><tr>
<td>Blob Name</td><td><input name='blobname' maxlength='1240' /></td>
</tr><tr>
<td>Type of call</td><td>
<select name='blobtypeofcall'>
<option>AddContainerMetadata</option>
    <option value='CreateContainer'>CreateContainer</option>
    <option value='DeleteBlob'>DeleteBlob</option>
    <option value='DeleteContainer'>DeleteContainer</option>
    <option value='DeleteContainerMetadata'>DeleteContainerMetadata</option>
    <option value='DisplayContainerMetadata'>DisplayContainerMetadata</option>
    <option value='GetBlob'>GetBlob</option>
    <option value='GetBlobList'>GetBlobList</option>
    <option value='GetContainerList'>GetContainerList</option>
    <option value='MakeContainerPrivate'>MakeContainerPrivate</option>
    <option value='MakeContainerPublic'>MakeContainerPublic</option></select>
</td>
</tr><tr>
<td colspan='2'><input type='submit' /></td>
</tr>
</table>
</form>
</body>
</html>";
          retValType = "text/html";
          //          retVal = @"<?xml version='1.0' encoding='utf-8' ?>
          //<ValidFormat>
          //  <!-- Post format -->
          //  <AccountName />
          //  <SharedKey />
          //  <ContainerName />
          //  <Blobname />
          //  <BlobTypeOfCall>
          //    <AddContainerMetadata />
          //    <CreateContainer />
          //    <DeleteBlob />
          //    <DeleteContainer />
          //    <DeleteContainerMetadata />
          //    <DisplayContainerMetadata />
          //    <GetBlob />
          //    <GetBlobList />
          //    <GetContainerList />
          //    <MakeContainerPrivate />
          //    <MakeContainerPublic />
          //  </BlobTypeOfCall>
          //</ValidFormat>";
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