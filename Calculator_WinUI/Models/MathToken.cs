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
        Logarithm,
        Postfix,
        Answer
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
        public bool DisplayFractions { get; } // MathDisplayStyle.UseDisplayFractions, passed through

        private readonly string _path; // address of the list being rendered, empty at the root
        private readonly int _tokenIndex; // the token in that list whose slots come next

        public LatexRenderContext(ScopeContext? activeScope, bool emitAddresses, bool displayFractions)
            : this(activeScope, emitAddresses, displayFractions, "", 0) { }

        private LatexRenderContext(ScopeContext? activeScope, bool emitAddresses, bool displayFractions,
            string path, int tokenIndex)
        {
            ActiveScope = activeScope;
            EmitAddresses = emitAddresses;
            DisplayFractions = displayFractions;
            _path = path;
            _tokenIndex = tokenIndex;
        }

        // remembers which token of the current list is being rendered, so its slots can name themselves
        public LatexRenderContext ForToken(int tokenIndex)
        {
            return new LatexRenderContext(ActiveScope, EmitAddresses, DisplayFractions, _path, tokenIndex);
        }

        // descends into one slot of that token
        public LatexRenderContext Slot(int slotIndex)
        {
            string step = $"{_tokenIndex}.{slotIndex}";
            string path = _path.Length == 0 ? step : $"{_path}/{step}";

            return new LatexRenderContext(ActiveScope, EmitAddresses, DisplayFractions, path, 0);
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
    // of collapsing; a cursor standing in that slot does not replace the box, it sits beside it, because
    // the box is the only thing holding the slot open
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

    // the previous result, carried as a token rather than as its digits, so the whole double survives
    // into the next calculation instead of only the twelve significant digits the display showed
    public class AnsToken : MathToken
    {
        public AnsToken() : base(TokenType.Answer, "Ans") { }

        public override string ToLatex(LatexRenderContext context) { return "\\text{Ans}"; }
    }

    // x!, the reciprocal and percent; all three stand behind their operand instead of in front of it,
    // which is the whole reason the evaluator has a postfix level at all
    //
    // the reciprocal is a bare superscript rather than a named call, so it sits on the operand the same
    // way a Casio prints it
    public class PostfixToken : MathToken
    {
        private readonly string _latex;

        public PostfixToken(string kind) : base(TokenType.Postfix, kind)
        {
            switch (kind)
            {
                case "!":
                    _latex = "!";
                    break;

                case "inv":
                    _latex = "{}^{-1}";
                    break;

                case "%":
                    _latex = "\\%";
                    break;

                default:
                    _latex = kind;
                    break;
            }
        }

        public override string ToLatex(LatexRenderContext context) { return _latex; }
    }

    // sin, cos, tan, ln and the hyperbolic family; renders as sin(x)
    //
    // the name the evaluator switches on is not always the name KaTeX is handed, so the two are kept
    // apart: Value stays the plain function name, _latexName is the command that draws it
    public class FunctionToken : MathToken
    {
        public List<MathToken> ParameterTokens { get; } = new List<MathToken>();

        private readonly string _latexName;
        private readonly bool _drawsAsBars;

        // whether this one draws as a pair of bars instead of a named call, which is a fact about the
        // shape of the token rather than about either output format, so both renderers read it here
        public bool DrawsAsBars => _drawsAsBars;

        public FunctionToken(string functionName) : base(TokenType.SimpleFunction, functionName)
        {
            _latexName = functionName;

            switch (functionName)
            {
                // KaTeX has no command for the inverse hyperbolics, so they print the way a Casio does,
                // as the plain function carrying a raised minus one
                case "arsinh":
                    _latexName = "sinh^{-1}";
                    break;

                case "arcosh":
                    _latexName = "cosh^{-1}";
                    break;

                case "artanh":
                    _latexName = "tanh^{-1}";
                    break;

                // the absolute value is a pair of bars rather than a named call; keeping it a
                // FunctionToken is what lets slots, navigation and Backspace stay untouched
                case "abs":
                    _drawsAsBars = true;
                    break;
            }
        }

        public override string ToLatex(LatexRenderContext context)
        {
            string innerLatex = LatexHelper.GetSlotLatex(ParameterTokens, context, 0);

            if (_drawsAsBars) return LatexHelper.Tagged("m-func", $"\\left|{innerLatex}\\right|");

            return LatexHelper.Tagged("m-func", $"\\{_latexName}({innerLatex})");
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

            // the base is braced because ^ raises whatever single atom stands in front of it, not the
            // whole slot; an unbraced base handed the exponent only the last thing in it, so a cursor
            // at the end of the base became the base, and KaTeX dropped the exponent onto the height
            // of a caret that has none
            return LatexHelper.Tagged("m-pow", $"{{{baseStr}}}^{{{expStr}}}");
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

            // dfrac keeps both halves at full text size and takes the wide display clearances with it
            string command = context.DisplayFractions ? "dfrac" : "frac";

            return LatexHelper.Tagged("m-frac", $"\\{command}{{{numStr}}}{{{denStr}}}");
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
        // an anchor of no size at all, in either direction; the visible bar is a css border on the
        // tagged span, and the class only survives when the render call runs with trust enabled
        //
        // giving it any extent in the LaTeX has gone wrong twice, because KaTeX measures the slot the
        // cursor stands in and lays the structure around it out from that measurement
        //
        // a height made a denominator report itself taller than its own digits, so KaTeX pushed the
        // denominator further down and the whole fraction climbed as the display recentred it
        //
        // \vphantom{0} reads as the natural way to reserve a height and is worse: KaTeX builds it as
        // an rlap, whose inner box is absolutely positioned inside a zero-width parent and hangs a
        // digits width out to the right, which counts towards the scrollable area and left the
        // horizontal scrollbar showing permanently with nothing to scroll to
        //
        // an empty slot keeps its height from its box instead, see GetSlotLatex
        public const string CursorLatex = "\\htmlClass{cursor}{\\rule{0em}{0em}}";

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

        // every token goes to the display as an ordinary atom, which is what puts the whole of the
        // spacing between tokens under our control
        //
        // KaTeX spaces adjacent atoms by their class pair: a binary operator carries a fixed medium
        // space on either side, and an ordinary atom before an operator name such as sin or cos gets
        // a thin space between them, neither of which any stylesheet can reach
        //
        // measured on 1 + cos: without this the plus sat 2.15px from what precedes it and 8.16px from
        // what follows, and the difference vanished as soon as anything untyped stood between them,
        // which is exactly what a caret is; with every token an ord the pair is always ord to ord,
        // the spacing is always zero, and a caret between two tokens can no longer change anything
        public static string Atomic(string latex)
        {
            return $"\\mathord{{{latex}}}";
        }

        // an operator under the class the display sizes and spaces it with
        public static string TaggedOperator(string symbol)
        {
            return Atomic(Tagged("m-op", symbol));
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

                    tokenLatex = Tagged("m-op", symbol);
                }
                else
                {
                    tokenLatex = currentToken.ToLatex(context.ForToken(i));
                }

                latex += Addressed(Atomic(tokenLatex), context, i);
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
            if (tokens.Count > 0) return latex;

            // a cursor standing in the slot does not count as content, so the box stays underneath it
            // rather than being replaced by it; that is what holds the slot open, and it is why the
            // caret itself can be given no size at all, see CursorLatex
            //
            // the box is also all there is to aim at in an empty slot, so it carries the address of
            // the one position inside it; without that an empty numerator could never be clicked into
            return latex + Addressed(EmptySlotLatex, slotContext, 0);
        }
    }


    // a detached deep copy of a token list
    //
    // the display needs one the moment = is pressed: the tree carries on being edited afterwards, since
    // = deliberately leaves it alone so a Math ERROR can be corrected, and the history line therefore
    // cannot simply hold a reference to it; MathInputManager clears its root list in place
    //
    // it sits beside the token classes rather than as a virtual on each of them, so the whole of the
    // copying is one thing to read and a token type added later fails loudly here instead of losing a
    // slot quietly
    public static class MathTokenCloner
    {
        public static List<MathToken> CloneList(IReadOnlyList<MathToken> tokens)
        {
            List<MathToken> copy = new List<MathToken>(tokens.Count);
            foreach (MathToken token in tokens) copy.Add(Clone(token));

            return copy;
        }

        public static MathToken Clone(MathToken token)
        {
            switch (token)
            {
                case FractionToken fraction:
                    FractionToken fractionCopy = new FractionToken();
                    fractionCopy.NumeratorTokens.AddRange(CloneList(fraction.NumeratorTokens));
                    fractionCopy.DenominatorTokens.AddRange(CloneList(fraction.DenominatorTokens));
                    return fractionCopy;

                case PowerToken power:
                    PowerToken powerCopy = new PowerToken();
                    powerCopy.BaseTokens.AddRange(CloneList(power.BaseTokens));
                    powerCopy.ExponentTokens.AddRange(CloneList(power.ExponentTokens));
                    return powerCopy;

                case RootToken root:
                    RootToken rootCopy = new RootToken();
                    rootCopy.IndexTokens.AddRange(CloneList(root.IndexTokens));
                    rootCopy.RadicandTokens.AddRange(CloneList(root.RadicandTokens));
                    return rootCopy;

                case LogarithmToken logarithm:
                    LogarithmToken logarithmCopy = new LogarithmToken();
                    logarithmCopy.BaseTokens.AddRange(CloneList(logarithm.BaseTokens));
                    logarithmCopy.ParameterTokens.AddRange(CloneList(logarithm.ParameterTokens));
                    return logarithmCopy;

                case FunctionToken function:
                    FunctionToken functionCopy = new FunctionToken(function.Value);
                    functionCopy.ParameterTokens.AddRange(CloneList(function.ParameterTokens));
                    return functionCopy;

                // the leaves rebuild themselves from their own name, which is what fills the private
                // fields a constant, a postfix and a function keep beside Value
                case ConstantToken constant: return new ConstantToken(constant.Value);
                case PostfixToken postfix: return new PostfixToken(postfix.Value);
                case AnsToken: return new AnsToken();
            }

            if (token.GetType() != typeof(MathToken))
            {
                throw new NotSupportedException($"no clone for {token.GetType().Name}");
            }

            return new MathToken(token.Type, token.Value);
        }
    }
}
