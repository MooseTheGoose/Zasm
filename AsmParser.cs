using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Zasm.AsmReporter;

namespace Zasm
{
    public enum DTKind
    {
        OPERATOR, IMM, REG, SPECREG, NORMPAIR,
        ADR, SPECPAIR, NMEMONIC, INSTRUCTION,
        CONDITION, IREG, IPAIR
    }

    public abstract class DTree
    {
        public DTKind kind;
        public DTree next = null, children = null;
    }

    public enum Condition
    {
        NZ = 0, Z = 1, NC = 2, C = 3,
        PO = 4, PE = 5, P = 6, M = 7
    }

    public class ConditionTree : DTree
    {
        public static readonly string[] condnames =
            { "NZ", "Z", "NC", "C", "PO", "PE", "P", "M" };

        public Condition cond;

        public static bool IsCondition(string s)
        { return Array.IndexOf(condnames, s) >= 0; }

        public ConditionTree(string cond)
        {
            kind = DTKind.CONDITION;
            int index = Array.IndexOf(condnames, cond);
            System.Diagnostics.Debug.Assert(index >= 0);
            this.cond = (Condition)index;
        }
    }

    public class OperatorTree : DTree
    {
        public OperatorKind op;

        public OperatorTree(OperatorKind op)
        {
            kind = DTKind.OPERATOR;
            this.op = op;
        }
    }

    public enum Register
    {
        B = 0, C = 1, D = 2, E = 3,
        H = 4, L = 5, A = 7
    };

    public class RegTree : DTree
    {
        public static readonly string[] regnames =
            { "B", "C", "D", "E", "H", "L", "A" };

        public static readonly int[] regvalues =
            { 0, 1, 2, 3, 4, 5, 7 };

        public Register reg;

        public static bool IsReg(string val)
        { return Array.IndexOf(regnames, val) >= 0; }

        public RegTree(string regname)
        {
            kind = DTKind.REG;

            int index = Array.IndexOf(regnames, regname);
            System.Diagnostics.Debug.Assert(index >= 0);
            reg = (Register)regvalues[index];
        }
    }

    public enum SpecReg
    {
        R = 0, I = 1
    }
    public class SpecRegTree : DTree
    {
        public static readonly string[] regnames =
            { "R", "I" };

        public SpecReg reg;

        public static bool IsSpecReg(string val)
        { return Array.IndexOf(regnames, val) >= 0; }

        public SpecRegTree(string regname)
        {
            kind = DTKind.SPECREG;

            int index = Array.IndexOf(regnames, regname);
            System.Diagnostics.Debug.Assert(index >= 0);
            reg = (SpecReg)index;
        }
    }

    public enum IReg
    {
        IXL = 0, IXH = 1, IYL = 2, IYH = 3
    }

    public class IRegTree : DTree
    {
        public static readonly string[] regnames =
        { "IXL", "IXH", "IYL", "IYH" };

        public IReg reg;

        public static bool IsIReg(string val)
        { return Array.IndexOf(regnames, val) >= 0; }

        public IRegTree(string regname)
        {
            kind = DTKind.IREG;

            int index = Array.IndexOf(regnames, regname);
            System.Diagnostics.Debug.Assert(index >= 0);
            reg = (IReg)index;
        }
    }

    public enum NormPair
    {
        BC = 0, DE = 1, HL = 2, SP = 3
    };

    public class NormPairTree : DTree
    {
        public static readonly string[] pairnames =
        { "BC", "DE", "HL", "SP" };

        public NormPair normpair;

        public static bool IsNormPair(string val)
        { return Array.IndexOf(pairnames, val) >= 0; }

        public NormPairTree(string pairname)
        {
            kind = DTKind.NORMPAIR;
            int pair = Array.IndexOf(pairnames, pairname);

            if (pair < 0)
            {
                Console.Error.WriteLine("ERROR in RPairTree()");
                Console.Error.WriteLine(normpair
                    + " is not a normal pair");
                Environment.Exit(-1);
            }

            normpair = (NormPair)pair;
        }
    }

