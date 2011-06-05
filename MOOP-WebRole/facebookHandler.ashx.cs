using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

using Microsoft.WindowsAzure.ServiceRuntime;
using Finsel.AzureCommands;
using System.Collections.Specialized;
using System.Text;
using System.Security.Cryptography;

using MOOP_Framework_Utilities;
using System.Xml;

namespace MOOP_WebRole
{

 


  

  /// <summary>
  /// Summary description for facebookHandler
  /// </summary>
  public class facebookHandler : IHttpHandler
  {

    public void ProcessRequest(HttpContext context)
    {
      StringBuilder retVal = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-16\" ?><root>");
      string accessCode = string.Empty;
      string accessToken = string.Empty;
      string redirectLocation = string.Empty;
      LogInformation(context.Request.RawUrl);
      string fbapp = string.Empty;
      string fbProcess = string.Empty;
      string fbApplicationID = string.Empty;
      string fbApiKey = string.Empty;
      string fbApplicationSecret = string.Empty;
      string fbMainPage = string.Empty;
      string fbProcessingQueue = string.Empty;
      string oAuthToken = string.Empty;
      string fbUserID = string.Empty;
      MOOP_Framework_Utilities.Utility.ParsedURI pUri = new MOOP_Framework_Utilities.Utility.ParsedURI(context.Request.RawUrl.Replace(":20000", ""));
      if (pUri.pageName.EndsWith(".fb"))
        fbapp = pUri.pageName;
      else
        fbapp = context.Request.Params["app"];
      GetFBInfo(fbapp, ref fbApplicationID, ref fbApiKey, ref fbApplicationSecret, ref fbMainPage, ref fbProcessingQueue);
      if (fbApiKey != string.Empty)
      {
        XmlDocument xdoc = new XmlDocument();
        xdoc.LoadXml(new Utility().getConfigXML());
        XmlNode xNode = xdoc.SelectSingleNode(string.Format("//blobData[@name='default']"));

        string azureAccount = xNode.SelectSingleNode("Setting[@name='account']").Attributes["value"].Value;
        string azureEndpoint = xNode.SelectSingleNode("Setting[@name='endpoint']").Attributes["value"].Value;
        string azureSharedKey = xNode.SelectSingleNode("Setting[@name='accountSharedKey']").Attributes["value"].Value;
        string blobStorage = azureEndpoint;
        
        if (context.Request.Params["signed_request"] != null)
        {
          NameValueCollection nvc = ParseFBSignature(context.Request.Params["signed_request"], fbApplicationSecret);
          if (nvc.Count > 0)
          {
            //"algorithm"="HMAC-SHA256";"expires"=0;"issued_at"=1290397336;"oauth_token"="124973180897202|4b611abbedc69357e0a82608-100000117684066|CIv0HZ1AQqn-FExSNgOfzij7ao4";"user_id"="100000117684066";
            oAuthToken = nvc["oauth_token"];
            fbUserID = "fb" + nvc["user_id"];
            //            retVal.AppendFormat("<oAuth>{0}</oAuth>", oAuthToken);
            DateTime fbIssuedAt = ConvertFromUnixTimestamp(Convert.ToDouble(nvc["issued_at"])).ToLocalTime();
            DateTime fbExpires = new DateTime();
            if (nvc["expires"] == "0")
              fbExpires = DateTime.MaxValue;
            else fbExpires = ConvertFromUnixTimestamp(Convert.ToDouble(nvc["expires"])).ToLocalTime();

            UpdateUserApplicationOAuth(fbUserID, fbApplicationID, oAuthToken, fbIssuedAt, fbExpires);
            if (DateTime.UtcNow < fbExpires)
            {
              string fbMessage = string.Format("<root><fbGetUserInfo fbUserID=\"{0}\" fbOAuthToken=\"{1}\" authExpires=\"{2}\" /></root>", fbUserID, oAuthToken, fbExpires);
              Finsel.AzureCommands.AzureQueueStorage aqs = new Finsel.AzureCommands.AzureQueueStorage(azureAccount, string.Format("http://{0}.queue.core.windows.net", azureAccount), azureSharedKey, "SharedKey");
              azureResults ar = aqs.Messages(cmdType.post, fbProcessingQueue, fbMessage, "", "");
            }

          }
        }
        if (fbUserID == string.Empty || fbUserID == "fb")
        {
          fbUserID = "fb" + context.Request.Params["userid"];
        }

        if (context.Request.Params.AllKeys.Contains("delete"))
        {
          if (oAuthToken == string.Empty)
          {
            oAuthToken = GetFBUserAuth(fbUserID, fbApplicationID);
          }
          Amundsen.Utilities.HttpClient hc = new Amundsen.Utilities.HttpClient();
          hc.Execute(string.Format("https://graph.facebook.com/{0}?access_token={1}", context.Request.Params["requestid"], oAuthToken), "DELETE");
        }


      }

