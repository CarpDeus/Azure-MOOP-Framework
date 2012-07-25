using System;
using System.Collections;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Web.Services.Protocols;
using System.Xml.Linq;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Text;
using Finsel.AzureCommands;
using System.Xml;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using Enyim.Caching;
using Yahoo.Yui.Compressor;

namespace MOOPFramework
{
  public class blobCombiner : IHttpHandler
  {


    static MemcachedClient client = WindowsAzureMemcachedHelpers.CreateDefaultClient("MainWebRole", "memcacheWeb");

    /// <summary>
    /// You will need to configure this handler in the web.config file of your 
    /// web and register it with IIS before being able to use it. For more information
    /// see the following link: http://go.microsoft.com/?linkid=8101007
    /// </summary>
    #region IHttpHandler Members

    public bool IsReusable
    {
      // Return false in case your Managed Handler cannot be reused for another request.
      // Usually this would be false in case you have some state information preserved per request.
      get { return true; }
    }

    public void ProcessRequest(HttpContext context)
    {
      if (!string.IsNullOrEmpty(context.Request.QueryString["files"]))
      {
        string eTag = string.Empty;
        string cacheKeyETag = string.Format("etag:{0}{1}{2}", context.Request.Url.DnsSafeHost, context.Request.QueryString["files"], context.Request.QueryString["m"]);
        string retVal = string.Empty;
        if (context.Request.Headers["If-None-Match"] != string.Empty && context.Request.Headers["If-None-Match"] != null)
        {
          retVal = client.Get(cacheKeyETag) as string;
        }
        if (retVal == string.Empty || retVal == null)
        {
          FrameworkUtility aau = new FrameworkUtility();
          string[] relativeFiles = context.Request.QueryString["files"].Split(',');
          string[] absoluteFiles = new string[relativeFiles.Length];
          bool minifyData = (context.Request.QueryString["m"] != null);

          // Azure Blob Set up
          //string retVal = string.Empty;
          XmlDocument xdoc = new XmlDocument();
          xdoc.LoadXml(new Utility().getConfigXML());
          string hostName = context.Request.Headers["Host"];
          if (hostName.Contains(":"))
            hostName = hostName.Substring(0, hostName.IndexOf(":"));
          XmlNode xNode = xdoc.SelectSingleNode(string.Format("//blobData[@name='{0}']", hostName));
          if (xNode == null)
            xNode = xdoc.SelectSingleNode(string.Format("//blobData[@name='{0}']", hostName.Substring(hostName.IndexOf(".") + 1)));

          if (xNode == null)
            xNode = xdoc.SelectSingleNode("//blobData[@name='default']");
          string mimeType = string.Empty;
          string azureAccount = xNode.SelectSingleNode("Setting[@name='account']").Attributes["value"].Value;
          string azureEndpoint = xNode.SelectSingleNode("Setting[@name='endpoint']").Attributes["value"].Value;
          string azureSharedKey = xNode.SelectSingleNode("Setting[@name='accountSharedKey']").Attributes["value"].Value;
          string azureRootContainer = string.Empty;
          try { azureRootContainer = xNode.SelectSingleNode("Setting[@name='rootContainer']").Attributes["value"].Value; }
          catch { azureRootContainer = "$root"; }
          string blobStorage = azureEndpoint;
          AzureBlobStorage abs = new AzureBlobStorage(azureAccount, blobStorage, azureSharedKey, "SharedKey");
          azureResults ar = new azureResults();
          context.Response.Headers.Add("P3P", "CP=HONK");
          SetCompression(context);
          StringBuilder sbReturnData = new StringBuilder();
          for (int i = 0; i < relativeFiles.Length; i++)
          {
            string cPath = relativeFiles[i];
            if (mimeType == string.Empty)
            {
              mimeType = GetMimeType(cPath.Substring(cPath.LastIndexOf('.'))); //ar.Headers["Content-Type"].ToString();
            }
            {
              try
              {
                if (!cPath.Contains(".") && !cPath.EndsWith("/"))
                  cPath += "/";
                string containerName = string.Empty;
                string blobName = string.Empty;
                string[] aPath = cPath.Split("/".ToCharArray());
                if (aPath.Count() > 1)
                  containerName = aPath[1];
                if (!cPath.Substring(1).Contains("/"))
                {
                  containerName = azureRootContainer;
                  blobName = cPath.Substring(1).TrimStart();
                }
                else
                {
                  if (cPath.Length > containerName.Length + 2)
                    blobName = cPath.Substring(containerName.Length + 2).TrimStart();
                }
                if (blobName != string.Empty)
                {
                  ar = new azureResults();
                  byte[] xmlFragment = abs.GetBlob(containerName, blobName, "", ref ar, eTag);
                  if (ar.StatusCode == System.Net.HttpStatusCode.NotModified)
                  {
                  }
                  else if (ar.StatusCode != System.Net.HttpStatusCode.OK)
                  {
                    Console.WriteLine("HUH?");
                  }
                  else
                  {
                    context.Response.AppendToLog(string.Format("trying to send {0} {1}", containerName, blobName));
                    System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                    eTag = string.Format("{0}{1}", eTag, ar.Headers["ETag"].ToString());
                    if (minifyData)
                    {
                      if (mimeType == "text/css")
                        sbReturnData.Append(new CssCompressor().Compress(enc.GetString(xmlFragment)));
                      else if (cPath.Substring(cPath.LastIndexOf('.')).ToLower() == ".js")
                        sbReturnData.Append(new JavaScriptCompressor().Compress(enc.GetString(xmlFragment)));
                      else sbReturnData.Append(enc.GetString(xmlFragment));
                    }
                    else
                      sbReturnData.Append(enc.GetString(xmlFragment));
                  }
                }

              }
              catch (Exception ex)
              {
                //context.Response.Status = 
                context.Response.StatusCode = 404;
                context.Response.Close();
              }
            }
          }
          retVal = sbReturnData.ToString();
          client.Store(Enyim.Caching.Memcached.StoreMode.Set, cacheKeyETag, retVal, new TimeSpan(0, 10, 0));
        }

          context.Response.Write(retVal );
        
        context.Response.ContentType = context.Request.QueryString["contenttype"];
        //context.Response.AddFileDependencies(files);
        //context.Response.Cache.VaryByParams["stylesheets"] = true;
        //context.Response.Cache.SetETagFromFileDependencies();
        //context.Response.Cache.SetLastModifiedFromFileDependencies();
        context.Response.Cache.SetValidUntilExpires(true);
        context.Response.Headers.Add("ETag", cacheKeyETag);
        context.Response.Cache.SetExpires(DateTime.Now.AddMinutes(10));
        context.Response.Cache.SetCacheability(HttpCacheability.Public);
        //SetHeaders(context, absoluteFiles);
      }
    }

  

  private string GetMimeType(string ext)
  {
    string mime = "application/octetstream";

    Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
    if (ext == ".js")
      mime = "application/x-javascript";
    else
      if (rk != null && rk.GetValue("Content Type") != null)
        mime = rk.GetValue("Content Type").ToString();

    return mime;
  }

  private void SetCompression(HttpContext ctx)
  {
    string accept = (ctx.Request.Headers["Accept-encoding"] != null ? ctx.Request.Headers["Accept-encoding"] : string.Empty);

    if (accept.Contains("gzip"))
    {
      ctx.Response.Filter = new GZipStream(ctx.Response.Filter, CompressionMode.Compress);
      ctx.Response.AppendHeader("Content-Encoding", "gzip");
    }
    else if (accept.Contains("deflate"))
    {
      ctx.Response.Filter = new DeflateStream(ctx.Response.Filter, CompressionMode.Compress);
      ctx.Response.AppendHeader("Content-Encoding", "deflate");
    }

    // if no match found
    return;
  }


    #endregion
  }
}

