using System;
using System.Collections.Generic;
using System.Net;
using System.Xml;
using System.Collections.Specialized;
using System.Web;
using System.IO;
using System.Text;
using System.Threading.Tasks;


namespace FoliaEntity {
  public class entity {
    // =========================================================================================================
    // Name: entity
    // Goal: This module implements Named Entity Linking
    // History:
    // 24/oct/2016 ERK Created
    // ========================================== LOCAL VARIABLES ==============================================
    private String sApiStart = "http://spotlight.sztaki.hu:2232/rest/";
    private int iBufSize = 1024;        // Buffer size
    private string strNs = "";          // Possible namespace URI
    private ErrHandle errHandle;        // Our own copy of the error handle
    private String loc_sEntity = "";    // The Named Entity string
    private String loc_sClass = "";     // The kind of NE (loc, per etc)
    private String loc_sSent = "";      // The sentence
    private String loc_sOffset = "";    // Offset as a string
    private String loc_sId = "";        // The id of the sentence
    private String loc_sRequest = "";   // Kind of request: disambiguate or annotate
    private int loc_iHits = 0;
    private int loc_iFail = 0;
    private List<link> loc_lstLinks = null; // Resulting links
    private String loc_sReqModel = "<annotation text=''><surfaceForm name='' offset='' /></annotation>";
    // =========================================================================================================
    public entity(ErrHandle objErr, String sEntity, String sClass, String sSent, String sOffset, String sId) {
      this.errHandle = objErr;
      this.loc_sClass = sClass;
      this.loc_sEntity = sEntity;
      this.loc_sId = sId;
      this.loc_sOffset = sOffset;
      this.loc_sSent = sSent;
      // Reset counters
      this.loc_iFail = 0;
      this.loc_iHits = 0;
    }

    // ================== GETTERS and SETTERS =================================================================
    public List<link> get_links() { return this.loc_lstLinks; }

