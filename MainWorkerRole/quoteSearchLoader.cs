using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Diagnostics;
using System.Diagnostics;

using Finsel.AzureCommands;
using System.Xml;
using System.Data;
using System.Data.SqlClient;
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

namespace MainWorkerRole
{
  class quoteSearchLoader
  {

    /// <summary>
    ///  Declare a delegate that takes a single string parameter and has no return type.
    /// </summary>
    /// <param name="message">Message to display</param>
    /// <param name="LogOnly">Whether this message should show up in the log only</param>
    public delegate void LogHandler(string message, bool LogOnly);

    /// <summary>
    ///  Declare a delegate that takes a single string parameter and has no return type.
    /// </summary>
    /// <param name="ex">The Exception</param>
    public delegate void ErrorHandler(Exception ex);


    /// <summary>
    /// Log handler for notifications to the client
    /// </summary>
    private LogHandler mvLogHandler;

    /// <summary>
    /// Log handler for notifications to the client
    /// </summary>
    private ErrorHandler mvErrorHandler;

    /// <summary>
    /// Default notifycaller is not log only
    /// </summary>
    /// <param name="msg">Message to display</param>
    void NotifyCaller(string msg)
    {
      if (mvLogHandler != null)
        mvLogHandler(msg, false);
      Trace.TraceInformation(msg);

    }

    /// <summary>
    /// Default notifyError is not log only
    /// </summary>
    /// <param name="msg">Message to display</param>
    void NotifyError(Exception ex)
    {
      if (mvErrorHandler != null)
        mvErrorHandler(ex);

      Trace.WriteLine(ex.ToString());
    }

    /// <summary>
    /// Structure to store Lucene Processing Data
    /// </summary>
    struct aLuceneData
    {
      /// <summary>
      /// Field.Store enum
      /// </summary>
      public Field.Store fieldStore;
      /// <summary>
      /// Field.Index enum
      /// </summary>
      public Field.Index indexType;
      /// <summary>
      /// Field Name in the Lucene Doc
      /// </summary>
      public string luceneName;
      /// <summary>
      /// Column name from the database
      /// </summary>
      public string dataName;



      /// <summary>
      /// Create and populate a Lucened Processing Data structure
      /// </summary>
      /// <param name="FieldStore">Text representation of a Field.Store enum</param>
      /// <param name="IndexType">Text representation of a Field.Index enum</param>
      /// <param name="LuceneName">Field name in the Lucene Doc</param>
      /// <param name="DataName">Column name from the database</param>
      public aLuceneData(string FieldStore, string IndexType, string LuceneName, string DataName)
      {
        switch (FieldStore.ToLower())
        {
          case "compress": fieldStore = Field.Store.COMPRESS; break;
          case "no": fieldStore = Field.Store.NO; break;
          case "yes": fieldStore = Field.Store.YES; break;
          default: fieldStore = Field.Store.NO; break;
        }

        switch (IndexType.ToLower())
        {
          case "analyzed": indexType = Field.Index.ANALYZED; break;
          case "analyzed_no_norms": indexType = Field.Index.ANALYZED_NO_NORMS; break;
          case "no": indexType = Field.Index.NO; break;
          case "no_norms": indexType = Field.Index.NOT_ANALYZED_NO_NORMS; break;
          case "not_analyzed": indexType = Field.Index.NOT_ANALYZED; break;
          case "not_analyzed_no_norms": indexType = Field.Index.NOT_ANALYZED_NO_NORMS; break;
          case "tokenized": indexType = Field.Index.ANALYZED; break;
          case "un_tokenized": indexType = Field.Index.NOT_ANALYZED; break;
          default: indexType = Field.Index.NO; break;
        }
        luceneName = LuceneName;
        dataName = DataName;
      }

    }

    public quoteSearchLoader()
    {
    }

