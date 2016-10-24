using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using FoliaEntity.conv;
using System.Diagnostics;

namespace FoliaEntity {
  /* -------------------------------------------------------------------------------------
   * Name:  fpConv
   * Goal:  Routines that perform the actual conversion
   * History:
   * 2/oct/2015 ERK Created
     ------------------------------------------------------------------------------------- */
  class feConv {
    // ========================= Declarations local to me =================================
    private ErrHandle errHandle = new ErrHandle();
    private String loc_sDirOut = "";
    private Regex regQuoted = new Regex("xmlns(\\:(\\w)*)?=\"(.*?)\"");
    // ======================== Getters and setters =======================================

    /* -------------------------------------------------------------------------------------
     * Name:        ParseOneFoliaEntity
     * Goal:        Master routine to parse one folia.xml Dutch file (Sonar + Lassy)
     * Parameters:  sFileIn     - File to be processed
     *              sDirOut     - Directory where the output files should come
     *              sDirParsed  - Directory where already parsed Alpino files are kept
     *              bOverwrite  - Overwrite existing output or not
     *              bIsDebug    - Debugging mode on or off
     *              bKeepGarbage- Do not delete temporary files
     * History:
     * 24/oct/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool ParseOneFoliaEntity(String sFileIn, String sDirOut, String sDirParsed,
        bool bOverwrite, bool bIsDebug, bool bKeepGarbage) {
      XmlDocument pdxSent = null; // The folia input sentence
      XmlNode ndxFoliaS;          // Input FoLiA format sentence
      XmlNamespaceManager nsFolia;// Namespace manager for folia input
      XmlReader rdFolia = null;
      XmlWriter wrFolia = null;
      XmlWriterSettings wrSet = null;
      int iSentNum;
      List<String> lstSent;       // Container for the tokenized sentences
      List<String> lstSentId;     // Container for the FoLiA id's of the tokenized sentences
      List<XmlDocument> lstAlp;   // Container for the Alpino parses
      List<String> lstAlpFile;    // Alpino file names
      util.xmlTools oXmlTools = new util.xmlTools(errHandle);

      try {
        // Validate
        if (!File.Exists(sFileIn)) return false;
        // Make sure dirout does not contain a trailing /
        if (sDirOut.EndsWith("/") || sDirOut.EndsWith("\\"))
          sDirOut = sDirOut.Substring(0, sDirOut.Length - 1);
        // Construct output file name
        String sFileOut = sDirOut + "/" + Path.GetFileName(sFileIn);
        this.loc_sDirOut = sDirOut;

        // If the output file is already there: skip it
        if (!bOverwrite && File.Exists(sFileOut)) { debug("Skip existing [" + sFileOut + "]"); return true; }


        // Other initialisations
        pdxSent = new XmlDocument();
        lstSent = new List<string>();
        lstSentId = new List<string>();
        lstAlp = new List<XmlDocument>();
        lstAlpFile = new List<String>();
        wrSet = new XmlWriterSettings();
        wrSet.Indent = true;
        wrSet.NewLineHandling = NewLineHandling.Replace;
        wrSet.NamespaceHandling = NamespaceHandling.OmitDuplicates;
        // wrSet.NewLineOnAttributes = true;

        // The 'twopass' method:
        //      (1) Read the FoLiA with a stream-reader sentence-by-sentence
        //          a. Extract the sentence text (tokenize)
        //          b. Parse it using Alpino
        //          c. Save the parse in a sub-directory
        //      (2) Read the Folia with a stream-reader again sentence-by-sentence
        //          a. Retrieve the stored Alpino parse
        //          b. Alpino >> Psdx + adaptation of structure
        //          c. Psdx >> Folia 
        //          d. Insert resulting <syntax> as child under existing <s>
        //          e. Write output into streamwriter
        // Open the input file
        debug("Starting: " + sFileIn);
        iSentNum = 0;
        // Make room for the intermediate results
        String sShort = Path.GetFileNameWithoutExtension(sFileOut);
        String sFileOutDir = Path.GetDirectoryName(sFileOut) + "/" + sShort;
        String sFileOutLog = sFileOutDir + ".log";
        if (!Directory.Exists(sFileOutDir)) Directory.CreateDirectory(sFileOutDir);
        // Remove the last slash for clarity
        if (sFileOutDir.EndsWith("/") || sFileOutDir.EndsWith("\\"))
          sFileOutDir = sFileOutDir.Substring(0, sFileOutDir.Length - 1);

        // Open input file and output file
        using (rdFolia = XmlReader.Create(new StreamReader(sFileIn))) {
          StreamWriter wrText = new StreamWriter(sFileOut);
          using (wrFolia = XmlWriter.Create(wrText, wrSet)) {
            iSentNum = 0;
            // Walk through the input file
            while (!rdFolia.EOF && rdFolia.Read()) {
              // The <annotations> need to be adapted: signal what layer we are adding
              if (rdFolia.IsStartElement("annotations")) {
                // ==========================================
                // Get the <annotations> section and adapt it
                String sAnnot = rdFolia.ReadOuterXml();
                // (1) Put into Xml document
                XmlDocument pdxAnn = new XmlDocument(); pdxAnn.LoadXml(sAnnot);
                // (2) Create a namespace mapping for the folia *source* xml document
                nsFolia = new XmlNamespaceManager(pdxAnn.NameTable);
                nsFolia.AddNamespace("df", pdxAnn.DocumentElement.NamespaceURI);
                // (3) Add the Lassy syntactic annotations
                oXmlTools.SetXmlDocument(pdxAnn);
                ndxFoliaS = pdxAnn.SelectSingleNode("./descendant-or-self::df:annotations", nsFolia);
                oXmlTools.AddXmlChild(ndxFoliaS, "alignment-annotation",
                  "annotator", "ErwinKomen", "attribute",
                  "annotatortype", "automatic", "attribute",
                  "set", "ErwinKomen-NEL", "attribute");
                // (4) Write the new <annotations> node to the writer
                XmlReader rdResult = XmlReader.Create(new StringReader(ndxFoliaS.SelectSingleNode("./descendant-or-self::df:annotations", nsFolia).OuterXml));
                wrFolia.WriteNode(rdResult, true);
                // wrFolia.WriteString("\n");
                wrFolia.Flush();
              } else if (rdFolia.IsStartElement("s")) {
                // ============================================
                // SENTENCE: Read the <s> element as one string
                String sWholeS = rdFolia.ReadOuterXml();
                iSentNum++;
                // Process the <s> element:
                // (1) put it into an XmlDocument
                XmlDocument pdxSrc = new XmlDocument();
                pdxSrc.LoadXml(sWholeS);
                // (2) Create a namespace mapping for the folia *source* xml document
                nsFolia = new XmlNamespaceManager(pdxSrc.NameTable);
                nsFolia.AddNamespace("df", pdxSrc.DocumentElement.NamespaceURI);
                // (3) preparations: read the sentence as XML
                ndxFoliaS = pdxSrc.SelectSingleNode("./descendant-or-self::df:s[1]", nsFolia);
                // (4) Check if this sentence contains an <entity>
                List<XmlNode> lstEnt = oXmlTools.FixList(ndxFoliaS.SelectNodes("./descendant::df:entity", nsFolia));
                if (lstEnt.Count>0) {
                  // (5) Get a list of <w> nodes under this <s> -- this is used in all <entity> items of this <s>
                  List<XmlNode> lstW = oXmlTools.FixList(ndxFoliaS.SelectNodes("./descendant::df:w/child::df:t", nsFolia));
                  // (6) preparations: retrieve the @xml:id attribute for logging
                  String sSentId = ndxFoliaS.Attributes["xml:id"].Value;
                  // (6b) Log the fact that we are processing this sentence
                  doOneLogLine(sFileOutLog, "s[" + iSentNum + "]: " + sSentId + "\r");

                  // There are one or more entities to be processed: process them one-by-one
                  for (int j = 0; j < lstEnt.Count; j++) {
                    // (6) Combine the WORDS within the entity ref into a string
                    List<XmlNode> lstEntW = oXmlTools.FixList(lstEnt[j].SelectNodes("./child::df:wref", nsFolia));
                    String sEntity = ""; String idStart = "";
                    for (int k = 0; k < lstEntW.Count; k++) {
                      if (sEntity == "") {
                        // Note the start id of the entity
                        idStart = lstEntW[k].Attributes["id"].Value;
                      } else sEntity += " ";
                      sEntity += lstEntW[k].Attributes["t"].Value;
                    }

                    // (7) Calculate the offset for this particular entity as well as the sentence string
                    String sSent = "";
                    int iOffset = 0;
                    for (int k = 0; k < lstW.Count; k++) {
                      // Make sure spaces are added at the appropriate places
                      if (sSent != "") sSent += " ";
                      // Note where the offset is
                      if (lstW[k].Attributes["id"].Value == idStart) {
                        { iOffset = sSent.Length; }
                      }
                      // Extend the sentence
                      sSent += lstW[k].Value;
                    }

                    // Check and remove any existing alignments...
                    List<XmlNode> lstAlg = oXmlTools.FixList(lstEnt[j].SelectNodes("./child::df:alignment", nsFolia));
                    for (int k=lstAlg.Count-1;k>=0;k--) { lstAlg[k].RemoveAll(); lstEnt[j].RemoveChild(lstAlg[k]); }

                    // Create an entity object to be processed

                  }
                  // (4) retrieve the parse of the tokenized sentence
                  String sAlpFile = sFileOutDir + "/" + iSentNum + ".xml";
                  if (!getAlpinoParse(sAlpFile, lstAlp, lstAlpFile)) {
                    errHandle.DoError("ParseOneFoliaWithAlpino", "Failed to perform getAlpinoParse");
                    return false;
                  }
                  if (lstAlp.Count == 0) { errHandle.DoError("ParseOneFoliaWithAlpino", "Could not retrieve parsed alpino sentence"); return false; }
                  // (5) Retrieve the *first* alpino parse
                  XmlNode ndxAlpino = lstAlp[0].SelectSingleNode("./descendant-or-self::alpino_ds");
                  // (6) ALpino -> Psdx: convert the <node> structure into a psdx one with <forest> and <eTree> etc
                  XmlNode ndxPsdx = objAlpPsdx.oneSent(ndxAlpino, sSentId, lstAlpFile[0], ref lstW);
                  // (7) Psdx -> FoLiA: Convert the <forest> structure to a FoLiA sentence <s>
                  XmlNode ndxFolia = objPsdxFolia.oneSent(ndxPsdx, sSentId, "", ref lstW);

                  // (8) Insert the <syntax> node as child to the original <s> node
                  ndxFoliaS = pdxSrc.SelectSingleNode("./descendant-or-self::df:s[@xml:id = '" + sSentId + "']", nsFolia);
                  // XmlDocument pdxFolia = ndxFolia.OwnerDocument;
                  XmlNode ndxSyntax = pdxSrc.ImportNode(ndxFolia.SelectSingleNode("./descendant-or-self::syntax"), true);
                  ndxFoliaS.AppendChild(ndxSyntax);

                  // (9) Copy the adaptations of the word list into the [pdxSrc] list
                  oXmlTools.SetXmlDocument(pdxSrc);
                  XmlNode ndxLastW = ndxFoliaS.SelectSingleNode("./descendant::df:w[last()]", nsFolia);
                  for (int j = 0; j < lstW.Count; j++) {
                    // Get this element and the class
                    XmlNode ndxOneW = lstW[j]; String sClass = ndxOneW.Attributes["class"].Value;
                    // Action depends on type
                    switch (sClass) {
                      case "Zero":
                      case "Star":
                        // Relocate the new node
                        ndxLastW.ParentNode.InsertAfter(ndxOneW, ndxLastW);
                        ndxLastW = ndxOneW;
                        break;
                      default:
                        // Adding the class attribute is not needed: it is already done in the list
                        //oXmlTools.AddAttribute(ndxFoliaS.SelectSingleNode("./descendant::df:w[@xml:id='" +
                        //  ndxOneW.Attributes["xml:id"].Value + "']", nsFolia), "class", sClass);
                        break;
                    }
                  }

                  // (9) Check for <t> nodes under the original folia
                  if (ndxFoliaS.SelectNodes("./child::t", nsFolia).Count == 0) {
                    // Get all <t> nodes in the created folia
                    XmlNodeList ndxTlist = ndxFolia.SelectNodes("./child::t");
                    for (int j = 0; j < ndxTlist.Count; j++) {
                      // Copy this <t> node
                      XmlNode ndxOneT = pdxSrc.ImportNode(ndxTlist[j], true);
                      ndxFoliaS.PrependChild(ndxOneT);
                    }
                  }
                }


                // (10) Write the new <s> node to the writer
                XmlReader rdResult = XmlReader.Create(new StringReader(ndxFoliaS.SelectSingleNode("./descendant-or-self::df:s", nsFolia).OuterXml));
                wrFolia.WriteNode(rdResult, true);
                // wrFolia.WriteString("\n");
                wrFolia.Flush();
                rdResult.Close();
              } else {
                // Just write it out
                WriteShallowNode(rdFolia, wrFolia);
              }
            }
            // Finish reading input
            rdFolia.Close();
            // Finish writing
            wrFolia.Flush();
            wrFolia.Close();
            wrFolia = null;
          }
        }

        //  Garbage collection
        if (!bKeepGarbage) {
          // Clean-up: remove temporary files as well as temporary directory
          Directory.Delete(sFileOutDir, true);
          // Clean-up: remove the .log file
          File.Delete(sFileOutDir + ".log");
        }


        // Be positive
        return true;
      } catch (Exception ex) {
        errHandle.DoError("ParseOneFoliaEntity", ex); // Provide standard error message
        return false;
      }
    }

    /// <summary>
    /// Write one message line to console and to log file
    /// </summary>
    /// <param name="sFileOutLog"></param>
    /// <param name="sLogMsg"></param>
    private void doOneLogLine(String sFileOutLog, String sLogMsg) {
      errHandle.Status(sLogMsg + "\n");
      File.AppendAllText(sFileOutLog, sLogMsg + "\n");
    }

 

    /* -------------------------------------------------------------------------------------
     * Name:  foliaTokenize
     * Goal:  Tokenize the FoLiA sentence
     * History:
     * 2/oct/2015   ERK Created
     * 26/oct/2015  ERK Added @sType
       ------------------------------------------------------------------------------------- */
    private String foliaTokenize(XmlDocument pdxSent, XmlNamespaceManager nsFolia, String sType = "bare") {
      // Get all the words in this sentence
      XmlNodeList ndxList = pdxSent.SelectNodes("//df:w", nsFolia);
      return foliaTokenize(ndxList, nsFolia, sType);
    }
    private String foliaTokenize(XmlNode ndxSent, XmlNamespaceManager nsFolia, String sType = "bare") {
      // Get all the words in this sentence
      XmlNodeList ndxList = ndxSent.SelectNodes("./descendant-or-self::df:w", nsFolia);
      return foliaTokenize(ndxList, nsFolia, sType);
    }
    private String foliaTokenize(XmlNodeList ndxList, XmlNamespaceManager nsFolia, String sType = "bare") {
      try {
        // Put the words into a list, as well as the identifiers
        List<String> lstWords = new List<string>();
        List<String> lstWrdId = new List<string>();
        for (int i = 0; i < ndxList.Count; i++) {
          lstWords.Add(ndxList.Item(i).SelectSingleNode("./child::df:t", nsFolia).InnerText);
          lstWrdId.Add(ndxList.Item(i).Attributes["xml:id"].Value);
        }
        // Combining the words together depends on @sType
        String sBack = "";
        switch (sType) {
          case "bare":
            // Combine identifier and words into sentence
            sBack = String.Join(" ", lstWords.ToArray());
            break;
          case "folia":
            // Combine identifier and words into sentence
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lstWords.Count; i++) {
              XmlNode ndxW = ndxList.Item(i);
              String sPosTag = ndxW.SelectSingleNode("./child::df:pos", nsFolia).Attributes["class"].Value;
              String sLemma = ndxW.SelectSingleNode("./child::df:lemma", nsFolia).Attributes["class"].Value;
              // Make sure a word does *NOT* contain spaces!!
              String sWord = lstWords[i].Replace(" ", "");
              sb.Append("[ @folia " + sLemma + " " + sPosTag + " " + sWord + " ] ");
            }
            sBack = sb.ToString();
            break;
        }
        // Return the result
        return sBack;
      } catch (Exception ex) {
        errHandle.DoError("foliaTokenize", ex); // Provide standard error message
        return "";
        throw;
      }
    }

 
    /* -------------------------------------------------------------------------------------
     * Name:  WriteShallowNode
     * Goal:  Copy piece-by-piece
     * History:
     * 2/oct/2015 ERK Created
       ------------------------------------------------------------------------------------- */
    static void WriteShallowNode(XmlReader reader, XmlWriter writer) {
      if (reader == null) {
        throw new ArgumentNullException("reader");
      }
      if (writer == null) {
        throw new ArgumentNullException("writer");
      }
      switch (reader.NodeType) {
        case XmlNodeType.Element:
          writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
          writer.WriteAttributes(reader, true);
          if (reader.IsEmptyElement) {
            writer.WriteEndElement();
          }
          break;
        case XmlNodeType.Text:
          writer.WriteString(reader.Value);
          break;
        case XmlNodeType.Whitespace:
        case XmlNodeType.SignificantWhitespace:
          writer.WriteWhitespace(reader.Value);
          break;
        case XmlNodeType.CDATA:
          writer.WriteCData(reader.Value);
          break;
        case XmlNodeType.EntityReference:
          writer.WriteEntityRef(reader.Name);
          break;
        case XmlNodeType.XmlDeclaration:
        case XmlNodeType.ProcessingInstruction:
          writer.WriteProcessingInstruction(reader.Name, reader.Value);
          break;
        case XmlNodeType.DocumentType:
          writer.WriteDocType(reader.Name, reader.GetAttribute("PUBLIC"), reader.GetAttribute("SYSTEM"), reader.Value);
          break;
        case XmlNodeType.Comment:
          writer.WriteComment(reader.Value);
          break;
        case XmlNodeType.EndElement:
          writer.WriteFullEndElement();
          break;
      }

    }

    /// <summary>
    /// Write a debugging message on the console
    /// </summary>
    /// <param name="sMsg"></param>
    static void debug(String sMsg) { Console.WriteLine(sMsg); }

 

 
  }
}