      context.Response.ContentType = "text/xml";
      retVal.AppendFormat("</root>");
      context.Response.Write(retVal.ToString());
    }

    static DateTime ConvertFromUnixTimestamp(double timestamp)
    {
      DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
      return origin.AddSeconds(timestamp);
    }

    private void UpdateUserApplicationOAuth(string UserID, string ApplicationID, string oAuthToken, DateTime IssuedAt, DateTime Expires)
    {
      SqlCommand cmd = new SqlCommand("UpdateUserApplicationOAuth", new SqlConnection(new Utility().ResolveDataConnection("runeLogConnection")));
      cmd.CommandType = CommandType.StoredProcedure;
      cmd.Parameters.Add("@UserID", SqlDbType.NVarChar, 255);
      cmd.Parameters.Add("@ApplicationID", SqlDbType.NVarChar, 255);
      cmd.Parameters.Add("@oAuthToken", SqlDbType.NVarChar, 256);
      cmd.Parameters.Add("@IssuedAt", SqlDbType.DateTime);
      cmd.Parameters.Add("@Expires", SqlDbType.DateTime);
      cmd.Parameters["@UserID"].Value = UserID;
      cmd.Parameters["@ApplicationID"].Value = ApplicationID;
      cmd.Parameters["@oAuthToken"].Value = oAuthToken;
      cmd.Parameters["@IssuedAt"].Value = IssuedAt.ToLocalTime();
      cmd.Parameters["@Expires"].Value = Expires.ToLocalTime();
      cmd.Connection.Open();
      cmd.ExecuteNonQuery();
      cmd.Connection.Close();
    }

    private void GetFBInfo(string ApplicationName, ref string AppID, ref string APIKey, ref string fbSecret, ref string fbMainPage, ref string fbProcessingQueue)
    {
      SqlCommand cmd = new SqlCommand("GetFBAppInfo", new SqlConnection( new Utility().ResolveDataConnection("runeLogConnection")));
      cmd.CommandType = CommandType.StoredProcedure;
      cmd.Parameters.Add("@ApplicationName", SqlDbType.NVarChar, 255);
      cmd.Parameters.Add("@FBApplicationID", SqlDbType.NVarChar, 255);
      cmd.Parameters.Add("@FBAPIKey", SqlDbType.NVarChar, 255);
      cmd.Parameters.Add("@FBApplicationSecret", SqlDbType.NVarChar, 255);
      cmd.Parameters.Add("@MainPageURI", SqlDbType.NVarChar, 1024);
      cmd.Parameters.Add("@ProcessingQueue", SqlDbType.NVarChar, 1024);
      cmd.Parameters["@FBApplicationID"].Direction = ParameterDirection.InputOutput;
      cmd.Parameters["@FBAPIKey"].Direction = ParameterDirection.InputOutput;
      cmd.Parameters["@FBApplicationSecret"].Direction = ParameterDirection.InputOutput;
      cmd.Parameters["@MainPageURI"].Direction = ParameterDirection.InputOutput;
      cmd.Parameters["@ProcessingQueue"].Direction = ParameterDirection.InputOutput;
      cmd.Parameters["@ApplicationName"].Value = ApplicationName;
      cmd.Parameters["@FBApplicationID"].Value = DBNull.Value;
      cmd.Parameters["@FBAPIKey"].Value = DBNull.Value;
      cmd.Parameters["@FBApplicationSecret"].Value = DBNull.Value;
      cmd.Parameters["@MainPageURI"].Value = DBNull.Value;
      cmd.Parameters["@ProcessingQueue"].Value = DBNull.Value;
      cmd.Connection.Open();
      cmd.ExecuteNonQuery();
      AppID = cmd.Parameters["@FBApplicationID"].Value.ToString();
      APIKey = cmd.Parameters["@FBAPIKey"].Value.ToString();
      fbSecret = cmd.Parameters["@FBApplicationSecret"].Value.ToString();
      fbMainPage = cmd.Parameters["@MainPageURI"].Value.ToString();
      fbProcessingQueue = cmd.Parameters["@ProcessingQueue"].Value.ToString();
      cmd.Connection.Close();
    }

