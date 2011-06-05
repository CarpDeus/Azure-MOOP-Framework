using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;


namespace MOOP_WebRole
{
  /// <summary>
  /// Summary description for fbDecoder
  /// </summary>
  public class facebookDecoder : IHttpHandler
  {

    public void ProcessRequest(HttpContext context)
    {
      string parm = context.Request.Params["signed_request"];
      UTF8Encoding encoding = new UTF8Encoding();
      string decodedJson = parm.Substring(parm.IndexOf(".")+1).Replace("=", string.Empty).Replace('-', '+').Replace('_', '/');
      byte[] base64JsonArray = Convert.FromBase64String(decodedJson.PadRight(decodedJson.Length + (4 - decodedJson.Length % 4) % 4, '='));
      string decodedText = encoding.GetString(base64JsonArray);
      context.Response.ContentType = "application/json";
      context.Response.Write(decodedText);
      context.Response.StatusCode = 200;
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