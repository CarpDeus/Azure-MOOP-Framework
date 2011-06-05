using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using Microsoft.WindowsAzure.ServiceRuntime;
using System.Text;
using Finsel.AzureCommands;
using System.Xml;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;

using System.Data;
using System.Collections;
using System.Collections.Specialized;
using Lucene.Net;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Search;
using Lucene.Net.QueryParsers;
using Lucene.Net.Analysis.Snowball;
using Lucene.Net.Store.Azure;



namespace MOOP_WebRole
{
  /// <summary>
  /// Summary description for lucene_search
  /// </summary>
  public class lucenesearch : IHttpHandler
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

        xNode = xdoc.SelectSingleNode(string.Format("//fragmentData/Setting[@name='luceneFragments']"));
        string fragmentLocation = xNode.Attributes["value"].Value ;

        string queryName = string.Empty;

        SetCompression(context);

        try
        {
          AzureBlobStorage abs = new AzureBlobStorage(azureAccount, blobStorage, azureSharedKey, "SharedKey");
          azureResults ar = new azureResults();
          // Get the page name and replace the .q extension with .xml
          queryName = context.Request.Path;
          queryName = queryName.Substring(queryName.LastIndexOf("/") + 1);
          queryName = queryName.Substring(0, queryName.LastIndexOf(".")) + ".xml";
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
            string azureContainerName = string.Empty;
            azureContainerName = xdoc.SelectSingleNode("//MOOPData/luceneSearch/azureContainer[1]").InnerText;
            Microsoft.WindowsAzure.StorageCredentialsAccountAndKey scaak = new Microsoft.WindowsAzure.StorageCredentialsAccountAndKey(azureAccount, azureSharedKey);
            Microsoft.WindowsAzure.CloudStorageAccount csa = new Microsoft.WindowsAzure.CloudStorageAccount(scaak, false);
            AzureDirectory azDir = new AzureDirectory(csa, azureContainerName, new RAMDirectory());
            

//            IndexSearcher searcher = new IndexSearcher("azure-lucene-search");
            try
            {
              var q = new BooleanQuery();
              XmlNode xn = xdoc.SelectSingleNode("//luceneSearch[1]");
              XmlNodeList xnl = xn.SelectNodes("//query");
              Query[] qArray = new Query[xnl.Count];
              bool hasReplaceableSearchValues = false;
              bool hasReplacedAtLeastOneValue = false;
              bool requirementsMet = true;
              for (int i = 0; i < xnl.Count; i++)
              {
                XmlNode node = xnl[i];
                string term = string.Empty;
                term = node.Attributes["term"].Value;                 
                string termValue = node.Attributes["termValue"].Value;
                string variableValue = node.Attributes["variableValue"].Value;
                string parmValue = string.Empty;
                string requiredField = node.Attributes["required"].Value;
                if (variableValue == "true") // See if there is a replacement attempt
                {
                  if (requiredField == "true" && !context.Request.Params.AllKeys.Contains(termValue))
                    requirementsMet = false;
                  hasReplaceableSearchValues = true; // Set the flag to say we are attempting to replace the value
                  if (context.Request.Params.AllKeys.Contains(termValue)) // The parameter exists
                  {
                    hasReplacedAtLeastOneValue = true;
                    parmValue = context.Request.Params[termValue].Replace("+", " ").Replace("%20", " ");
                  }
                }
                if (node.Attributes["useAnalyzer"].Value == "true") // Should we use the analyzer
                {

                  if (variableValue == "true" && context.Request.Params.AllKeys.Contains(termValue)) // Did we actually have a replaceable value or a static value
                    qArray[i] = new QueryParser(Lucene.Net.Util.Version.LUCENE_29, term, new SnowballAnalyzer("English")).Parse(parmValue);
                   if (variableValue != "true")
                    qArray[i] = new QueryParser(Lucene.Net.Util.Version.LUCENE_29, term, new SnowballAnalyzer("English")).Parse(termValue);
                }
                else // Using a Boolean
                {
                  if (variableValue == "true" && context.Request.Params.AllKeys.Contains(termValue)) // Did we actually have a replaceable value or a static value
                    qArray[i] = new TermQuery(new Term(term, parmValue));
                  if (variableValue != "true")
                    qArray[i] = new TermQuery(new Term(term, termValue));
                }
                if (qArray[i] != null)
                {
                  switch (node.Attributes["booleanClauseOccurs"].Value.ToLower())
                  {
                    case "must": q.Add(new BooleanClause(qArray[i], BooleanClause.Occur.MUST)); break;
                    case "must_not": q.Add(new BooleanClause(qArray[i], BooleanClause.Occur.MUST_NOT)); break;
                    case "should": q.Add(new BooleanClause(qArray[i], BooleanClause.Occur.SHOULD)); break;
                    default: q.Add(new BooleanClause(qArray[i], BooleanClause.Occur.MUST)); break;
                  }
                }
              }
              IndexSearcher searcher2 = new IndexSearcher(azDir, true);
              TopScoreDocCollector collector = TopScoreDocCollector.create(1000, true);
              // If we have either no replaceable values or have replaceable values and at least one was provided
              if (!hasReplaceableSearchValues || (hasReplaceableSearchValues && hasReplacedAtLeastOneValue))
                if (requirementsMet)
                  searcher2.Search(q, collector);
              int indexID = 1;
              ScoreDoc[] hits = collector.TopDocs().scoreDocs;
              StringBuilder xmlOutput = new StringBuilder();
              xmlOutput.AppendFormat("<?xml version=\"1.0\"?><root>");
              for (int i = 0; i < hits.Length; ++i)
              {
                xmlOutput.AppendFormat("<hit>");
                int docId = hits[i].doc;
                Document d = searcher2.Doc(docId);
                xmlOutput.AppendFormat("<score>{0}</score><docID>{1}</docID>", hits[i].score, indexID ++);
                foreach (Field f in d.GetFields())
                {
                  if (f.StringValue() == null)
                    xmlOutput.AppendFormat("<{0} />", f.Name());
                  else
                    xmlOutput.AppendFormat("<{0}><![CDATA[{1}]]></{0}>", f.Name(), f.StringValue());
                }
                xmlOutput.AppendFormat("</hit>");
              }
              xmlOutput.AppendFormat("</root>");
              retVal = xmlOutput.ToString();
            }
            catch (Exception ex)
            {
              retVal = "<root />";
            }
            string luceneTransform = string.Empty;
            luceneTransform = xdoc.SelectSingleNode("/MOOPData/luceneSearch/transform").InnerText;
            if (luceneTransform != string.Empty && luceneTransform != null)
            {
              xmlFragment = abs.GetBlob(fragmentLocation, luceneTransform, "", ref ar, "");
              if (ar.Succeeded)
              {
                MOOP_Framework_Utilities.XsltUtil xslu = new MOOP_Framework_Utilities.XsltUtil();
                retVal = MOOP_Framework_Utilities.XsltUtil.TransformXml(retVal, System.Text.ASCIIEncoding.ASCII.GetString(xmlFragment));
                string transformContentType = "text/html";
                try { transformContentType = xdoc.SelectSingleNode("/MOOPData/luceneSearch/transform").Attributes["contentType"].Value; }
                catch { }
                  context.Response.ContentType = transformContentType;
              }
            }
            else
            {
              context.Response.ContentType = "text/xml";
            }
          }
        }
        catch (Exception ex)
        {
          MOOP_Framework_Utilities.Utility u = new MOOP_Framework_Utilities.Utility(new Utility().ResolveDataConnection("sqlAzureConnection"));
          MOOP_Framework_Utilities.Utility.ParsedURI pUri = new MOOP_Framework_Utilities.Utility.ParsedURI(context.Request.RawUrl);
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
