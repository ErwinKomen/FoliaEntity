using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace FoliaEntity.util {
  public class XPathFunctions {
    public static string TREEBANK_EXTENSIONS = "http://www.ru.nl/letteren/xpath/tb";
    public static CustomContext conTb = new CustomContext();
    private ErrHandle errHandle = new ErrHandle();
    private string SPEC_CHAR_IN = "tadegTADEG";
    private string SPEC_CHAR_OUT = "þæđëġÞÆĐËĠ";

    // ------------------------------------------------------------------------------------
    // Name:   DepType
    // Goal:   Return the type of node [ndxThis] for dependency conversion processing
    // Note:   The types that are returned:
    //           cnode       - non-end node, but only descendant is *con*
    //           code        - code node
    //           dnode       - node in its dislocated position
    //           droot       - location where the dislocated node should actually appear
    //           empty       - zero endnode or [ndxThis] itself is nothing or *con* node
    //           endnode     - 'normal' endnode with a vernacular eleaf child
    //           node        - normal non-end node
    //           punct       - punctuation endnode
    //           star        - unspecified endnode with * value
    //           tnode       - original node to which a trace points
    //           troot       - trace (empty) node, pointing to traceNd
    //           (nothing)   - error
    // History:
    // 30-08-2013  ERK Created
    // ------------------------------------------------------------------------------------
    public string DepType(ref XmlNode ndxThis) {
      XmlNode ndxFor = null;
      // Forest
      XmlNode ndxWork = null;
      // Working node
      XmlNode ndxLeaf = null;
      // Leaf
      XmlNodeList ndxList = null;
      // List of nodes
      string strValue = null;
      // Value
      int intExt = 0;
      // Label extension number
      int intI = 0;
      // Counter

      try {
        // Validate
        if ((ndxThis == null))
          return "empty";
        if ((ndxThis.Name != "eTree"))
          return "empty";
        // First check: do I have any 'Vern|Star|Punct' descendants?
        if ((ndxThis.SelectSingleNode("./descendant::eLeaf[tb:matches(@Type,'Vern|Star|Punct')]", conTb) == null))
          return "empty";
        // Check if I am a code node
        if ((ndxThis.Attributes["Label"].Value == "CODE"))
          return "code";
        // Check for cnode
        ndxList = ndxThis.SelectNodes("./descendant::eLeaf");
        if ((ndxList.Count == 1)) {
          // Check for [cnode]
          if ((ndxList[0].Attributes["Text"].Value == "*con*"))
            return "cnode";
        }
        // Get possible label number
        intExt = LabelExtNum(ref ndxThis);
        // See if this is an endnode
        ndxLeaf = ndxThis.SelectSingleNode("./child::eLeaf");
        if ((ndxLeaf != null)) {
          // Type depends on the type of <eLeaf>
          switch (ndxLeaf.Attributes["Type"].Value) {
            case "Vern":
              // Action depends on existence of label number
              if ((intExt < 0))
                return "endnode";
              break;
            // So if there IS a label number, then we continue
            // Example: (WPRO-5 wes)
            case "Zero":
              return "empty";
            case "Star":
              // There are several possibilities...
              strValue = ndxLeaf.Attributes["Text"].Value;
              if ((strValue == "*con*") || (strValue == "*pro*")) {
                return "empty";
              } else if ((strValue == "*exp*")) {
                // Extraposed clause
                return "droot";
              } else if (strValue.StartsWith("*ICH*")) {
                // Dislocated: node with label -n may exist
                return "droot";
              } else if (strValue.StartsWith("*T*"))  {
                // Trace: node with label -n MAY exist
                return "troot";
              } else {
                // This should not happen, but: who knows?
                return "star";
              }
            case "Punct":
              return "punct";
            default:
              // This should not happen, but return the name of the type itself
              return ndxLeaf.Attributes["Type"].Value;
          }
        }
        // Check if we have a number
        if ((intExt >= 0)) {
          // There is a label extension number (e.g: NP_SBJ-1)
          // We need to determine what type of coreference goes on:
          // a - Trace source          NP_SBJ-1 >> (NP_SBJ *T*-1)
          // b - Trace source          WPRO-2   >> (NP_OB1-2 *T*)
          // c - Dislocation pointer   PP-2     >> (NP *ICH*-2)
          // d - Dislocation pointer   CP_REL-1 >> (CP_REL-1 *ICH*)
          // e - Extraposition         CP_THT-5 >> (NP_SBJ-5 *exp*)
          // f - Coreference           NP_LFD-1 >> (PRO-1 sy)... (PRO-1 sy)
          // We need to find if there is a node this points to, and if so, which one
          // (1) get the forest
          ndxFor = ndxThis.SelectSingleNode("./ancestor::forest");
          // (2) Check for type [a]
          ndxWork = ndxFor.SelectSingleNode("./descendant::eTree[child::eLeaf[@Text='*T*-" + intExt + "']]");
          if ((ndxWork != null))
            return "tnode";
          // (3) Check for type [c]
          ndxWork = ndxFor.SelectSingleNode("./descendant::eTree[child::eLeaf[@Text='*ICH*-" + intExt + "']]");
          if ((ndxWork != null))
            return "dnode";
          // (4) Check for type [b] 
          ndxList = ndxFor.SelectNodes("./descendant::eTree[child::eLeaf[@Text='*T*']]");
          // Walk the results
          for (intI = 0; intI <= ndxList.Count - 1; intI++) {
            // Is this our node?
            if (ndxList[intI].Attributes["Label"].Value.IsLike("*-" + intExt))
              return "tnode";
          }
          // (5) Check for type [d] 
          ndxList = ndxFor.SelectNodes("./descendant::eTree[child::eLeaf[@Text='*ICH*']]");
          // Walk the results
          for (intI = 0; intI <= ndxList.Count - 1; intI++) {
            // Is this our node?
            if (ndxList[intI].Attributes["Label"].Value.IsLike("*-" + intExt))
              return "dnode";
          }
          // (6) Check for type [e] 
          ndxList = ndxFor.SelectNodes("./descendant::eTree[child::eLeaf[@Text='*exp*']]");
          // Walk the results
          for (intI = 0; intI <= ndxList.Count - 1; intI++) {
            // Is this our node?
            if (ndxList[intI].Attributes["Label"].Value.IsLike("*-" + intExt))
              return "dnode";
          }
          // (7) we do not have to check for type [f], because such nodes are regarded as regular, and will come out with the default type.
          // (8) It may be a normal endnode
          if ((ndxLeaf != null) && (ndxLeaf.Attributes["Type"].Value == "Vern"))
            return "endnode";
        }
        // Default: this is a normal node
        return "node";
      } catch (Exception ex) {
        // Show error
        this.errHandle.DoError("XpathExt/DepType", ex);
        // Return failure
        return "";
      }
    }
    // ------------------------------------------------------------------------------------
    // Name:   LabelExtNum
    // Goal:   Get the extension-number after the @Label feature of [ndxThis]
    // History:
    // 30-08-2013  ERK Created
    // ------------------------------------------------------------------------------------
    public int LabelExtNum(ref XmlNode ndxThis) {
      string strLabel = null;
      // Label
      string strLast = null;
      // String follownig the last hyphen
      int intPos = 0;
      // Position in string

      try {
        // Validate
        if ((ndxThis == null))
          return -1;
        if ((ndxThis.Name != "eTree"))
          return -1;
        // Get label
        if ((ndxThis.Attributes["Label"] == null))
          return -1;
        strLabel = ndxThis.Attributes["Label"].Value;
        // Look for the last hyphen
        intPos = strLabel.LastIndexOf("-");
        if ((intPos > 0)) {
          // Get the string following the last hyphen
          strLast = strLabel.Substring(intPos + 1);
          // Is this numeric?
          if ((General.IsNumeric(strLast))) {
            // Return the number
            return Convert.ToInt32(strLast);
          }
        }
        // Return failure
        return -1;
      } catch (Exception ex) {
        // Show error
        this.errHandle.DoError("XpathExt/LabelExtNum", ex);
        // Return failure
        return -1;
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

  // ===============================================================================================================
  // Name :  XPathExtensions
  // Goal :  The interface that resolves and executes a specified user-defined function. 
  // History:
  // 17-06-2010  ERK Taken from http://msdn.microsoft.com/en-us/library/dd567715.aspx
  // ===============================================================================================================
  public class XPathExtensions : IXsltContextFunction {
    private ErrHandle errHandle = new ErrHandle();

    // The data types of the arguments passed to XPath extension function.
    private XPathResultType[] m_ArgTypes;
    // The minimum number of arguments that can be passed to function.
    private int m_MinArgs;
    // The maximum number of arguments that can be passed to function.
    private int m_MaxArgs;
    // The data type returned by extension function.
    private XPathResultType m_ReturnType;
    // The name of the extension function.
    private string m_FunctionName;
    // Make sure we have a link to our own Xpath functions

    private XPathFunctions m_XpFun = new XPathFunctions();
    // Constructor used in the ResolveFunction method of the custom XsltContext 
    // class to return an instance of IXsltContextFunction at run time.
    public XPathExtensions(int MinArgs, int MaxArgs, XPathResultType ReturnType, XPathResultType[] ArgTypes, string FunctionName) {
      m_MinArgs = MinArgs;
      m_MaxArgs = MaxArgs;
      m_ReturnType = ReturnType;
      m_ArgTypes = ArgTypes;
      m_FunctionName = FunctionName;
    }

    // Readonly property methods to access private fields.
    public XPathResultType[] ArgTypes {
      get { return m_ArgTypes; }
    }

    public int MaxArgs {
      get { return m_MaxArgs; }
    }
    int IXsltContextFunction.Maxargs {
      get { return MaxArgs; }
    }

    public int MinArgs {
      get { return m_MinArgs; }
    }
    int IXsltContextFunction.Minargs {
      get { return MinArgs; }
    }

    public XPathResultType ReturnType {
      get { return m_ReturnType; }
    }

    // Function to execute a specified user-defined XPath 
    // extension function at run time.
    public object Invoke(XsltContext Context, object[] Args, XPathNavigator DocContext) {
      string strOne = null;
      string strTwo = null;
      XPathNodeIterator Node = null;
      IHasXmlNode objThis = null;
      XmlNode ndxThis = null;
      // Working node

      switch (m_FunctionName) {
        case "CountChar":
          return CountChar((XPathNodeIterator)Args[0], Convert.ToChar(Args[1]));
        case "FindTaskBy":
          return FindTaskBy((XPathNodeIterator)Args[0], Convert.ToString(Args[1].ToString()));
        case "Left":
          String sLeftArg0 = Convert.ToString(Args[0]);
          int iLeftArg1 = Convert.ToInt32(Args[1]);
          return sLeftArg0.Substring(0, iLeftArg1);
        case "Right":
          String sRightArg0 = Convert.ToString(Args[0]);
          int iRightArg1 = Convert.ToInt32(Args[1]);
          return sRightArg0.Substring(sRightArg0.Length - iRightArg1);
        case "verntoenglish":
        case "VernToEnglish":
          if ((object.ReferenceEquals(Args[0].GetType(), System.Type.GetType("System.String")))) {
            strOne = Convert.ToString(Args[0].ToString());
          } else {
            Node = (XPathNodeIterator)Args[0];
            Node.MoveNext();
            strOne = Node.Current.Value;
          }
          return VernToEnglish(strOne);
        case "Like":
        case "matches":
          if ((object.ReferenceEquals(Args[0].GetType(), System.Type.GetType("System.String")))) {
            strOne = Convert.ToString(Args[0].ToString());
          } else {
            Node = (XPathNodeIterator)Args[0];
            // Move to the first selected node
            // See "XpathNodeIterator Class":
            //     An XPathNodeIterator object returned by the XPathNavigator class is not positioned on the first node 
            //     in a selected set of nodes. A call to the MoveNext method of the XPathNodeIterator class must be made 
            //     to position the XPathNodeIterator object on the first node in the selected set of nodes. 
            Node.MoveNext();
            // Get the value of this attribute or node
            strOne = Node.Current.Value;
            //' Check the kind of node we have
            //Select Case Node.Current.Name
            //  Case "eTree"
            //    ' Get the @Label
            //    strOne = Node.Current.GetAttribute("Label", "")
            //  Case "eLeaf"
            //    Node.MoveNext()
            //    ' Get the @Text
            //    strOne = Node.Current.GetAttribute("Text", "")
            //    strOne = Node.CurrentPosition
            //  Case "Feature"
            //    ' Get the @Value
            //    strOne = Node.Current.GetAttribute("Value", "")
            //  Case "Result"
            //    ' Since nothing is specified, I really do not know which of the features to take...
            //    strOne = ""
            //  Case Else
            //    strOne = ""
            //End Select
          }
          // If (InStr(strOne, "some") > 0) Then Stop

          strTwo = Convert.ToString(Args[1]);
          return DoLike(strOne, strTwo);
        case "DepType":
        case "deptype":
          // ndxThis = DirectCast(Args(0), XmlNode)
          Node = (XPathNodeIterator)Args[0];
          Node.MoveNext();
          objThis = (IHasXmlNode)Node.Current;
          ndxThis = objThis.GetNode();
          //ndxThis = DirectCast(Node.Current, XmlNode)
          return DepType(ref ndxThis);
        case "LabelExtNum":
        case "labelnum":
          // ndxThis = DirectCast(Args(0), XmlNode)
          Node = (XPathNodeIterator)Args[0];
          Node.MoveNext();
          objThis = (IHasXmlNode)Node.Current;
          ndxThis = objThis.GetNode();
          //ndxThis = DirectCast(Node.Current, XmlNode)
          return LabelExtNum(ref ndxThis);
        default:

          break;
      }
      // Return Nothing for unknown function name.
      return null;

    }

    // XPath extension functions.
    private int CountChar(XPathNodeIterator Node, char CharToCount) {

      int CharCount = 0;

      for (int CharIndex = 0; CharIndex <= Node.Current.Value.Length - 1; CharIndex++) {
        if (Node.Current.Value[CharIndex] == CharToCount) {
          CharCount += 1;
        }
      }

      return CharCount;

    }
    // ------------------------------------------------------------------------------------
    // Name:   DoLike
    // Goal:   Perform the "Like" function using the pattern (or patterns) stored in [strPattern]
    //         There can be more than 1 pattern in [strPattern], which must be separated
    //         by a vertical bar: |
    // History:
    // 17-06-2010  ERK Created
    // ------------------------------------------------------------------------------------
    private bool DoLike(string strText, string strPattern) {
      string[] arPattern = null;
      // Array of patterns
      int intI = 0;
      // Counter

      try {
        // Reduce the [strPattern]
        strPattern = strPattern.Trim();
        // ============== DEBUG ==============
        // If (strPattern = "a") Then Stop
        // ===================================
        // SPlit the [strPattern] into different ones
        arPattern = strPattern.Split(new string[] { "|" }, StringSplitOptions.None);
        // Perform the "Like" operation for all needed patterns
        for (intI = 0; intI < arPattern.Length; intI++) {
          // See if something positive comes out of this comparison
          if (strText.IsLike(strPattern)) return true;
        }
        // No match has happened, so return false
        return false;
      } catch (Exception ex) {
        // Show error
        errHandle.DoError("XpathExt/DoLike", ex);
        // Return failure
        return false;
      }
    }
    // ================== GETTERS to interface with XpathFunctions =======================
    private string DepType(ref XmlNode ndxThis) { return m_XpFun.DepType(ref ndxThis); }
    private string VernToEnglish(string sInput) { return m_XpFun.VernToEnglish(sInput); }
    private int LabelExtNum(ref XmlNode ndxThis) { return m_XpFun.LabelExtNum(ref ndxThis); }

    // This overload will not force the user 
    // to cast to string in the xpath expression
    private string FindTaskBy(XPathNodeIterator Node, string Text) {

      if ((Node.Current.Value.Contains(Text))) {
        return Node.Current.Value;
      } else {
        return "";
      }

    }

  }
  // ===============================================================================================================
  // Name :  CustomContext
  // Goal :  Provide a custom context
  // History:
  // 17-06-2010  ERK Taken from http://msdn.microsoft.com/en-us/library/dd567715.aspx
  // ===============================================================================================================

  public class CustomContext : XsltContext {

    private string ExtensionsNamespaceUri = XPathFunctions.TREEBANK_EXTENSIONS;
    // XsltArgumentList to store names and values of user-defined variables.

    private XsltArgumentList m_ArgList;

    public CustomContext() {
    }

    public CustomContext(NameTable NT, XsltArgumentList Args)
      : base(NT) {
      m_ArgList = Args;
    }

    // Empty implementation, returns 0.
    public override int CompareDocument(string BaseUri, string NextBaseUri) {
      return 0;
    }

    // Empty implementation, returns false.
    public override bool PreserveWhitespace(XPathNavigator Node) {
      return false;
    }

    public override IXsltContextFunction ResolveFunction(string Prefix, string Name, XPathResultType[] ArgTypes) {

      if (LookupNamespace(Prefix) == ExtensionsNamespaceUri) {
        switch (Name) {
          case "CountChar":

            return new XPathExtensions(2, 2, XPathResultType.Number, ArgTypes, "CountChar");
          case "FindTaskBy":
            // Implemented but not called.

            return new XPathExtensions(2, 2, XPathResultType.String, ArgTypes, "FindTaskBy");
          case "Right":
            // Implemented but not called.

            return new XPathExtensions(2, 2, XPathResultType.String, ArgTypes, "Right");
          case "Left":
            // Implemented but not called.

            return new XPathExtensions(2, 2, XPathResultType.String, ArgTypes, "Left");
          case "Like":
          case "matches":
            // Implemented but not called.
            return new XPathExtensions(2, 2, XPathResultType.Boolean, ArgTypes, "Like");
          case "verntoenglish":
          case "VernToEnglish":
            return new XPathExtensions(1, 1, XPathResultType.String, ArgTypes, "VernToEnglish");
          case "DepType":
          case "deptype":
            return new XPathExtensions(1, 1, XPathResultType.String, ArgTypes, "DepType");
          case "LabelExtNum":
          case "labelnum":
            return new XPathExtensions(1, 1, XPathResultType.Number, ArgTypes, "LabelExtNum");
          default:

            break;
        }
      }
      // Return Nothing if none of the functions match name.
      return null;

    }

    // Function to resolve references to user-defined XPath 
    // extension variables in XPath query.
    public override IXsltContextVariable ResolveVariable(string Prefix, string Name) {
      if (LookupNamespace(Prefix) == ExtensionsNamespaceUri || Prefix.Length > 0) {
        throw new XPathException(string.Format("Variable '{0}:{1}' is not defined.", Prefix, Name));
      }

      switch (Name) {
        case "charToCount":
        case "left":
        case "right":
        case "text":
          // Create an instance of an XPathExtensionVariable 
          // (custom IXsltContextVariable implementation) object 
          // by supplying the name of the user-defined variable to resolve.

          return new XPathExtensionVariable(Prefix, Name);
        // The Evaluate method of the returned object will be used at run time
        // to resolve the user-defined variable that is referenced in the XPath
        // query expression. 
        default:

          break;
      }
      // Return Nothing if none of the variables match name.
      return null;

    }

    public override bool Whitespace {
      get { return true; }
    }

    // The XsltArgumentList property is accessed by the Evaluate method of the 
    // XPathExtensionVariable object that the ResolveVariable method returns. 
    // It is used to resolve references to user-defined variables in XPath query 
    // expressions. 
    public XsltArgumentList ArgList {
      get { return m_ArgList; }
    }
  }

  // ===============================================================================================================
  // Name :  XPathExtensions
  // Goal :  The interface used to resolve references to user-defined variables
  //           in XPath query expressions at run time. An instance of this class 
  //           is returned by the overridden ResolveVariable function of the 
  //           custom XsltContext class. 
  // History:
  // 17-06-2010  ERK Taken from http://msdn.microsoft.com/en-us/library/dd567715.aspx
  // ===============================================================================================================
  public class XPathExtensionVariable : IXsltContextVariable {

    // Namespace of user-defined variable.
    private string m_Prefix;
    // The name of the user-defined variable.

    private string m_VarName;
    // Constructor used in the overridden ResolveVariable function of custom XsltContext.
    public XPathExtensionVariable(string Prefix, string VarName) {
      m_Prefix = Prefix;
      m_VarName = VarName;
    }

    // Function to return the value of the specified user-defined variable.
    // The GetParam method of the XsltArgumentList property of the active
    // XsltContext object returns value assigned to the specified variable.
    public object Evaluate(XsltContext Context) {
      XsltArgumentList vars = ((CustomContext)Context).ArgList;
      return vars.GetParam(m_VarName, m_Prefix);
    }

    // Determines whether this variable is a local XSLT variable.
    // Needed only when using a style sheet.
    public bool IsLocal {
      get { return false; }
    }

    // Determines whether this parameter is an XSLT parameter.
    // Needed only when using a style sheet.
    public bool IsParam {
      get { return false; }
    }

    public XPathResultType VariableType {
      get { return XPathResultType.Any; }
    }
  }
}
