using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zasm
{
    public static class AsmReporter
    {
        public static void TokenErr()
        {
            Console.Error.WriteLine("Error at line " + AsmVars.LineNo);
            Console.Error.WriteLine("Unidentifiable token");
            Console.ReadKey();
            Environment.Exit(1);
        }

        public static void SyntaxErr()
        {
            Console.Error.WriteLine("Error at line " + AsmVars.LineNo);
            Console.Error.WriteLine("Syntax Error");
            Console.ReadKey();
            Environment.Exit(1);
        }

        public static void EvalErr()
        {
            Console.Error.WriteLine("Error at line " + AsmVars.LineNo);
            Console.Error.WriteLine("Evaluation error");
            Console.ReadKey();
            Environment.Exit(1);
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
