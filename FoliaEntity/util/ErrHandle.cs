using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FoliaEntity {
  public class ErrHandle {
    /* -------------------------------------------------------------------------------------
     * Name:  SyntaxError
     * Goal:  General error-handling routine
     * History:
     * 2/oct/2015 ERK Created
       ------------------------------------------------------------------------------------- */
    public void DoError(String sLocation, Exception ex) {
      Console.WriteLine("Error in [" + sLocation + "]: " + ex.Message + "\n" + "Stack: " + ex.StackTrace + "\n");
      int i = 0;
    }
    public void DoError(String sLocation, String sMsg) {
      Console.WriteLine("Error in [" + sLocation + "]: " + sMsg + "\n");
      int i = 0;
    }
    public static void HandleErr(String sLocation, Exception ex) {
      Console.WriteLine("Error in [" + sLocation + "]: " + ex.Message + "\n" + "Stack: " + ex.StackTrace + "\n");
      int i = 0;
    }
    public void Status(String sMsg) {
      Console.WriteLine(sMsg);
    }
  }
}
