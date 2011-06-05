using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;

using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Data.SqlClient;
using System.Data;

using System.IO;
using System.Xml;
using System.Xml.Xsl;

namespace MOOP_Framework_Utilities
{
  public class Utility
  {

    public string dataConnectionString = string.Empty;

    /// <summary>
    /// ParsedURL represents the domain/folder/page structure used for lookups.
    /// Regex help from http://www.cambiaresearch.com/cambia3/snippets/csharp/regex/uri_regex.aspx#parsing
    /// </summary>
    public struct ParsedURI
    {
      public string domainName,
          folderName,
          pageName;

      public ParsedURI(string URL)
      {
        string regexPattern = @"^(?<s1>(?<s0>[^:/\?#]+):)?(?<a1>"
              + @"//(?<a0>[^/\?#]*))?(?<p0>[^\?#]*)"
              + @"(?<q1>\?(?<q0>[^#]*))?"
              + @"(?<f1>#(?<f0>.*))?";

        Regex re = new Regex(regexPattern, RegexOptions.ExplicitCapture);
        Match m = re.Match(URL);
        domainName = m.Groups["a0"].Value;// +"  (Authority without //)<br>";
        folderName = m.Groups["p0"].Value.Substring(0, m.Groups["p0"].Value.LastIndexOf("/")); // +"  (Path)<br>";
        pageName = m.Groups["p0"].Value.Substring(m.Groups["p0"].Value.LastIndexOf("/") + 1);

        // Note: The passed in URL should never have arguments built in but, if it does, this strips
        //       them out.
        if (pageName.IndexOf(",") != -1)
          pageName = pageName.Substring(0, pageName.IndexOf(",")) + pageName.Substring(pageName.LastIndexOf("."));
      }
    }

    /// <summary>
    /// Convert a string to a secure, irreversible hash
    /// </summary>
    /// <param name="data">String to convert</param>
    /// <returns>md5 version of the string</returns>
    public string md5(string data)
    {
      return Convert.ToBase64String(new System.Security.Cryptography.MD5CryptoServiceProvider().ComputeHash(System.Text.Encoding.Default.GetBytes(data)));
    }


    public Utility(string dbConnection)
    { dataConnectionString = dbConnection; }


    public Utility()
    { }

    public string nvc2XML(NameValueCollection nvc)
    {
      StringBuilder sb = new StringBuilder("<?xml version=\"1.0\"?><data>");
      foreach (string key in nvc.AllKeys)
      {
        sb.AppendFormat("<nvp><name><![CDATA[{0}]]></name><value><![CDATA[{1}]]></value></nvp>", key, nvc[key]);
      }
      sb.Append("</data>");
      return sb.ToString();
    }




    public void LogData(string domainName, string folderPath, string pageName, string method, string ipAddress, string userID, string logEvent, string logMessage, string parameterInformation)
    {
      SqlCommand cmd = new SqlCommand("auditing.GeneralLogInsert", new SqlConnection(dataConnectionString));
      cmd.CommandType = CommandType.StoredProcedure;
      cmd.Parameters.Add("@DomainName", SqlDbType.NVarChar, 50);
      cmd.Parameters.Add("@FolderPath", SqlDbType.NVarChar, 2083);
      cmd.Parameters.Add("@PageName", SqlDbType.NVarChar, 2083);
      cmd.Parameters.Add("@Method", SqlDbType.NVarChar, 10);
      cmd.Parameters.Add("@IPAddress", SqlDbType.NVarChar, 50);
      cmd.Parameters.Add("@UserID", SqlDbType.NVarChar, 255);
      cmd.Parameters.Add("@LogEvent", SqlDbType.NVarChar, 100);
      cmd.Parameters.Add("@LogMessage", SqlDbType.NVarChar);
      cmd.Parameters.Add("@ParameterInformation", SqlDbType.Xml);
      if (domainName == string.Empty)
        domainName = "genericerror";

      cmd.Parameters["@DomainName"].Value = domainName;
      cmd.Parameters["@FolderPath"].Value = folderPath;
      cmd.Parameters["@PageName"].Value = pageName;
      cmd.Parameters["@Method"].Value = method;
      cmd.Parameters["@IPAddress"].Value = ipAddress;
      cmd.Parameters["@UserID"].Value = userID;
      cmd.Parameters["@LogEvent"].Value = logEvent;
      cmd.Parameters["@LogMessage"].Value = logMessage;
      cmd.Parameters["@ParameterInformation"].Value = parameterInformation;
      cmd.Connection.Open();
      cmd.ExecuteNonQuery();
      cmd.Connection.Close();
    }
  }

  public class XsltUtil
  {
    /// <summary>
    /// Transforms the supplied xml using the supplied xslt and returns the 
    /// result of the transformation
    /// </summary>
    /// <param name="xml">The xml to be transformed</param>
    /// <param name="xslt">The xslt to transform the xml</param>
    /// <returns>The transformed xml</returns>
    public static string TransformXml(string xml, string xslt)
    {
      // Simple data checks
      if (string.IsNullOrEmpty(xml))
      {
        throw new ArgumentException("Param cannot be null or empty", "xml");
      }
      if (string.IsNullOrEmpty(xslt))
      {
        throw new ArgumentException("Param cannot be null or empty", "xslt");
      }

      // Create required readers for working with xml and xslt
      StringReader xsltInput = new StringReader(xslt);
      StringReader xmlInput = new StringReader(xml);
      XmlTextReader xsltReader = new XmlTextReader(xsltInput);
      XmlTextReader xmlReader = new XmlTextReader(xmlInput);

      // Create required writer for output
      StringWriter stringWriter = new StringWriter();
      XmlTextWriter transformedXml = new XmlTextWriter(stringWriter);

      // Create a XslCompiledTransform to perform transformation
      XslCompiledTransform xsltTransform = new XslCompiledTransform();

      try
      {
        xsltTransform.Load(xsltReader);
        xsltTransform.Transform(xmlReader, transformedXml);
      }
      catch (XmlException xmlEx)
      {
        // TODO : log - "Could not load XSL transform: \n\n" + xmlEx.Message
        throw;
      }
      catch (XsltException xsltEx)
      {
        // TODO : log - "Could not process the XSL: \n\n" + xsltEx.Message + "\nOn line " + xsltEx.LineNumber + " @ " + xsltEx.LinePosition)
        throw;
      }
      catch (Exception ex)
      {
        // TODO : log
        throw;
      }

      return stringWriter.ToString();
    }
  }
}