    public enum SpecPair
    {
        AF = 0, AFSHADOW = 1
    }

    public class SpecPairTree : DTree
    {
        public static readonly string[] pairnames =
        {
            "AF", "AF'"
        };

        public SpecPair specpair;

        public static bool IsSpecPair(string val)
        { return Array.IndexOf(pairnames, val) >= 0; }

        public SpecPairTree(string pairname)
        {
            kind = DTKind.SPECPAIR;
            int pair = Array.IndexOf(pairnames, pairname);

            if (pair < 0)
            {
                Console.Error.WriteLine("ERROR in SpecPairTree()");
                Console.Error.WriteLine(pairname
                    + " is not a special pair");
                Environment.Exit(-1);
            }

            specpair = (SpecPair)pair;
        }
    }

    public enum IPair
    {
        IX = 0, IY = 1
    }

    public class IPairTree : DTree
    {
        public static readonly string[] pairnames =
        {
            "IX", "IY"
        };

        public IPair ipair;

        public static bool IsIPair(string val)
        { return Array.IndexOf(pairnames, val) >= 0; }

        public IPairTree(string pairname)
        {
            kind = DTKind.IPAIR;
            int pair = Array.IndexOf(pairnames, pairname);

            if (pair < 0)
            {
                Console.Error.WriteLine("ERROR in SpecPairTree()");
                Console.Error.WriteLine(pairname
                    + " is not a special pair");
                Environment.Exit(-1);
            }

            ipair = (IPair)pair;
        }
    }

    public class ImmTree : DTree
    {
        public int immediate;

        public ImmTree(int number)
        {
            kind = DTKind.IMM;
            immediate = number;
        }
    }

    public enum Nmemonic
    {
        LD = 0, PUSH = 1, POP = 2, EX = 3, EXX = 4, 
        LDI = 5, LDIR = 6, LDD = 7, LDDR = 8, CPI = 9, 
        CPIR = 10, CPD = 11, CPDR = 12, ADD = 13, ADC = 14, 
        SUB = 15, SBC = 16, AND = 17, OR = 18, XOR = 19, 
        INC = 20, DEC = 21, DAA = 22, CPL = 23, NEG = 24, 
        CCF = 25, SCF = 26, NOP = 27, HALT = 28,
        DI = 29, EI = 30, IM = 31, RLCA = 32, RLA = 33, 
        RRCA = 34, RRA = 35, RLC = 36, RL = 37, RRC = 38, 
        RR = 39, SLA = 40, SRA = 41, SRL = 42, 
        RLD = 43, RRD = 44, BIT = 45, SET = 46, RES = 47, 
        JP = 48, JR = 49, DJNZ = 50, CALL = 51, RET = 52,
        RETI = 53, RETN = 54, RST = 55, IN = 56, INI = 57,
        INIR = 58, IND = 59, INDR = 60, OUT = 61, OUTI = 62,
        OUTIR = 63, OUTD = 64, OUTDR = 65, CP = 66
    };

    public class NmemonicTree : DTree
    {
        public static readonly string[] NmemonicTable =
        {
            "LD", "PUSH", "POP", "EX", "EXX", "LDI",
            "LDIR", "LDD", "LDDR", "CPI", "CPIR",
            "CPD", "CPDR", "ADD", "ADC", "SUB", "SBC",
            "AND", "OR", "XOR", "INC", "DEC", "DAA",
            "CPL", "NEG", "CCF", "SCF", "NOP", "HALT",
            "DI", "EI", "IM", "RLCA", "RLA", "RRCA", "RRA",
            "RLC", "RL", "RRC", "RR", "SLA", "SRA", "SRL",
            "RLD", "RRD", "BIT", "SET", "RES", "JP", "JR",
            "DJNZ", "CALL", "RET", "RETI", "RETN", "RST",
            "IN", "INI", "INIR", "IND", "INDR", "OUT",
            "OUTI", "OUTIR", "OUTD", "OUTDR", "CP"
        };

