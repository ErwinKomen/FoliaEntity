using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace FoliaEntity.util {
  public class xmlTools {
    // =========================================================================================================
    // Name: xmlTools
    // Goal: This module implements additional features for working with an XmlDocument and XmlNode elements
    // History:
    // 22/sep/2010 ERK Created
    // ========================================== LOCAL VARIABLES ==============================================
    private XmlDocument pdxDoc = null;  // The XML document serving as basis
    private string strNs = "";          // Possible namespace URI
    private ErrHandle errHandle;        // Our own copy of the error handle
    private string strSpace = " ,.<>?/;:\\|[{]}=+-_)(*&^%$#@!~`" + "\"" + "\t\n\r";
    // =========================================================================================================
    public xmlTools(ErrHandle objErr) {
      this.errHandle = objErr;
    }

    // ================ Getters and setters ====================================================================
    public XmlDocument getCurrentDoc() { return pdxDoc; }
    // =========================================================================================================


    // ------------------------------------------------------------------------------------
    // Name:   SetXmlDocument
    // Goal:   Set the local copy of the xml document
    // History:
    // 22-09-2010  ERK Created
    // ------------------------------------------------------------------------------------
    public void SetXmlDocument(XmlDocument pdxThis, string nsThis = "") {
      // Set the document to the indicated one
      pdxDoc = pdxThis;
      // Possible namespace URI
      strNs = nsThis;
    }

    // ------------------------------------------------------------------------------------
    // Name:   AddXmlChild
    // Goal:   Make a new XmlNode element of type [strTag] using the [arValue] values
    //         These values consist of:
    //         (a) itemname
    //         (b) itemvalue
    //         (c) itemtype: "attribute" or "child"
    //         Append this node as child under [ndxParent]
    // Return: The XmlNode element that has been made is returned
    // History:
    // 22-09-2010  ERK Created
    // ------------------------------------------------------------------------------------
    public XmlNode AddXmlChild(XmlNode ndxParent, string strTag, params string[] arValue) {
      XmlNode ndxThis = null;       // Working node
      XmlNode ndxChild = null;      // Child node
      XmlAttribute atxChild = null; // The attribute we are looking for
      int intI = 0;                 // Counter

      try
      {
        // Validate (NB: we DO allow empty parents)
        if ((string.IsNullOrEmpty(strTag)) || (pdxDoc == null)) return null;
        // Make a new XmlNode in the local XML document
        if (string.IsNullOrEmpty(strNs))
          ndxThis = pdxDoc.CreateNode(XmlNodeType.Element, strTag, null);
        else
          ndxThis = pdxDoc.CreateNode(XmlNodeType.Element, strTag, strNs);
        // Validate
        if (ndxThis == null) return null;
        // Do we have a parent?
        if (ndxParent == null) {
          // Take the document as starting point
          pdxDoc.AppendChild(ndxThis);
        } else {
          // Just append it
          ndxParent.AppendChild(ndxThis);
        }
        // Walk through the values
        for (intI = 0; intI <= arValue.GetUpperBound(0); intI += 3) {
          // Action depends on the type of value
          switch (arValue[intI + 2]) {
            case "attribute":
              // Create attribute
              atxChild = pdxDoc.CreateAttribute(arValue[intI]);
              // Fillin value of this attribute
              atxChild.Value = arValue[intI + 1];
              // Append attribute to this node
              ndxThis.Attributes.Append(atxChild);
              break;
            case "child":
              // Create this node
              if (string.IsNullOrEmpty(strNs))
                ndxChild = pdxDoc.CreateNode(XmlNodeType.Element, arValue[intI], null);
              else
                ndxChild = pdxDoc.CreateNode(XmlNodeType.Element, arValue[intI], strNs);
              // Fill in the value of this node
              ndxChild.InnerText = arValue[intI + 1];
              // Append this node as child
              ndxThis.AppendChild(ndxChild);
              break;
            default:
              // There is no other option yet, so return failure
              return null;
          }
        }
        // Return the new node
        return ndxThis;
      } catch (Exception ex) {
        // Warn user
        errHandle.DoError("modXmlNode/AddXmlChild", ex);
        // Return failure
        return null;
      }
    }

    // ------------------------------------------------------------------------------------
    // Name:   AddXmlAttribute
    // Goal:   Add an attribute to the indicated node
    // History:
    // 23-02-2011  ERK Created
    // ------------------------------------------------------------------------------------
    public bool AddXmlAttribute(XmlDocument pdxThis, ref XmlNode ndxThis, string strAttrName, string strAttrValue = "") {
      XmlAttribute atxChild = null; // The attribute we are looking for

      try {
        // Validate
        if (ndxThis == null) {
          return false;
        }
        // Check if hte attribute is already there
        if (ndxThis.Attributes[strAttrName] == null) {
          // It is not there, so add it
          atxChild = pdxThis.CreateAttribute(strAttrName);
          // Append attribute to this node
          ndxThis.Attributes.Append(atxChild);
        }
        // Optionally give the value
        if (!string.IsNullOrEmpty(strAttrValue)) {
          ndxThis.Attributes[strAttrName].Value = strAttrValue;
        }
        // Return success
        return true;
      } catch (Exception ex) {
        // Warn user
        errHandle.DoError("modXmlNode/AddXmlAttribute", ex);
        // Return failure
        return false;
      }
    }

    // ------------------------------------------------------------------------------------
    // Name:   AddAttribute
    // Goal:   Add the attribute to the node, if the attribute does not exist there
    // History:
    // 26-05-2010  ERK Created
    // ------------------------------------------------------------------------------------
    public bool AddAttribute(XmlNode ndxThis, string strAttrName, string strAttrValue) {
      return AddAttribute(this.pdxDoc, ref ndxThis, strAttrName, strAttrValue);
    }
    public bool AddAttribute(XmlDocument pdxThis, ref XmlNode ndxThis, string strAttrName, string strAttrValue) {
      XmlAttribute atrThis = null; // New attribute

      try {
        // Validate
        if (ndxThis == null) {
          return false;
        }
        // Check if the attribute is there
        if (ndxThis.Attributes[strAttrName] == null) {
          // Create an attribute
          atrThis = pdxThis.CreateAttribute(strAttrName);
          // Append this attribute
          ndxThis.Attributes.Append(atrThis);
        }
        // Set the value of this attribute
        ndxThis.Attributes[strAttrName].Value = strAttrValue;
        // Return success
        return true;
      } catch (Exception ex) {
        // Show error
        errHandle.DoError("modAdapt/AddAttribute", ex);
        // Return failure
        return false;
      }
    }

    // ------------------------------------------------------------------------------------
    // Name:   SetXmlNodeChild
    // Goal:   Set the attribute [strChildAttr] of the child named [strChildName] of 
    //           xml node [ndxThis] with value [strChildValue]
    //         If this child does not exist, then create it as the FIRST child
    // History:
    // 12-05-2010  ERK Created
    // ------------------------------------------------------------------------------------
    public XmlNode SetXmlNodeChild(XmlDocument pdxThisFile, ref XmlNode ndxThis, string strChildName, string strAttrSel, string strChildAttr, string strChildValue) {
      XmlNode ndxChild = null; // Child being looked for
      XmlAttribute atxChild = null; // The attribute we are looking for
      string[] arAttr = null; // Array of attribute name + value
      string strSelect = null; // The string to find the appropriate child
      int intI = 0; // Counter

      try {
        // Verify the input
        if ((ndxThis == null) || (string.IsNullOrEmpty(strChildName)) || (string.IsNullOrEmpty(strChildAttr))) {
          return null;
        }
        // Start making the selection string
        strSelect = "./" + strChildName;
        if (!string.IsNullOrEmpty(strAttrSel)) {
          // Extract the list of necessary attributes
          arAttr = strAttrSel.Split(';');
          // Append left bracket
          strSelect += "[";
          // Visit all attributes
          for (intI = 0; intI <= arAttr.GetUpperBound(0); intI += 2) {
            // Should we add the logical "AND"?
            if (intI > 0) {
              strSelect += " and ";
            }
            // Add this attribute to the list
            strSelect += "@" + arAttr[intI] + "='" + arAttr[intI + 1] + "'";
          }
          // Append right bracket
          strSelect += "]";
        }
        // Try to get the appropriate child
        ndxChild = ndxThis.SelectSingleNode(strSelect);
        // Is there a child?
        if (ndxChild == null) {
          // Create a new child node
          ndxChild = pdxThisFile.CreateNode(XmlNodeType.Element, strChildName, null);
          // Should first other attributes be created?
          if (!string.IsNullOrEmpty(strAttrSel)) {
            // Create all necessary attributes
            for (intI = 0; intI <= arAttr.GetUpperBound(0); intI += 2) {
              // Add this attribute
              atxChild = pdxThisFile.CreateAttribute(arAttr[intI]);
              atxChild.Value = arAttr[intI + 1];
              ndxChild.Attributes.Append(atxChild);
            }
          }
          // Create a new attribute
          atxChild = pdxThisFile.CreateAttribute(strChildAttr);
          // Add the attribute to the node
          ndxChild.Attributes.Append(atxChild);
          // Make this new node the FIRST child of [ndxThis]
          ndxThis.PrependChild(ndxChild);
        }
        // Find the attribute of the child
        atxChild = ndxChild.Attributes[strChildAttr];
        if ((atxChild) == null) {
          // The attribute does not exist, so create it
          atxChild = pdxThisFile.CreateAttribute(strChildAttr);
          // Add the attribute to the node
          ndxChild.Attributes.Append(atxChild);
        }
        // Set the attribute of the child
        atxChild.Value = strChildValue;
        // Return the child
        return ndxChild;
      } catch (Exception ex) {
        // Show error
        errHandle.DoError("modEditor/SetXmlNodeChild", ex);
        // Return failure
        return null;
      }
    }

    // ------------------------------------------------------------------------------------------------------------
    // Name:   getCommonAncestor
    // Goal:   Get the nearest common ancestor of two nodes
    // History:
    // 26-06-2015  ERK Created
    // ------------------------------------------------------------------------------------------------------------
    public XmlNode getCommonAncestor(ref XmlNode ndxBef, ref XmlNode ndxAft, String strCond, ref XmlNode ndxLeft, ref XmlNode ndxRight) {
      XmlNode ndxMyBef = default(XmlNode);
      // My copy of ndxBef
      XmlNode ndxWork = default(XmlNode);
      // working node
      string strType = null;
      // Kind of nodes

      try {
        // Validate
        if ((ndxBef == null) || (ndxAft == null))
          return null;
        if ((object.ReferenceEquals(ndxBef, ndxAft)))
          return ndxBef;
        // Determine node type
        strType = ndxBef.Name;
        // Initialize left and right
        ndxLeft = ndxBef;
        ndxRight = ndxAft;
        ndxMyBef = ndxBef;
        // Outer loop: before
        while ((ndxMyBef != null) && (ndxMyBef.Name == strType)) {
          // Find out if there is an ancestor of ndxAft equal to ndxBef
          ndxWork = ndxAft;
          while ((ndxWork != null) && (ndxWork.Name == strType)) {
            // Test
            if ((object.ReferenceEquals(ndxMyBef, ndxWork))) {
              // Found it
              return ndxMyBef;
            }
            // adjust right
            ndxRight = ndxWork;
            // Go higher
            ndxWork = ndxWork.ParentNode;
          }
          // Adjust left
          ndxLeft = ndxMyBef;
          // Try parent
          ndxMyBef = ndxMyBef.ParentNode;
        }

        // Not found
        return null;
      } catch (Exception ex) {
        // Warn the user
        errHandle.DoError("modXmlNode/getCommonAncestor", ex);
        // Return failure
        return null;
      }
    }


    // ------------------------------------------------------------------------------------------------------------
    // Name:   FixList
    // Goal:   Convert a flexibal NodeList into a fixed list of nodes
    // History:
    // 06-10-2015  ERK Created
    // ------------------------------------------------------------------------------------------------------------
    public List<XmlNode> FixList(XmlNodeList ndxList) {
      try {
        List<XmlNode> lstBack = new List<XmlNode>();
        for (int i = 0; i < ndxList.Count; i++) {
          lstBack.Add(ndxList[i]);
        }
        return lstBack;
      } catch (Exception ex) {
        // Warn the user
        errHandle.DoError("modXmlNode/FixList", ex);
        // Return failure
        return null;
      }
    }
  }
}
