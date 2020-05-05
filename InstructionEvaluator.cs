using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Zasm.AsmReporter;

namespace Zasm
{
    public struct AdrData
    {
        public DTKind kind;
        public int rep;
        public bool hasDisplacement;
        public int displacement;

        public AdrData(AdrTree t)
        {
            DTree current = t.children;

            kind = current.kind;
            rep = 0;
            hasDisplacement = false;
            displacement = 0;

            switch (kind)
            {
                case DTKind.REG:
                    rep = (int)((RegTree)current).reg;
                    break;
                case DTKind.NORMPAIR:
                    rep = (int)((NormPairTree)current).normpair;
                    break;
                case DTKind.IPAIR:
                    rep = (int)((IPairTree)current).ipair;
                    break;
                case DTKind.IMM:
                    rep = ((ImmTree)current).immediate;
                    break;
            }

            current = current.next;
            if (current != null)
            {
                System.Diagnostics.Debug.Assert(kind == DTKind.IPAIR);
                System.Diagnostics.Debug.Assert(current.kind == DTKind.OPERATOR);

                OperatorTree op = (OperatorTree)current;

                System.Diagnostics.Debug.Assert
                    (op.op == OperatorKind.PLUS || op.op == OperatorKind.MINUS);

                current = current.next;
                System.Diagnostics.Debug.Assert(current.kind == DTKind.IMM);


                displacement = ((ImmTree)current).immediate;
                if (op.op == OperatorKind.MINUS)
                {
                    displacement = -displacement;
                }
                System.Diagnostics.Debug.Assert(current.next == null);

                hasDisplacement = true;
            }
        }
    }

    public class InstructionEvaluator
    {
        public List<byte> code;

        /*
         *  All nmemonics with no arguments which are not
         *  prefixed with 0xED follow this format
         */
        private bool EvalNoArgs(int argc, int opcode)
        {
            if (argc != 0)
            { return EvalErr(); }
            code.Add((byte)opcode);
            return true;
        }

        private bool EvalNoArgsED(int argc, int opcode)
        {
            if (argc != 0)
            { return EvalErr(); }
            code.Add(0xED);
            code.Add((byte)opcode);
            return true;
        }

        private bool EvalRET(DTree argv, int argc)
        {
            if (argc == 0)
            {
                code.Add(0xC9);
            }
            else if (argc == 1)
            {
                DTree arg = argv;

                if (arg.kind != DTKind.CONDITION) { return EvalErr(); }

                int cond = (int)((ConditionTree)arg).cond;

                code.Add((byte)(0xC0 | cond << 3));
            }
            else { return EvalErr(); }
            return true;
        }

        private bool EvalCALL(DTree argv, int argc)
        {
            if (argc == 1)
            {
                DTree arg = argv;

                if (arg.kind != DTKind.IMM) { return EvalErr(); }

                int imm = ((ImmTree)arg).immediate;

                Imm16WarnOverflow(imm);
                code.Add(0xCD);
                code.Add((byte)imm);
                code.Add((byte)((uint)imm >> 8));
            }
            else if (argc == 2)
            {
                DTree arg1 = argv, arg2 = argv.next;

                if (arg1.kind != DTKind.CONDITION 
                    || arg2.kind != DTKind.IMM)
                { return EvalErr(); }

                int cond = (int)((ConditionTree)arg1).cond;
                int imm = ((ImmTree)arg2).immediate;

                Imm16WarnOverflow(imm);

                code.Add((byte)(0xC4 | cond << 3));
                code.Add((byte)imm);
                code.Add((byte)((uint)imm >> 8));
            }
            else { return EvalErr(); }

            return true;
        }

        private bool EvalRST(DTree argv, int argc)
        {
            if (argc != 1) { return EvalErr(); }

            DTree arg = argv;

            if (arg.kind != DTKind.IMM) { return EvalErr(); }

            int imm = ((ImmTree)arg).immediate;
            if ((imm & ~0x38) != 0) { return EvalErr(); }

            code.Add((byte)(0xC7 | imm));

            return true;
        }

        private bool EvalEX(DTree argv, int argc)
        {
            if (argc != 2) { return EvalErr(); }

            DTree arg1 = argv, arg2 = argv.next;

            switch (arg1.kind)
            {
                case DTKind.SPECPAIR:
                    if (arg2.kind != DTKind.SPECPAIR) { return EvalErr(); }
                    SpecPair sreg1 = ((SpecPairTree)arg1).specpair,
                             sreg2 = ((SpecPairTree)arg2).specpair;

                    if (sreg1 != SpecPair.AF
                        || sreg2 != SpecPair.AFSHADOW)
                    { return EvalErr(); }
                    code.Add(0x08);
                    break;
                case DTKind.NORMPAIR:
                    if (arg2.kind != DTKind.NORMPAIR) { return EvalErr(); }
                    NormPair npair1 = ((NormPairTree)arg1).normpair,
                             npair2 = ((NormPairTree)arg2).normpair;

                    if (npair1 != NormPair.DE
                        || npair2 != NormPair.HL)
                    { return EvalErr(); }

                    code.Add(0xEB);
                    break;
                case DTKind.ADR:
                    AdrData data = new AdrData((AdrTree)arg1);
                    if (data.hasDisplacement
                        || data.kind != DTKind.NORMPAIR
                        || data.rep != (int)NormPair.SP)
                    { return EvalErr(); }

                    if (arg2.kind == DTKind.NORMPAIR
                        && ((NormPairTree)arg2).normpair
                            == NormPair.HL)
                    { code.Add(0xE3); }

                    else if (arg2.kind == DTKind.IPAIR)
                    {
                        IPair ipair = ((IPairTree)arg2).ipair;

                        if (ipair == IPair.IX) { code.Add(0xDD); }
                        else                   { code.Add(0xFD); }

                        code.Add(0xE3);
                    }
                    else { return EvalErr(); }
                    break;
                default:
                    return EvalErr();
            }

            return true;
        }

