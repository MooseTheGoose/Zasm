using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zasm
{
    public static class AsmVars
    {
        public static Dictionary<string, int> Label2Adr
            = new Dictionary<string, int>();

        public static int CurAdr = 0;
        public static int LineNo = 0;
        public static int[] TokenIndicies;
        public static string CurLine;

        public static void ReportUserWarning(string message, int tokenindex)
        {
            StringBuilder pointer = new StringBuilder();
            for (int i = 0; i < TokenIndicies[tokenindex]; i++)
            { pointer.Append(' '); }

            pointer.Append('^');

            Console.Error.WriteLine("Warning at " + LineNo + ":" + TokenIndicies[tokenindex]);
            Console.Error.WriteLine(message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CurLine);
            Console.Error.WriteLine(pointer);
            Console.ReadKey();
        }

        public static void ReportUserFatalError(string message, int tokenindex)
        {
            StringBuilder pointer = new StringBuilder();
            for (int i = 0; i < TokenIndicies[tokenindex]; i++)
            { pointer.Append(' '); }

            pointer.Append('^');

            Console.Error.WriteLine("Error at " + LineNo + ":" + TokenIndicies[tokenindex]);
            Console.Error.WriteLine(message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CurLine);
            Console.Error.WriteLine(pointer);
            Console.ReadKey();
            Environment.Exit(1);
        }
    }
}
