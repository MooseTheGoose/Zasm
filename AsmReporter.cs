using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zasm
{
    public static class AsmReporter
    {
        public static bool TokenErr()
        {
            Console.Error.WriteLine("Error at line " + AsmVars.LineNo);
            Console.Error.WriteLine("Unidentifiable token");
            return false;
        }

        public static bool SyntaxErr()
        {
            Console.Error.WriteLine("Error at line " + AsmVars.LineNo);
            Console.Error.WriteLine("Syntax Error");
            return false;
        }

        public static bool EvalErr()
        {
            Console.Error.WriteLine("Error at line " + AsmVars.LineNo);
            Console.Error.WriteLine("Evaluation error");
            return false;
        }

        public static void Imm16WarnOverflow(int imm)
        {
            if (imm > 0xFFFF || imm < -0x8000)
            {
                Console.WriteLine("Warning at line " + AsmVars.LineNo);
                Console.WriteLine("Immediate in instruction overflows");
            }
        }

        public static void Imm8WarnOverflow(int imm)
        {
            if (imm > 0xFF || imm < -0x80)
            {
                Console.WriteLine("Warning at line " + AsmVars.LineNo);
                Console.WriteLine("Immediate in instruction overflows");
            }
        }

        public static void SImm8WarnOverflow(int imm)
        {
            if (imm > 0x7F || imm < -0x80)
            {
                Console.WriteLine("Warning at line " + AsmVars.LineNo);
                Console.WriteLine("Signed immediate in instruction overflows");
            }
        }
    }
}