        private bool EvalOUT(DTree argv, int argc)
        {
            if (argc != 2) { return EvalErr(); }

            DTree arg1 = argv, arg2 = argv.next;

            if (arg1.kind != DTKind.ADR) { return EvalErr(); }

            AdrData data = new AdrData((AdrTree)arg1);

            if (data.hasDisplacement) { return EvalErr(); }

            if (data.kind == DTKind.IMM)
            {
                if (arg2.kind != DTKind.REG
                    || ((RegTree)arg2).reg != Register.A)
                { return EvalErr(); }

                int imm = data.rep;
                Imm8WarnOverflow(imm);
                code.Add(0xD3);
                code.Add((byte)imm);
            }
            else if (data.kind == DTKind.REG)
            {
                if (data.rep != (int)Register.C) { return EvalErr(); }
                
                code.Add(0xED);

                if (arg2.kind == DTKind.REG)
                {
                    int reg = (int)((RegTree)arg2).reg;
                    code.Add((byte)(0x41 | reg << 3));
                }
                else if (arg2.kind == DTKind.IMM)
                {
                    int imm = ((ImmTree)arg2).immediate;
                    if (imm != 0) { return EvalErr(); }
                    code.Add(0x71);
                }
                else { return EvalErr(); }
            }
            else { return EvalErr(); }

            return true;
        }

        private bool EvalIN(DTree argv, int argc)
        {
            if (argc == 1)
            {
                DTree arg = argv;

                if (arg.kind != DTKind.ADR) { return EvalErr(); }

                AdrData data = new AdrData((AdrTree)arg);

                if (data.hasDisplacement
                    || data.kind != DTKind.REG
                    || data.rep != (int)Register.C) 
                { return EvalErr(); }

                code.Add(0xED);
                code.Add(0x70);
            }
            else if (argc == 2)
            {
                DTree arg1 = argv, arg2 = argv.next;

                if (arg2.kind != DTKind.ADR) { return EvalErr(); }

                AdrData data = new AdrData((AdrTree)arg2);

                if (data.hasDisplacement) { return EvalErr(); }

                if (data.kind == DTKind.IMM)
                {
                    if (arg1.kind != DTKind.REG
                        || ((RegTree)arg1).reg != Register.A)
                    { return EvalErr(); }

                    int imm = data.rep;
                    Imm8WarnOverflow(imm);

                    code.Add(0xDB);
                    code.Add((byte)imm);
                }
                else if (data.kind == DTKind.REG
                    && data.rep == (int)Register.C)
                {
                    
                    if (arg1.kind != DTKind.REG) { return EvalErr(); }
                    int reg = (int)((RegTree)arg1).reg;

                    code.Add(0xED);
                    code.Add((byte)(0x40 | reg << 3));
                }
                else { return EvalErr(); }
            }
            else { return EvalErr(); }

            return true;
        }

        private bool EvalIM(DTree argv, int argc)
        {
            if (argc != 1) { return EvalErr(); }

            DTree arg = argv;
            if (arg.kind != DTKind.IMM) { return EvalErr(); }

            int imm = ((ImmTree)arg).immediate;

            if (imm != 0 && imm != 1
                && imm != 2) { return EvalErr(); }

            code.Add(0xED);

            if (imm == 0)      { code.Add(0x46); }
            else if (imm == 2) { code.Add(0x5E); }
            else               { code.Add(0x56); }

            return true;
        }

        private bool EvalJR(DTree argv, int argc)
        {
            if (argc == 1)
            {
                DTree dtree = argv;
                if (dtree.kind != DTKind.IMM)
                {
                    return EvalErr();

                }
                int imm = ((ImmTree)dtree).immediate;

                SImm8WarnOverflow(imm);
                code.Add(0x18);
                code.Add((byte)imm);
            }
            else if (argc == 2)
            {
                DTree condtree = argv, immtree = argv.next;

                if (condtree.kind != DTKind.CONDITION
                    || immtree.kind != DTKind.IMM)
                {
                    return EvalErr();
                }

                int cond = (int)((ConditionTree)condtree).cond;
                int imm = ((ImmTree)immtree).immediate;

                if (cond > (int)Condition.C)
                {
                    return EvalErr();
                }

                SImm8WarnOverflow(imm);

                code.Add((byte)(0x20 | cond << 3));
                code.Add((byte)imm);
            }
            else
            {
                return EvalErr();
            }

            return true;
        }

        private bool EvalDJNZ(DTree argv, int argc)
        {
            if (argc != 1) { return EvalErr(); }

            DTree arg = argv;

            if (arg.kind != DTKind.IMM) { return EvalErr(); }
            int imm = ((ImmTree)arg).immediate;

            SImm8WarnOverflow(imm);

            code.Add(0x10);
            code.Add((byte)imm);

            return true;
        }

        private bool EvalJP(DTree argv, int argc)
        {
            if (argc == 1)
            {
                DTree dtree = argv;
                if (dtree.kind == DTKind.IMM)
                {
                    int imm = ((ImmTree)dtree).immediate;

                    Imm16WarnOverflow(imm);
                    code.Add(0xC3);
                    code.Add((byte)imm);
                    code.Add((byte)(imm >> 8));
                }
                else if (dtree.kind == DTKind.ADR)
                {
                    AdrData data = new AdrData((AdrTree)dtree);

                    if (data.hasDisplacement)
                    {
                        return EvalErr();
                    }

                    if (data.kind == DTKind.IPAIR)
                    {
                        if (data.rep == (int)IPair.IX)
                        { code.Add(0xDD); }
                        else
                        { code.Add(0xFD); }
                    }
                    else if (data.kind != DTKind.NORMPAIR
                        || data.rep != (int)NormPair.HL)
                    {
                        return EvalErr();
                    }

                    code.Add(0xE9);
                }
                else { return EvalErr(); }
            }
            else if (argc == 2)
            {
                DTree condtree = argv, immtree = argv.next;

                if (condtree.kind != DTKind.CONDITION
                    || immtree.kind != DTKind.IMM)
                {
                    return EvalErr();
                }

                int cond = (int)((ConditionTree)condtree).cond;
                int imm = ((ImmTree)immtree).immediate;

                Imm16WarnOverflow(imm);

                code.Add((byte)(0xC2 | cond << 3));
                code.Add((byte)imm);
                code.Add((byte)(imm >> 8));
            }
            else
            {
                return EvalErr();
            }

            return true;
        }