        public Nmemonic nmemonic;

        public static bool IsNmemonic(string s)
        {
            return Array.IndexOf(NmemonicTable, s) >= 0;
        }

        public NmemonicTree(string nmemonic)
        {
            kind = DTKind.NMEMONIC;
            int index = Array.IndexOf(NmemonicTable, nmemonic);

            if (index < 0)
            {
                Console.Error.WriteLine("ERROR in NmemonicTree()");
                Console.Error.WriteLine(nmemonic 
                    + " is not a nmemonic");
                Environment.Exit(-1);
            }

            this.nmemonic = (Nmemonic)index;
        }
    }

    public class AdrTree : DTree
    {
        public AdrTree(Token[] adrtokens, int startindex)
        {
            kind = DTKind.ADR;

            Token curtoken;
            DTree currchild, tempchild;

            if (adrtokens.Length <= 0)
            {
                Console.Error.WriteLine("ERROR in AdrTree()");
                Console.Error.WriteLine("Tokens array is empty");
                Environment.Exit(-1);
            }

            curtoken = adrtokens[0];

            if (curtoken.id == TokenKind.IDENTIFIER)
            {
                string iden = ((IdentifierToken)curtoken).identifier.ToUpper();

                if (NormPairTree.IsNormPair(iden))
                {
                    this.children = new NormPairTree(iden);
                }
                else if (IPairTree.IsIPair(iden))
                {
                    this.children = new IPairTree(iden);
                }
                else if (RegTree.IsReg(iden))
                {
                    this.children = new RegTree(iden);
                }
                else
                {
                    SyntaxErr();
                }

                currchild = this.children;

                if (adrtokens.Length == 3)
                {
                    curtoken = adrtokens[1];
                    Token numbertoken = adrtokens[2];

                    if (curtoken.id == TokenKind.OPERATOR &&
                        numbertoken.id == TokenKind.NUMBER)
                    {
                        OperatorKind op = ((OperatorToken)curtoken).op;
                        int number = ((NumberToken)numbertoken).number;

                        if (op != OperatorKind.PLUS && op != OperatorKind.MINUS)
                        {
                            SyntaxErr();
                        }

                        tempchild = new OperatorTree(op);
                        currchild.next = tempchild;
                        currchild = tempchild;
                        tempchild = new ImmTree(number);
                        currchild.next = tempchild;
                        currchild = tempchild;
                    }
                }
                else if (adrtokens.Length != 1)
                {
                    SyntaxErr();
                }

            }
            else if (curtoken.id == TokenKind.NUMBER)
            {
                this.children = new ImmTree(((NumberToken)curtoken).number);

                if (adrtokens.Length != 1)
                {
                    SyntaxErr();
                }
            }
            else 
            {
                SyntaxErr();
            }
        }
    }

    public class InstructionTree : DTree
    {
        public InstructionTree()
        {
            kind = DTKind.INSTRUCTION;
        }

