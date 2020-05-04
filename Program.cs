using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zasm
{
    class Program
    {
        public static void DebugToken(Token t)
        {
            switch (t.id)
            {
                case TokenKind.NUMBER:
                    Console.WriteLine("TOKEN: NUMBER");
                    Console.WriteLine("VALUE: " + ((NumberToken)t).number);
                    break;
                case TokenKind.IDENTIFIER:
                    Console.WriteLine("TOKEN: IDENTIFIER");
                    Console.WriteLine("VALUE: " + ((IdentifierToken)t).identifier);
                    break;
                case TokenKind.OPERATOR:
                    Console.WriteLine("TOKEN: OPERATOR");
                    Console.WriteLine("VALUE: " + 
                        OperatorToken.OperatorTable[
                            (int)((OperatorToken)t).op
                            ]);
                    break;
            }
        }

        public static void DebugTree(DTree t, int indent)
        {
            StringBuilder sb = new StringBuilder(indent);
            DTree child;

            for (int i = 0; i < indent; i++)
            {
                sb.Append(' ');
            }

            switch (t.kind)
            {
                case DTKind.NMEMONIC:
                    NmemonicTree nm = (NmemonicTree)t;
                    Console.Write(sb);
                    Console.WriteLine("(NMEMONIC) { " + nm.nmemonic + " }");
                    break;
                case DTKind.OPERATOR:
                    OperatorTree op = (OperatorTree)t;
                    Console.Write(sb);
                    Console.WriteLine("(OPERATOR) { " + op.op + " }");
                    break;
                case DTKind.REG:
                    RegTree reg = (RegTree)t;
                    Console.Write(sb);
                    Console.WriteLine("(REGISTER) { " + reg.reg + " }");
                    break;
                case DTKind.NORMPAIR:
                    NormPairTree npair = (NormPairTree)t;
                    Console.Write(sb);
                    Console.WriteLine("(NORMPAIR) { " + npair.normpair + " }");
                    break;
                case DTKind.SPECPAIR:
                    SpecPairTree spair = (SpecPairTree)t;
                    Console.Write(sb);
                    Console.WriteLine("(SPECPAIR) { " + spair.specpair + " }");
                    break;
                case DTKind.IPAIR:
                    IPairTree ipair = (IPairTree)t;
                    Console.Write(sb);
                    Console.WriteLine("(IPAIR) { " + ipair.ipair + " }");
                    break;
                case DTKind.IMM:
                    ImmTree imm = (ImmTree)t;
                    Console.Write(sb);
                    Console.WriteLine("(IMM) { " + imm.immediate + " }");
                    break;
                case DTKind.ADR:
                    AdrTree adr = (AdrTree)t;
                    child = adr.children;
                    Console.Write(sb);
                    Console.WriteLine("(ADR) {");
                    while (child != null)
                    {
                        DebugTree(child, indent + 4);
                        child = child.next;
                    }
                    Console.Write(sb);
                    Console.WriteLine("}");
                    break;
                case DTKind.INSTRUCTION:
                    InstructionTree inst = (InstructionTree)t;
                    child = inst.children;
                    Console.Write(sb);
                    Console.WriteLine("(INSTRUCTION) {");
                    while (child != null)
                    {
                        DebugTree(child, indent + 4);
                        child = child.next;
                    }
                    Console.Write(sb);
                    Console.WriteLine("}");
                    break;
                case DTKind.CONDITION:
                    ConditionTree cond = (ConditionTree)t;
                    Console.Write(sb);
                    Console.WriteLine("(CONDITION) { " + cond.cond + " }");
                    break;
                case DTKind.SPECREG:
                    SpecRegTree specreg = (SpecRegTree)t;
                    Console.Write(sb);
                    Console.WriteLine("(SPECREG) { " + specreg.reg + " }");
                    break;
                default:
                    break;
            }
        }

        public static readonly string TestString = "add a, (ix + 20)";
        static void Main(string[] args)
        {
            Tokenizer t = new Tokenizer();
            InstructionTree i = new InstructionTree();
            InstructionEvaluator ieval = new InstructionEvaluator();

            t.Tokenize(TestString);

            foreach (Token s in t.tokens)
            {
               DebugToken(s);
            }

            AsmVars.TokenIndicies = t.indicies.ToArray();
            AsmVars.CurLine = TestString;
            i.Derive(t.tokens.ToArray());
            DebugTree(i, 0);

            ieval.Evaluate(i);
            foreach (byte b in ieval.code)
            {
                Console.Write(String.Format("{0,0:X}", b) + " ");
            }
            Console.WriteLine();

            Console.ReadKey();
        }
    }
}