    public void optimizeIndex(string azureContainerName)
    {

      XmlDocument xdoc = new XmlDocument();
      xdoc.LoadXml(new Utility().getConfigXML());
      XmlNode xNode = xdoc.SelectSingleNode(string.Format("//blobdata[@name='default']"));

      string azureAccount = xNode.Attributes["account"].Value;
      string azureEndpoint = xNode.Attributes["endpoint"].Value;
      string azureSharedKey = xNode.Attributes["accountSharedKey"].Value;
      string blobStorage = xNode.Attributes["endpoint"].Value;

      xNode = xdoc.SelectSingleNode(string.Format("//fragmentData/Setting[@name='HandlerFragments']"));
      string fragmentLocation = xNode.Attributes["value"].Value;

      Microsoft.WindowsAzure.StorageCredentialsAccountAndKey scaak = new Microsoft.WindowsAzure.StorageCredentialsAccountAndKey(azureAccount, azureSharedKey);
      Microsoft.WindowsAzure.CloudStorageAccount csa = new Microsoft.WindowsAzure.CloudStorageAccount(scaak, false);
      AzureDirectory azureDirectory = new AzureDirectory(csa, azureContainerName, new RAMDirectory());
      bool findexExists = false;
      try
      {
        findexExists = IndexReader.IndexExists(azureDirectory);
        if ((findexExists) && IndexWriter.IsLocked(azureDirectory))
          azureDirectory.ClearLock("write.lock");
      }
      catch (Exception e)
      {
        Trace.WriteLine(e.ToString());
        return;
      }

      IndexWriter idxW = new IndexWriter(azureDirectory, new SnowballAnalyzer("English"), !findexExists, new IndexWriter.MaxFieldLength(1024));
      idxW.SetRAMBufferSizeMB(10.0);
      idxW.SetUseCompoundFile(false);
      idxW.SetMaxMergeDocs(10000);
      idxW.SetMergeFactor(100);
      idxW.Optimize();
    }