        /*
         * PUSH and POP follow the same argument format but
         * have different offsets.
         */
        private bool EvalPUSHPOP(DTree argv, int argc, int basecode)
        {
            if (argc != 1)
            {
                return EvalErr();
            }

            DTree arg = argv;
            switch (arg.kind)
            {
                case DTKind.NORMPAIR:
                    NormPair npair = ((NormPairTree)arg).normpair;
                    if (npair == NormPair.SP)
                    {
                        return EvalErr();
                    }
                    else { code.Add((byte)(basecode | ((int)npair) << 4)); }
                    break;
                case DTKind.SPECPAIR:
                    SpecPair spair = ((SpecPairTree)arg).specpair;
                    if (spair != SpecPair.AF)
                    {
                        return EvalErr();
                    }
                    else { code.Add((byte)(basecode | 0x30)); }
                    break;
                case DTKind.IPAIR:
                    IPair ipair = ((IPairTree)arg).ipair;
                    if (ipair == IPair.IX)
                    { code.Add(0xDD); }
                    else
                    { code.Add(0xFD); }

                    code.Add((byte)(basecode | 0x20));
                    break;
                default:
                    return EvalErr();
            }

            return true;
        }

        private bool EvalADD(DTree argv, int argc)
        {
            if (argc != 2)
            { return EvalErr(); }

            DTree arg1 = argv, arg2 = argv.next;
            int pair, pair2, reg, reg2;
            IPair ipair;
            AdrData data;

            switch (arg1.kind)
            {
                case DTKind.NORMPAIR:
                    pair = (int)((NormPairTree)arg1).normpair;

                    if (pair != (int)NormPair.HL
                        || arg2.kind != DTKind.NORMPAIR)
                    { return EvalErr(); }

                    pair2 = (int)((NormPairTree)arg2).normpair;

                    code.Add((byte)(0x09 | pair2 << 4));

                    break;
                case DTKind.REG:
                    reg = (int)((RegTree)arg1).reg;

                    if (reg != (int)Register.A)
                    { return EvalErr(); }

                    switch (arg2.kind)
                    {
                        case DTKind.REG:
                            reg2 = (int)((RegTree)arg2).reg;
                            code.Add((byte)(0x80 | reg2));
                            break;
                        case DTKind.IREG:
                            IReg ireg = ((IRegTree)arg2).reg;

                            if (ireg == IReg.IXH || ireg == IReg.IXL)
                            { code.Add(0xDD); }
                            else
                            { code.Add(0xFD); }

                            if (ireg == IReg.IXH || ireg == IReg.IYH)
                            { code.Add(0x84); }
                            else
                            { code.Add(0x85); }              

                            break;
                        case DTKind.ADR:
                            data = new AdrData((AdrTree)arg2);

                            if (data.kind == DTKind.IPAIR)
                            {
                                int disp = data.hasDisplacement ?
                                           0 : data.displacement;
                                SImm8WarnOverflow(disp);

                                if (data.rep == (int)IPair.IX)
                                { code.Add(0xDD); }
                                else
                                { code.Add(0xFD); }

                                code.Add(0x86);
                                code.Add((byte)disp);
                            }
                            else if (!data.hasDisplacement
                                && data.kind == DTKind.NORMPAIR)
                            {
                                if (data.rep != (int)NormPair.HL) 
                                { return EvalErr(); }

                                code.Add(0x86);
                            }
                            else { return EvalErr(); }

                            break;
                        case DTKind.IMM:
                            int imm = ((ImmTree)arg2).immediate;
                            Imm8WarnOverflow(imm);
                            code.Add(0xC6);
                            code.Add((byte)imm);
                            break;
                        default:
                            return EvalErr();
                    }
                    break;
                case DTKind.IPAIR:
                    ipair = ((IPairTree)arg1).ipair;

                    if (ipair == IPair.IX)
                    { code.Add(0xDD); }
                    else
                    { code.Add(0xFD); }

                    if (arg2.kind == DTKind.NORMPAIR)
                    {
                        pair2 = (int)((NormPairTree)arg2).normpair;

                        if (pair2 == (int)NormPair.HL) { return EvalErr(); }

                        code.Add((byte)(0x9 | pair2 << 4));
                    }
                    else if (arg2.kind == DTKind.IPAIR)
                    {
                        bool sameIPair = 
                            ipair == ((IPairTree)arg2).ipair;

                        if (!sameIPair) { return EvalErr(); }

                        code.Add(0x29);
                    }

                    break;
                default:
                    return EvalErr();
            }

            return true;
        }

