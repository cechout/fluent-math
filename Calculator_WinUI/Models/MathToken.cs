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


    // what a ToLatex call needs to know besides the token itself: which list the cursor is in, and the
    // address of the list being rendered, so that a click in the display can be resolved back into a
    // cursor position
    //
    // the address is built as the walk descends rather than stored on the tokens, because only the walk
    // knows which token and which slot it is currently inside; a token never has to know its own place
    //
    // a step is written tokenIndex.slotIndex, and the slot index is the position in the list
    // MathInputManager.GetSlots returns; the two have to stay in the same order, the address means
    // nothing otherwise
    public class LatexRenderContext
    {
        public ScopeContext? ActiveScope { get; }
        public bool EmitAddresses { get; } // off for the history line, nothing there is clickable

        private readonly string _path; // address of the list being rendered, empty at the root
        private readonly int _tokenIndex; // the token in that list whose slots come next

        public LatexRenderContext(ScopeContext? activeScope, bool emitAddresses)
            : this(activeScope, emitAddresses, "", 0) { }

        private LatexRenderContext(ScopeContext? activeScope, bool emitAddresses, string path, int tokenIndex)
        {
            ActiveScope = activeScope;
            EmitAddresses = emitAddresses;
            _path = path;
            _tokenIndex = tokenIndex;
        }

        // remembers which token of the current list is being rendered, so its slots can name themselves
        public LatexRenderContext ForToken(int tokenIndex)
        {
            return new LatexRenderContext(ActiveScope, EmitAddresses, _path, tokenIndex);
        }

        // descends into one slot of that token
        public LatexRenderContext Slot(int slotIndex)
        {
            string step = $"{_tokenIndex}.{slotIndex}";
            string path = _path.Length == 0 ? step : $"{_path}/{step}";

            return new LatexRenderContext(ActiveScope, EmitAddresses, path, 0);
        }

        // the address of one cursor position in the list being rendered
        public string Address(int cursorIndex)
        {
            return $"{_path}@{cursorIndex}";
        }
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
    // ToLatex takes a render context because the cursor is drawn into the LaTeX itself, and because a
    // token has to hand its slots their own address; everything else about the context is passed down
    // untouched until the one list it belongs to renders it
    public class MathToken
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }

        public MathToken(TokenType type, string value = "")
        {
            Type = type;
            Value = value;
        }

        public virtual string ToLatex(LatexRenderContext context) { return Value; }
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

        public override string ToLatex(LatexRenderContext context) { return _latex; }
    }

    // sin, cos, tan, ln; renders as sin(x)
    public class FunctionToken : MathToken
    {
        public List<MathToken> ParameterTokens { get; } = new List<MathToken>();

        public FunctionToken(string functionName) : base(TokenType.SimpleFunction, functionName) { }

        public override string ToLatex(LatexRenderContext context)
        {
            string innerLatex = LatexHelper.GetSlotLatex(ParameterTokens, context, 0);
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

        public override string ToLatex(LatexRenderContext context)
        {
            string baseStr = LatexHelper.GetSlotLatex(BaseTokens, context, 0);
            string expStr = LatexHelper.GetSlotLatex(ExponentTokens, context, 1);

            return LatexHelper.Tagged("m-pow", $"{baseStr}^{{{expStr}}}");
        }
    }

    // \sqrt[index]{radicand}, where an empty index means a plain square root rather than an empty slot
    public class RootToken : MathToken
    {
        public List<MathToken> IndexTokens { get; } = new List<MathToken>();
        public List<MathToken> RadicandTokens { get; } = new List<MathToken>();

        public RootToken() : base(TokenType.Root) { }

        public override string ToLatex(LatexRenderContext context)
        {
            string radStr = LatexHelper.GetSlotLatex(RadicandTokens, context, 1);

            // an empty index is a plain square root rather than an empty slot, so it gets no box; a
            // cursor standing in it still renders, which is what keeps the slot reachable while typing
            string indexStr = LatexHelper.GetListLatex(IndexTokens, context.Slot(0));
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

        public override string ToLatex(LatexRenderContext context)
        {
            string paramStr = LatexHelper.GetSlotLatex(ParameterTokens, context, 1);

            // no base written out means the common logarithm, the same default the evaluator applies, so
            // an untouched base slot disappears instead of showing an empty box
            string baseStr = LatexHelper.GetListLatex(BaseTokens, context.Slot(0));
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

        public override string ToLatex(LatexRenderContext context)
        {
            string numStr = LatexHelper.GetSlotLatex(NumeratorTokens, context, 0);
            string denStr = LatexHelper.GetSlotLatex(DenominatorTokens, context, 1);

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

        public static string GetListLatex(List<MathToken> tokens, LatexRenderContext context)
        {
            ScopeContext? activeScope = context.ActiveScope;
            bool isActiveList = activeScope != null && ReferenceEquals(tokens, activeScope.Tokens);

            string latex = "";
            for (int i = 0; i < tokens.Count; i++)
            {
                if (isActiveList && i == activeScope!.CursorIndex) latex += CursorLatex;

                MathToken currentToken = tokens[i];
                string tokenLatex;

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

                    tokenLatex = TaggedOperator(symbol);
                }
                else
                {
                    tokenLatex = currentToken.ToLatex(context.ForToken(i));
                }

                latex += Addressed(tokenLatex, context, i);
            }

            if (isActiveList && activeScope!.CursorIndex >= tokens.Count) latex += CursorLatex;
            return latex;
        }

        // hands a token to the display carrying the cursor position it begins at, which is what lets a
        // click on it be turned back into a place in the tree
        //
        // which side of the token was hit decides whether the cursor goes before or after it, so one
        // address per token is enough and the end of a list needs no marker of its own
        private static string Addressed(string latex, LatexRenderContext context, int cursorIndex)
        {
            if (!context.EmitAddresses) return latex;

            return $"\\htmlData{{p={context.Address(cursorIndex)}}}{{{latex}}}";
        }

        // a slot of a structured token, where nothing at all would let the structure collapse; the box
        // only appears when the slot is truly empty, a cursor standing in it counts as content
        public static string GetSlotLatex(List<MathToken> tokens, LatexRenderContext context, int slotIndex)
        {
            LatexRenderContext slotContext = context.Slot(slotIndex);

            string latex = GetListLatex(tokens, slotContext);
            if (latex.Length > 0) return latex;

            // the box is all there is to aim at in an empty slot, so it carries the address of the one
            // position inside it; without that an empty numerator could never be clicked into
            return Addressed(EmptySlotLatex, slotContext, 0);
        }
    }
}
