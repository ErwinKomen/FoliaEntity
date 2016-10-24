using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using FoliaEntity.util;

namespace FoliaEntity {
  /* -------------------------------------------------------------------------------------
   * Name:  feMain
   * Goal:  entry point of command-line "foliaentity" program
   *        Assuming a .folia.xml file that contains an <entity> layer,
   *          visit each Named Entity, find a linking URL to the outside,
   *          and add this URL as an <alignment>
   *          
   *        Example:
   *        <entities>
   *          <entity class="loc" xml:id="WR-P-P-C-0000000044.p.65.s.1.entity.1">
   *           <wref id="WR-P-P-C-0000000044.p.65.s.1.w.16" t="Nederlanders"/>
   *           <alignment format="application/json" 
   *                      class="NEL" 
   *                      xlink:href="http://nl.dbpedia.org/resource/Nederland" 
   *                      xlink:type="simple"/>
   *          </entity>
   *         </entities>
   * History:
   * 24/oct/2016 ERK Created
     ------------------------------------------------------------------------------------- */
  class feMain {
    // =================== My own static variables =======================================
    static ErrHandle errHandle = new ErrHandle();
    static String[] arInput;   // Array of input files
    static String strOutDir;   // Output directory
    // =================== Local variables ===============================================

    // Command-line entry point + argument handling
    static void Main(string[] args) {
      String sInput = "";       // Input file or dir
      String sOutput = "";      // Output directory
      String sAlpino = "";      // Possible location of program
      String sParsed = "";      // Directory where parsed files are kept
      bool bIsDebug = false;    // Debugging
      bool bKeepGarbage = false;// Keep garbage?
      bool bOverwrite = false;  // Do not overwrite

      try {
        // Check command-line options
        for (int i = 0; i < args.Length; i++) {
          // get this argument
          String sArg = args[i];
          if (sArg.StartsWith("-")) {
            // Check out the arguments
            switch (sArg.Substring(1)) {
              case "i": // Input file or directory with .folia.xml files
                sInput = args[++i];
                break;
              case "o": // Output directory
                sOutput = args[++i];
                break;
              case "d": // Debugging
                bIsDebug = true;
                break;
              case "w": // Overwrite output
                bOverwrite = true;
                break;
              case "a": // Alpino executable location (alternative w.r.t. default Science one)
                sAlpino = args[++i].ToLower();
                break;
              case "g": // Keep garbage for manual inspection
                bKeepGarbage = true;
                break;
              case "p": // Directory where corresponding [p]arsed files are kept
                        // These are in .data and .index files (possibly with .data.dz)
                sParsed = args[++i];
                break;
            }
          } else {
            // Throw syntax error and leave
            SyntaxError("1 - i=" + i + " args=" + args.Length + " argCurrent=[" + sArg + "]"); return;
          }
        }
        // Check presence of input/output
        if (sInput == "" || sOutput == "") { SyntaxError("2"); return; }
        // Check if the input is a directory or file
        if (File.Exists(sInput) && sInput.EndsWith(".folia.xml")) {
          // Input is one file
          arInput = new String[1]; arInput[0] = sInput;
        } else if (Directory.Exists(sInput)) {
          // Input is a dir of files
          if (sInput == sOutput) { SyntaxError("3"); return; }
          // Get all files in this dir
          // arInput = Directory.GetFiles(sInput, "*.folia.xml", SearchOption.AllDirectories);
          arInput = General.getFilesSorted(sInput, "*.folia.xml", "name").ToArray();
        } else {
          // Show we don't have input file
          errHandle.DoError("Main", "Cannot find input file(s) in: " + sInput);
        }
        // Validate
        if (arInput.Length == 0) { SyntaxError("4"); return; }
        // Check existence of output dir
        if (!Directory.Exists(sOutput)) {
          // Create the output directory
          Directory.CreateDirectory(sOutput);
        }
        // Call the main entry point for the conversion
        feConv objConv = new feConv();

        // Other initialisations
        // Initialise the Treebank Xpath functions, which may make use of tb:matches()
        util.XPathFunctions.conTb.AddNamespace("tb", util.XPathFunctions.TREEBANK_EXTENSIONS);

        // Loop through the input files
        for (int i = 0; i < arInput.Length; i++) {
          // Parse this input file to the output directory
          if (!objConv.ParseOneFoliaEntity(arInput[i], sOutput, sParsed, bOverwrite, bIsDebug, bKeepGarbage)) {
            errHandle.DoError("Main", "Could not parse file [" + arInput[i] + "]");
            return;
          }
        }
        // Exit the program
        Console.WriteLine("Ready");
      } catch (Exception ex) {
        errHandle.DoError("Main", ex); // Provide standard error message
        throw;
      }
    }



    /* -------------------------------------------------------------------------------------
     * Name:  SyntaxError
     * Goal:  Show simple syntax error message to the user
     * History:
     * 2/oct/2015 ERK Created
       ------------------------------------------------------------------------------------- */
    static void SyntaxError(String sChk) {
      Console.WriteLine("Syntax: foliaparse -i inputFileOrDir -o outputDir [-d] [-g] " +
        "[-m {method}] [-a {alpinolocation}]\n" +
        "\n\n\tMethods: file, sentence, two-pass\n" +
        "\n\n\tNote: output directory must differ from input one\n" +
        "\n\tCheckpoint #" + sChk);
    }

  }
}
