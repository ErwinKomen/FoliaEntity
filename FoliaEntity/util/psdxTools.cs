using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace FoliaEntity.util {
  public class psdxTools {
    // =========================================================================================================
    // Name: psdxTools
    // Goal: This class implements XML functions for the Psdx format
    // History:
    // 22/sep/2010 ERK Created
    // ========================================== LOCAL VARIABLES ==============================================
    private ErrHandle errHandle;        // Our own copy of the error handle
    private xmlTools oXmlTools;         // Pointer to XmlTools that do the actual job
    private XmlDocument pdxCurrentFile; // Own copy of where XmlTools point to
    private int intMaxEtreeId = 0;      // Counter for Id of etree
    private string strNoText = "CODE|META|METADATA|E_S";
    private string strSpace = " \t\n\r";
    private string strNoWord = ") \t\n\r";
    private string SPEC_CHAR_IN = "tadegTADEG";
    private string SPEC_CHAR_OUT = "þæđëġÞÆĐËĠ";

    // =========================================================================================================
    public psdxTools(ErrHandle objErr, XmlDocument pdxThis) {
      this.errHandle = objErr;
      oXmlTools = new xmlTools(objErr);
      oXmlTools.SetXmlDocument(pdxThis);
      pdxCurrentFile = pdxThis;
    }

    // ============================ get/set ====================================================================
    public void setCurrentFile(XmlDocument pdxThis) { 
      this.pdxCurrentFile = pdxThis;
      // Also adapt the XmlDocument for my own oXmlTools
      oXmlTools.SetXmlDocument(pdxThis);
    }
    public XmlDocument getCurrentFile() { return this.pdxCurrentFile; }

    // ----------------------------------------------------------------------------------------------------------
    // Name :  AddEtreeChild
    // Goal :  Add an <eTree> child under [ndxParent]
    // History:
    // 26-04-2011  ERK Created
    // ----------------------------------------------------------------------------------------------------------
    public XmlNode AddEtreeChild(ref XmlNode ndxParent, int intId, string strLabel, int intSt, int intEn) {
      try {
        // Add the child
        return oXmlTools.AddXmlChild(ndxParent, "eTree",
          "Id", intId.ToString(), "attribute",
          "Label", strLabel, "attribute",
          "from", intSt.ToString(), "attribute",
          "to", intEn.ToString(), "attribute");
      } catch (Exception ex) {
        // Warn the user
        errHandle.DoError("modXmlNode/AddEtreeChild", ex);
        // Return failure
        return null;
      }
    }

    // ----------------------------------------------------------------------------------------------------------
    // Name :  AddEleafChild
    // Goal :  Add an <eLeaf> child under [ndxParent]
    // History:
    // 26-04-2011  ERK Created
    // ----------------------------------------------------------------------------------------------------------
    public XmlNode AddEleafChild(ref XmlNode ndxParent, string strType, string strText, int intSt, int intEn) {
      try {
        // Add the child
        return oXmlTools.AddXmlChild(ndxParent, "eLeaf",
          "Type", strType, "attribute",
          "Text", strText, "attribute",
          "from", intSt.ToString(), "attribute",
          "to", intEn.ToString(), "attribute",
          "n", "0", "attribute");
      } catch (Exception ex) {
        // Warn the user
        errHandle.DoError("modXmlNode/AddEleafChild", ex);
        // Return failure
        return null;
      }
    }

    // ------------------------------------------------------------------------------------
    // Name:   AddFeature
    // Goal:   Add the feature [strFname] with value [strFvalue] to the node
    //         Result:
    //           <fs type='[strFStype]'>
    //             <f name='[strFname] value='[strFvalue]' />
    //           </fs>
    // History:
    // 26-05-2010  ERK Created
    // ------------------------------------------------------------------------------------
    public bool AddFeature(ref XmlDocument pdxThis, ref XmlNode ndxThis, string strFStype, string strFname, string strFvalue) {
      XmlNode ndxChild = null; // Child node of type <fs>

      try {
        // Validate
        if (pdxThis == null) {
          return false;
        }
        // Add this inside <fs type='NP'> to <f name='NPtype' value='...'/>
        ndxChild = oXmlTools.SetXmlNodeChild(pdxThis, ref ndxThis, "fs", "type;" + strFStype, "type", strFStype);
        // Validate
        if (ndxChild == null) {
          // Show error
          errHandle.Status("Could not add an appropriate <fs type='" + strFStype + "'> child");
          // Return failure
          return false;
        }
        // Add or replace the <f> child
        if (oXmlTools.SetXmlNodeChild(pdxThis, ref ndxChild, "f", "name;" + strFname, "value", strFvalue) == null) {
          // Show error
          errHandle.Status("Could not add an appropriate <f name='" + strFname + "'> child");
          // Return failure
          return false;
        }
        // Return success
        return true;
      } catch (Exception ex) {
        // Show error
        errHandle.DoError("modAdapt/AddFeature", ex);
        // Return failure
        return false;
      }
    }

    // ------------------------------------------------------------------------------------
    // Name:   GetFeature
    // Goal:   Get the value of the feature having <fs type='strType'> and name <f name='strName'>
    // History:
    // 03-06-2010  ERK Created
    // ------------------------------------------------------------------------------------
    public string GetFeature(XmlNode ndxSrc, string strType, string strName) {
      XmlNode ndxThis = null; // Result of select statement

      try {
        // Determine validity
        if (ndxSrc == null) {
          return "";
        }
        // Valid input... -- see if we can get a child...
        ndxThis = ndxSrc.SelectSingleNode("./fs[@type='" + strType + "']");
        // Is this correct?
        if (ndxThis != null) {
          // Get the necessary <f> element
          ndxThis = ndxThis.SelectSingleNode("./f[@name='" + strName + "']");
          // Does it exist?
          if (ndxThis != null) {
            // Return the value of this attribute
            return ndxThis.Attributes["value"].Value.Trim(' ');
          }
        }
        // Return failure
        return "";
      } catch (Exception ex) {
        // Show error
        errHandle.DoError("modEditor/GetFeature", ex);
        // Return failure
        return "";
      }
    }

    // ------------------------------------------------------------------------------------
    // Name:   eTreeInsertLevel
    // Goal:   Insert a node:
    //           eTree, eLeaf - between me and my parent
    //           forest       - between forest and remainder
    // History:
    // 03-01-2013  ERK Created
    // ------------------------------------------------------------------------------------
    public bool eTreeInsertLevel(ref XmlNode ndxThis, ref XmlNode ndxNew) {
      XmlNode ndxChild = null; // Working node
      XmlNodeList ndxList = null; // List of children
      int intI = 0; // Counter

      try {
        // Validate something is selected
        if (ndxThis != null) {
          switch (ndxThis.Name) {
            case "eLeaf":
            case "eTree":
              // (1) Create a new <eTree> element
              ndxNew = CreateNewEtree(ref pdxCurrentFile);
              // (2) Replace the parent's child with the new one
              ndxThis.ParentNode.ReplaceChild(ndxNew, ndxThis);
              // (3) Set its (only) child
              ndxNew.PrependChild(ndxThis);
              // (5) Get the appropriate values for @from and @to
              ndxNew.Attributes["from"].Value = ndxThis.Attributes["from"].Value;
              ndxNew.Attributes["to"].Value = ndxThis.Attributes["to"].Value;
              // Return success
              return true;
            case "forest":
              // Insert a level between the <forest> node and the remainder
              // (1) Create a new <eTree> element
              ndxNew = CreateNewEtree(ref pdxCurrentFile);
              // (2) Prepare all the children of the forest parent
              ndxList = ndxThis.SelectNodes("./eTree");
              // (3) Make sure the new node is the child of the forest
              ndxThis.PrependChild(ndxNew);
              for (intI = 0; intI < ndxList.Count; intI++) {
                ndxChild = ndxList[intI];
                // Check this is not the new one
                if (ndxChild != ndxNew) {
                  // Replace the parent of this item
                  ndxNew.AppendChild(ndxChild);
                }
              }
              // Return success
              return true;
          }
        }
        // Return failure
        return false;
      } catch (Exception ex) {
        // Show error
        errHandle.DoError("modEditor/eTreeInsertLevel", ex);
        // Return failure
        return false;
      }
    }

    // ------------------------------------------------------------------------------------
    // Name:   eTreeAdd
    // Goal:   Add a node:
    //           eTree         - replace me as child by all my children
    //           forest, eLeaf - not possible
    //         strType can be:
    //           right         - Add a sibling to my right
    //           left          - Add a sibling to my left
    //           child         - Create/add a child <eTree> node
    //           eLeaf         - Create an <eLeaf> node under me (if there is none)
    // History:
    // 03-01-2013  ERK Created
    // ------------------------------------------------------------------------------------
    public bool eTreeAdd(ref XmlNode ndxThis, ref XmlNode ndxNew, string strType) {
      XmlNode ndxWork = null; // Working node

      try {
        // Validate something is selected
        if (ndxThis != null) {
          switch (ndxThis.Name) {
            case "eLeaf":
            case "forest":
              // Impossible
              return false;
            case "eTree":
              // Check what is our task
              switch (strType) {
                case "right":
                case "Right": // Add a sibling <eTree> to my right
                  // (1) Create a new <eTree> element
                  ndxNew = CreateNewEtree(ref pdxCurrentFile);
                  // (2) Get my parent
                  ndxWork = ndxThis.ParentNode;
                  if (ndxWork != null) {
                    // Insert the new node as child after me
                    ndxWork.InsertAfter(ndxNew, ndxThis);
                    //' Adapt the <eTree>/@Id values from [ndxThis] onwards
                    //AdaptEtreeId(ndxThis.Attributes("Id").Value)
                  }
                  // Adapt the sentence, but don't re-calculate "org"
                  eTreeSentence(ref ndxNew, ref ndxNew, bDoOrg: false);
                  // Return success
                  return true;
                case "left":
                case "Left": // Add a sibling <eTree> to my left
                  // (1) Create a new <eTree> element
                  ndxNew = CreateNewEtree(ref pdxCurrentFile);
                  // (2) Get my parent
                  ndxWork = ndxThis.ParentNode;
                  if (ndxWork != null) {
                    // Insert the new node as child before me
                    ndxWork.InsertBefore(ndxNew, ndxThis);
                    //' Go to the first <eTree> child of [ndxWork]
                    //ndxWork = ndxWork.SelectSingleNode("./child::eTree[1]")
                    //If (ndxWork IsNot Nothing) Then
                    //  ' Adapt the <eTree>/@Id values from [ndxWork] onwards
                    //  AdaptEtreeId(ndxWork.Attributes("Id").Value)
                    //End If
                  }
                  // Adapt the sentence, but don't re-calculate "org"
                  eTreeSentence(ref ndxNew, ref ndxNew, bDoOrg: false);
                  // Return success
                  return true;
                case "child":
                case "Child":
                case "childlast": // Add a child <eTree> under me
                  // (1) Create a new <eTree> element
                  ndxNew = CreateNewEtree(ref pdxCurrentFile);
                  // (2) Add the <eTree> child under me
                  ndxThis.AppendChild(ndxNew);
                  //' Adapt the <eTree>/@Id values from [ndxThis] onwards
                  //AdaptEtreeId(ndxThis.Attributes("Id").Value)
                  // Adapt the sentence, but don't re-calculate "org"
                  eTreeSentence(ref ndxNew, ref ndxNew, bDoOrg: false);
                  // Return success
                  return true;
                case "firstchild":
                case "childfirst": // Add a child <eTree> under me as the first one
                  // (1) Create a new <eTree> element
                  ndxNew = CreateNewEtree(ref pdxCurrentFile);
                  // (2) Add the <eTree> child under me
                  ndxThis.PrependChild(ndxNew);
                  //' Adapt the <eTree>/@Id values from [ndxThis] onwards
                  //AdaptEtreeId(ndxThis.Attributes("Id").Value)
                  // Adapt the sentence, but don't re-calculate "org"
                  eTreeSentence(ref ndxNew, ref ndxNew, bDoOrg: false);
                  // Return success
                  return true;
                case "eLeaf":
                case "eleaf":
                case "leaf":
                case "endnode":
                  // (1) Check: do I have any <eLeaf> or <eTree> children?
                  if ((ndxThis.SelectSingleNode("./child::eLeaf") == null) && (ndxThis.SelectSingleNode("./child::eTree") == null)) {
                    // (1) Create a new <eLeaf> element
                    ndxNew = CreateNewEleaf(ref pdxCurrentFile);
                    // (2) Add it under me
                    ndxThis.AppendChild(ndxNew);
                    // Re-do the sentence, including "org"
                    eTreeSentence(ref ndxThis, ref ndxNew);
                    // Return success
                    return true;
                  }
                  break;
                default:
                  return false;
              }
              break;
          }
        }
        // Return failure
        return false;
      } catch (Exception ex) {
        // Show error
        errHandle.DoError("modEditor/eTreeAdd", ex);
        // Return failure
        return false;
      }
    }
    // ------------------------------------------------------------------------------------
    // Name:   CreateNewEtree
    // Goal:   Create a new <eTree> element to the indicated xml document
    // History:
    // 03-01-2012  ERK Created
    // 01-02-2014  ERK Added @b and @e attributes
    // ------------------------------------------------------------------------------------
    public XmlNode CreateNewEtree(ref XmlDocument pdxThisFile) {
      int intI = 0; // Counter
      XmlNode ndxThis = null; // Newly to be created node
      XmlAttribute atxChild = null; // The attribute we are looking for
      string[] arAttr = { "Id", "1", "Label", "new", "IPnum", "0", "from", "0", "to", "0" };

      try {
        // Validate
        if (pdxThisFile == null) {
          return null;
        }
        // Create a new <eTree> node
        ndxThis = pdxThisFile.CreateNode(XmlNodeType.Element, "eTree", null);
        // Create all necessary attributes
        for (intI = 0; intI <= arAttr.GetUpperBound(0); intI += 2) {
          // Add this attribute
          atxChild = pdxThisFile.CreateAttribute(arAttr[intI]);
          atxChild.Value = arAttr[intI + 1];
          ndxThis.Attributes.Append(atxChild);
        }
        // Set the Id attribute to a different value
        intMaxEtreeId += 1;
        ndxThis.Attributes["Id"].Value = intMaxEtreeId.ToString();
        // Return the new node
        return ndxThis;
      } catch (Exception ex) {
        // Show error
        errHandle.DoError("modEditor/CreateNewEtree", ex);
        // Return failure
        return null;
      }
    }
    // ------------------------------------------------------------------------------------
    // Name:   CreateNewEleaf
    // Goal:   Create a new <eLeaf> element to the indicated xml document
    // History:
    // 03-01-2012  ERK Created
    // 01-02-2014  ERK Added @n feature
    // ------------------------------------------------------------------------------------
    public XmlNode CreateNewEleaf(ref XmlDocument pdxThisFile) {
      int intI = 0; // Counter
      XmlNode ndxThis = null; // Newly to be created node
      XmlAttribute atxChild = null; // The attribute we are looking for
      string[] arAttr = { "Type", "Vern", "Text", "new", "prob", "0", "from", "0", "to", "0", "n", "0" };

      try {
        // Validate
        if (pdxThisFile == null) {
          return null;
        }
        // Create a new <eTree> node
        ndxThis = pdxThisFile.CreateNode(XmlNodeType.Element, "eLeaf", null);
        // Create all necessary attributes
        for (intI = 0; intI <= arAttr.GetUpperBound(0); intI += 2) {
          // Add this attribute
          atxChild = pdxThisFile.CreateAttribute(arAttr[intI]);
          atxChild.Value = arAttr[intI + 1];
          ndxThis.Attributes.Append(atxChild);
        }
        // Return the new node
        return ndxThis;
      } catch (Exception ex) {
        // Show error
        errHandle.DoError("modEditor/CreateNewEleaf", ex);
        // Return failure
        return null;
      }
    }

   // ------------------------------------------------------------------------------------
    // Name:   eTreeSentence
    // Goal:   Re-analyze a whole sentence in the following way:
    //         1. Based on the content of the [eLeaf] nodes:
    //            a. Determine the <seg> text
    //            b. Determine @from and @to for the [eLeaf] nodes
    //         2. Determine @from and @to for all the [eTree] nodes again
    // History:
    // 03-01-2013  ERK Created
    // ------------------------------------------------------------------------------------
    public bool eTreeSentence(ref XmlNode ndxThis, ref XmlNode ndxNew, bool bVerbose = false, 
      bool bOldEnglish = false, bool bDoOrg = true) {
      XmlNode ndxFor = null;      // My parent forest node
      // XmlNode ndxChild = null; // Working node
      XmlNodeList ndxList = null; // List of children
      XmlNode ndxVern = null;     // Vernacular text line
      XmlNode ndxLeaf = null;     // One working leaf
      int intI = 0;               // Counter
      int intFrom = 0;            // Word starting point
      int intTo = 0;              // End of word
      bool bNeedSpace = false;    // No space needed after this word
      bool bChanged = false;      // Whether anythying has in fact changed
      string strLine = "";        // Text of this line

      try {
        // Validate something is selected
        if (ndxThis == null) {
          return false;
        }
        // Determine the parent forest node
        ndxFor = ndxThis.SelectSingleNode("./ancestor-or-self::forest[1]");
        if (ndxFor == null) {
          return false;
        }
        // Need to recalculate the "org" text?
        if (bDoOrg) {
          // Get the vernacular text line
          ndxVern = ndxFor.SelectSingleNode("./child::div[@lang='org']/seg");
          if (ndxVern == null) {
            return false;
          }
          // Get all the [eLeaf] children, but only if they have no CODE nor METADATA ancestor
          ndxList = ndxFor.SelectNodes(".//descendant::eLeaf[count(ancestor::eTree[tb:matches(@Label, '" + strNoText + "')])=0]", XPathFunctions.conTb);
          // Walk all the children
          for (intI = 0; intI < ndxList.Count; intI++) {
            // ============ DEBUG =========
            // If (intI = 11) Then Stop
            // ============================
            // Process this <eLeaf>
            // Check if this <eLeaf> has the correct type
            if ((ndxList[intI].Attributes["Type"].Value == "Punct") && 
              (General.DoLike(ndxList[intI].Attributes["Text"].Value, "*[a-zA-Z]*"))) {
              // It must be of type "Vern" instead
              ndxList[intI].Attributes["Type"].Value = "Vern";
            }
            switch (ndxList[intI].Attributes["Type"].Value) {
              case "Vern":
                // Need to add a space?
                if (bNeedSpace) {
                  strLine += " ";
                }
                // Get the starting point of the word
                intFrom = strLine.Length;
                // Add word to the text of this line
                if (bOldEnglish) {
                  strLine += VernToEnglish(ndxList[intI].Attributes["Text"].Value);
                } else {
                  strLine += ndxList[intI].Attributes["Text"].Value;
                }
                // Get the correct ending point of the word
                intTo = strLine.Length;
                // Normally each word should be followed by a space
                bNeedSpace = true;
                break;
              case "Punct":
                // Are we supposed to add a space?
                if (bNeedSpace) {
                  // Check if this punctuation should be PRECEDED by a space
                  switch (ndxList[intI].Attributes["Text"].Value) {
                    case ":": case ",": case ".": case "!": case "?": case ";": case ">>":
                      // A space may NOT precede this punctuation
                      break;
                    case "»":
                      // A space may NOT precede this punctuation
                      break;
                    case "«": case "<<": // A space must precede this punctuation
                      strLine += " ";
                      break;
                    case "'": case "\"":
                      // Check if a word is preceding or not
                      if (intI > 0) {
                        // We are not at the beginning...
                        if (ndxList[intI - 1].Attributes["Type"].Value != "Vern") {
                          // There is NO word preceding, so DO add a space
                          strLine += " ";
                        }
                      }
                      break;
                    default:
                      // In all other cases a space has to be added
                      strLine += " ";
                      break;
                  }
                }
                // Get the starting point of the word
                intFrom = strLine.Length;
                // Add word to the text of this line
                strLine += ndxList[intI].Attributes["Text"].Value;
                // Get the correct ending point of the word
                intTo = strLine.Length;
                // Check if this punctuation should be FOLLOWED by a space
                switch (ndxList[intI].Attributes["Text"].Value) {
                  case ":": case ",": case ".": case "!": case "?":  case ";": case ">>":
                    // A space must follow
                    bNeedSpace = true;
                    break;
                  case "»":
                    // A space should follow this punctuation
                    bNeedSpace = true;
                    break;
                  case "«":
                    // A space should not follow
                    bNeedSpace = false;
                    break;
                  case "'": case "\"":
                    // Check if a word is preceding or not
                    if (intI > 0) {
                      // We are not at the beginning...
                      if (ndxList[intI - 1].Attributes["Type"].Value == "Vern") {
                        // There is a word preceding, so DO add a space
                        bNeedSpace = true;
                      }
                    }
                    break;
                  default:
                    // Reset spacing
                    bNeedSpace = false;
                    break;
                }
                break;
              case "Star":
                // A star item must contain at least a space
                intFrom = strLine.Length;
                // Add this space
                strLine += " ";
                bNeedSpace = false;
                // Get the correct ending point of the word
                intTo = strLine.Length;
                break;
              case "Zero":
                // Get the starting point of the word
                intFrom = strLine.Length;
                intTo = intFrom;
                break;
            }
            // Validate existence of from and to
            XmlNode ndxListItem = ndxList[intI];
            if (ndxList[intI].Attributes["from"] == null) {
              oXmlTools.AddAttribute(ndxListItem, "from", "0");
            }
            if (ndxList[intI].Attributes["to"] == null) {
              oXmlTools.AddAttribute(ndxListItem, "to", "0");
            }
            // Adapt the start and end of the word
            intFrom += 1;
            if (ndxList[intI].Attributes["from"].Value != intFrom.ToString()) {
              ndxList[intI].Attributes["from"].Value = intFrom.ToString();
              bChanged = true;
            }
            if (ndxList[intI].Attributes["to"].Value != intTo.ToString()) {
              ndxList[intI].Attributes["to"].Value = intTo.ToString();
              bChanged = true;
            }
          }
          // Adapt the sentence in the vernacular
          ndxVern.InnerText = strLine;
          // Make sure editor is set to dirty
          // bEdtDirty = true;
        }
        // Get all the <eTree> nodes
        ndxList = ndxFor.SelectNodes("./descendant::eTree");
        // Treat them all
        for (intI = 0; intI < ndxList.Count; intI++) {
          // Access this one
          // Determine their @from and @to values
          ndxLeaf = ndxList[intI].SelectSingleNode("./descendant::eLeaf[1]");
          if (ndxLeaf != null) {
            // Double check
            if (ndxLeaf.Attributes["from"] == null) {
              oXmlTools.AddXmlAttribute(pdxCurrentFile, ref ndxLeaf, "from", "0");
            }
            // Get the value
            intFrom = Convert.ToInt32(ndxLeaf.Attributes["from"].Value);
            // Validate
            if (ndxList[intI].Attributes["from"] == null) {
              XmlNode ndxListItem = ndxList[intI];
              oXmlTools.AddAttribute(ndxListItem, "from", intFrom.ToString());
            } else {
              // See if we need changing
              if (ndxList[intI].Attributes["from"].Value != intFrom.ToString()) {
                ndxList[intI].Attributes["from"].Value = intFrom.ToString();
                bChanged = true;
              }
            }
          }
          ndxLeaf = ndxList[intI].SelectSingleNode("./descendant::eLeaf[last()]");
          if (ndxLeaf != null) {
            // Double check
            if (ndxLeaf.Attributes["to"] == null) {
              oXmlTools.AddXmlAttribute(pdxCurrentFile, ref ndxLeaf, "to", "0");
            }
            // Get the value
            intTo = Convert.ToInt32(ndxLeaf.Attributes["to"].Value);
            // Validate
            if (ndxList[intI].Attributes["to"] == null) {
              XmlNode ndxListItem = ndxList[intI];
              oXmlTools.AddAttribute(ndxListItem, "to", intTo.ToString());
            } else {
              // See if we need changing
              if (ndxList[intI].Attributes["to"].Value != intTo.ToString()) {
                ndxList[intI].Attributes["to"].Value = intTo.ToString();
                bChanged = true;
              }
            }
          }
        }
        // We end with the same node we started with
        // NO!! then we change it... ndxNew = ndxThis
        // Give message to user
        if ((bChanged) && (bVerbose)) {
          errHandle.Status("Word positions in line " + ndxFor.Attributes["forestId"].Value);
          //Else
          //  Logging("No changes were needed")
        }
        // Return success
        return bChanged;
      } catch (Exception ex) {
        // Show error
        errHandle.DoError("modEditor/eTreeSentence", ex);
        // Return failure
        return false;
      }
    }


    // -------------------------------------------------------------------------------------------------------
    // Name: VernToEnglish
    // Goal: Convert a vernacular OE text into intelligable English
    // Notes:
    //       - Special characters are defined as '+' + character:
    //         +t, +a,
    // History:
    // 27-11-2008    ERK Created
    // 19-12-2008    ERK Adapted for VB2005 TreeBank module
    // -------------------------------------------------------------------------------------------------------
    public string VernToEnglish(string strText) {
      string strOut = ""; // Output to be build up
      int intI = 0; // Position in the string

      // Check all characters of the input
      for (intI = 1; intI <= strText.Length; intI++) {
        // Check this character for a key
        switch (strText.Substring(intI - 1, 1)) {
          case "+": // This could be a special character
            // Is the next character a special one?
            if (SPEC_CHAR_IN.IndexOf(strText.Substring(intI, 1)) + 1 == 0) {
              // Just copy the input
              strOut += strText.Substring(intI - 1, 1);
            } else {
              // Goto next character
              intI = intI + 1;
              // Copy the translation of the special character
              strOut += GetSpecChar(strText.Substring(intI - 1, 1));
            }
            break;
          default: // Just copy the input
            strOut += strText.Substring(intI - 1, 1);
            break;
        }
      }
      // Return the string we have now made
      return strOut;
    }
    // -------------------------------------------------------------------------------------------------------
    // Name: GetSpecChar
    // Goal: Given a special character with a + sign, convert into "normal" English
    // History:
    // 27-11-2008    ERK Created
    // -------------------------------------------------------------------------------------------------------
    private string GetSpecChar(string strIn) {
      string tempGetSpecChar = null;
      int intPos = 0;

      // Get position in input string
      intPos = SPEC_CHAR_IN.IndexOf(strIn.Substring(0, 1)) + 1;
      if (intPos > 0) {
        tempGetSpecChar = SPEC_CHAR_OUT.Substring(intPos - 1, 1);
      } else {
        // Output the input with + sign
        tempGetSpecChar = "+" + strIn;
      }
      return tempGetSpecChar;
    }
  
  }
}
