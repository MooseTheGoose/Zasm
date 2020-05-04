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
                case DTKind.SPECPAIR:
                    rep = (int)((SpecPairTree)current).specpair;
                    break;
                case DTKind.IMM:
                    rep = ((ImmTree)current).immediate;
                    break;
            }

            current = current.next;
            if (current != null)
            {
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
        private void EvalNoArgs(int argc, int opcode)
        {
            if (argc != 0)
            { EvalErr(); }
            code.Add((byte)opcode);
        }

        private void EvalNoArgsED(int argc, int opcode)
        {
            if (argc != 0)
            { EvalErr(); }
            code.Add(0xED);
            code.Add((byte)opcode);
        }

        private void EvalRET(DTree argv, int argc)
        {
            if (argc == 0)
            {
                code.Add(0xC9);
            }
            else if (argc == 1)
            {
                DTree arg = argv;

                if (arg.kind != DTKind.CONDITION) { EvalErr(); }

                int cond = (int)((ConditionTree)arg).cond;

                code.Add((byte)(0xC0 | cond << 3));
            }
            else { EvalErr(); }
        }

        private void EvalJR(DTree argv, int argc)
        {
            if (argc == 1)
            {
                DTree dtree = argv;
                if (dtree.kind != DTKind.IMM)
                {
                    EvalErr();

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
                    EvalErr();
                }

                int cond = (int)((ConditionTree)condtree).cond;
                int imm = ((ImmTree)immtree).immediate;

                if (cond > (int)Condition.C)
                {
                    EvalErr();
                }

                SImm8WarnOverflow(imm);

                code.Add((byte)(0x20 | cond << 3));
                code.Add((byte)imm);
            }
            else
            {
                EvalErr();
            }
        }

        private void EvalJP(DTree argv, int argc)
        {
            if (argc == 1)
            {
                DTree dtree = argv;
                if (dtree.kind == DTKind.IMM)
                {
                    int imm = ((ImmTree)dtree).immediate;

                    Imm16WarnOverflow(imm);
                    code.Add(0x18);
                    code.Add((byte)imm);
                    code.Add((byte)(imm >> 8));
                }
                else if (dtree.kind == DTKind.ADR)
                {
                    AdrData data = new AdrData((AdrTree)dtree);

                    if (data.hasDisplacement)
                    {
                        EvalErr();
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
                        EvalErr();
                    }

                    code.Add(0xE9);
                }
                else { EvalErr(); }
            }
            else if (argc == 2)
            {
                DTree condtree = argv, immtree = argv.next;

                if (condtree.kind != DTKind.CONDITION
                    || immtree.kind != DTKind.IMM)
                {
                    EvalErr();
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
                EvalErr();
            }
        }

        private void EvalPUSH(DTree argv, int argc)
        {
            if (argc != 1)
            {
                EvalErr();
            }

            DTree arg = argv;
            switch (arg.kind)
            {
                case DTKind.NORMPAIR:
                    NormPair npair = ((NormPairTree)arg).normpair;
                    if (npair == NormPair.SP)
                    {
                        EvalErr();
                    }
                    else { code.Add( (byte)(0xC5 | ((int)npair) << 4) ); }
                    break;
                case DTKind.SPECPAIR:
                    SpecPair spair = ((SpecPairTree)arg).specpair;
                    if (spair != SpecPair.AF)
                    {
                        EvalErr();
                    }
                    else { code.Add(0xF5); }
                    break;
                case DTKind.IPAIR:
                    IPair ipair = ((IPairTree)arg).ipair;
                    if (ipair == IPair.IX)
                    { code.Add(0xDD); }
                    else
                    { code.Add(0xFD); }

                    code.Add(0xE5);
                    break;
                default:
                    EvalErr();
                    break;
            }
        }

        private void EvalPOP(DTree argv, int argc)
        {
            if (argc != 1)
            {
                EvalErr();
            }

            DTree arg = argv;
            switch (arg.kind)
            {
                case DTKind.NORMPAIR:
                    NormPair npair = ((NormPairTree)arg).normpair;
                    if (npair == NormPair.SP)
                    {
                        EvalErr();
                    }
                    else { code.Add((byte)(0xC1 | ((int)npair) << 4)); }
                    break;
                case DTKind.SPECPAIR:
                    SpecPair spair = ((SpecPairTree)arg).specpair;
                    if (spair != SpecPair.AF)
                    {
                        EvalErr();
                    }
                    else { code.Add(0xF1); }
                    break;
                case DTKind.IPAIR:
                    IPair ipair = ((IPairTree)arg).ipair;
                    if (ipair == IPair.IX)
                    { code.Add(0xDD); }
                    else
                    { code.Add(0xFD); }

                    code.Add(0xE7);
                    break;
                default:
                    EvalErr();
                    break;
            }
        }

        private void EvalADD(DTree argv, int argc)
        {
            if (argc != 2)
            { EvalErr(); }

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
                    { EvalErr(); }

                    pair2 = (int)((NormPairTree)arg2).normpair;

                    code.Add((byte)(0x09 | pair2 << 4));

                    break;
                case DTKind.REG:
                    reg = (int)((RegTree)arg1).reg;

                    if (reg != (int)Register.A)
                    { EvalErr(); }

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
                                { EvalErr(); }

                                code.Add(0x86);
                            }
                            else { EvalErr(); }

                            break;
                        case DTKind.IMM:
                            int imm = ((ImmTree)arg2).immediate;
                            Imm8WarnOverflow(imm);
                            code.Add(0xC6);
                            code.Add((byte)imm);
                            break;
                        default:
                            EvalErr();
                            break;
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

                        if (pair2 == (int)NormPair.HL) { EvalErr(); }

                        code.Add((byte)(0x9 | pair2 << 4));
                    }
                    else if (arg2.kind == DTKind.IPAIR)
                    {
                        bool sameIPair = 
                            ipair == ((IPairTree)arg2).ipair;

                        if (!sameIPair) { EvalErr(); }

                        code.Add(0x29);
                    }

                    break;
                default:
                    EvalErr();
                    break;
            }
        }

        /*
         *  ADC, and SBC follow the same format
         *  of encoding. They only have different
         *  offsets.
         */
        private void EvalArithC(DTree argv, int argc, int npaircode, int regcode)
        {
            if (argc != 2) { EvalErr(); }

            DTree arg1 = argv, arg2 = argv.next;

            switch (arg1.kind)
            {
                case DTKind.NORMPAIR:
                    if (((NormPairTree)arg1).normpair != NormPair.HL
                        || arg2.kind != DTKind.NORMPAIR) 
                    { EvalErr(); }

                    int pair = (int)((NormPairTree)arg2).normpair;
                    code.Add(0xED);
                    code.Add((byte)(npaircode | pair << 4));

                    break;
                case DTKind.REG:
                    if(((RegTree)arg1).reg != Register.A) { EvalErr(); }

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
                            { EvalErr(); }

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
                            EvalErr();
                            break;
                    }
                    break;
                default:
                    EvalErr();
                    break;
            }
        }

        /*
         * SUB, XOR, OR, AND, and CP follow the same
         * format of encoding. They only have different
         * offsets.
         */
        private void EvalArith(DTree argv, int argc, int basecode)
        {
            if (argc != 1) { EvalErr(); }

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
                    { EvalErr(); }

                    code.Add((byte)(basecode | 6));
                    break;
                case DTKind.IMM:
                    int imm = ((ImmTree)arg).immediate;
                    SImm8WarnOverflow(imm);
                    code.Add((byte)(basecode | 0x46));
                    code.Add((byte)imm);
                    break;
                default:
                    EvalErr();
                    break;
            }
        }

        /*
         * RLC, RRC, RL, RR, SLA, SRA, SLL, and SRL all follow
         * the same format of encoding. They only have
         * different offsets.
         */
        private void EvalRotShift(DTree argv, int argc, int basecode)
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
                        { code.Add(0xFD); }

                        int disp = data.hasDisplacement ?
                                   0 : data.displacement;

                        SImm8WarnOverflow(data.displacement);
                        code.Add(0xCB);
                        code.Add((byte)data.displacement);
                        code.Add((byte)(basecode | 6));
                    }
                    else if (!data.hasDisplacement && data.kind == DTKind.NORMPAIR)
                    {
                        if (data.rep != (int)NormPair.HL) { EvalErr(); }
                        code.Add(0xCB);
                        code.Add((byte)(basecode | 6));
                    }
                    else { EvalErr(); }
                }
                else
                { EvalErr(); }
            }
            else if (argc == 2)
            {
                DTree arg1 = argv, arg2 = argv.next;

                if (arg1.kind != DTKind.ADR || arg2.kind != DTKind.REG)
                { EvalErr(); }

                AdrData data = new AdrData((AdrTree)arg1);
                int reg = (int)((RegTree)arg2).reg;

                if (data.kind != DTKind.IPAIR)
                { EvalErr(); }

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
            else { EvalErr(); }
        }

        /*
         *  BIT, SET, and RES all follow the same format of encoding.
         *  They only have different offsets
         */
        private void EvalBit(DTree argv, int argc, int basecode)
        {
            if (argc == 2)
            {
                DTree arg1 = argv, arg2 = argv.next;

                if (arg1.kind != DTKind.IMM) { EvalErr(); }

                int imm = ((ImmTree)arg1).immediate;
                if (imm < 0 || imm > 7) { EvalErr(); }

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
                        if (data.rep != (int)NormPair.HL) { EvalErr(); }
                        code.Add(0xCB);
                    }
                    else { EvalErr(); }

                    code.Add((byte)(basecode | imm << 3 | 6));
                }
                else { EvalErr(); }
            }
            else if (argc == 3)
            {
                DTree arg1 = argv; 
                DTree arg2 = arg1.next;
                DTree arg3 = arg2.next;

                if (arg1.kind != DTKind.IMM
                    || arg2.kind != DTKind.ADR
                    || arg3.kind != DTKind.REG)
                { EvalErr(); }

                int imm = ((ImmTree)arg1).immediate;
                AdrData data = new AdrData((AdrTree)arg2);
                int reg = (int)((RegTree)arg2).reg;

                if (imm < 0 || imm > 7) { EvalErr(); }

                if (data.kind != DTKind.IPAIR) 
                { EvalErr(); }

                if (data.rep == (int)IPair.IX) { code.Add(0xDD); }
                else                           { code.Add(0xFD); }

                int disp = data.hasDisplacement ?
                           0 : data.displacement;

                SImm8WarnOverflow(disp);

                code.Add(0xCB);
                code.Add((byte)disp);
                code.Add((byte)(basecode | imm << 3 | reg));
            }
            else { EvalErr(); }
        }

        /* ... Putting the C in CISC */

        private void EvalLD(DTree argv, int argc)
        {
            if (argc != 2)
            {
                EvalErr();
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
                            EvalErr();
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
                                    EvalErr();
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
                                EvalErr();
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
                                    code.Add((byte)(0x34 | pair << 4));
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
                            EvalErr();
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
                            EvalErr();
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
                                EvalErr();
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
                                EvalErr();
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

                        if (reg1 > (int)Register.E)
                        {
                            EvalErr();
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
                            EvalErr();
                        }

                        code.Add(0xED);
                        if (sreg == SpecReg.I)
                        { code.Add(0x57); }
                        else
                        { code.Add(0x5F); }
                    }
                    else
                    {
                        EvalErr();
                    }
                    break;
                case DTKind.NORMPAIR:
                    pair = (int)((NormPairTree)arg1).normpair;
                    if (arg2.kind == DTKind.ADR)
                    {
                        data = new AdrData((AdrTree)arg2);

                        if (data.kind != DTKind.IMM)
                        {
                            EvalErr();
                        }

                        if (pair == (int)NormPair.HL)
                        {
                            code.Add(0x2A);
                        }
                        else
                        {
                            code.Add(0xED);
                            code.Add((byte)(0x4A | pair << 4));
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
                            EvalErr();
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
                            EvalErr();
                        }
                    }
                    else
                    {
                        EvalErr();
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
                            EvalErr();
                        }

                        imm = data.rep;

                        Imm16WarnOverflow(imm);

                        code.Add(0x2A);

                        code.Add((byte)imm);
                        code.Add((byte)((uint)imm >> 8));
                    }
                    else
                    {
                        EvalErr();
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
                            EvalErr();
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
                            EvalErr();
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
                        EvalErr();
                    }
                    break;
                case DTKind.SPECREG:
                    sreg = ((SpecRegTree)arg1).reg;
                    if (arg2.kind != DTKind.REG
                        || ((RegTree)arg2).reg != Register.A)
                    {
                        EvalErr();
                    }
                    code.Add(0xED);
                    if (sreg == SpecReg.I)
                    { code.Add(0x47); }
                    else
                    { code.Add(0x4F); }
                    break;
                default:
                    EvalErr();
                    break;
            }
        }

        public void Evaluate(InstructionTree inst)
        {
            int argc = 0;
            NmemonicTree nmemonic = (NmemonicTree)inst.children;
            DTree arguments = nmemonic.next;
            DTree currchild = nmemonic.next;

            code = new List<byte>();

            while (currchild != null)
            {
                argc++;
                currchild = currchild.next;
            }

            switch (nmemonic.nmemonic)
            {
                case Nmemonic.RRA:
                    EvalNoArgs(argc, 0x1F);
                    break;
                case Nmemonic.PUSH:
                    EvalPUSH(arguments, argc);
                    break;
                case Nmemonic.POP:
                    EvalPOP(arguments, argc);
                    break;
                case Nmemonic.LD:
                    EvalLD(arguments, argc);
                    break;
                case Nmemonic.CP:
                    EvalArith(arguments, argc, 0xB8);
                    break;
                case Nmemonic.OR:
                    EvalArith(arguments, argc, 0xB0);
                    break;
                case Nmemonic.XOR:
                    EvalArith(arguments, argc, 0xA8);
                    break;
                case Nmemonic.SUB:
                    EvalArith(arguments, argc, 0x90);
                    break;
                case Nmemonic.ADD:
                    EvalADD(arguments, argc);
                    break;
                case Nmemonic.AND:
                    EvalArith(arguments, argc, 0xA0);
                    break;
                case Nmemonic.ADC:
                    EvalArithC(arguments, argc, 0x4A, 0x88);
                    break;
                case Nmemonic.SBC:
                    EvalArithC(arguments, argc, 0x42, 0x98);
                    break;
                case Nmemonic.RLC:
                    EvalRotShift(arguments, argc, 0);
                    break;
                case Nmemonic.RRC:
                    EvalRotShift(arguments, argc, 0x8);
                    break;
                case Nmemonic.RL:
                    EvalRotShift(arguments, argc, 0x10);
                    break;
                case Nmemonic.RR:
                    EvalRotShift(arguments, argc, 0x18);
                    break;
                case Nmemonic.SLA:
                    EvalRotShift(arguments, argc, 0x20);
                    break;
                case Nmemonic.SRA:
                    EvalRotShift(arguments, argc, 0x28);
                    break;
                case Nmemonic.SLL:
                    EvalRotShift(arguments, argc, 0x30);
                    break;
                case Nmemonic.SRL:
                    EvalRotShift(arguments, argc, 0x40);
                    break;
                case Nmemonic.DAA:
                    EvalNoArgs(argc, 0x27);
                    break;
                case Nmemonic.CCF:
                    EvalNoArgs(argc, 0x3F);
                    break;
                case Nmemonic.EXX:
                    EvalNoArgs(argc, 0xD9);
                    break;
                case Nmemonic.JR:
                    EvalJR(arguments, argc);
                    break;
                case Nmemonic.JP:
                    EvalJP(arguments, argc);
                    break;
                case Nmemonic.LDI:
                    EvalNoArgsED(argc, 0xA0);
                    break;
                case Nmemonic.RET:
                    EvalRET(arguments, argc);
                    break;
                case Nmemonic.RETI:
                    EvalNoArgsED(argc, 0x4D);
                    break;
            }
        }
    }
}
