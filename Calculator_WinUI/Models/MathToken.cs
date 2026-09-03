using System;
using System.Collections.Generic;

namespace Calculator_WinUI.Models
{
    public enum TokenType
    {
        Number,
        Operator,
        Constant,
        BracketOpen,
        BracketClose,
        Fraction,
        SimpleFunction,
        Power,
        Root,
        Logarithm
    }


    // one node of the input tree; a plain MathToken is a leaf carrying its own text (a number or an
    // operator), the subclasses below add child token lists and become the branches
    //
    // ToLatex is the single place a token turns into something renderable, so a new token kind needs
    // exactly one override here and no change anywhere else
    //
    // every subclass falls back to a placeholder for an empty child list, so a half-typed structure
    // still draws instead of collapsing; a blank space where the construct already shows a frame of its
    // own (fraction bar, root sign), a "?" where the empty slot would otherwise be invisible
    public class MathToken
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }

        public MathToken(TokenType type, string value = "")
        {
            Type = type;
            Value = value;
        }

        public virtual string ToLatex() { return Value; }
    }


    // pi and e; both the numeric value and the symbol hang off the token, so a further constant is one
    // entry here and no change in the evaluator or the renderer
    public class ConstantToken : MathToken
    {
        public double NumericValue { get; }

        private readonly string _latex;

        public ConstantToken(string name) : base(TokenType.Constant, name)
        {
            switch (name)
            {
                case "pi":
                    NumericValue = Math.PI;
                    _latex = "\\pi";
                    break;

                case "e":
                    NumericValue = Math.E;
                    _latex = "e";
                    break;

                default:
                    NumericValue = 0;
                    _latex = name;
                    break;
            }
        }

        public override string ToLatex() { return _latex; }
    }

    // sin, cos, tan, ln; renders as sin(x)
    public class FunctionToken : MathToken
    {
        public List<MathToken> ParameterTokens { get; } = new List<MathToken>();

        public FunctionToken(string functionName) : base(TokenType.SimpleFunction, functionName) { }

        public override string ToLatex()
        {
            string innerLatex;
            if (ParameterTokens.Count > 0) { innerLatex = LatexHelper.GetListLatex(ParameterTokens); }
            else { innerLatex = "?"; }

            return $"\\{Value}({innerLatex})";
        }
    }

    // base^{exponent}
    // BaseTokens is settable because StartPower moves an already-typed number into it after the fact
    public class PowerToken : MathToken
    {
        public List<MathToken> BaseTokens { get; set; } = new List<MathToken>();
        public List<MathToken> ExponentTokens { get; } = new List<MathToken>();

        public PowerToken() : base(TokenType.Power) { }

        public override string ToLatex()
        {
            string baseStr;
            if (BaseTokens.Count > 0) { baseStr = LatexHelper.GetListLatex(BaseTokens); }
            else { baseStr = "?"; }

            string expStr;
            if (ExponentTokens.Count > 0) { expStr = LatexHelper.GetListLatex(ExponentTokens); }
            else { expStr = "?"; }

            return $"{baseStr}^{{{expStr}}}";
        }
    }

    // \sqrt[index]{radicand}, where an empty index means a plain square root rather than an empty slot
    public class RootToken : MathToken
    {
        public List<MathToken> IndexTokens { get; } = new List<MathToken>();
        public List<MathToken> RadicandTokens { get; } = new List<MathToken>();

        public RootToken() : base(TokenType.Root) { }

        public override string ToLatex()
        {
            string radStr;
            if (RadicandTokens.Count > 0) { radStr = LatexHelper.GetListLatex(RadicandTokens); }
            else { radStr = " "; }

            if (IndexTokens.Count > 0)
            {
                return $"\\sqrt[{LatexHelper.GetListLatex(IndexTokens)}]{{{radStr}}}";
            }
            return $"\\sqrt{{{radStr}}}";
        }
    }

    // \log_{base}(x)
    public class LogarithmToken : MathToken
    {
        public List<MathToken> BaseTokens { get; } = new List<MathToken>();
        public List<MathToken> ParameterTokens { get; } = new List<MathToken>();

        public LogarithmToken() : base(TokenType.Logarithm) { }

        public override string ToLatex()
        {
            string baseStr;
            if (BaseTokens.Count > 0) { baseStr = LatexHelper.GetListLatex(BaseTokens); }
            else { baseStr = "?"; }

            string paramStr;
            if (ParameterTokens.Count > 0) { paramStr = LatexHelper.GetListLatex(ParameterTokens); }
            else { paramStr = " "; }

            return $"\\log_{{{baseStr}}}({paramStr})";
        }
    }

    // \frac{numerator}{denominator}
    public class FractionToken : MathToken
    {
        public List<MathToken> NumeratorTokens { get; } = new List<MathToken>();
        public List<MathToken> DenominatorTokens { get; } = new List<MathToken>();

        public FractionToken() : base(TokenType.Fraction) { }

        public override string ToLatex()
        {
            string numStr;
            if (NumeratorTokens.Count > 0) { numStr = LatexHelper.GetListLatex(NumeratorTokens); }
            else { numStr = " "; }

            string denStr;
            if (DenominatorTokens.Count > 0) { denStr = LatexHelper.GetListLatex(DenominatorTokens); }
            else { denStr = " "; }

            return $"\\frac{{{numStr}}}{{{denStr}}}";
        }
    }


    // walks a token list and concatenates the LaTeX of every node; the recursion into nested lists
    // happens through the ToLatex overrides above, which call back in here
    public static class LatexHelper
    {
        public static string GetListLatex(List<MathToken> tokens)
        {
            string latex = "";
            foreach (var currentToken in tokens)
            {
                // operators are the one kind that does not render as its own value; * and / get proper
                // math symbols, and every operator gets padding so terms do not run together
                if (currentToken.Type == TokenType.Operator)
                {
                    latex += currentToken.Value switch
                    {
                        "*" => " \\cdot ",
                        "/" => " \\div ",
                        _ => $" {currentToken.Value} "
                    };
                }
                else
                {
                    latex += currentToken.ToLatex();
                }
            }
            return latex;
        }
    }
}
