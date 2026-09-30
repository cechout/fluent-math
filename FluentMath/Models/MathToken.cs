using System;
using System.Collections.Generic;

namespace FluentMath.Models
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
        Answer,
        Random,
        MixedFraction,
        Recurring,
        Variable,
        LargeOperator,
        Derivative
    }


    // what a ToLatex call needs besides the token: the list the cursor is in, and the address of the list
    // being rendered, built as the walk descends
    // (a step is tokenIndex.slotIndex; the slot index has to match the order of MathInputManager.GetSlots)
    public class LatexRenderContext
    {
        public ScopeContext? ActiveScope { get; }
        public bool EmitAddresses { get; } // off for the history line, nothing there is clickable
        public bool DisplayFractions { get; } // MathLayoutStyle.UseDisplayFractions, passed through

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


    // one node of the input tree:
    // a plain MathToken is a leaf with its own text (a number or an operator); the subclasses below add
    // child token lists and become the branches
    // (ToLatex writes the LaTeX the tests read; an empty slot is a box, a cursor in it stands beside it)
    public class MathToken
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }

        // set on the digits a result was seeded as, see SeededValue
        public SeededValue? Seed { get; set; }

        public MathToken(TokenType type, string value = "")
        {
            Type = type;
            Value = value;
        }

        public virtual string ToLatex(LatexRenderContext context) { return Value; }
    }

    // the full value behind the digits a shown result is carried on as, so 1÷3 = ×3 is 1, not 0.999999999999
    //
    // every digit of the run shares one; the evaluator reads it while the run is exactly those digits,
    // an edit makes the run read as typed
    // (the magnitude only, a minus is a token of its own; the exact value rides along, so √2 stays √2)
    public sealed class SeededValue
    {
        public MathValue Magnitude { get; }
        public int DigitCount { get; }

        public SeededValue(MathValue magnitude, int digitCount)
        {
            Magnitude = magnitude;
            DigitCount = digitCount;
        }
    }


    // pi and e, with their value and symbol
    public class ConstantToken : MathToken
    {
        public double NumericValue { get; }

        // π has an exact value and e does not, which is what keeps e out of every exact form
        public ExactValue? Exact { get; }

        private readonly string _latex;

        public ConstantToken(string name) : base(TokenType.Constant, name)
        {
            switch (name)
            {
                case "pi":
                    NumericValue = Math.PI;
                    Exact = ExactValue.Pi;
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

    // the previous result as a token, so the whole double survives, not only the twelve digits shown
    public class AnsToken : MathToken
    {
        public AnsToken() : base(TokenType.Answer, "Ans") { }

        public override string ToLatex(LatexRenderContext context) { return "\\text{Ans}"; }
    }

    // Ran#, a new random number on every evaluation
    public class RandomToken : MathToken
    {
        public RandomToken() : base(TokenType.Random, "Ran#") { }

        public override string ToLatex(LatexRenderContext context) { return "\\text{Ran\\#}"; }
    }

    // x, the variable a calculus structure runs over; 0 anywhere else, as an empty variable on a Casio
    public class VariableToken : MathToken
    {
        public VariableToken() : base(TokenType.Variable, "x") { }

        public override string ToLatex(LatexRenderContext context) { return "x"; }
    }

    // the period of a recurring decimal, drawn under a bar (0.3 with a bar for 1÷3); only in a result
    public class RecurringToken : MathToken
    {
        public RecurringToken(string digits) : base(TokenType.Recurring, digits) { }

        public override string ToLatex(LatexRenderContext context) { return $"\\overline{{{Value}}}"; }
    }

    // x!, the reciprocal, percent, the decimal prefixes and the °′″ markers, all behind their operand
    // (the reciprocal is a bare raised minus one, the way a Casio prints it)
    public class PostfixToken : MathToken
    {
        // the three angle markers, with the divisor of the number in front and the sign; in writing order
        private static readonly Dictionary<string, (int Divisor, string Symbol, string Latex)> SexagesimalMarkers =
            new Dictionary<string, (int Divisor, string Symbol, string Latex)>
            {
                ["degrees"] = (1, "°", "{}^{\\circ}"),
                ["minutes"] = (60, "′", "{}'"),
                ["seconds"] = (3600, "″", "{}''")
            };

        // the decimal prefixes by name, with their power of ten and symbol; 5k binds as tightly as 5!
        // (exa stays in, since an E never stands for the exponent here)
        private static readonly Dictionary<string, (int Exponent, string Symbol)> Prefixes =
            new Dictionary<string, (int Exponent, string Symbol)>
            {
                ["femto"] = (-15, "f"),
                ["pico"] = (-12, "p"),
                ["nano"] = (-9, "n"),
                ["micro"] = (-6, "μ"),
                ["milli"] = (-3, "m"),
                ["kilo"] = (3, "k"),
                ["mega"] = (6, "M"),
                ["giga"] = (9, "G"),
                ["tera"] = (12, "T"),
                ["peta"] = (15, "P"),
                ["exa"] = (18, "E")
            };

        private readonly string _latex;

        // what the display writes behind the operand; (not read for the reciprocal)
        public string Symbol { get; }

        public PostfixToken(string kind) : base(TokenType.Postfix, kind)
        {
            Symbol = kind;

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

                    if (Prefixes.TryGetValue(kind, out (int Exponent, string Symbol) prefix))
                    {
                        Symbol = prefix.Symbol;
                        _latex = kind == "micro" ? "\\mu" : $"\\mathrm{{{prefix.Symbol}}}";
                    }

                    if (SexagesimalMarkers.TryGetValue(kind, out (int Divisor, string Symbol, string Latex) marker))
                    {
                        Symbol = marker.Symbol;
                        _latex = marker.Latex;
                    }
                    break;
            }
        }

        // what a number in front of a sexagesimal marker is divided by, or null for a postfix that is not one
        public static int? SexagesimalDivisor(string kind)
        {
            return SexagesimalMarkers.TryGetValue(kind, out (int Divisor, string Symbol, string Latex) marker) ? marker.Divisor : null;
        }

        // the power of ten a prefix multiplies by, or null for a postfix that is not a prefix
        public static int? PrefixExponent(string kind)
        {
            return Prefixes.TryGetValue(kind, out (int Exponent, string Symbol) prefix) ? prefix.Exponent : null;
        }

        // the other way round, the prefix that stands for a power of ten, or null where there is none
        public static string? PrefixFor(int exponent)
        {
            foreach (KeyValuePair<string, (int Exponent, string Symbol)> prefix in Prefixes)
            {
                if (prefix.Value.Exponent == exponent) return prefix.Key;
            }

            return null;
        }

        public override string ToLatex(LatexRenderContext context) { return _latex; }
    }

    // how a function is drawn: a name in front of a bracket pair, or a pair of delimiters of its own
    public enum FunctionShape
    {
        Named,
        Bars,     // the absolute value
        Floor,
        Ceiling
    }

    // sin, cos, tan, ln, the hyperbolic family and the named functions of the panels, as sin(x)
    // Value is the name the evaluator switches on, DisplayName the one drawn
    // (one argument or two, fixed by the name; the separator is drawn, so no comma key is needed)
    public class FunctionToken : MathToken
    {
        public IReadOnlyList<List<MathToken>> Arguments { get; }

        // the first argument, for most functions the only one
        public List<MathToken> ParameterTokens => Arguments[0];

        // the name in front of the bracket, and whether it carries a raised minus one
        // (ToLatex and the layout both read it here)
        public string DisplayName { get; }
        public bool IsInverse { get; }

        // a pair of delimiters instead of a named call
        public FunctionShape Shape { get; }

        // the names LaTeX has a command for; everything else goes through \operatorname
        private static readonly HashSet<string> LatexCommands = new HashSet<string>
        {
            "sin", "cos", "tan", "cot", "sec", "csc", "sinh", "cosh", "tanh", "coth", "ln"
        };

        public FunctionToken(string functionName) : base(TokenType.SimpleFunction, functionName)
        {
            (DisplayName, IsInverse) = NameOf(functionName);
            Shape = ShapeOf(functionName);

            List<MathToken>[] arguments = new List<MathToken>[ArgumentCount(functionName)];
            for (int index = 0; index < arguments.Length; index++) arguments[index] = new List<MathToken>();

            Arguments = arguments;
        }

        // every inverse prints as the plain function with a raised minus one, the panel functions the way
        // a Casio spells them
        private static (string Name, bool Inverse) NameOf(string functionName)
        {
            return functionName switch
            {
                "arcsin" => ("sin", true),
                "arccos" => ("cos", true),
                "arctan" => ("tan", true),
                "arcsec" => ("sec", true),
                "arccsc" => ("csc", true),
                "arccot" => ("cot", true),
                "arsinh" => ("sinh", true),
                "arcosh" => ("cosh", true),
                "artanh" => ("tanh", true),
                "arsech" => ("sech", true),
                "arcsch" => ("csch", true),
                "arcoth" => ("coth", true),
                "int" => ("Int", false),
                "intg" => ("Intg", false),
                "gcd" => ("GCD", false),
                "lcm" => ("LCM", false),
                "ranint" => ("RanInt#", false),
                "rndfix" => ("RndFix", false),
                "rnd" => ("Rnd", false),
                "pol" => ("Pol", false),
                "rec" => ("Rec", false),
                _ => (functionName, false)
            };
        }

        // abs, floor and ceiling are pairs of delimiters; (still FunctionTokens, so slots, navigation and
        // Backspace need nothing new)
        private static FunctionShape ShapeOf(string functionName)
        {
            return functionName switch
            {
                "abs" => FunctionShape.Bars,
                "floor" => FunctionShape.Floor,
                "ceil" => FunctionShape.Ceiling,
                _ => FunctionShape.Named
            };
        }

        private static int ArgumentCount(string functionName)
        {
            return functionName switch
            {
                "gcd" or "lcm" or "ranint" or "rndfix" or "pol" or "rec" => 2,
                _ => 1
            };
        }

        public override string ToLatex(LatexRenderContext context)
        {
            string[] slots = new string[Arguments.Count];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = LatexHelper.GetSlotLatex(Arguments[index], context, index);
            }

            string innerLatex = string.Join(",", slots);

            switch (Shape)
            {
                case FunctionShape.Bars:
                    return LatexHelper.Tagged("m-func", $"\\left|{innerLatex}\\right|");

                case FunctionShape.Floor:
                    return LatexHelper.Tagged("m-func", $"\\left\\lfloor{innerLatex}\\right\\rfloor");

                case FunctionShape.Ceiling:
                    return LatexHelper.Tagged("m-func", $"\\left\\lceil{innerLatex}\\right\\rceil");
            }

            string command = LatexCommands.Contains(DisplayName)
                ? DisplayName
                : $"operatorname{{{DisplayName.Replace("#", "\\#")}}}";

            string raised = IsInverse ? "^{-1}" : "";

            return LatexHelper.Tagged("m-func", $"\\{command}{raised}({innerLatex})");
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

            // braced, since ^ raises only the single atom in front of it
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

            // an empty index gets no box; a cursor in it still renders
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

            // no base is the common logarithm, so an untouched base slot shows no box
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

    // the mixed number of the Casio key: whole part, numerator and denominator in one token
    // (a number before a plain fraction reads as their product, hence the whole part slot)
    public class MixedFractionToken : MathToken
    {
        public List<MathToken> WholeTokens { get; } = new List<MathToken>();
        public List<MathToken> NumeratorTokens { get; } = new List<MathToken>();
        public List<MathToken> DenominatorTokens { get; } = new List<MathToken>();

        public MixedFractionToken() : base(TokenType.MixedFraction) { }

        public override string ToLatex(LatexRenderContext context)
        {
            string wholeStr = LatexHelper.GetSlotLatex(WholeTokens, context, 0);
            string numStr = LatexHelper.GetSlotLatex(NumeratorTokens, context, 1);
            string denStr = LatexHelper.GetSlotLatex(DenominatorTokens, context, 2);

            string command = context.DisplayFractions ? "dfrac" : "frac";

            return LatexHelper.Tagged("m-mixed", $"{{{wholeStr}}}\\{command}{{{numStr}}}{{{denStr}}}");
        }
    }


    // the three structures that run x from a lower bound to an upper one
    public enum LargeOperatorKind
    {
        Sum,
        Product,
        Integral
    }

    // Σ, Π and the integral: a lower bound, an upper bound, and the body x runs through
    // (walked lower, upper, body, as drawn; a Casio walks Σ body first)
    public class LargeOperatorToken : MathToken
    {
        public LargeOperatorKind Kind { get; }

        public List<MathToken> LowerTokens { get; } = new List<MathToken>();
        public List<MathToken> UpperTokens { get; } = new List<MathToken>();
        public List<MathToken> BodyTokens { get; } = new List<MathToken>();

        public LargeOperatorToken(LargeOperatorKind kind) : base(TokenType.LargeOperator, kind.ToString())
        {
            Kind = kind;
        }

        public override string ToLatex(LatexRenderContext context)
        {
            string lowerStr = LatexHelper.GetSlotLatex(LowerTokens, context, 0);
            string upperStr = LatexHelper.GetSlotLatex(UpperTokens, context, 1);
            string bodyStr = LatexHelper.GetSlotLatex(BodyTokens, context, 2);

            if (Kind == LargeOperatorKind.Integral)
            {
                return LatexHelper.Tagged("m-int", $"\\int_{{{lowerStr}}}^{{{upperStr}}}{bodyStr}\\,dx");
            }

            string command = Kind == LargeOperatorKind.Sum ? "sum" : "prod";

            return LatexHelper.Tagged("m-series", $"\\{command}_{{x={lowerStr}}}^{{{upperStr}}}({bodyStr})");
        }
    }

    // the derivative of a function of x at one point, written d/dx of the function with the point under a
    // bar behind it
    public class DerivativeToken : MathToken
    {
        public List<MathToken> FunctionTokens { get; } = new List<MathToken>();
        public List<MathToken> PointTokens { get; } = new List<MathToken>();

        public DerivativeToken() : base(TokenType.Derivative) { }

        public override string ToLatex(LatexRenderContext context)
        {
            string functionStr = LatexHelper.GetSlotLatex(FunctionTokens, context, 0);
            string pointStr = LatexHelper.GetSlotLatex(PointTokens, context, 1);

            return LatexHelper.Tagged("m-deriv", $"\\frac{{d}}{{dx}}({functionStr})\\Big|_{{x={pointStr}}}");
        }
    }


    // walks a token list and concatenates the LaTeX of every node, recursing through the ToLatex overrides
    // (the cursor is drawn here, in the one list activeScope names)
    public static class LatexHelper
    {
        // the cursor, an anchor of no size; (an empty slot keeps its height from its box, see GetSlotLatex)
        public const string CursorLatex = "\\htmlClass{cursor}{\\rule{0em}{0em}}";

        // the box a Casio shows for a slot that still has to be filled
        private const string EmptySlotLatex = "\\square";

        // wraps a whole structured token in a class, never one of its slots
        public static string Tagged(string cssClass, string latex)
        {
            return $"\\htmlClass{{{cssClass}}}{{{latex}}}";
        }

        // every token as an ordinary atom, so no class pair adds spacing between tokens
        public static string Atomic(string latex)
        {
            return $"\\mathord{{{latex}}}";
        }

        // an operator, tagged as one
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

                // operators do not render as their own value; * and / get proper math symbols
                if (currentToken.Type == TokenType.Operator)
                {
                    string symbol = currentToken.Value switch
                    {
                        "*" => "\\cdot",
                        "/" => "\\div",
                        "÷R" => "\\div\\mathrm{R}",
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

        // tags a token with the cursor position it begins at; one per token is enough, the side hit picks
        // before or after
        private static string Addressed(string latex, LatexRenderContext context, int cursorIndex)
        {
            if (!context.EmitAddresses) return latex;

            return $"\\htmlData{{p={context.Address(cursorIndex)}}}{{{latex}}}";
        }

        // a slot of a structured token; an empty one shows a box, a cursor in it stands beside the box
        public static string GetSlotLatex(List<MathToken> tokens, LatexRenderContext context, int slotIndex)
        {
            LatexRenderContext slotContext = context.Slot(slotIndex);

            string latex = GetListLatex(tokens, slotContext);
            if (tokens.Count > 0) return latex;

            // the box carries the address of the one position inside, so an empty slot can be clicked into
            return latex + Addressed(EmptySlotLatex, slotContext, 0);
        }
    }


    // a detached deep copy of a token list, for the history line, since the tree is edited on after =
    // (one switch rather than a virtual per token, so a new token type fails loudly here)
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

                case MixedFractionToken mixed:
                    MixedFractionToken mixedCopy = new MixedFractionToken();
                    mixedCopy.WholeTokens.AddRange(CloneList(mixed.WholeTokens));
                    mixedCopy.NumeratorTokens.AddRange(CloneList(mixed.NumeratorTokens));
                    mixedCopy.DenominatorTokens.AddRange(CloneList(mixed.DenominatorTokens));
                    return mixedCopy;

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
                    for (int index = 0; index < function.Arguments.Count; index++)
                    {
                        functionCopy.Arguments[index].AddRange(CloneList(function.Arguments[index]));
                    }
                    return functionCopy;

                case LargeOperatorToken largeOperator:
                    LargeOperatorToken largeOperatorCopy = new LargeOperatorToken(largeOperator.Kind);
                    largeOperatorCopy.LowerTokens.AddRange(CloneList(largeOperator.LowerTokens));
                    largeOperatorCopy.UpperTokens.AddRange(CloneList(largeOperator.UpperTokens));
                    largeOperatorCopy.BodyTokens.AddRange(CloneList(largeOperator.BodyTokens));
                    return largeOperatorCopy;

                case DerivativeToken derivative:
                    DerivativeToken derivativeCopy = new DerivativeToken();
                    derivativeCopy.FunctionTokens.AddRange(CloneList(derivative.FunctionTokens));
                    derivativeCopy.PointTokens.AddRange(CloneList(derivative.PointTokens));
                    return derivativeCopy;

                // the leaves rebuild from their name, which fills their private fields
                case ConstantToken constant: return new ConstantToken(constant.Value);
                case PostfixToken postfix: return new PostfixToken(postfix.Value);
                case AnsToken: return new AnsToken();
                case RandomToken: return new RandomToken();
                case VariableToken: return new VariableToken();
                case RecurringToken recurring: return new RecurringToken(recurring.Value);
            }

            if (token.GetType() != typeof(MathToken))
            {
                throw new NotSupportedException($"no clone for {token.GetType().Name}");
            }

            return new MathToken(token.Type, token.Value) { Seed = token.Seed };
        }
    }
}