        /*
         * Method assumes preprocessor came
         * in and dealt with labels and directives.
         */
        public void Derive(Token[] tokens)
        {
            DTree curchild = null, tempchild = null;
            Token curtoken;
            string iden;
            int i = 0;

            next = null;
            children = null;

            if (tokens.Length == 0) return;

            curtoken = tokens[0];
            if (curtoken.id != TokenKind.IDENTIFIER)
            {
                SyntaxErr();
            }

            iden = ((IdentifierToken)curtoken).identifier.ToUpper();
            if (NmemonicTree.IsNmemonic(iden))
            {
                children = new NmemonicTree(iden);
                curchild = children;
                i = 1;
            }
            else
            {
                SyntaxErr();
            }


            while (i < tokens.Length)
            {
                curtoken = tokens[i];

                switch (curtoken.id)
                {
                    case TokenKind.IDENTIFIER:
                        iden = ((IdentifierToken)curtoken).identifier.ToUpper();


                        if (RegTree.IsReg(iden))
                        { tempchild = new RegTree(iden); }

                        else if (NormPairTree.IsNormPair(iden))
                        { tempchild = new NormPairTree(iden); }

                        else if (SpecPairTree.IsSpecPair(iden))
                        { tempchild = new SpecPairTree(iden); }

                        else if (ConditionTree.IsCondition(iden))
                        { tempchild = new ConditionTree(iden); }

                        else if (SpecRegTree.IsSpecReg(iden))
                        { tempchild = new SpecRegTree(iden); }

                        else if (IRegTree.IsIReg(iden))
                        { tempchild = new IRegTree(iden); }

                        else if (IPairTree.IsIPair(iden))
                        { tempchild = new IPairTree(iden); }

                        else
                        {

                            SyntaxErr();
                        }

                        curchild.next = tempchild;
                        curchild = tempchild;

                        i++;
                        break;
                    case TokenKind.NUMBER:
                        tempchild = new ImmTree( ((NumberToken)curtoken).number );
                        curchild.next = tempchild;
                        curchild = tempchild;
                        i++;
                        break;
                    case TokenKind.OPERATOR:
                        OperatorKind op = ((OperatorToken)curtoken).op;
                        switch (op)
                        {
                            case OperatorKind.LPAREN:
                                int start = i;
                                Token[] adrtokens;
                                i++;
                                while (i < tokens.Length)
                                {
                                    curtoken = tokens[i];
                                    if (curtoken.id == TokenKind.OPERATOR
                                        && ((OperatorToken)curtoken).op
                                        == OperatorKind.RPAREN)
                                    {
                                        break;
                                    }
                                    i++;
                                }
                                if (i >= tokens.Length)
                                {
                                    SyntaxErr();
                                }
                                adrtokens = new Token[i-start-1];
                                Array.Copy(tokens, start+1, 
                                    adrtokens, 0, adrtokens.Length);
                                tempchild = new AdrTree(adrtokens, start + 1);
                                curchild.next = tempchild;
                                curchild = tempchild;
                                i++;
                                break;
                            default:
                                SyntaxErr();
                                break;
                        }
                        break;
                    default:
                        break;
                }

                if (i < tokens.Length)
                {
                    curtoken = tokens[i];
                    if (curtoken.id != TokenKind.OPERATOR ||
                        ((OperatorToken)curtoken).op != OperatorKind.COMMA)
                    {
                        SyntaxErr();
                    }
                    i++;
                    if (i >= tokens.Length)
                    {
                        SyntaxErr();
                    }
                }
            }
        }
    }
}

/* Deals with labels. Let the Preprocessor deal with that. */

/*if (!AsmVars.Label2Adr.ContainsKey(iden))
           {
               AsmVars.Label2Adr.Add(iden, AsmVars.CurAdr);
               if (tokens.Length < 2)
               {
                   AsmVars.ReportUserFatalError
                       ("Expected colon after label", 0);
               }

               i++;
               curtoken = tokens[i];

               if (curtoken.id != TokenKind.OPERATOR
                   || ((OperatorToken)curtoken).op != OperatorKind.COLON)
               {
                   AsmVars.ReportUserFatalError
                       ("Colon required for label definition", 1);
               }

               i++;
               curtoken = tokens[i];
               if (curtoken.id == TokenKind.IDENTIFIER)
               {
                   iden = ((IdentifierToken)curtoken).identifier;
                   if (!NmemonicTree.IsNmemonic(iden))
                   {
                       AsmVars.ReportUserFatalError
                        ("Valid nmemonic or directive must come after label", i);
                   }
               }
               else
               {
                   AsmVars.ReportUserFatalError
                   ("Expected identifier after label", i);
               }
               children = new NmemonicTree(iden);
               curchild = children;
               i++;
           }
           else
           {
               AsmVars.ReportUserFatalError
                   ("Error: Label " + iden + " already defined", 0);
           }
           */
