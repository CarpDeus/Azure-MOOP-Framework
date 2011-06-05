using System;
using System.Data;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Xml.Linq;
using System.Xml;

using Microsoft.WindowsAzure.ServiceRuntime;
using Finsel.AzureCommands;

using System.IO.Compression;

namespace MOOP_WebRole
{
  public class GetBlob : IHttpModule 
  {
    public void Dispose()
    {
    }

    public void Init(HttpApplication context)
    {
      context.BeginRequest += new EventHandler(OnPreRequestHandlerExecute);
    }

    public void OnPreRequestHandlerExecute(Object source, EventArgs e)
    {
      HttpApplication app = (HttpApplication)source;
      HttpContext context = app.Context;
      HttpRequest request = app.Context.Request;
      HttpResponse response = app.Context.Response;
      byte[] xmlFragment = null;

      if (!System.IO.File.Exists(request.PhysicalPath))
      {
        try
        {
          string retVal = string.Empty;
          XmlDocument xdoc = new XmlDocument();
          xdoc.LoadXml(new Utility().getConfigXML());
          XmlNode xNode = xdoc.SelectSingleNode(string.Format("//blobData[@name='default']"));

          string azureAccount = xNode.SelectSingleNode("Setting[@name='account']").Attributes["value"].Value;
          string azureEndpoint = xNode.SelectSingleNode("Setting[@name='endpoint']").Attributes["value"].Value;
          string azureSharedKey = xNode.SelectSingleNode("Setting[@name='accountSharedKey']").Attributes["value"].Value;
          string blobStorage = azureEndpoint;
          string[]  publicContainers = RoleEnvironment.GetConfigurationSettingValue("webPublicContainers").Split(";".ToCharArray());
          // Get the requested path
          string queryName = request.Path;
          string containerName = queryName.Split("/".ToCharArray())[1];
          System.Collections.Hashtable ht = new System.Collections.Hashtable();
          foreach (string key in request.Headers.AllKeys)
          {
            ht.Add(key, request.Headers[key]);
          }
          string webRootContainer = RoleEnvironment.GetConfigurationSettingValue("webRootContainer");
          if (!queryName.Substring(1).Contains("/"))
            containerName = webRootContainer;
          if (request.Path != "/" && 
            ( (publicContainers.Contains(containerName) || 
            containerName == webRootContainer)))
          {
            if (containerName != webRootContainer)
              queryName = queryName.Substring(containerName.Length + 2);
            else
              queryName = queryName.Substring(1);
            Finsel.AzureCommands.AzureBlobStorage abs = new Finsel.AzureCommands.AzureBlobStorage(azureAccount, blobStorage, azureSharedKey, "SharedKey");
            azureResults ar = new azureResults();
            bool cached = false;
            if (request.Headers.AllKeys.Contains("if-modified-since") || request.Headers.AllKeys.Contains("etag"))
            {
              azureDirect ad = new azureDirect(abs.auth);
              ar = ad.ProcessRequest(cmdType.head, string.Format("http://{0}..blob.core.windows.net/{1)/{2}",
              abs.auth.Account, containerName, queryName), string.Empty ,ht );
              if (ar.Succeeded )
              {
                cached = true;
                response.StatusCode = Convert.ToInt32(ar.StatusCode);
                response.Close();
              }
            }
            if (!cached)
            {
              xmlFragment = abs.GetBlob(containerName, queryName, "", ref ar);

              if (ar.Headers.Count > 0)
              {
                foreach (System.Collections.DictionaryEntry item in ar.Headers)
                {
                  if (item.Key.ToString().ToLower().StartsWith("x-ms-meta-"))// || key.ToLower()=="etag" || key.ToLower()=="LastModified")
                  {
                    if (response.Headers.AllKeys.Contains(item.Key.ToString()))
                    {
                      response.Headers[item.Key.ToString()] = string.Format("{0},{1}", response.Headers[item.Key.ToString()], item.Value.ToString());
                    }
                    else
                      response.Headers.Add(item.Key.ToString(), item.Value.ToString());
                  }
                  if (item.Key.ToString().ToLower() == "etag")
                  {
                    response.Cache.SetETag(item.Value.ToString());
                  }
                  if (item.Key.ToString().ToLower() == "LastModified")
                  {
                    response.Cache.SetLastModified(Convert.ToDateTime(item.Value.ToString()));
                  }
                }
              }
            }
            System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
            response.ContentType = ar.Headers["Content-Type"].ToString();
            response.Write(enc.GetString(xmlFragment));
            response.End();
          }
        }
        catch (Exception ex)
        {
          //context.Response.StatusCode = 404;
          ////context.Response.Status = string.Format("File not found\r\n{0}",ex.ToString());
          //context.Response.Close();
        }
      }


      //if (!String.IsNullOrEmpty(request.Headers["Referer"]))
      //{
      //  throw new HttpException(403,
      //                                          "Uh-uh!");
      //} 
    }


    private void SetCompression(HttpContext ctx)
    {
      string accept = (ctx.Request.Headers["Accept-encoding"] != null ? ctx.Request.Headers["Accept-encoding"] : string.Empty);

      if (accept.Contains("gzip"))
      {
        ctx.Response.Filter = new GZipStream(ctx.Response.Filter, CompressionMode.Compress);
        ctx.Response.AppendHeader("Content-Encoding", "gzip");
        return;
      }

      if (accept.Contains("deflate"))
      {
        ctx.Response.Filter = new DeflateStream(ctx.Response.Filter, CompressionMode.Compress);
        ctx.Response.AppendHeader("Content-Encoding", "deflate");
        return;
      }

      // if no match found
      return;
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
