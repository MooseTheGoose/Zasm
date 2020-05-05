using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Zasm.AsmReporter;

namespace Zasm
{

    public enum TokenKind
    {
        OPERATOR, IDENTIFIER, NUMBER,
    }

    public abstract class Token
    {
        public TokenKind id;
    }

    public enum OperatorKind
    {
        LPAREN = 0, RPAREN = 1, PLUS = 2, COMMA = 3,
        COLON = 4, MINUS = 5
    }

    public class OperatorToken : Token 
    {
        public static readonly string[] OperatorTable =
        {
            "(", ")", "+", ",", ":", "-"
        };

        public OperatorKind op;

        public static string OperatorPrefix(string s)
        {
            foreach (string x in OperatorTable)
            {
                if (s.StartsWith(x)) { return x; }
            }
            return null;
        }

        public OperatorToken(string op) 
        { 
            this.id = TokenKind.OPERATOR;

            int index = Array.IndexOf(OperatorTable, op);
            System.Diagnostics.Debug.Assert(index >= 0);
            this.op = (OperatorKind)index;
        }
    }

    public class IdentifierToken : Token
    {
        public string identifier;

        public IdentifierToken(string identifier)
        {
            this.id = TokenKind.IDENTIFIER;
            this.identifier = identifier;
        }
    }

    public class NumberToken : Token
    {
        public int number;

        public bool FormNumberToken(string number)
        {
            this.id = TokenKind.NUMBER;
            int imm = 0;
            int nbase = 10;
            char currC = number[0];
            int i = 0;
            int len = number.Length;

            if (currC == '0')
            {
                currC = number[1];
                if (currC == 'x') { nbase = 16; }
                else if(currC == 'o') { nbase = 8; }
                i = 2;
            }

            while (i < len)
            {
                currC = char.ToUpper(number[i]);
                int digit = char.IsDigit(currC) ? currC - '0' : currC - 'A';

                if (digit < 0 || digit >= nbase) { return TokenErr(); }

                imm *= nbase;
                imm += digit;

                i++;
            }

            this.number = imm;

            return true;
        }
    }

    public class Tokenizer
    {
        public List<Token> tokens;

        public static bool IsAsciiLetter(char c)
        {
            c = char.ToLower(c);
            return c >= 'a' && c <= 'z';
        }

        public bool Tokenize(string line)
        {
            int i = 0;
            int len = line.Length;
            string token;

            tokens = new List<Token>();

            while (i < len)
            {
                while (i < len && char.IsWhiteSpace(line[i])) 
                { i++; }

                if (i >= len) break;

                int start = i;
                char currC = line[i];

                /* Token is identifier*/
                if (IsAsciiLetter(currC) || currC == '_')
                {
                    do
                    {
                        currC = line[i];

                        if (!(IsAsciiLetter(currC) || currC == '_'
                              || char.IsDigit(currC)))
                        { break; }

                        i++;
                    }
                    while (i < len);

                    /* AF' doesn't follow regex above, 
                     * but is still identifier. Add it
                     */

                    if (currC == '\'' && 
                        line.Substring(start, i - start).
                        Equals("AF"))
                    { i++; }

                    tokens.Add(
                        new IdentifierToken(
                            line.Substring(start, i - start)
                            )
                        );
                }

                /* Token is a number */
                else if (char.IsDigit(currC))
                {
                    if (currC == '0' && i < len - 1)
                    {
                        char basec = char.ToLower(line[i + 1]);

                        if (basec == 'x' || basec == 'o') 
                        { 
                            i += 2;
                            if (i >= len || !char.IsDigit(line[i])) 
                            { return TokenErr(); }
                        }
                    }

                    do
                    {
                        currC = line[i];

                        if (!char.IsDigit(currC)) { break; }

                        i++;
                    }
                    while (i < len);

                    NumberToken num = new NumberToken();
                    if (!num.FormNumberToken(
                        line.Substring(start, i - start)
                        ))
                    {
                        return false;
                    }

                    tokens.Add(num);
                }

                /* Token is operator */
                else if
                ((token = OperatorToken.
                          OperatorPrefix(line.Substring(i))) != null)
                {
                    tokens.Add(new OperatorToken(token));
                    i += token.Length;
                }
                else
                {
                    return TokenErr();
                }
            }

            return true;
        }
    }
}
