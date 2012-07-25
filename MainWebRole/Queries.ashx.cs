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

using System.IO.Compression;

using Google.ProtocolBuffers.Serialization.Http;
using Newtonsoft.Json;

namespace MOOPFramework
{
  /// <summary>
  /// Summary description for $codebehindclassname$
  /// </summary>
  [WebService(Namespace = "http://tempuri.org/")]
  [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
  public class Queries : IHttpHandler
  {
    public void ProcessRequest(HttpContext context)
    {

      Int64 expirationSeconds = 1;
      HttpCacheability hc = HttpCacheability.NoCache;

      try
      {
        string retVal = string.Empty;

        XmlDocument xdoc = new XmlDocument();
        xdoc.LoadXml(new Utility().getConfigXML());
        XmlNode xNode = xdoc.SelectSingleNode("//blobData[@name='default']");

        string azureAccount = xNode.SelectSingleNode("Setting[@name='account']").Attributes["value"].Value;
        string azureEndpoint = xNode.SelectSingleNode("Setting[@name='endpoint']").Attributes["value"].Value;
        string azureSharedKey = xNode.SelectSingleNode("Setting[@name='accountSharedKey']").Attributes["value"].Value;
        string blobStorage = azureEndpoint;

        xNode = xdoc.SelectSingleNode(string.Format("//fragmentData/Setting[@name='HandlerFragments']"));
        string fragmentLocation = xNode.Attributes["value"].Value;
        string queryName = string.Empty;

        SetCompression(context);
        
        try
        {
          AzureBlobStorage abs = new AzureBlobStorage(azureAccount, blobStorage, azureSharedKey, "SharedKey");
          azureResults ar = new azureResults();
          // Get the page name and replace the .q extension with .xml
          queryName = context.Request.Path;
          queryName = queryName.Substring(queryName.LastIndexOf("/") + 1);
          queryName = queryName.Substring(0, queryName.Length - 2) + ".xml";
          byte[] xmlFragment = abs.GetBlob(fragmentLocation, queryName, "", ref ar, "");
          if (!ar.Succeeded)
          {
            context.Response.StatusCode = (int)ar.StatusCode;
          }
          else
          {
            xdoc = new XmlDocument();
            System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
            xdoc.LoadXml(enc.GetString(xmlFragment));
            /* 
             * http://azure-architect.com/portals/16/MOOPData.xsd
             
             */
            XmlNode xn = xdoc.SelectSingleNode("//storedProcedure[1]");
            string storedProcedureName = xn.Attributes["procedureName"].Value;
            string connectionStringName = xn.Attributes["connectionName"].Value;
            string requirePost = xn.Attributes["requirePost"].Value;
            if ((requirePost == "true" || requirePost == "1") && context.Request.HttpMethod != "POST") // throw an error
            {
              context.Response.StatusDescription = "This page requires using the POST method";
              context.Response.StatusCode = 400; // Bad Request
            }
            else
            {
              SqlCommand cmd = new SqlCommand(storedProcedureName, new SqlConnection(new Utility().ResolveDataConnection( connectionStringName)));
              cmd.CommandType = CommandType.StoredProcedure;
              XmlNodeList xnl = xdoc.SelectNodes("//parameter");
              foreach (XmlNode node in xnl)
              {
                string parameterName = node.Attributes["parameterName"].Value;
                string urlParameterName = node.Attributes["urlParameterName"].Value;
                string dataType = node.Attributes["dataType"].Value;
                string dataLength = node.Attributes["dataLength"].Value;
                string defaultValue = node.Attributes["defaultValue"].Value;
                if (!parameterName.StartsWith("@"))
                  parameterName = "@" + parameterName;
                SqlParameter sp = new SqlParameter();
                sp.ParameterName = parameterName;
                switch (dataType)
                {
                  case "bigint": sp.SqlDbType = SqlDbType.BigInt; break;
                  case "binary": sp.SqlDbType = SqlDbType.Binary; break;
                  case "bit": sp.SqlDbType = SqlDbType.Bit; break;
                  case "char": sp.SqlDbType = SqlDbType.Char; break;
                  case "date": sp.SqlDbType = SqlDbType.Date; break;
                  case "datetime": sp.SqlDbType = SqlDbType.DateTime; break;
                  case "datetime2": sp.SqlDbType = SqlDbType.DateTime2; break;
                  case "datetimeoffset": sp.SqlDbType = SqlDbType.DateTimeOffset; break;
                  case "decimal": sp.SqlDbType = SqlDbType.Decimal; break;
                  case "float": sp.SqlDbType = SqlDbType.Float; break;
                  case "geography": sp.SqlDbType = SqlDbType.Structured; break;
                  case "geometry": sp.SqlDbType = SqlDbType.Structured; break;
                  case "hierarchyid": sp.SqlDbType = SqlDbType.Structured; break;
                  case "image": sp.SqlDbType = SqlDbType.Image; break;
                  case "int": sp.SqlDbType = SqlDbType.Int; break;
                  case "money": sp.SqlDbType = SqlDbType.Money; break;
                  case "nchar": sp.SqlDbType = SqlDbType.NChar; break;
                  case "ntext": sp.SqlDbType = SqlDbType.NText; break;
                  case "nvarchar": sp.SqlDbType = SqlDbType.NVarChar; break;
                  case "real": sp.SqlDbType = SqlDbType.Real; break;
                  case "smalldatetime": sp.SqlDbType = SqlDbType.SmallDateTime; break;
                  case "smallint": sp.SqlDbType = SqlDbType.SmallInt; break;
                  case "smallmoney": sp.SqlDbType = SqlDbType.SmallMoney; break;
                  case "sql_variant": sp.SqlDbType = SqlDbType.Variant; break;
                  case "text": sp.SqlDbType = SqlDbType.Text; break;
                  case "time": sp.SqlDbType = SqlDbType.Time; break;
                  case "timestamp": sp.SqlDbType = SqlDbType.Timestamp; break;
                  case "tinyint": sp.SqlDbType = SqlDbType.TinyInt; break;
                  case "uniqueidentifier": sp.SqlDbType = SqlDbType.UniqueIdentifier; break;
                  case "varbinary": sp.SqlDbType = SqlDbType.VarBinary; break;
                  case "varchar": sp.SqlDbType = SqlDbType.VarChar; break;
                  case "xml": sp.SqlDbType = SqlDbType.Xml; break;
                  default: sp.SqlDbType = SqlDbType.Variant; break;
                }
                switch (urlParameterName.ToLower())
                {
                  case "ipaddress": sp.Value = context.Request.UserHostAddress; break;
                  //case "domainname": sp.Value = context.Request.Url.DnsSafeHost; break;
                  case "domainname": sp.Value = context.Request.Headers["Host"]; break;
                  default: if (context.Request.Params[urlParameterName] != null)
                      sp.Value = context.Request.Params[urlParameterName];
                    else
                      sp.Value = (defaultValue.ToLower() == "dbnull" ? DBNull.Value
                      : (object)defaultValue);
                    break;
                }
                cmd.Parameters.Add(sp);
              }

              xnl = xdoc.SelectNodes("//cacheInformation[1]");
              foreach (XmlNode node in xnl)
              {
                if (node.Attributes["expireSeconds"] != null)
                  expirationSeconds = Convert.ToInt64(node.Attributes["expireSeconds"].Value);
                if (node.Attributes["cacheability"] != null)
                {
                  switch (node.Attributes["cacheability"].Value.ToLower())
                  {
                    case "nocache": hc = HttpCacheability.NoCache; break;
                    case "private": hc = HttpCacheability.Private; context.Response.Headers.Add("ETag", md5(context.Request.RawUrl)); break;
                    case "public": hc = HttpCacheability.Public; context.Response.Headers.Add("ETag", md5(context.Request.RawUrl)); break;
                    case "server": hc = HttpCacheability.Server; context.Response.Headers.Add("ETag", md5(context.Request.RawUrl)); break;
                    case "serverandnocache": hc = HttpCacheability.ServerAndNoCache; context.Response.Headers.Add("ETag", md5(context.Request.RawUrl)); break;
                    case "serverandprivate": hc = HttpCacheability.ServerAndPrivate; context.Response.Headers.Add("ETag", md5(context.Request.RawUrl)); break;
                    default: hc = HttpCacheability.NoCache; break;
                  }
                }
              }
              cmd.Connection.Open();
              SqlDataReader dr = cmd.ExecuteReader();
              while (dr.Read())
                retVal = retVal + dr[0].ToString();
              cmd.Connection.Close();
              if (!retVal.StartsWith("<?xml"))
              {
                retVal = "<?xml version=\"1.0\" encoding=\"iso-8859-1\" ?>" + retVal;
                retVal = retVal.Trim();
                context.Response.ContentType = "text/xml; charset=iso-8859-1";
              }
              string xmlTransform = string.Empty;
              try { xmlTransform = xdoc.SelectSingleNode("/MOOPData/storedProcedure/transform").InnerText; }
              catch { }
              if (xmlTransform != string.Empty && xmlTransform != null && !context.Request.Params.AllKeys.Contains("ignore_transform"))
              {
                  string transformContentType = "text/html";
                  try { transformContentType = xdoc.SelectSingleNode("/MOOPData/storedProcedure/transform").Attributes["contentType"].Value; }
                  catch { }
                  xmlFragment = abs.GetBlob(fragmentLocation, xmlTransform, "", ref ar, "");
                  if (ar.Succeeded)
                  {
                    XsltUtil xslu = new XsltUtil();
                      retVal = XsltUtil.TransformXml(retVal, System.Text.ASCIIEncoding.ASCII.GetString(xmlFragment));

                      context.Response.ContentType = transformContentType;
                  }
                 
                  

              }
              else
              {
                  // Check for JSon Request
                  
                  MessageFormatOptions defaultOptions = new MessageFormatOptions();
                  string preferredContentType = string.Empty;
                  if (context.Request.HttpMethod == "GET" || context.Request.HttpMethod == "DELETE")
                  {
                      preferredContentType = (context.Request.AcceptTypes ?? new string[0])
                                .Select(m => m.Split(';')[0])
                                .FirstOrDefault(m => defaultOptions.MimeInputTypes.ContainsKey(m))
                                ?? defaultOptions.DefaultContentType;
                      if (preferredContentType.Trim() == string.Empty)
                          preferredContentType = context.Request.Headers["Content-Type"];
                  }
                  else preferredContentType = context.Request.Headers["Content-Type"];
                  if (preferredContentType == "application/json")
                  {
                      context.Response.ContentType = preferredContentType;
                      XmlDocument doc = new XmlDocument();
                      doc.LoadXml(retVal);
                      retVal = JsonConvert.SerializeXmlNode(doc);
                  }
                  else
                      context.Response.ContentType = "text/xml";
              }
            }
          }
        }
        catch (Exception ex)
        {
          FrameworkUtility u = new FrameworkUtility(new Utility().ResolveDataConnection("sqlAzureConnection"));
          FrameworkUtility.ParsedURI pUri = new FrameworkUtility.ParsedURI(context.Request.RawUrl);
          u.LogData(pUri.domainName, pUri.folderName, pUri.pageName, context.Request.HttpMethod, context.Request.UserHostAddress, "", "QueryError", ex.ToString(),
            u.nvc2XML(context.Request.Headers));
          //retVal = string.Format("<!-- {0} -->", ex.ToString());
          context.Response.StatusDescription = "An error occured but it was logged for later review";
          context.Response.StatusCode = 400;
        }
        finally { if (retVal == string.Empty) retVal = "<root />"; }
        
        context.Response.Cache.SetExpires(DateTime.Now.AddSeconds(expirationSeconds));
        context.Response.Cache.SetCacheability(hc);
        context.Response.Write(retVal);
      }
      catch (Exception ex)
      {
        context.Response.StatusCode = 404;
        context.Response.Close();
      }
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

    /// <summary>
    /// Convert a string to a secure, irreversible hash
    /// </summary>
    /// <param name="data">String to convert</param>
    /// <returns>md5 version of the string</returns>
    private string md5(string data)
    {
      return Convert.ToBase64String(new System.Security.Cryptography.MD5CryptoServiceProvider().ComputeHash(System.Text.Encoding.Default.GetBytes(data)));
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