        /*
         *  ADC, and SBC follow the same format
         *  of encoding. They only have different
         *  offsets.
         */
        private bool EvalArithC(DTree argv, int argc, int npaircode, int regcode)
        {
            if (argc != 2) { return EvalErr(); }

            DTree arg1 = argv, arg2 = argv.next;

            switch (arg1.kind)
            {
                case DTKind.NORMPAIR:
                    if (((NormPairTree)arg1).normpair != NormPair.HL
                        || arg2.kind != DTKind.NORMPAIR) 
                    { return EvalErr(); }

                    int pair = (int)((NormPairTree)arg2).normpair;
                    code.Add(0xED);
                    code.Add((byte)(npaircode | pair << 4));

                    break;
                case DTKind.REG:
                    if(((RegTree)arg1).reg != Register.A) { return EvalErr(); }

                    switch (arg2.kind)
                    {
                        case DTKind.REG:
                            int reg = (int)((RegTree)arg2).reg;
                            code.Add((byte)(regcode | reg));
                            break;
                        case DTKind.ADR:
                            AdrData data = new AdrData((AdrTree)arg2);

                            if (data.kind == DTKind.IPAIR)
                            {
                                int disp = data.hasDisplacement ?
                                           0 : data.displacement;

                                if (data.rep == (int)IPair.IX)
                                { code.Add(0xDD); }
                                else
                                { code.Add(0xFD); }

                                SImm8WarnOverflow(disp);
                                code.Add((byte)disp);
                            }
                            else if (data.hasDisplacement
                                     || data.kind != DTKind.NORMPAIR
                                     || data.rep != (int)NormPair.HL)
                            { return EvalErr(); }

                            code.Add((byte)(regcode | 6));
                            break;
                        case DTKind.IREG:
                            IReg ireg = ((IRegTree)arg2).reg;
                            if (ireg == IReg.IXH || ireg == IReg.IXL)
                            { code.Add(0xDD); }
                            else
                            { code.Add(0xFD); }

                            if (ireg == IReg.IXH || ireg == IReg.IYH)
                            { code.Add((byte)(regcode | 4)); }
                            else
                            { code.Add((byte)(regcode | 5)); }

                            break;
                        case DTKind.IMM:
                            int imm = ((ImmTree)arg2).immediate;
                            Imm8WarnOverflow(imm);
                            code.Add((byte)(regcode | 0x46));
                            code.Add((byte)imm);
                            break;
                        default:
                            return EvalErr();
                    }
                    break;
                default:
                    return EvalErr();
            }

            return true;
        }

        /*
         * SUB, XOR, OR, AND, and CP follow the same
         * format of encoding. They only have different
         * offsets.
         */
        private bool EvalArith(DTree argv, int argc, int basecode)
        {
            if (argc != 1) { return EvalErr(); }

            DTree arg = argv;

            switch (arg.kind)
            {
                case DTKind.REG:
                    int reg = (int)((RegTree)arg).reg;
                    code.Add((byte)(basecode | reg));
                    break;
                case DTKind.IREG:
                    IReg ireg = (IReg)((IRegTree)arg).reg;

                    if (ireg == IReg.IXH || ireg == IReg.IXL)
                    { code.Add(0xDD); }
                    else
                    { code.Add(0xFD); }

                    if (ireg == IReg.IXH || ireg == IReg.IXL)
                    { code.Add((byte)(basecode | 4)); }
                    else
                    { code.Add((byte)(basecode | 5)); }

                    break;
                case DTKind.ADR:
                    AdrData data = new AdrData((AdrTree)arg);
                    if (data.kind == DTKind.IPAIR)
                    {
                        int disp = data.hasDisplacement ?
                                   0 : data.displacement;

                        IPair pair = (IPair)data.rep;

                        if (pair == IPair.IX)
                        { code.Add(0xDD); }
                        else
                        { code.Add(0xFD); }

                        SImm8WarnOverflow(disp);
                        code.Add((byte)disp);
                    }
                    else if (data.hasDisplacement
                        || data.kind != DTKind.NORMPAIR
                        || data.rep != (int)NormPair.HL)
                    { return EvalErr(); }

                    code.Add((byte)(basecode | 6));
                    break;
                case DTKind.IMM:
                    int imm = ((ImmTree)arg).immediate;
                    SImm8WarnOverflow(imm);
                    code.Add((byte)(basecode | 0x46));
                    code.Add((byte)imm);
                    break;
                default:
                    return EvalErr();
            }

            return true;
        }

        /*
         * RLC, RRC, RL, RR, SLA, SRA, SLL, and SRL all follow
         * the same format of encoding. They only have
         * different offsets.
         */
        private bool EvalRotShift(DTree argv, int argc, int basecode)
        {
            if (argc == 1)
            {
                DTree arg = argv;

                if (arg.kind == DTKind.REG)
                {
                    int reg = (int)((RegTree)arg).reg;

                    code.Add(0xCB);
                    code.Add((byte)(basecode | reg));
                }
                else if (arg.kind == DTKind.ADR)
                {
                    AdrData data = new AdrData((AdrTree)arg);

                    if (data.kind == DTKind.IPAIR)
                    {
                        if (data.rep == (int)IPair.IX)
                        { code.Add(0xDD); }
                        else
                        { code.Add(0xFD);  }

                        int disp = data.hasDisplacement ?
                                   0 : data.displacement;

                        SImm8WarnOverflow(data.displacement);
                        code.Add(0xCB);
                        code.Add((byte)data.displacement);
                        code.Add((byte)(basecode | 6));
                    }
                    else if (!data.hasDisplacement && data.kind == DTKind.NORMPAIR)
                    {
                        if (data.rep != (int)NormPair.HL) { return EvalErr(); }
                        code.Add(0xCB);
                        code.Add((byte)(basecode | 6));
                    }
                    else { return EvalErr(); }
                }
                else
                { return EvalErr(); }
            }
            else if (argc == 2)
            {
                DTree arg1 = argv, arg2 = argv.next;

                if (arg1.kind != DTKind.ADR || arg2.kind != DTKind.REG)
                { return EvalErr(); }

                AdrData data = new AdrData((AdrTree)arg1);
                int reg = (int)((RegTree)arg2).reg;

                if (data.kind != DTKind.IPAIR)
                { return EvalErr(); }

                if (data.rep == (int)IPair.IX)
                { code.Add(0xDD); }
                else
                { code.Add(0xFD); }

                int disp = data.hasDisplacement ?
                           0 : data.displacement;

                SImm8WarnOverflow(disp);

                code.Add(0xCB);
                code.Add((byte)disp);
                code.Add((byte)(basecode | reg));
            }
            else { return EvalErr(); }

            return true;
        }