    /* -------------------------------------------------------------------------------------
     * Name:        oneEntityToLinks
     * Goal:        Try to provide a link to the NE we have stored
     * Parameters:  sConfidence     - Minimal level of confidence that should be met
     * History:
     * 24/oct/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool oneEntityToLinks(String sConfidence) {
      try {
        // Initialize the return list
        this.loc_lstLinks = new List<link>();

        // Try making a disambiguation spotlight request
        bool bResult = this.oneSpotlightRequest("disambiguate", sConfidence, ref loc_lstLinks);
        if(!bResult) {
          // Give it another go: try the 'annotate' method
          bResult = this.oneSpotlightRequest("annotate", sConfidence, ref loc_lstLinks);
        }

        // Return what we found
        return bResult;
      } catch (Exception ex) {
        errHandle.DoError("entity/oneEntityToLinks", ex); // Provide standard error message
        return false;
      }
    }

    /* -------------------------------------------------------------------------------------
     * Name:        oneSpotlightRequest
     * Goal:        Perform one request to SPOTLIGHT
     * Parameters:  sMethod     - Either 'disambiguate' or 'annotate'
     *              sConfidence - Minimal level of confidence that should be met
     *              lstLinks    - List of 'link' objects
     * History:
     * 24/oct/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    private bool oneSpotlightRequest(String sMethod, String sConfidence, ref List<link> lstLinks) {
      String sXmlPost = "";
      String sData = "";
      XmlWriterSettings wrSet = new XmlWriterSettings();

      try {

        // Action depends on the type of request
        switch(sMethod) {
          case "annotate":
            sXmlPost = this.loc_sEntity;
            break;
          case "disambiguate":
            // Create a data XML object
            XmlDocument pdxData = new XmlDocument();
            pdxData.LoadXml(loc_sReqModel);
            // Fill in the variables in this XML document
            XmlNode ndxAnn = pdxData.SelectSingleNode("./descendant-or-self::annotation");
            ndxAnn.Attributes["text"].Value = this.loc_sSent;
            XmlNode ndxSurface = pdxData.SelectSingleNode("./descendant::surfaceForm");
            ndxSurface.Attributes["name"].Value = this.loc_sEntity;
            ndxSurface.Attributes["offset"].Value = this.loc_sOffset;
            // Convert the xml document to a string
            wrSet.OmitXmlDeclaration = true;
            wrSet.Encoding = Encoding.UTF8;
            using (var stringWriter = new StringWriter())
              using (var xmlTextWriter = XmlWriter.Create(stringWriter, wrSet)) {
              pdxData.WriteTo(xmlTextWriter);
              xmlTextWriter.Flush();
              sXmlPost = stringWriter.GetStringBuilder().ToString();
            }
            break;
        }
        // Prepare the POST string to be sent
        NameValueCollection oQueryString = HttpUtility.ParseQueryString(String.Empty, Encoding.UTF8);
        oQueryString.Add("confidence", sConfidence);
        oQueryString.Add("text", sXmlPost);
        sData = Uri.EscapeUriString(HttpUtility.UrlDecode(oQueryString.ToString()));

        // Make a request
        XmlDocument pdxReply = MakeXmlPostRequest(sMethod, sData);

        // Check the reply and process it
        if (pdxReply != null) {
          // Find a list of all <Resource> answers
          XmlNodeList lstResources = pdxReply.SelectNodes("./descendant::Resource");
          for (int i=0;i<lstResources.Count;i++) {
            // Get access to this resource
            XmlNode resThis = lstResources[i];
            // Calculate 'found' and 'classmatch'
            String eClass = this.loc_sClass;
            String resType = resThis.Attributes["types"].Value;
            String sClassMatch = "no";
            String sHit = "";
            bool bFound = false;
            switch (eClass) {
              case "loc":   // location
                if (resType.Contains(":Place")) { bFound = true; sClassMatch = "yes"; }
                break;
              case "org":   // organization
                if (resType.Contains("Organization") || resType.Contains("Organisation")) { bFound = true; sClassMatch = "yes"; }
                break;
              case "pro":   // product
                if (resType.Contains(":Language")) { bFound = true; sClassMatch = "yes"; }
                break;
              case "per":   // person
                if (resType.Contains(":Agent")) { bFound = true; sClassMatch = "yes"; }
                break;
              case "misc":  // miscellaneous
                bFound = true; sClassMatch = "misc";
                break;
              default:      // Anything else
                if (resType == "") { bFound = true; sClassMatch = "empty"; }
                break;
            }

            // Do we have a hit?
            if (bFound) {
              this.loc_iHits++; sHit = "true";
            } else {
              this.loc_iFail++; sHit = "false";
            }

            // Create a link object
            link oLink = new link(resThis.Attributes["URI"].Value,
              resThis.Attributes["surfaceForm"].Value, 
              resThis.Attributes["types"].Value,
              sClassMatch, 
              resThis.Attributes["support"].Value, 
              resType,
              resThis.Attributes["similarityScore"].Value, 
              resThis.Attributes["percentageOfSecondRank"].Value,
              sHit);
            // Add the link object to the list of what is returned
            lstLinks.Add(oLink);
          }
        }

        // Be positive
        return true;
      } catch (Exception ex) {
        errHandle.DoError("entity/oneSpotlightRequest", ex); // Provide standard error message
        return false;
      }

    }

    /* -------------------------------------------------------------------------------------
     * Name:        MakeXmlPostRequest
     * Goal:        Issue a POST request 
     * Parameters:  sMethod     - Either 'disambiguate' or 'annotate'
     *              sConfidence - Minimal level of confidence that should be met
     *              lstLinks    - List of 'link' objects
     * History:
     * 24/oct/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    private XmlDocument MakeXmlPostRequest(String sMethod, String sData) {
      try {
        // Prepare the data
        ASCIIEncoding ascii = new ASCIIEncoding();
        byte[] postBytes = ascii.GetBytes(sData.ToString());

        // Create the request string
        String sRequest = sApiStart + sMethod.ToLower();

        // Create a request
        HttpWebRequest request = (HttpWebRequest) WebRequest.Create(sRequest);
        // Set the method correctly
        request.Method = "POST";
        request.ContentLength = postBytes.Length;
        request.ContentType = "application/x-www-form-urlencoded";
        request.Accept = "text/xml";

        Stream dataStream = request.GetRequestStream();
        // Write the data to the request stream
        dataStream.Write(postBytes, 0, postBytes.Length);
        dataStream.Close();

        //// Set the header such a way that an XML reply is expected
        //request.Headers.Add(HttpRequestHeader.Accept, "text/xml");

        // Get a response
        HttpWebResponse response = null;
        try {
          response = (HttpWebResponse)request.GetResponse();
        } catch (Exception e) {
          errHandle.DoError("entity/MakeXmlPostRequest", e); // Provide standard error message
          return null;
        }
        StringBuilder sbReply = new StringBuilder();
        // Process the result
        using (Stream strResponse = response.GetResponseStream())
        using (StreamReader rdThis = new StreamReader(strResponse)) {
          Char[] readBuff = new Char[iBufSize];
          int iCount = rdThis.Read(readBuff, 0, iBufSize);
          while (iCount > 0) {
            // Append the information to the stringbuilder
            sbReply.Append(new String(readBuff, 0, iCount));
            // Make a follow-up request
            iCount = rdThis.Read(readBuff, 0, iBufSize);
          }
        }
        // Convert the XML reply to a processable object
        XmlDocument pdxReply = new XmlDocument();
        pdxReply.LoadXml(sbReply.ToString());
        // Return the XML document
        return pdxReply;
      } catch (Exception e) {
        errHandle.DoError("entity/MakeXmlPostRequest", e); // Provide standard error message
        return null;
      }
    }

  }

  public class link {
    public String uri = "";
    public String form = "";
    public String type = "";
    public String classmatch = "";
    public String support = "";
    public String offset = "";
    public String similarityScore = "";
    public String percentageOfSecondRank = "";
    public String hit = "";
    public link(String sUri, String sForm, String sType, String sClassmatch, String sSupport, String sOffset, 
      String sSimilarityScore, String sPercentageOfSecondRank, String sHit) {
      this.uri = sUri;
      this.form = sForm;
      this.classmatch = sClassmatch;
      this.support = sSupport;
      this.offset = sOffset;
      this.similarityScore = sSimilarityScore;
      this.percentageOfSecondRank = sPercentageOfSecondRank;
      this.hit = sHit;
    }
  }
}