    private void LogInformation(string info)
    {
          string sqlCMD = string.Format("insert into testLog values(getdate(), '{0}')", info.Replace("'","''"));
          SqlCommand cmd = new SqlCommand(sqlCMD, new SqlConnection(new Utility().ResolveDataConnection("runeLogConnection")));
          cmd.CommandType = CommandType.Text;
          cmd.Connection.Open();
          cmd.ExecuteNonQuery();
          cmd.Connection.Close();
    }

    private string GetFBUserAuth(string fbUserID, string fbApplicationID)
    {
      string retVal = string.Empty;
      if (!fbUserID.StartsWith("fb"))
        fbUserID = "fb" + fbUserID;
      SqlCommand cmd = new SqlCommand("GetUserAuth", new SqlConnection(new Utility().ResolveDataConnection("runeLogConnection")));
      cmd.CommandType = CommandType.StoredProcedure;
      cmd.Parameters.Add("@SocialUserID", SqlDbType.NVarChar, 255);
      cmd.Parameters.Add("@ApplicationID", SqlDbType.NVarChar, 255);
      cmd.Parameters.Add("@oAuthToken", SqlDbType.NVarChar, 255);
      cmd.Parameters["@oAuthToken"].Direction = ParameterDirection.InputOutput;
      cmd.Parameters["@SocialUserID"].Value=fbUserID;
      cmd.Parameters["@ApplicationID"].Value=fbApplicationID;
      cmd.Parameters["@oAuthToken"].Value= DBNull.Value;
      cmd.Connection.Open();
      cmd.ExecuteNonQuery();
      retVal  = cmd.Parameters["@oAuthToken"].Value.ToString();
      cmd.Connection.Close();
      return retVal;
    }

    private NameValueCollection ParseFBSignature(string signedRequest, string fbSecret)
    {
      NameValueCollection retVal = new NameValueCollection();
      try
      {
        UTF8Encoding encoding = new UTF8Encoding();
        string[] splitRequest = signedRequest.Split('.');
        string expectedSignature = splitRequest[0];
        string payload = splitRequest[1];
        string decodedJson = splitRequest[1].Replace("=", string.Empty).Replace('-', '+').Replace('_', '/');
        if (ValidateSignedRequest(expectedSignature, payload, fbSecret))
        {
          byte[] base64JsonArray = Convert.FromBase64String(decodedJson.PadRight(decodedJson.Length + (4 - decodedJson.Length % 4) % 4, '='));
          string decodedText = encoding.GetString(base64JsonArray);

          decodedText = decodedText.Substring(1, decodedText.Length - 2);
          string[] JsonArray = decodedText.Split(',');
          foreach (string s in JsonArray)
          {
            string[] details = s.Split(':');
            //sb.AppendFormat("<{0}>{1}</{0}>\r\n", details[0], details[1]);
            retVal.Add(details[0].Replace("\"", ""), details[1].Replace("\"", ""));
          }
        }
        else
        {
          Console.Write("nope!");
        }
      }
      catch { }
      return retVal;
    }

    public bool ValidateSignedRequest(string expectedSignature, string payload, string applicationSecret)
    {

      // Attempt to get same hash
      var Hmac = SignWithHmac(UTF8Encoding.UTF8.GetBytes(payload), UTF8Encoding.UTF8.GetBytes(applicationSecret));
      var HmacBase64 = ToUrlBase64String(Hmac);

      return (HmacBase64 == expectedSignature);
    }

    private string ToUrlBase64String(byte[] Input)
    {
      return Convert.ToBase64String(Input).Replace("=", String.Empty)
                                          .Replace('+', '-')
                                          .Replace('/', '_');
    }
    private byte[] SignWithHmac(byte[] dataToSign, byte[] keyBody)
    {
      using (var hmacAlgorithm = new HMACSHA256(keyBody))
      {
        hmacAlgorithm.ComputeHash(dataToSign);
        return hmacAlgorithm.Hash;
      }
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