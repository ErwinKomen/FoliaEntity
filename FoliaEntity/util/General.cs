using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.IO.Compression;

namespace FoliaEntity.util {
  class General {
    static ErrHandle errHandle = new ErrHandle();
    // ------------------------------------------------------------------------------------
    // Name:   DoLike
    // Goal:   Perform the "Like" function using the pattern (or patterns) stored in [strPattern]
    //         There can be more than 1 pattern in [strPattern], which must be separated
    //         by a vertical bar: |
    // History:
    // 17-06-2010  ERK Created
    // ------------------------------------------------------------------------------------
    public static bool DoLike(string strText, string strPattern) {
      string[] arPattern = null;
      // Array of patterns
      int intI = 0;
      // Counter

      try {
        // Validate
        if ((strText == null))
          return true;
        // If the text to compare is empty, then we return false
        if ((string.IsNullOrEmpty(strText)))
          return false;
        // Reduce the [strPattern]
        strPattern = strPattern.Trim().ToLower();
        // Take lower case of the text
        strText = strText.ToLower();
        // SPlit the [strPattern] into different ones
        arPattern = strPattern.Split(new string[] { "|" }, StringSplitOptions.None);
        // Perform the "Like" operation for all needed patterns
        for (intI = 0; intI < arPattern.Length; intI++) {
          // See if something positive comes out of this comparison
          if (strText.IsLike(arPattern[intI])) return true;
        }
        // No match has happened, so return false
        return false;
      } catch (Exception ex) {
        // Show error
        ErrHandle.HandleErr("General/DoLike", ex);
        // Return failure
        return false;
      }
    }

    public static bool IsNumeric(String sText) {
      return Regex.IsMatch(sText, @"^\d+$");
    }