        /*
         *  BIT, SET, and RES all follow the same format of encoding.
         *  They only have different offsets
         */
        private bool EvalBit(DTree argv, int argc, int basecode)
        {
            if (argc == 2)
            {
                DTree arg1 = argv, arg2 = argv.next;

                if (arg1.kind != DTKind.IMM) { return EvalErr(); }

                int imm = ((ImmTree)arg1).immediate;
                if (imm < 0 || imm > 7) { return EvalErr(); }

                if (arg2.kind == DTKind.REG)
                {
                    int reg = (int)((RegTree)arg2).reg;

                    code.Add(0xCB);
                    code.Add((byte)(basecode | imm << 3 | reg));
                }
                else if (arg2.kind == DTKind.ADR)
                {
                    AdrData data = new AdrData((AdrTree)arg2);

                    if (data.kind == DTKind.IPAIR)
                    {
                        if (data.rep == (int)IPair.IX)
                        { code.Add(0xDD); }
                        else
                        { code.Add(0xFD); }

                        int disp = data.hasDisplacement ?
                                   0 : data.displacement;

                        SImm8WarnOverflow(disp);

                        code.Add(0xCB);
                        code.Add((byte)disp);
                    }
                    else if (!data.hasDisplacement
                        && data.kind == DTKind.NORMPAIR)
                    {
                        if (data.rep != (int)NormPair.HL) { return EvalErr(); }
                        code.Add(0xCB);
                    }
                    else { return EvalErr(); }

                    code.Add((byte)(basecode | imm << 3 | 6));
                }
                else { return EvalErr(); }
            }
            else if (argc == 3)
            {
                DTree arg1 = argv; 
                DTree arg2 = arg1.next;
                DTree arg3 = arg2.next;

                if (arg1.kind != DTKind.IMM
                    || arg2.kind != DTKind.ADR
                    || arg3.kind != DTKind.REG)
                { return EvalErr(); }

                int imm = ((ImmTree)arg1).immediate;
                AdrData data = new AdrData((AdrTree)arg2);
                int reg = (int)((RegTree)arg3).reg;

                if (imm < 0 || imm > 7) { return EvalErr(); }

                if (data.kind != DTKind.IPAIR) 
                { return EvalErr(); }

                if (data.rep == (int)IPair.IX) { code.Add(0xDD); }
                else                           { code.Add(0xFD); }

                int disp = data.hasDisplacement ?
                           0 : data.displacement;

                SImm8WarnOverflow(disp);

                code.Add(0xCB);
                code.Add((byte)disp);
                code.Add((byte)(basecode | imm << 3 | reg));
            }
            else { return EvalErr(); }

            return true;
        }

        /*
         * INC and DEC follow the same format of encoding
         * but with different offsets.
         */
        private bool EvalINCDEC(DTree argv, int argc, int pairbase, int regbase)
        {
            if (argc != 1) { return EvalErr(); }

            DTree arg = argv;

            switch (arg.kind)
            {
                case DTKind.REG:
                    int reg = (int)((RegTree)arg).reg;
                    code.Add((byte)(regbase | reg << 3));
                    break;
                case DTKind.NORMPAIR:
                    int npair = (int)((NormPairTree)arg).normpair;
                    code.Add((byte)(pairbase | npair << 4));
                    break;
                case DTKind.IPAIR:
                    IPair ipair = ((IPairTree)arg).ipair;

                    if (ipair == IPair.IX) { code.Add(0xDD); }
                    else                   { code.Add(0xFD); }

                    code.Add((byte)(pairbase | 0x20));
                    break;
                case DTKind.IREG:
                    IReg ireg = ((IRegTree)arg).reg;

                    if (ireg == IReg.IXH || ireg == IReg.IXL)
                    { code.Add(0xDD); }
                    else
                    { code.Add(0xFD); }

                    if (ireg == IReg.IXH || ireg == IReg.IYH)
                    { code.Add((byte)(regbase | 0x4 << 3)); }
                    else
                    { code.Add((byte)(regbase | 0x5 << 3)); }
                    break;
                case DTKind.ADR:
                    AdrData data = new AdrData((AdrTree)arg);

                    if (data.kind == DTKind.IPAIR)
                    {
                        if (data.rep == (int)IPair.IX) { code.Add(0xDD); }
                        else                           { code.Add(0xFD); }

                        int disp = data.hasDisplacement ?
                                   0 : data.displacement;

                        SImm8WarnOverflow(disp);
                        code.Add((byte)(regbase | 0x6 << 3));
                        code.Add((byte)disp);
                    }
                    else if (!data.hasDisplacement && data.kind == DTKind.NORMPAIR)
                    {
                        if (data.rep != (int)NormPair.HL) { return EvalErr(); }

                        code.Add((byte)(regbase | 0x6 << 3));
                    }
                    break;
                default:
                    return EvalErr();
            }

            return true;
        }

        /* ... Putting the C in CISC */

