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
    // an empty child list falls back to an empty-slot box so a half-typed structure still draws instead
    // of collapsing; when the cursor sits in that slot it takes the place of the box, which is what makes
    // the cursor visible inside a structure that has nothing in it yet
    //
    // ToLatex takes the scope the cursor is in because the cursor is drawn into the LaTeX itself; the
    // parameter is handed down untouched until the one list it belongs to renders it
    public class MathToken
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }

        public MathToken(TokenType type, string value = "")
        {
            Type = type;
            Value = value;
        }

        public virtual string ToLatex(ScopeContext? activeScope) { return Value; }
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

        public override string ToLatex(ScopeContext? activeScope) { return _latex; }
    }

    // sin, cos, tan, ln; renders as sin(x)
    public class FunctionToken : MathToken
    {
        public List<MathToken> ParameterTokens { get; } = new List<MathToken>();

        public FunctionToken(string functionName) : base(TokenType.SimpleFunction, functionName) { }

        public override string ToLatex(ScopeContext? activeScope)
        {
            string innerLatex = LatexHelper.GetSlotLatex(ParameterTokens, activeScope);
            return LatexHelper.Tagged("m-func", $"\\{Value}({innerLatex})");
        }
    }

    // base^{exponent}
    // BaseTokens is settable because StartPower moves an already-typed number into it after the fact
    public class PowerToken : MathToken
    {
        public List<MathToken> BaseTokens { get; set; } = new List<MathToken>();
        public List<MathToken> ExponentTokens { get; } = new List<MathToken>();

        public PowerToken() : base(TokenType.Power) { }

        public override string ToLatex(ScopeContext? activeScope)
        {
            string baseStr = LatexHelper.GetSlotLatex(BaseTokens, activeScope);
            string expStr = LatexHelper.GetSlotLatex(ExponentTokens, activeScope);

            return LatexHelper.Tagged("m-pow", $"{baseStr}^{{{expStr}}}");
        }
    }

    // \sqrt[index]{radicand}, where an empty index means a plain square root rather than an empty slot
    public class RootToken : MathToken
    {
        public List<MathToken> IndexTokens { get; } = new List<MathToken>();
        public List<MathToken> RadicandTokens { get; } = new List<MathToken>();

        public RootToken() : base(TokenType.Root) { }

        public override string ToLatex(ScopeContext? activeScope)
        {
            string radStr = LatexHelper.GetSlotLatex(RadicandTokens, activeScope);

            // an empty index is a plain square root rather than an empty slot, so it gets no box; a
            // cursor standing in it still renders, which is what keeps the slot reachable while typing
            string indexStr = LatexHelper.GetListLatex(IndexTokens, activeScope);
            if (indexStr.Length > 0) return LatexHelper.Tagged("m-root", $"\\sqrt[{indexStr}]{{{radStr}}}");

            return LatexHelper.Tagged("m-root", $"\\sqrt{{{radStr}}}");
        }
    }

    // \log_{base}(x)
    public class LogarithmToken : MathToken
    {
        public List<MathToken> BaseTokens { get; } = new List<MathToken>();
        public List<MathToken> ParameterTokens { get; } = new List<MathToken>();

        public LogarithmToken() : base(TokenType.Logarithm) { }

        public override string ToLatex(ScopeContext? activeScope)
        {
            string paramStr = LatexHelper.GetSlotLatex(ParameterTokens, activeScope);

            // no base written out means the common logarithm, the same default the evaluator applies, so
            // an untouched base slot disappears instead of showing an empty box
            string baseStr = LatexHelper.GetListLatex(BaseTokens, activeScope);
            if (baseStr.Length > 0) return LatexHelper.Tagged("m-log", $"\\log_{{{baseStr}}}({paramStr})");

            return LatexHelper.Tagged("m-log", $"\\log({paramStr})");
        }
    }

    // \frac{numerator}{denominator}
    public class FractionToken : MathToken
    {
        public List<MathToken> NumeratorTokens { get; } = new List<MathToken>();
        public List<MathToken> DenominatorTokens { get; } = new List<MathToken>();

        public FractionToken() : base(TokenType.Fraction) { }

        public override string ToLatex(ScopeContext? activeScope)
        {
            string numStr = LatexHelper.GetSlotLatex(NumeratorTokens, activeScope);
            string denStr = LatexHelper.GetSlotLatex(DenominatorTokens, activeScope);

            return LatexHelper.Tagged("m-frac", $"\\frac{{{numStr}}}{{{denStr}}}");
        }
    }


    // walks a token list and concatenates the LaTeX of every node; the recursion into nested lists
    // happens through the ToLatex overrides above, which call back in here
    //
    // the cursor is drawn here rather than by the caller, because only this loop knows where one token
    // ends and the next begins; activeScope names the single list it belongs to, every other list is
    // rendered without a cursor
    public static class LatexHelper
    {
        // two halves with two jobs, neither of which draws anything
        //
        // \vphantom{0} is zero wide and exactly as tall as a digit in the slot it stands in; that is
        // what keeps a slot holding nothing but the cursor from collapsing, and it is why the caret can
        // never make a denominator taller than its own content
        //
        // the tagged rule is a zero-size anchor the css paints the visible bar from, out of the flow,
        // so the caret occupies no space at all and the digits on either side of it never move; the
        // class only survives when the render call runs with trust enabled
        public const string CursorLatex = "\\vphantom{0}\\htmlClass{cursor}{\\rule{0em}{0em}}";

        // the box a Casio shows for a slot that still has to be filled
        private const string EmptySlotLatex = "\\square";

        // hands a whole structured token to the display under a css class, which is what lets the size
        // of a fraction or a root be set from C# without touching the LaTeX around it
        //
        // the tag always goes around the whole structure and never around one of its slots: KaTeX writes
        // the offsets inside a fraction or a superscript as inline em values, so a size set on the
        // wrapper carries content and alignment with it, while one set on a numerator alone would leave
        // those offsets sized for the old em and drag the content away from the bar
        public static string Tagged(string cssClass, string latex)
        {
            return $"\\htmlClass{{{cssClass}}}{{{latex}}}";
        }

        // an operator under the class the display sizes and spaces it with
        //
        // \mathord is what makes that spacing controllable at all: KaTeX pads a binary operator with a
        // fixed medium space on either side, and demoting it to an ordinary atom is the only way to get
        // that space back before the css puts a chosen amount of it in again
        public static string TaggedOperator(string symbol)
        {
            return $"\\mathord{{{Tagged("m-op", symbol)}}}";
        }

        public static string GetListLatex(List<MathToken> tokens, ScopeContext? activeScope)
        {
            bool isActiveList = activeScope != null && ReferenceEquals(tokens, activeScope.Tokens);

            string latex = "";
            for (int i = 0; i < tokens.Count; i++)
            {
                if (isActiveList && i == activeScope!.CursorIndex) latex += CursorLatex;

                MathToken currentToken = tokens[i];

                // operators are the one kind that does not render as its own value; * and / get proper
                // math symbols, and the size and spacing of all of them comes from the display
                if (currentToken.Type == TokenType.Operator)
                {
                    string symbol = currentToken.Value switch
                    {
                        "*" => "\\cdot",
                        "/" => "\\div",
                        _ => currentToken.Value
                    };

                    latex += TaggedOperator(symbol);
                }
                else
                {
                    latex += currentToken.ToLatex(activeScope);
                }
            }

            if (isActiveList && activeScope!.CursorIndex >= tokens.Count) latex += CursorLatex;
            return latex;
        }

        // a slot of a structured token, where nothing at all would let the structure collapse; the box
        // only appears when the slot is truly empty, a cursor standing in it counts as content
        public static string GetSlotLatex(List<MathToken> tokens, ScopeContext? activeScope)
        {
            string latex = GetListLatex(tokens, activeScope);
            if (latex.Length > 0) return latex;

            return EmptySlotLatex;
        }
    }
}
