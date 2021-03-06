﻿using System;
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
  /// <summary>
  /// Summary description for $codebehindclassname$
  /// </summary>
  [WebService(Namespace = "http://tempuri.org/")]
  [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
  public class AzureBlobHandler : IHttpHandler
  {
    static MemcachedClient client = WindowsAzureMemcachedHelpers.CreateDefaultClient("MainWebRole", "memcacheWeb");

    public void ProcessRequest(HttpContext context)
    {
      Utility u = new Utility();
      string cacheKeyETag = string.Format("etag:{0}{1}", context.Request.Url.DnsSafeHost, context.Request.Url.LocalPath);
      context.Response.AppendToLog(string.Format("Processing {0}", context.Request.Url));
      FrameworkUtility aau = new FrameworkUtility();
      string ifMatchTag = string.Empty;
      try { ifMatchTag = client.Get(cacheKeyETag) as string; }
      catch { }
      string eTag = (context.Request.Headers["If-None-Match"] != null ? context.Request.Headers["If-None-Match"].ToString() : string.Empty);

      if (ifMatchTag == eTag && (context.Request.HttpMethod == "GET" || context.Request.HttpMethod == "HEAD"))
      {
        context.Response.StatusCode = (int)System.Net.HttpStatusCode.NotModified;
      }
      else
      {
        try
        {
          string retVal = string.Empty;
          XmlDocument xdoc = new XmlDocument();
          xdoc.LoadXml(u.getConfigXML());
          string hostName = context.Request.Headers["Host"];
          if (hostName.Contains(":"))
            hostName = hostName.Substring(0, hostName.IndexOf(":"));
          XmlNode xNode = xdoc.SelectSingleNode(string.Format("//blobData[@name='{0}']", hostName));
          if (xNode == null)
            xNode = xdoc.SelectSingleNode(string.Format("//blobData[@name='{0}']", hostName.Substring(hostName.IndexOf(".") + 1)));

          if (xNode == null)
            xNode = xdoc.SelectSingleNode("//blobData[@name='default']");

          string azureAccount = xNode.SelectSingleNode("Setting[@name='account']").Attributes["value"].Value;
          string azureEndpoint = xNode.SelectSingleNode("Setting[@name='endpoint']").Attributes["value"].Value;
          string azureSharedKey = xNode.SelectSingleNode("Setting[@name='accountSharedKey']").Attributes["value"].Value;
          string azureRootContainer = string.Empty;
          try { azureRootContainer = xNode.SelectSingleNode("Setting[@name='rootContainer']").Attributes["value"].Value; }
          catch { azureRootContainer = "$root"; }
          string blobStorage = azureEndpoint;
          string cPath = context.Request.Path;
          if (!cPath.Contains(".") && !cPath.EndsWith("/"))
            cPath += "/";
          if (cPath.EndsWith("/")) cPath += "default.htm";
          if (!cPath.EndsWith("/") && File.Exists(context.Request.PhysicalPath))
          {
            string localEtag = aau.md5(hostName + File.GetLastWriteTimeUtc(context.Request.PhysicalPath).ToLongTimeString()) + aau.md5(context.Request.PhysicalPath);
            string headerEtag = string.Empty;
            if (context.Request.Headers["If-None-Match"] != null)
              headerEtag = context.Request.Headers["If-None-Match"];
            if (headerEtag == localEtag)
            {
              context.Response.StatusCode = (int)System.Net.HttpStatusCode.NotModified;
              client.Store(Enyim.Caching.Memcached.StoreMode.Set, cacheKeyETag, localEtag, new TimeSpan(0, 10, 0));
            }
            else
            {
              context.Response.Headers.Add("P3P", "CP=HONK");
              context.Response.Headers.Add("ETag", localEtag);
              context.Response.Cache.SetExpires(DateTime.Now.AddSeconds(3600));
              context.Response.Cache.SetCacheability(HttpCacheability.Private);
              byte[] xmlFragment = File.ReadAllBytes(context.Request.PhysicalPath);
              System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
              context.Response.ContentType = GetMimeType(new FileInfo(context.Request.PhysicalPath).Extension);
              context.Response.TransmitFile(context.Request.PhysicalPath);
              client.Store(Enyim.Caching.Memcached.StoreMode.Set, cacheKeyETag, localEtag, new TimeSpan(0, 10, 0));
            }
          }
          else
          {
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
            if (blobName != string.Empty && containerName != u.configBlobContainer )
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
                  //string eTag = (context.Request.Headers["If-None-Match"] != null ? context.Request.Headers["If-None-Match"].ToString() : string.Empty);
                  byte[] xmlFragment = abs.GetBlob(containerName, blobName, "", ref ar, eTag);
                  if (ar.StatusCode == System.Net.HttpStatusCode.NotModified)
                  {
                    context.Response.StatusCode = (int)ar.StatusCode;
                    try
                    {
                      client.Store(Enyim.Caching.Memcached.StoreMode.Set, cacheKeyETag, ar.Headers["ETag"].ToString(), new TimeSpan(0, 10, 0));
                    }
                    catch { }
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
                    client.Store(Enyim.Caching.Memcached.StoreMode.Set, cacheKeyETag, ar.Headers["ETag"].ToString(), new TimeSpan(0, 10, 0));
                    context.Response.Headers.Add("P3P", "CP=HONK");
                    context.Response.ContentType = GetMimeType(blobName.Substring(blobName.LastIndexOf('.'))); //ar.Headers["Content-Type"].ToString();
                    SetCompression(context);
                    context.Response.Cache.SetCacheability(HttpCacheability.Private);
                    context.Response.Cache.SetExpires(DateTime.Now.AddMinutes(10));
                    context.Response.Cache.SetMaxAge(new TimeSpan(0, 10, 0));
                    try { context.Response.AddHeader("Last-Modified", ar.Headers["Last-Modified"].ToString()); }
                    catch { }

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
                      client.Store(Enyim.Caching.Memcached.StoreMode.Set, cacheKeyETag, ar.Headers["ETag"].ToString(), new TimeSpan(0, 10, 0));
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

                    context.Response.Headers.Add("P3P", "CP=HONK");
                    context.Response.ContentType = GetMimeType(blobName.Substring(blobName.LastIndexOf('.'))); //ar.Headers["Content-Type"].ToString();
                    context.Response.Cache.SetExpires(DateTime.Now.AddMinutes(10));
                    context.Response.Cache.SetValidUntilExpires(true);
                    context.Response.Cache.SetCacheability(HttpCacheability.Private);
                    context.Response.Cache.SetETag (ar.Headers["ETag"].ToString());
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

    

    public bool IsReusable
    {
      get
      {
        return true;
      }
    }
  }
}