    /* -------------------------------------------------------------------------------------
     * Name:  getFilesSorted
     * Goal:  Make a list of the files in the indicated directory
     * History:
     * 2/oct/2015 ERK Created
       ------------------------------------------------------------------------------------- */
    public static List<String> getFilesSorted(String sDir, String sFilter, String sType = "length") {
      try {

        // Access the file system
        System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(sDir);
        // Then get the files from here
        IEnumerable<System.IO.FileInfo> fileList = dir.GetFiles(sFilter, System.IO.SearchOption.AllDirectories);
        // Create a list for the files
        List<String> lstBack = new List<String>();
        // SOrt the files
        switch (sType) {
          case "length":  // order by length ascending
            var querySizeOrderL =
              from file in fileList
              let len = GetFileLength(file)
              orderby len ascending
              select file;
            // Add the files to a list
            foreach (var file in querySizeOrderL) {
              lstBack.Add(file.FullName);
            }
            break;
          case "name":    // Order by name ascending
            var querySizeOrderN =
              from file in fileList
              let name = file.Name
              orderby name ascending
              select file;
            // Add the files to a list
            foreach (var file in querySizeOrderN) {
              lstBack.Add(file.FullName);
            }
            break;
        }
        // Rerturn the list
        return lstBack;
      } catch (Exception ex) {
        ErrHandle.HandleErr("General/getFilesSorted", ex); // Provide standard error message
        return null;
      }
    }
    static long GetFileLength(System.IO.FileInfo fi) {
      long retval;
      try {
        retval = fi.Length;
      } catch (System.IO.FileNotFoundException) {
        // If a file is no longer present,

        // just add zero bytes to the total.

        retval = 0;
      }
      return retval;
    }
    /// <summary>
    /// In-memory decompression of GZIPped data 
    /// </summary>
    /// <param name="gzip"></param>
    /// <returns></returns>
    public static byte[] DecompressMem(byte[] gzip) {
      // Create a GZIP stream with decompression mode.
      // ... Then create a buffer and write into while reading from the GZIP stream.
      using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress)) {
        const int size = 4096;
        byte[] buffer = new byte[size];
        using (MemoryStream memory = new MemoryStream()) {
          int count = 0;
          do {
            count = stream.Read(buffer, 0, size);
            if (count > 0) {
              memory.Write(buffer, 0, count);
            }
          }
          while (count > 0);
          return memory.ToArray();
        }
      }
    }


    /// <summary>
    /// File-to-file buffered Compression
    /// </summary>
    /// <param name="sFileIn"></param>
    /// <param name="sFileOut"></param>
    /// <returns></returns>
    public static bool CompressFile(String sFileIn, String sFileOut) {
      const int size = 8192;
      byte[] buffer = new byte[size];

      try {
        using (FileStream fDecom = new FileStream(sFileIn, FileMode.Open, FileAccess.Read))
        using (FileStream fCompr = new FileStream(sFileOut, FileMode.Create, FileAccess.Write))
        using (GZipStream alg = new GZipStream(fCompr, CompressionMode.Compress)) {
          int bytesRead = 0;
          do {
            // Read buffer
            bytesRead = fDecom.Read(buffer, 0, buffer.Length);
            // Write buffer away
            if (bytesRead > 0) alg.Write(buffer, 0, bytesRead);

          } while (bytesRead > 0);
          // finish writing
          alg.Flush();
          alg.Close(); alg.Dispose();
          // Finish reading
          fCompr.Close();
          fDecom.Close(); fDecom.Dispose();
        }
        // Return success
        return true;
      } catch (Exception ex) {
        ErrHandle.HandleErr("CompressFile", ex);
        // Return failure
        return false;
      }
    }


    /// <summary>
    /// File-to-file buffered decompression
    /// </summary>
    /// <param name="sFileIn"></param>
    /// <param name="sFileOut"></param>
    /// <returns></returns>
    public static bool DecompressFile(String sFileIn, String sFileOut) {
      const int size = 8192;
      byte[] buffer = new byte[size];

      try {
        using (FileStream fCompr = new FileStream(sFileIn, FileMode.Open, FileAccess.Read))
        using (FileStream fDecom = new FileStream(sFileOut, FileMode.Create, FileAccess.Write))
        using (GZipStream alg = new GZipStream(fCompr, CompressionMode.Decompress)) {
          int bytesRead = 0;
          do {
            // Read buffer
            bytesRead = alg.Read(buffer, 0, buffer.Length);
            // Write buffer away
            if (bytesRead > 0) fDecom.Write(buffer, 0, bytesRead);

          } while (bytesRead > 0);
          // finish writing
          fDecom.Close();
          // Finish reading
          alg.Close();
          fCompr.Close();
        }
        // Return success
        return true;
      } catch (Exception ex) {
        ErrHandle.HandleErr("DecompressFile", ex);
        // Return failure
        return false;
      }
    }

    /// <summary>
    /// In-place file decompression
    /// </summary>
    /// <param name="fileToDecompress"></param>
    public static void Decompress(FileInfo fileToDecompress) {
      using (FileStream originalFileStream = fileToDecompress.OpenRead()) {
        string currentFileName = fileToDecompress.FullName;
        string newFileName = currentFileName.Remove(currentFileName.Length - fileToDecompress.Extension.Length);

        using (FileStream decompressedFileStream = File.Create(newFileName)) {
          using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress)) {
            decompressionStream.CopyTo(decompressedFileStream);
            Console.WriteLine("Decompressed: {0}", fileToDecompress.Name);
          }
        }
      }
    }

    /// <summary>
    /// Convert a string consisting of base64 encoded characters to a number
    /// </summary>
    /// <param name="sEncoded"></param>
    /// <returns></returns>
    public static int base64ToInt(String sEncoded) {
      int iBack = 0;
      try {
        // COnvert string into byte array
        char[] arEncoded = sEncoded.ToCharArray();
        // Treat the characters one-by-one from right-to-left
        for (int i = 0 ; i< arEncoded.Length; i++) {
          // Conversion depends on the character values
          char chThis = arEncoded[i];
          int iThis = 0;
          switch (chThis) {
            case 'A': iThis = 0; break; case 'B': iThis = 1; break; case 'C': iThis = 2; break; case 'D': iThis = 3; break;
            case 'E': iThis = 4; break; case 'F': iThis = 5; break; case 'G': iThis = 6; break; case 'H': iThis = 7; break;
            case 'I': iThis = 8; break; case 'J': iThis = 9; break; case 'K': iThis = 10; break; case 'L': iThis = 11; break;
            case 'M': iThis = 12; break; case 'N': iThis = 13; break; case 'O': iThis = 14; break; case 'P': iThis = 15; break;
            case 'Q': iThis = 16; break; case 'R': iThis = 17; break; case 'S': iThis = 18; break; case 'T': iThis = 19; break;
            case 'U': iThis = 20; break; case 'V': iThis = 21; break; case 'W': iThis = 22; break; case 'X': iThis = 23; break;
            case 'Y': iThis = 24; break; case 'Z': iThis = 25; break; case 'a': iThis = 26; break; case 'b': iThis = 27; break;
            case 'c': iThis = 28; break; case 'd': iThis = 29; break; case 'e': iThis = 30; break; case 'f': iThis = 31; break;
            case 'g': iThis = 32; break; case 'h': iThis = 33; break; case 'i': iThis = 34; break; case 'j': iThis = 35; break;
            case 'k': iThis = 36; break; case 'l': iThis = 37; break; case 'm': iThis = 38; break; case 'n': iThis = 39; break;
            case 'o': iThis = 40; break; case 'p': iThis = 41; break; case 'q': iThis = 42; break; case 'r': iThis = 43; break;
            case 's': iThis = 44; break; case 't': iThis = 45; break; case 'u': iThis = 46; break; case 'v': iThis = 47; break;
            case 'w': iThis = 48; break; case 'x': iThis = 49; break; case 'y': iThis = 50; break; case 'z': iThis = 51; break;
            case '0': iThis = 52; break; case '1': iThis = 53; break; case '2': iThis = 54; break; case '3': iThis = 55; break;
            case '4': iThis = 56; break; case '5': iThis = 57; break; case '6': iThis = 58; break; case '7': iThis = 59; break;
            case '8': iThis = 60; break; case '9': iThis = 61; break; case '+': iThis = 62; break; case '/': iThis = 63; break;
          }
          // Add the new value to the existing ones
          iBack = iBack * 64 + iThis;
        }
        return iBack;
      } catch (Exception ex) {
        ErrHandle.HandleErr("base64ToInt", ex);
        // Return failure
        return -1;
      }
    }

  }
  // ====================================== OTHER CLASS ========================================
  /* ====================================================
     This code has been copied from:
     http://www.blackbeltcoder.com/Articles/net/implementing-vbs-like-operator-in-c
     ==================================================== */
  static class StringCompareExtensions {
    /// <summary>
    /// Implement's VB's Like operator logic.
    /// </summary>
    public static bool IsLike(this string s, string pattern) {
      // Characters matched so far
      int matched = 0;

      // Loop through pattern string
      for (int i = 0; i < pattern.Length; ) {
        // Check for end of string
        if (matched >= s.Length)
          return false;

        // Get next pattern character
        char c = pattern[i++];
        if (c == '[') // Character list
            {
          // Test for exclude character
          bool exclude = (i < pattern.Length && pattern[i] == '!');
          if (exclude)
            i++;
          // Build character list
          int j = pattern.IndexOf(']', i);
          if (j < 0)
            j = s.Length;
          HashSet<char> charList = CharListToSet(pattern.Substring(i, j - i));
          i = j + 1;

          if (charList.Contains(s[matched]) == exclude)
            return false;
          matched++;
        } else if (c == '?') // Any single character
            {
          matched++;
        } else if (c == '#') // Any single digit
            {
          if (!Char.IsDigit(s[matched]))
            return false;
          matched++;
        } else if (c == '*') // Zero or more characters
            {
          if (i < pattern.Length) {
            // Matches all characters until
            // next character in pattern
            char next = pattern[i];
            int j = s.IndexOf(next, matched);
            if (j < 0)
              return false;
            matched = j;
          } else {
            // Matches all remaining characters
            matched = s.Length;
            break;
          }
        } else // Exact character
            {
          if (c != s[matched])
            return false;
          matched++;
        }
      }
      // Return true if all characters matched
      return (matched == s.Length);
    }

    /// <summary>
    /// Converts a string of characters to a HashSet of characters. If the string
    /// contains character ranges, such as A-Z, all characters in the range are
    /// also added to the returned set of characters.
    /// </summary>
    /// <param name="charList">Character list string</param>
    private static HashSet<char> CharListToSet(string charList) {
      HashSet<char> set = new HashSet<char>();

      for (int i = 0; i < charList.Length; i++) {
        if ((i + 1) < charList.Length && charList[i + 1] == '-') {
          // Character range
          char startChar = charList[i++];
          i++; // Hyphen
          char endChar = (char)0;
          if (i < charList.Length)
            endChar = charList[i++];
          for (int j = startChar; j <= endChar; j++)
            set.Add((char)j);
        } else set.Add(charList[i]);
      }
      return set;
    }

 
  }

}