        private bool EvalLD(DTree argv, int argc)
        {
            if (argc != 2)
            {
                return EvalErr();
            }

            DTree arg1 = argv, arg2 = argv.next;
            AdrData data;
            IReg ireg;
            IPair ipair;
            SpecReg sreg;
            int pair;
            int reg;
            int imm;

            switch (arg1.kind)
            {
                case DTKind.ADR:
                    data = new AdrData((AdrTree)arg1);
                    if (data.kind == DTKind.IPAIR)
                    {
                        if (data.rep == (int)IPair.IX)
                        { code.Add(0xDD); }
                        else if (data.rep == (int)IPair.IY)
                        { code.Add(0xFD); }

                        int disp = data.hasDisplacement ?
                                   0 : data.displacement;

                        SImm8WarnOverflow(data.displacement);

                        if (arg2.kind == DTKind.REG)
                        {
                            reg = (int)((RegTree)arg2).reg;
                            code.Add((byte)(0x70 | reg));
                            code.Add((byte)data.displacement);
                        }
                        else if (arg2.kind == DTKind.IMM)
                        {
                            imm = ((ImmTree)arg2).immediate;

                            Imm8WarnOverflow(imm);

                            code.Add(0x36);
                            code.Add((byte)data.displacement);
                            code.Add((byte)imm);
                        }
                        else
                        {
                            return EvalErr();
                        }
                    }
                    else
                    {
                        if (data.kind == DTKind.NORMPAIR)
                        {
                            pair = data.rep;

                            if (arg2.kind == DTKind.REG)
                            {
                                reg = (int)((RegTree)arg2).reg;

                                if ((pair == (int)NormPair.BC
                                    || pair == (int)NormPair.DE)
                                    && reg == (int)Register.A)
                                {
                                    code.Add((byte)(0x2 | pair << 4));
                                }
                                else if (pair == (int)NormPair.HL)
                                {
                                    code.Add((byte)(0x70 | reg));
                                }
                                else
                                {
                                    return EvalErr();
                                }
                            }
                            else if (arg2.kind == DTKind.IMM
                                && data.rep == (int)NormPair.HL)
                            {
                                int immed = ((ImmTree)arg2).immediate;

                                Imm8WarnOverflow(immed);

                                code.Add(0x36);
                                code.Add((byte)immed);
                            }
                            else
                            {
                                return EvalErr();
                            }
                        }
                        else if (data.kind == DTKind.IMM)
                        {
                            imm = data.rep;

                            Imm16WarnOverflow(imm);

                            if (arg2.kind == DTKind.REG
                                && ((RegTree)arg2).reg == Register.A)
                            {
                                code.Add(0x32);
                            }
                            else if (arg2.kind == DTKind.NORMPAIR)
                            {
                                pair = (int)((NormPairTree)arg2).normpair;
                                if (pair == (int)NormPair.HL)
                                {
                                    code.Add(0x22);
                                }
                                else
                                {
                                    code.Add(0xED);
                                    code.Add((byte)(0x43 | pair << 4));
                                }
                            }
                            else if (arg2.kind == DTKind.IPAIR)
                            {
                                pair = (int)((IPairTree)arg2).ipair;

                                if (pair == (int)IPair.IX)
                                {
                                    code.Add(0xDD);
                                }
                                else if (pair == (int)IPair.IY)
                                {
                                    code.Add(0xFD);
                                }
                                code.Add(0x22);
                            }

                            code.Add((byte)imm);
                            code.Add((byte)(imm >> 8));
                        }
                        else
                        {
                            return EvalErr();
                        }
                    }

                    break;

                case DTKind.REG:
                    int reg1 = (int)((RegTree)arg1).reg;
                    if (arg2.kind == DTKind.REG)
                    {
                        int reg2 = (int)((RegTree)arg2).reg;

                        code.Add((byte)(0x40 | reg1 << 3 | reg2));
                    }
                    else if (arg2.kind == DTKind.IMM)
                    {
                        imm = (int)((ImmTree)arg2).immediate;
                        if (imm > 255 || imm < -128)
                        {
                            return EvalErr();
                        }
                        code.Add((byte)(0x6 | reg1 << 3));
                        code.Add((byte)imm);
                    }
                    else if (arg2.kind == DTKind.ADR)
                    {
                        data = new AdrData((AdrTree)arg2);

                        if (data.kind == DTKind.NORMPAIR)
                        {
                            if (data.hasDisplacement)
                            {
                                return EvalErr();
                            }

                            NormPair npair = (NormPair)data.rep;
                            if (npair == NormPair.HL)
                            {
                                code.Add((byte)(0x46 | reg1 << 3));
                            }
                            else if ((npair == NormPair.BC
                                || npair == NormPair.DE)
                                && reg1 == (int)Register.A)
                            {
                                code.Add((byte)(0x0A | (int)npair << 4));
                            }
                        }
                        else if (data.kind == DTKind.IMM)
                        {
                            int immediate = data.rep;
                            if (reg1 != (int)Register.A)
                            {
                                return EvalErr();
                            }
                            Imm16WarnOverflow(immediate);
                            code.Add(0x3A);
                            code.Add((byte)immediate);
                            code.Add((byte)((uint)immediate >> 8));
                        }
                        else if (data.kind == DTKind.IPAIR)
                        {
                            IPair spair = (IPair)data.rep;

                            int disp = data.hasDisplacement ?
                                       0 : data.displacement;

                            SImm8WarnOverflow(disp);

                            if (spair == IPair.IX)
                            {
                                code.Add(0xDD);
                            }
                            else if (spair == IPair.IY)
                            {
                                code.Add(0xFD);
                            }
                            code.Add((byte)(0x46 | reg1 << 3));
                            code.Add((byte)disp);
                        }
                    }
                    else if (arg2.kind == DTKind.IREG)
                    {
                        ireg = ((IRegTree)arg2).reg;

                        if (reg1 > (int)Register.E
                            && reg1 != (int)Register.A)
                        {
                            return EvalErr();
                        }

                        switch (ireg)
                        {
                            case IReg.IXL:

                                code.Add(0xDD);
                                code.Add((byte)(0x45 | reg1 << 3));
                                break;
                            case IReg.IXH:

                                code.Add(0xDD);
                                code.Add((byte)(0x44 | reg1 << 3));
                                break;
                            case IReg.IYL:
                                code.Add(0xFD);
                                code.Add((byte)(0x45 | reg1 << 3));
                                break;
                            case IReg.IYH:
                                code.Add(0xFD);
                                code.Add((byte)(0x44 | reg1 << 3));
                                break;
                        }
                    }
                    else if (arg2.kind == DTKind.SPECREG)
                    {
                        sreg = ((SpecRegTree)arg2).reg;

                        if (reg1 != (int)Register.A)
                        {
                            return EvalErr();
                        }

                        code.Add(0xED);
                        if (sreg == SpecReg.I)
                        { code.Add(0x57); }
                        else
                        { code.Add(0x5F); }
                    }
                    else
                    {
                        return EvalErr();
                    }
                    break;
                case DTKind.NORMPAIR:
                    pair = (int)((NormPairTree)arg1).normpair;
                    if (arg2.kind == DTKind.ADR)
                    {
                        data = new AdrData((AdrTree)arg2);

                        if (data.kind != DTKind.IMM)
                        {
                            return EvalErr();
                        }

                        if (pair == (int)NormPair.HL)
                        {
                            code.Add(0x2A);
                        }
                        else
                        {
                            code.Add(0xED);
                            code.Add((byte)(0x4B | pair << 4));
                        }
                    }
                    else if (arg2.kind == DTKind.IMM)
                    {
                        imm = ((ImmTree)arg2).immediate;
                        Imm16WarnOverflow(imm);
                        code.Add((byte)(0x1 | pair << 4));
                        code.Add((byte)imm);
                        code.Add((byte)((uint)imm >> 8));
                    }
                    else if (arg2.kind == DTKind.IPAIR)
                    {
                        ipair = ((IPairTree)arg2).ipair;
                        if (pair != (int)NormPair.SP)
                        {
                            return EvalErr();
                        }

                        if (ipair == IPair.IX)
                        {
                            code.Add(0xDD);
                            code.Add(0xF9);
                        }
                        else if (ipair == IPair.IY)
                        {
                            code.Add(0xFD);
                            code.Add(0xF9);
                        }
                        else
                        {
                            return EvalErr();
                        }
                    }
                    else if (arg2.kind == DTKind.NORMPAIR)
                    {
                        if (pair != (int)NormPair.SP
                            || ((NormPairTree)arg2).normpair
                               != NormPair.HL)
                        { return EvalErr(); }

                        code.Add(0xF9);
                    }
                    else
                    {
                        return EvalErr();
                    }
                    break;
                case DTKind.IPAIR:
                    ipair = ((IPairTree)arg1).ipair;

                    if (ipair == IPair.IX)
                    { code.Add(0xDD); }
                    else
                    { code.Add(0xFD); }

                    if (arg2.kind == DTKind.IMM)
                    {
                        imm = ((ImmTree)arg2).immediate;

                        Imm16WarnOverflow(imm);

                        code.Add(0x21);

                        code.Add((byte)imm);
                        code.Add((byte)(imm >> 8));
                    }
                    else if (arg2.kind == DTKind.ADR)
                    {
                        data = new AdrData((AdrTree)arg2);

                        if (data.kind != DTKind.IMM)
                        {
                            return EvalErr();
                        }

                        imm = data.rep;

                        Imm16WarnOverflow(imm);

                        code.Add(0x2A);

                        code.Add((byte)imm);
                        code.Add((byte)((uint)imm >> 8));
                    }
                    else
                    {
                        return EvalErr();
                    }
                    break;
                case DTKind.IREG:
                    ireg = ((IRegTree)arg1).reg;

                    if (ireg == IReg.IXL || ireg == IReg.IXH)
                    { code.Add(0xDD); }
                    else
                    { code.Add(0xFD); }

                    if (arg2.kind == DTKind.IMM)
                    {
                        imm = ((ImmTree)arg2).immediate;
                        switch (ireg)
                        {
                            case IReg.IXL:
                            case IReg.IYL:
                                code.Add(0x2E);
                                break;
                            case IReg.IXH:
                            case IReg.IYH:
                                code.Add(0x26);
                                break;
                        }
                        Imm8WarnOverflow(imm);

                        code.Add((byte)imm);
                    }
                    else if (arg2.kind == DTKind.REG)
                    {
                        reg = (int)((RegTree)arg2).reg;

                        if (reg == (int)Register.H
                            || reg == (int)Register.L)
                        {
                            return EvalErr();
                        }
                        else
                        {
                            switch (ireg)
                            {
                                case IReg.IXH:
                                case IReg.IYH:
                                    code.Add((byte)(0x60 | reg));
                                    break;

                                case IReg.IXL:
                                case IReg.IYL:
                                    code.Add((byte)(0x68 | reg));
                                    break;
                            }
                        }
                    }
                    else if (arg2.kind == DTKind.IREG)
                    {
                        IReg ireg2 = ((IRegTree)arg2).reg;

                        bool iregIsIX = ireg == IReg.IXH
                            || ireg == IReg.IXL;
                        bool ireg2IsIX = ireg2 == IReg.IXH
                            || ireg2 == IReg.IXL;

                        if (iregIsIX && !ireg2IsIX
                            || !iregIsIX && ireg2IsIX)
                        {
                            return EvalErr();
                        }

                        switch (ireg2)
                        {
                            case IReg.IXH:
                            case IReg.IYH:
                                code.Add(0x64);
                                break;
                            case IReg.IXL:
                            case IReg.IYL:
                                code.Add(0x65);
                                break;
                        }
                    }
                    else
                    {
                        return EvalErr();
                    }
                    break;
                case DTKind.SPECREG:
                    sreg = ((SpecRegTree)arg1).reg;
                    if (arg2.kind != DTKind.REG
                        || ((RegTree)arg2).reg != Register.A)
                    {
                        return EvalErr();
                    }
                    code.Add(0xED);
                    if (sreg == SpecReg.I)
                    { code.Add(0x47); }
                    else
                    { code.Add(0x4F); }
                    break;
                default:
                    return EvalErr();
            }

            return true;
        }

