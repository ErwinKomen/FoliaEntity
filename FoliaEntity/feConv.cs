using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml;
using System.Diagnostics;
using FoliaEntity;

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
    private bool bSpotlight = false;  // Use SPOTLIGHT api
    private bool bHistograph = false; // Use HISTOGRAPH api
    private bool bFlask = false;      // Use FLASK api
    private bool bLaundromat = false; // Use LOD Laundromat api
    private Regex regQuoted = new Regex("xmlns(\\:(\\w)*)?=\"(.*?)\"");
    // ======================== Getters and setters =======================================
    public void set_method(String sMethods) {
      for (int j = 0; j < sMethods.Length; j++) {
        String sSub = sMethods.Substring(j, 1);
        switch (sSub) {
          case "f": bFlask = true; break;
          case "s": bSpotlight = true; break;
          case "l": bLaundromat = true; break;
          case "h": bHistograph = true; break;
        }
      }
    }
    // Class initializers
    public feConv() {
      // Class may be initialized just like that
    }
    public feConv(String sMethods) {
      // Initialize class by passing on methods
      this.set_method(sMethods);
    }

    /* -------------------------------------------------------------------------------------
     * Name:        ParseOneFoliaEntity
     * Goal:        Master routine to parse one folia.xml Dutch file (Sonar + Lassy)
     * Parameters:  sDirIn      - Input directory
     *              sFileIn     - File to be processed
     *              sDirOut     - Directory where the output files should come
     *              sAnnotator  - Name of the annotator
     *              bOverwrite  - Overwrite existing output or not
     *              bIsDebug    - Debugging mode on or off
     *              bKeepGarbage- Do not delete temporary files
     * History:
     * 24/oct/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool ParseOneFoliaEntity(String sDirIn, String sFileIn, String sDirOut, String sAnnotator,
        bool bOverwrite, bool bIsDebug, bool bKeepGarbage, ref int iHits, ref int iFail) {
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
      String sConfidence = "0.20";
      String sFileShort = "";     // Short file name
      StringWriter swText = null; // The whole text of the document (as context)
      List<String> lstEntExpr;    // List of named entity expressions
      List<int> lstEntIdx;        // List of indices of these named entity expressions
      List<link> lstEntFlask;     // Flask answer
      int iTextLength = 0;        // Length of the text

      try {
        // Validate
        if (!File.Exists(sFileIn)) return false;
        // Make sure dirout does not contain a trailing /
        if (sDirOut.EndsWith("/") || sDirOut.EndsWith("\\"))
          sDirOut = sDirOut.Substring(0, sDirOut.Length - 1);
        // Construct output file name
        sFileShort = Path.GetFileName(sFileIn);
        String sSubDir = sDirOut;
        if (Directory.Exists(sDirIn)) {
          String sFileInDir = Path.GetDirectoryName(sFileIn);
          sSubDir += sFileInDir.Substring(sDirIn.Length) ;
        }
        String sFileOut = Path.GetFullPath(sSubDir + "/" + sFileShort);
        String sFileTmp = sFileOut + ".tmp";
        this.loc_sDirOut = sDirOut;

        // If the output file is already there: skip it
        if (!bOverwrite && File.Exists(sFileOut)) { debug("Skip existing [" + sFileOut + "]"); return true; }

        // Some kind of logging to see what is going on
        if (bIsDebug) {
          errHandle.Status("Input: " + sFileIn + "\nOutput: " + sFileOut);
        }

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
        // wrSet.ConformanceLevel = ConformanceLevel.Auto;
        iHits = 0;
        iFail = 0;
        // wrSet.NewLineOnAttributes = true;

        lstEntExpr = new List<String>();  // List of named-entities
        lstEntIdx = new List<int>();      // List of named-entity positions
        lstEntFlask = new List<link>();   // List of links found by FLASK

        // Two methods are implemented, each of which works on different APIs
        // (1) Single-pass method
        //     a. context is <s> (sentence)
        //     b. NE-linking is done one-by-one, with sentence-context as background
        // (2) Double-pass method
        //     a. First pass collects (i) all NE's, and (ii) text of whole document
        //     b. NE-links are then determined with the information from (a)
        //     c. Second pass adds the found information for each NE

        // Open the input file
        debug("Starting: " + sFileIn);
        iSentNum = 0;
        // Make room for the intermediate results
        String sShort = Path.GetFileNameWithoutExtension(sFileOut);
        String sFileOutDir = Path.GetDirectoryName(sFileOut) + "/" + sShort;
        String sFileOutLog = Path.GetFullPath( sFileOutDir + ".log");
        // Clear any previous logging
        if (File.Exists(sFileOutLog)) {
          // Make sure to overwrite what is there
          File.WriteAllText(sFileOutLog, "");
        }

        // Open input file and output file
        using (rdFolia = XmlReader.Create(new StreamReader(sFileIn))) {
          StreamWriter wrText = new StreamWriter(sFileTmp);
          swText = new StringWriter();      // Storage for the whole text context
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
                  "annotator", sAnnotator, "attribute",
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

                  // We also need to get the full sentence text
                  String sSent = "";

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
                    // (6b) Log the fact that we are processing this sentence
                    if (bIsDebug) {
                      errHandle.Status("s[" + iSentNum + "]: " + sSentId + " [" + sEntity + "]\r");
                    }

                    // (7) Calculate the offset for this particular entity as well as the sentence string
                    int iOffset = 0;
                    for (int k = 0; k < lstW.Count; k++) {
                      // Make sure spaces are added at the appropriate places
                      if (sSent != "") sSent += " ";
                      // Note where the offset is
                      if (lstW[k].ParentNode.Attributes["xml:id"].Value == idStart) {
                        iOffset = sSent.Length;
                      }
                      // Extend the sentence
                      sSent += lstW[k].InnerText;
                    }

                    // Process the named entity and the position within the larger text
                    lstEntExpr.Add(sEntity);
                    lstEntIdx.Add(iOffset + iTextLength);

                    // Removing previous alignments depends on the method used
                    if (this.bFlask || this.bHistograph || this.bLaundromat || this.bSpotlight) {
                      // Check and remove any existing alignments...
                      List<XmlNode> lstAlg = oXmlTools.FixList(lstEnt[j].SelectNodes("./child::df:alignment", nsFolia));
                      for (int k = lstAlg.Count - 1; k >= 0; k--) { lstAlg[k].RemoveAll(); lstEnt[j].RemoveChild(lstAlg[k]); }
                    }

                    // Create an entity object to be processed
                    String sClass = lstEnt[j].Attributes["class"].Value;
                    entity oEntity = new entity(this.errHandle, sEntity, sClass, sSent, iOffset.ToString(), idStart);
                    oEntity.set_debug(bIsDebug);
                    // Pass on the settings of the different SINGLE_PASS methods
                    oEntity.set_histograph(bHistograph);
                    oEntity.set_laundromat(bLaundromat);
                    oEntity.set_spotlight(bSpotlight);

                    if (oEntity.oneEntityToLinks(sConfidence)) {
                      // Keep track of hits and failures
                      iHits += oEntity.get_hits();
                      iFail += oEntity.get_fail();
                      // Process the alignments that have been found
                      List<link> lstAlign = oEntity.get_links();
                      for (int k=0;k<lstAlign.Count;k++) {
                        // Process this link
                        link lnkThis = lstAlign[k];
                        // Convert this link into an <alignment> item and add it to the current <entity>
                        XmlDocument pdxAlign = new XmlDocument();
                        String sAlignModel = "<FoLiA xmlns:xlink='http://www.w3.org/1999/xlink' xmlns='http://ilk.uvt.nl/folia'>" +
                          "<s><alignment format='application/json' class='NEL' xlink:href='' xlink:type='simple' src=''></alignment>" +
                          "</s></FoLiA>";
                        pdxAlign.LoadXml(sAlignModel);
                        // Set up a namespace manager for folia
                        XmlNamespaceManager nmsDf = new XmlNamespaceManager(pdxAlign.NameTable);
                        nmsDf.AddNamespace("df", pdxAlign.DocumentElement.NamespaceURI);

                        XmlNode ndxAlignment = pdxAlign.SelectSingleNode("./descendant-or-self::df:alignment", nmsDf);
                        ndxAlignment.Attributes["xlink:href"].Value = lnkThis.uri;
                        ndxAlignment.Attributes["src"].Value = lnkThis.service;
                        lstEnt[j].AppendChild(pdxSrc.ImportNode(ndxAlignment, true));

                        // Process logging output
                        String sLogMsg = sFileShort + "\t" + sSentId + "\t" + sClass + "\t" + lnkThis.toCsv();
                        doOneLogLine(sFileOutLog, sLogMsg);
                      }
                    }

                  }

                  // Process the sentence into the larger text
                  swText.WriteLine(sSent);
                  // Keep track of the text length
                  iTextLength += sSent.Length + 1;

                }


                // (10) Write the new <s> node to the writer
                XmlReader rdResult = null;
                try {
                  rdResult = XmlReader.Create(new StringReader(ndxFoliaS.SelectSingleNode("./descendant-or-self::df:s", nsFolia).OuterXml));
                  wrFolia.WriteNode(rdResult, true);
                  // wrFolia.WriteString("\n");
                } catch (Exception ex) {
                  errHandle.DoError("ParseOneFoliaEntity", ex); // Provide standard error message
                  return false;
                }
                wrFolia.Flush();
                rdResult.Close();
              } else {
                // Just write it out
                try {
                  WriteShallowNode(rdFolia, wrFolia);
                } catch (Exception ex) {
                  errHandle.DoError("ParseOneFoliaEntity", ex); // Provide standard error message
                  return false;
                }
              }
            }
            // Finish reading input
            rdFolia.Close();
            // Finish writing
            wrFolia.Flush();
            wrFolia.Close();
            wrFolia = null;
          }
          wrText.Close();
        }

        // Are we to use DOCUMENT-based methods??
        if (bFlask) {
          // Prepare document-based methods
          String sDocText = swText.ToString();
          entity oDocMethods = new entity(errHandle, "", "", "", "", "");
          // Pass on the settings of the different methods
          oDocMethods.set_flask(bFlask);
          // Call the method
          if (!oDocMethods.docEntityToLinks(sDocText, lstEntExpr, lstEntIdx, ref lstEntFlask)) {
            // Do something
            errHandle.DoError("parseOneFoliaEntity", "Could not perform docEntityToLinks");
          }

          // Second pass: go through the text once more
          int iEntityIndex = -1;
          using (rdFolia = XmlReader.Create(new StreamReader(sFileTmp))) {
            StreamWriter wrText = new StreamWriter(sFileOut);
            swText = new StringWriter();      // Storage for the whole text context
            using (wrFolia = XmlWriter.Create(wrText, wrSet)) {
              iSentNum = 0;
              // Walk through the input file
              while (!rdFolia.EOF && rdFolia.Read()) {
                // Look for a sentence again
                if (rdFolia.IsStartElement("s")) {
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
                  if (lstEnt.Count > 0) {
                    // (6) preparations: retrieve the @xml:id attribute for logging
                    String sSentId = ndxFoliaS.Attributes["xml:id"].Value;

                    // There are one or more entities to be processed: process them one-by-one
                    for (int j = 0; j < lstEnt.Count; j++) {
                      // Look in the stored list of named entity expressions and offsets
                      iEntityIndex++;
                      String sEntity = lstEntExpr[iEntityIndex];
                      int iOffset = lstEntIdx[iEntityIndex];

                      // (6b) Log the fact that we are processing this sentence
                      if (bIsDebug) {
                        errHandle.Status("s2[" + iSentNum + "]: " + sSentId + " [" + sEntity + "]\r");
                      }

                      // Get the FLASK entity resolution, which should provide exactly one additional alignment
                      link lnkThis = lstEntFlask[iEntityIndex];

                      if (lnkThis != null) {
                        // Convert this link into an <alignment> item and add it to the current <entity>
                        XmlDocument pdxAlign = new XmlDocument();
                        String sAlignModel = "<FoLiA xmlns:xlink='http://www.w3.org/1999/xlink' xmlns='http://ilk.uvt.nl/folia'>" +
                          "<s><alignment format='application/json' class='NEL' xlink:href='' xlink:type='simple' src=''></alignment>" +
                          "</s></FoLiA>";
                        pdxAlign.LoadXml(sAlignModel);
                        // Set up a namespace manager for folia
                        XmlNamespaceManager nmsDf = new XmlNamespaceManager(pdxAlign.NameTable);
                        nmsDf.AddNamespace("df", pdxAlign.DocumentElement.NamespaceURI);

                        XmlNode ndxAlignment = pdxAlign.SelectSingleNode("./descendant-or-self::df:alignment", nmsDf);
                        ndxAlignment.Attributes["xlink:href"].Value = lnkThis.uri;
                        ndxAlignment.Attributes["src"].Value = lnkThis.service;
                        lstEnt[j].AppendChild(pdxSrc.ImportNode(ndxAlignment, true));

                        String sClass = "";

                        // Process logging output
                        String sLogMsg = sFileShort + "\t" + sSentId + "\t" + sClass + "\t" + lnkThis.toCsv();
                        doOneLogLine(sFileOutLog, sLogMsg);
                      }

                    }
                  }


                  // (10) Write the new <s> node to the writer
                  XmlReader rdResult = null;
                  try {
                    rdResult = XmlReader.Create(new StringReader(ndxFoliaS.SelectSingleNode("./descendant-or-self::df:s", nsFolia).OuterXml));
                    wrFolia.WriteNode(rdResult, true);
                    // wrFolia.WriteString("\n");
                  } catch (Exception ex) {
                    errHandle.DoError("ParseOneFoliaEntity", ex); // Provide standard error message
                    return false;
                  }
                  wrFolia.Flush();
                  rdResult.Close();
                } else {
                  // Just write it out
                  try {
                    WriteShallowNode(rdFolia, wrFolia);
                  } catch (Exception ex) {
                    errHandle.DoError("ParseOneFoliaEntity", ex); // Provide standard error message
                    return false;
                  }
                }
              }
              // Finish reading input
              rdFolia.Close();
              // Finish writing
              wrFolia.Flush();
              wrFolia.Close();
              wrFolia = null;
            }
            wrText.Close();
          }
        }

        //  Garbage collection
        if (!bKeepGarbage) {
          //// Clean-up: remove temporary files as well as temporary directory
          //Directory.Delete(sFileOutDir, true);
          // Clean-up: remove the .log file
          File.Delete(sFileOutLog);
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
      // errHandle.Status(sLogMsg + "\r");
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