    public void processRequest(string queryName, NameValueCollection htKeys)
    {

      string retVal = string.Empty;
      XmlDocument xdoc = new XmlDocument();
      xdoc.LoadXml(new Utility().getConfigXML());
      XmlNode xNode = xdoc.SelectSingleNode(string.Format("//blobdata[@name='default']"));

      string azureAccount = xNode.Attributes["account"].Value;
      string azureEndpoint = xNode.Attributes["endpoint"].Value;
      string azureSharedKey = xNode.Attributes["accountSharedKey"].Value;
      string blobStorage = xNode.Attributes["endpoint"].Value;

      xNode = xdoc.SelectSingleNode(string.Format("//fragmentData/Setting[@name='HandlerFragments']"));
      string fragmentLocation = xNode.Attributes["value"].Value;
      try
      {
        AzureBlobStorage abs = new AzureBlobStorage(azureAccount, blobStorage, azureSharedKey, "SharedKey");
        azureResults ar = new azureResults();
        // Get the page name and replace the .q extension with .xml
        if (!queryName.ToLower().EndsWith(".xml"))
          queryName += ".xml";
        byte[] xmlFragment = abs.GetBlob(fragmentLocation, queryName, "", ref ar, "");
        if (!ar.Succeeded)
        {
          NotifyError(new Exception(ar.StatusCode.ToString()));
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

          SqlCommand cmd = new SqlCommand(storedProcedureName, new SqlConnection(new Utility().ResolveDataConnection(connectionStringName)));
          cmd.CommandType = CommandType.StoredProcedure;
          XmlNodeList xnl = xdoc.SelectNodes("/MOOPData/luceneData/field");
          Field.Store[] fieldStore = new Field.Store[xnl.Count];
          Field.Index[] indexType = new Field.Index[xnl.Count];
          string[] luceneName = new string[xnl.Count];
          string[] dataName = new string[xnl.Count];
          bool[] isIncludedInOlioSearchFlag = new bool[xnl.Count];
          bool[] isKeyFieldFlag = new bool[xnl.Count];
          string olioSearchFieldName = string.Empty;
          string azureContainerName = string.Empty;
          olioSearchFieldName = xdoc.SelectSingleNode("//MOOPData/luceneData/olioSearchFieldName[1]").InnerText;
          azureContainerName = xdoc.SelectSingleNode("//MOOPData/luceneData/azureContainer[1]").InnerText;
          for (int i = 0; i < xnl.Count; i++)
          {
            XmlNode node = xnl[i];
            switch (node.Attributes["store"].Value.ToLower())
            {
              case "compress": fieldStore[i] = Field.Store.COMPRESS; break;
              case "no": fieldStore[i] = Field.Store.NO; break;
              case "yes": fieldStore[i] = Field.Store.YES; break;
              default: fieldStore[i] = Field.Store.NO; break;
            }

            switch (node.Attributes["index"].Value.ToLower())
            {
              case "analyzed": indexType[i] = Field.Index.ANALYZED; break;
              case "analyzed_no_norms": indexType[i] = Field.Index.ANALYZED_NO_NORMS; break;
              case "no": indexType[i] = Field.Index.NO; break;
              case "no_norms": indexType[i] = Field.Index.NOT_ANALYZED_NO_NORMS; break;
              case "not_analyzed": indexType[i] = Field.Index.NOT_ANALYZED; break;
              case "not_analyzed_no_norms": indexType[i] = Field.Index.NOT_ANALYZED_NO_NORMS; break;
              case "tokenized": indexType[i] = Field.Index.ANALYZED; break;
              case "un_tokenized": indexType[i] = Field.Index.NOT_ANALYZED; break;
              default: indexType[i] = Field.Index.NO; break;
            }
            dataName[i] = node.Attributes["dataName"].Value;
            luceneName[i] = node.Attributes["luceneName"].Value;
            isKeyFieldFlag[i] = node.Attributes["isKeyField"].Value == "true";
            isKeyFieldFlag[i] = node.Attributes["isKeyField"].Value == "true";
            isIncludedInOlioSearchFlag[i] = node.Attributes["isIncludedInOlioSearch"].Value == "true";
          }

          xnl = xdoc.SelectNodes("//parameter");
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
              case "ipaddress": sp.Value = "127.0.0.1"; break;
              case "domainname": sp.Value = ""; break;
              default: if (htKeys[urlParameterName] != null)
                  sp.Value = htKeys[urlParameterName];
                else
                  sp.Value = (defaultValue.ToLower() == "dbnull" ? DBNull.Value
                  : (object)defaultValue);
                break;
            }
            cmd.Parameters.Add(sp);
          }

          cmd.Connection.Open();
          SqlDataReader dr = cmd.ExecuteReader();
          Microsoft.WindowsAzure.StorageCredentialsAccountAndKey scaak = new Microsoft.WindowsAzure.StorageCredentialsAccountAndKey(azureAccount, azureSharedKey);
          Microsoft.WindowsAzure.CloudStorageAccount csa = new Microsoft.WindowsAzure.CloudStorageAccount(scaak, false);
          AzureDirectory azureDirectory = new AzureDirectory(csa, azureContainerName, new RAMDirectory());
          bool findexExists = false;
          try
          {
            findexExists = IndexReader.IndexExists(azureDirectory);
            if ((findexExists) && IndexWriter.IsLocked(azureDirectory))
              azureDirectory.ClearLock("write.lock");
          }
          catch (Exception e)
          {
            Trace.WriteLine(e.ToString());
            return;
          }

          IndexWriter idxW = new IndexWriter(azureDirectory, new SnowballAnalyzer("English"), !findexExists, new IndexWriter.MaxFieldLength(1024));
          idxW.SetRAMBufferSizeMB(10.0);
          idxW.SetUseCompoundFile(false);
          idxW.SetMaxMergeDocs(10000);
          idxW.SetMergeFactor(100);
          while (dr.Read())
          {
            StringBuilder olioSearch = new StringBuilder();
            Document doc = new Document();
            for (int i = 0; i <= dataName.GetUpperBound(0); i++)
            {


              if (isKeyFieldFlag[i])
              {

                NotifyCaller(string.Format("Processing {0}", dr[dataName[i]].ToString().ToLower()));
                idxW.DeleteDocuments(new Term(luceneName[i], dr[dataName[i]].ToString().ToLower()));
                doc.Add(new Field(luceneName[i], dr[dataName[i]].ToString().ToLower(), Field.Store.YES, Field.Index.NOT_ANALYZED));
              }
              else
                try
                {
                  doc.Add(new Field(luceneName[i], dr[dataName[i]].ToString(), fieldStore[i], indexType[i]));

                  if (isIncludedInOlioSearchFlag[i])
                    olioSearch.AppendFormat("\r\n{0}", dr[dataName[i]].ToString());
                }
                catch (Exception ex)
                {
                  NotifyError(ex);
                }
            }
            if (olioSearch.ToString() != string.Empty && olioSearchFieldName != string.Empty)
              doc.Add(new Field(olioSearchFieldName, olioSearch.ToString(), Field.Store.NO, Field.Index.ANALYZED));
            idxW.AddDocument(doc);
          }
          idxW.Commit();
          idxW.Close();

        }
      }

      catch (Exception ex)
      {
        MOOPFramework.FrameworkUtility u = new MOOPFramework.FrameworkUtility(new Utility().ResolveDataConnection("sqlAzureConnection"));
        u.LogData("localhost", "quoteSearchLoader", "testing", string.Empty, string.Empty, "", "QueryError", ex.ToString(),
          u.nvc2XML(htKeys));
        //retVal = string.Format("<!-- {0} -->", ex.ToString());
        NotifyError(new Exception("An error occured but it was logged for later review"));
      }
      finally { if (retVal == string.Empty) retVal = "<root />"; }

    }

  }
}