        public bool Evaluate(InstructionTree inst)
        {
            int argc = 0;
            NmemonicTree nmemonic = (NmemonicTree)inst.children;

            code = new List<byte>();

            if (nmemonic == null) return true;

            DTree arguments = nmemonic.next;
            DTree currchild = nmemonic.next;

            while (currchild != null)
            {
                argc++;
                currchild = currchild.next;
            }

            switch (nmemonic.nmemonic)
            {
                case Nmemonic.NOP:
                    return EvalNoArgs(argc, 0x0);

                case Nmemonic.HALT:
                    return EvalNoArgs(argc, 0x76);

                case Nmemonic.RLCA:
                    return EvalNoArgs(argc, 0x07);

                case Nmemonic.RRCA:
                    return EvalNoArgs(argc, 0x0F);

                case Nmemonic.RLA:
                    return EvalNoArgs(argc, 0x17);

                case Nmemonic.RRA:
                    return EvalNoArgs(argc, 0x1F);

                case Nmemonic.DI:
                    return EvalNoArgs(argc, 0xF3);

                case Nmemonic.EI:
                    return EvalNoArgs(argc, 0xFB);

                case Nmemonic.PUSH:
                    return EvalPUSHPOP(arguments, argc, 0xC5);

                case Nmemonic.POP:
                    return EvalPUSHPOP(arguments, argc, 0xC1);

                case Nmemonic.LD:
                    return EvalLD(arguments, argc);

                case Nmemonic.CP:
                    return EvalArith(arguments, argc, 0xB8);

                case Nmemonic.OR:
                    return EvalArith(arguments, argc, 0xB0);

                case Nmemonic.XOR:
                    return EvalArith(arguments, argc, 0xA8);

                case Nmemonic.SUB:
                    return EvalArith(arguments, argc, 0x90);

                case Nmemonic.ADD:
                    return EvalADD(arguments, argc);

                case Nmemonic.AND:
                    return EvalArith(arguments, argc, 0xA0);

                case Nmemonic.ADC:
                    return EvalArithC(arguments, argc, 0x4A, 0x88);

                case Nmemonic.SBC:
                    return EvalArithC(arguments, argc, 0x42, 0x98);

                case Nmemonic.RLC:
                    return EvalRotShift(arguments, argc, 0);

                case Nmemonic.RRC:
                    return EvalRotShift(arguments, argc, 0x8);

                case Nmemonic.RL:
                    return EvalRotShift(arguments, argc, 0x10);

                case Nmemonic.RR:
                    return EvalRotShift(arguments, argc, 0x18);

                case Nmemonic.SLA:
                    return EvalRotShift(arguments, argc, 0x20);

                case Nmemonic.SRA:
                    return EvalRotShift(arguments, argc, 0x28);

                case Nmemonic.SLL:
                    return EvalRotShift(arguments, argc, 0x30);

                case Nmemonic.SRL:
                    return EvalRotShift(arguments, argc, 0x40);

                case Nmemonic.BIT:
                    return EvalBit(arguments, argc, 0x40);

                case Nmemonic.RES:
                    return EvalBit(arguments, argc, 0x80);

                case Nmemonic.SET:
                    return EvalBit(arguments, argc, 0xC0);

                case Nmemonic.DAA:
                    return EvalNoArgs(argc, 0x27);

                case Nmemonic.CCF:
                    return EvalNoArgs(argc, 0x3F);

                case Nmemonic.CPL:
                    return EvalNoArgs(argc, 0x2F);

                case Nmemonic.SCF:
                    return EvalNoArgs(argc, 0x37);

                case Nmemonic.EXX:
                    return EvalNoArgs(argc, 0xD9);

                case Nmemonic.JR:
                    return EvalJR(arguments, argc);

                case Nmemonic.DJNZ:
                    return EvalDJNZ(arguments, argc);

                case Nmemonic.JP:
                    return EvalJP(arguments, argc);

                case Nmemonic.LDI:
                    return EvalNoArgsED(argc, 0xA0);

                case Nmemonic.CPI:
                    return EvalNoArgsED(argc, 0xA1);

                case Nmemonic.INI:
                    return EvalNoArgsED(argc, 0xA2);

                case Nmemonic.OUTI:
                    return EvalNoArgsED(argc, 0xA3);

                case Nmemonic.LDIR:
                    return EvalNoArgsED(argc, 0xB0);

                case Nmemonic.CPIR:
                    return EvalNoArgsED(argc, 0xB1);

                case Nmemonic.INIR:
                    return EvalNoArgsED(argc, 0xB2);

                case Nmemonic.OTIR:
                    return EvalNoArgsED(argc, 0xB3);

                case Nmemonic.LDD:
                    return EvalNoArgsED(argc, 0xA8);

                case Nmemonic.CPD:
                    return EvalNoArgsED(argc, 0xA9);

                case Nmemonic.IND:
                    return EvalNoArgsED(argc, 0xAA);

                case Nmemonic.OUTD:
                    return EvalNoArgsED(argc, 0xAB);

                case Nmemonic.LDDR:
                    return EvalNoArgsED(argc, 0xB8);

                case Nmemonic.CPDR:
                    return EvalNoArgsED(argc, 0xB9);

                case Nmemonic.INDR:
                    return EvalNoArgsED(argc, 0xBA);

                case Nmemonic.OTDR:
                    return EvalNoArgsED(argc, 0xBB);

                case Nmemonic.RET:
                    return EvalRET(arguments, argc);

                case Nmemonic.RETI:
                    return EvalNoArgsED(argc, 0x4D);

                case Nmemonic.CALL:
                    return EvalCALL(arguments, argc);

                case Nmemonic.INC:
                    return EvalINCDEC(arguments, argc, 0x3, 0x4);

                case Nmemonic.DEC:
                    return EvalINCDEC(arguments, argc, 0xB, 0x5);

                case Nmemonic.RST:
                    return EvalRST(arguments, argc);

                case Nmemonic.EX:
                    return EvalEX(arguments, argc);

                case Nmemonic.OUT:
                    return EvalOUT(arguments, argc);

                case Nmemonic.IN:
                    return EvalIN(arguments, argc);

                case Nmemonic.IM:
                    return EvalIM(arguments, argc);

                case Nmemonic.RETN:
                    return EvalNoArgsED(argc, 0x45);

                case Nmemonic.NEG:
                    return EvalNoArgsED(argc, 0x44);

                case Nmemonic.RRD:
                    return EvalNoArgsED(argc, 0x67);

                case Nmemonic.RLD:
                    return EvalNoArgsED(argc, 0x6F);
            }

            return false;
        }
    }
}
