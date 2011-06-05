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


namespace MOOP_WebRole
{
  /// <summary>
  /// Summary description for $codebehindclassname$
  /// </summary>
  [WebService(Namespace = "http://tempuri.org/")]
  [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
  public class AzureBlobHandler : IHttpHandler
  {

    public void ProcessRequest(HttpContext context)
    {
      context.Response.AppendToLog(string.Format("Processing {0}", context.Request.Url));
      MOOP_Framework_Utilities.Utility aau = new MOOP_Framework_Utilities.Utility();
      try
      {
        string retVal = string.Empty;
        XmlDocument xdoc = new XmlDocument();
        xdoc.LoadXml(new Utility().getConfigXML());

        XmlNode xNode = xdoc.SelectSingleNode("//blobData[@name='default']");

        string azureAccount =  xNode.SelectSingleNode("Setting[@name='account']").Attributes["value"].Value;
        string azureEndpoint = xNode.SelectSingleNode("Setting[@name='endpoint']").Attributes["value"].Value;
        string azureSharedKey = xNode.SelectSingleNode("Setting[@name='accountSharedKey']").Attributes["value"].Value;
        string blobStorage = azureEndpoint;
        string cPath = context.Request.Path;
        if (cPath.EndsWith("/")) cPath += "default.htm";
        if (!context.Request.Path.EndsWith("/") &&  File.Exists(context.Request.PhysicalPath))
        {
          string localEtag = aau.md5(File.GetLastWriteTimeUtc(context.Request.PhysicalPath).ToLongTimeString()) + aau.md5(context.Request.PhysicalPath);
          string headerEtag = string.Empty;
          if (context.Request.Headers["If-None-Match"] != null)
            headerEtag = context.Request.Headers["If-None-Match"];
          if (headerEtag == localEtag)
          {
            context.Response.StatusCode = (int)System.Net.HttpStatusCode.NotModified;
          }
          else
          {
            context.Response.Headers.Add("P3P", "CP=HONK");
            context.Response.Headers.Add("ETag", localEtag);
            context.Response.Cache.SetExpires(DateTime.Now.AddSeconds(3600));
            context.Response.Cache.SetCacheability(HttpCacheability.Public);
            byte[] xmlFragment = File.ReadAllBytes(context.Request.PhysicalPath);
            System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
            context.Response.ContentType = GetMimeType(new FileInfo(context.Request.PhysicalPath).Extension);
            context.Response.TransmitFile(context.Request.PhysicalPath);
          }
        }
        else
        {
          string containerName = string.Empty;
          string blobName = string.Empty;
          string[] aPath = context.Request.Path.Split("/".ToCharArray());
          if (aPath.Count() > 1)
            containerName = aPath[1];
          if (!context.Request.Path.Substring(1).Contains("/"))
          {
            containerName = "$root";
            blobName = cPath.Substring(1).TrimStart();
          }
          else
          {
            if (cPath.Length > containerName.Length + 2)
              blobName = cPath.Substring(containerName.Length + 2).TrimStart();
          }
          if (blobName != string.Empty)
          {
            //if (blobName.Contains(',')) // we have parameters in the page name that need to be taken care of
            //{
            //  string[] pageParts = blobName.Split(',');
            //  if (pageParts.Length > 1)
            //  {
            //    if (!pageParts[1].Contains("="))
            //      context.Request.Params.Add("ID", pageParts[1]);
            //    for (int i = 1; i < pageParts.Length; i++)
            //    {
            //      if (pageParts[i].Contains("="))
            //      {
            //        string[] parmItem = pageParts[i].Split('=');
            //        context.Request.Params.Add( parmItem[0], parmItem[1]);
            //      }
            //    }
            //  }
            //  blobName = string.Format("{0}{1}", blobName.Substring(0, blobName.IndexOf(',')), blobName.Substring(blobName.LastIndexOf(".")));
            //}

            AzureBlobStorage abs = new AzureBlobStorage(azureAccount, blobStorage, azureSharedKey, "SharedKey");
            azureResults ar = new azureResults();
            //
            //
            switch (context.Request.HttpMethod)
            {
              case "GET":
                //// Make head call to see if we need to 
                //if (context.Request.Headers["If-None-Match"] != null)
                //{
                //  ar = abs.CheckBlobCache(containerName, blobName, context.Request.Headers["If-None-Match"]);
                //  if (ar.StatusCode == System.Net.HttpStatusCode.NotModified)
                //  {
                //    context.Response.StatusCode = (int)ar.StatusCode;
                //  }
                //}

                //else
                //{
                string eTag = (context.Request.Headers["If-None-Match"] != null ? context.Request.Headers["If-None-Match"].ToString() : string.Empty);
                  byte[] xmlFragment = abs.GetBlob(containerName, blobName, "", ref ar, eTag );
                  if (ar.StatusCode == System.Net.HttpStatusCode.NotModified)
                  {
                    context.Response.StatusCode = (int)ar.StatusCode;
                  }
                  else if (ar.StatusCode != System.Net.HttpStatusCode.OK)
                  {
                    Console.WriteLine("HUH?");
                  }
                  else
                  {
                    context.Response.AppendToLog(string.Format("trying to send {0} {1}", containerName, blobName));
                    System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                    if (context.Response.Headers["ETag"] == null)
                      context.Response.Headers.Add("ETag", ar.Headers["ETag"].ToString());
                    else
                      context.Response.Headers["ETag"] = ar.Headers["ETag"].ToString();
                    context.Response.Headers.Add("P3P", "CP=HONK");
                    context.Response.ContentType = GetMimeType(blobName.Substring(blobName.LastIndexOf('.'))); //ar.Headers["Content-Type"].ToString();
                    context.Response.Cache.SetExpires(DateTime.Now.AddSeconds(3600));
                    context.Response.Cache.SetCacheability(HttpCacheability.Public);

                    if (context.Response.ContentType.Contains("text"))
                      context.Response.Write(enc.GetString(xmlFragment));
                    else
                      context.Response.BinaryWrite(xmlFragment);
                  }
                //}
                break;
              case "HEAD":
                if (context.Request.Headers["If-None-Match"] != null)
                {
                  ar = abs.CheckBlobCache(containerName, blobName, context.Request.Headers["If-None-Match"]);
                  if (ar.StatusCode == System.Net.HttpStatusCode.NotModified)
                  {
                    context.Response.StatusCode = (int)ar.StatusCode;
                  }
                }                
                break;
              case "POST":
                //// Make head call to see if we need to 
                //if (context.Request.Headers["If-None-Match"] != null)
                //{
                //  ar = abs.CheckBlobCache(containerName, blobName, context.Request.Headers["If-None-Match"]);
                //  if (ar.StatusCode == System.Net.HttpStatusCode.NotModified)
                //  {
                //    context.Response.StatusCode = (int)ar.StatusCode;
                //  }
                //}

                //else
                //{
                eTag = (context.Request.Headers["If-None-Match"] != null ? context.Request.Headers["If-None-Match"].ToString() : string.Empty);
                xmlFragment = abs.GetBlob(containerName, blobName, "", ref ar, eTag);
                if (ar.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                  context.Response.StatusCode = (int)ar.StatusCode;
                }
                else if (ar.StatusCode != System.Net.HttpStatusCode.OK)
                {
                  Console.WriteLine("HUH?");
                }
                else
                {
                  context.Response.AppendToLog(string.Format("trying to send {0} {1}", containerName, blobName));
                  System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                  if (context.Response.Headers["ETag"] == null)
                    context.Response.Headers.Add("ETag", ar.Headers["ETag"].ToString());
                  else
                    context.Response.Headers["ETag"] = ar.Headers["ETag"].ToString();
                  context.Response.Headers.Add("P3P", "CP=HONK");
                  context.Response.ContentType = GetMimeType(blobName.Substring(blobName.LastIndexOf('.'))); //ar.Headers["Content-Type"].ToString();
                  context.Response.Cache.SetExpires(DateTime.Now.AddSeconds(3600));
                  context.Response.Cache.SetCacheability(HttpCacheability.Public);

                  if (context.Response.ContentType.Contains("text"))
                    context.Response.Write(enc.GetString(xmlFragment));
                  else
                    context.Response.BinaryWrite(xmlFragment);
                }
                //}
                break;
              default:
                context.Response.StatusCode = 404;
                context.Response.Status = "File not found";
                context.Response.Close();
                break;
            }
          }

          else
            context.Response.Close();
        }
      }
      catch (Exception ex)
      {
        //context.Response.Status = 
        context.Response.StatusCode = 404;
        context.Response.Close();
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
        return true;
      }
    }
  }
}