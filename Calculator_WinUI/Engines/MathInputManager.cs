using System.Collections.Generic;
using System.Linq;
using Calculator_WinUI.Models;

namespace Calculator_WinUI.Engines
{
    // the whole input model of the calculator: it owns the token tree the user is building and the
    // cursor that walks through it, and it is the only thing allowed to mutate either
    //
    // the tree is not flat text, so a cursor cannot be a character offset; instead every editable slot
    // (a numerator, an exponent, a function argument) is a ScopeContext with its own token list and its
    // own cursor index, and those contexts live on a stack whose top is whatever the user is typing into
    // right now; that is what makes nesting work without any position arithmetic
    //
    // the class knows nothing about buttons or LaTeX beyond GetLatexString; StandardViewModel does the
    // translating in both directions
    public class MathInputManager
    {
        // === fields ===

        private readonly List<MathToken> _rootTokens = new List<MathToken>();
        private readonly Stack<ScopeContext> _scopeStack = new Stack<ScopeContext>();

        // the outermost scope, kept as a field so resetting can push the same instance back instead of
        // building a new one around the same _rootTokens list
        private readonly ScopeContext _rootContext;

        private List<MathToken> CurrentScope => _scopeStack.Peek().Tokens; // the token list being written into
        private ScopeContext CurrentContext => _scopeStack.Peek(); // that list plus its role and cursor position


        // === constructor ===

        public MathInputManager()
        {
            _rootContext = new ScopeContext(_rootTokens, parentToken: null, ScopeRole.Root);
            ResetToRoot();
        }

        // drops back out of every nested scope without touching what was typed
        public void ResetToRoot()
        {
            _scopeStack.Clear();
            _scopeStack.Push(_rootContext);
            _rootContext.CursorIndex = _rootTokens.Count;
        }


        // === cursor movement ===

        // moving inside the current scope always wins; only once the cursor is already at a scope edge,
        // or the direction is Up/Down, does the scopes own role decide where it goes next
        public void Move(NavDirection direction)
        {
            var ctx = CurrentContext;

            // step 1: try to move within the current scope
            if (direction == NavDirection.Left)
            {
                // is there a structured token here we should step into rather than skip over?
                if (ctx.CursorIndex > 0)
                {
                    MathToken tokenLeftOfCursor = ctx.Tokens[ctx.CursorIndex - 1];
                    if (TryEnterTokenFromRight(tokenLeftOfCursor)) return;

                    ctx.CursorIndex--;
                    return;
                }
                // cursor sits at index 0, fall through to the scope-exit logic
            }
            else if (direction == NavDirection.Right)
            {
                if (ctx.CursorIndex < ctx.Tokens.Count)
                {
                    MathToken tokenRightOfCursor = ctx.Tokens[ctx.CursorIndex];
                    if (TryEnterTokenFromLeft(tokenRightOfCursor)) return;

                    ctx.CursorIndex++;
                    return;
                }
                // cursor sits at the end of the list, fall through to the scope-exit logic
            }

            // step 2: at a scope edge, or Up/Down, so hand over to the role-specific handler
            if (ctx.Role == ScopeRole.Root) return; // root has nowhere to exit to

            switch (ctx.Role)
            {
                case ScopeRole.Numerator:
                    HandleNumeratorNavigation(direction, ctx);
                    break;

                case ScopeRole.Denominator:
                    HandleDenominatorNavigation(direction, ctx);
                    break;

                case ScopeRole.Exponent:
                case ScopeRole.RootRadicand:
                case ScopeRole.RootIndex:
                case ScopeRole.FunctionParameter:
                case ScopeRole.LogBase:
                case ScopeRole.LogParameter:
                    HandleDefaultLinearNavigation(direction, ctx);
                    break;
            }
        }

        // both helpers below follow the same shape for every structured token: push the sub-scope the
        // cursor should land in and park the cursor at the far end, so entering from the right starts at
        // the end of the slot and entering from the left starts at its beginning
        //
        // a fraction is deliberately missing from both; it is atomic for Left/Right and only Up/Down
        // ever crosses the bar, otherwise walking past a fraction would force the user through both halves

        // cursor sits right of the token and the user pressed Left
        // returns false when the token should be treated as atomic and simply stepped over
        private bool TryEnterTokenFromRight(MathToken token)
        {
            if (token is FractionToken) return false;

            if (token is PowerToken powerToken)
            {
                var newCtx = new ScopeContext(powerToken.ExponentTokens, powerToken, ScopeRole.Exponent);
                newCtx.CursorIndex = powerToken.ExponentTokens.Count;
                _scopeStack.Push(newCtx);
                return true;
            }
            if (token is RootToken rootToken)
            {
                var newCtx = new ScopeContext(rootToken.RadicandTokens, rootToken, ScopeRole.RootRadicand);
                newCtx.CursorIndex = rootToken.RadicandTokens.Count;
                _scopeStack.Push(newCtx);
                return true;
            }
            if (token is FunctionToken funcToken)
            {
                var newCtx = new ScopeContext(funcToken.ParameterTokens, funcToken, ScopeRole.FunctionParameter);
                newCtx.CursorIndex = funcToken.ParameterTokens.Count;
                _scopeStack.Push(newCtx);
                return true;
            }
            if (token is LogarithmToken logToken)
            {
                var newCtx = new ScopeContext(logToken.ParameterTokens, logToken, ScopeRole.LogParameter);
                newCtx.CursorIndex = logToken.ParameterTokens.Count;
                _scopeStack.Push(newCtx);
                return true;
            }
            return false;
        }

        // cursor sits left of the token and the user pressed Right
        private bool TryEnterTokenFromLeft(MathToken token)
        {
            if (token is FractionToken) return false;

            if (token is PowerToken powerToken)
            {
                var newCtx = new ScopeContext(powerToken.ExponentTokens, powerToken, ScopeRole.Exponent);
                newCtx.CursorIndex = 0;
                _scopeStack.Push(newCtx);
                return true;
            }
            if (token is RootToken rootToken)
            {
                var newCtx = new ScopeContext(rootToken.RadicandTokens, rootToken, ScopeRole.RootRadicand);
                newCtx.CursorIndex = 0;
                _scopeStack.Push(newCtx);
                return true;
            }
            if (token is FunctionToken funcToken)
            {
                var newCtx = new ScopeContext(funcToken.ParameterTokens, funcToken, ScopeRole.FunctionParameter);
                newCtx.CursorIndex = 0;
                _scopeStack.Push(newCtx);
                return true;
            }
            if (token is LogarithmToken logToken)
            {
                var newCtx = new ScopeContext(logToken.ParameterTokens, logToken, ScopeRole.LogParameter);
                newCtx.CursorIndex = 0;
                _scopeStack.Push(newCtx);
                return true;
            }
            return false;
        }

        // the two fraction halves are the only scopes where Up/Down means something: they swap sides of
        // the same fraction instead of leaving it
        private void HandleNumeratorNavigation(NavDirection direction, ScopeContext context)
        {
            var fraction = context.ParentToken as FractionToken;
            if (fraction == null) return;

            if (direction == NavDirection.Down)
            {
                _scopeStack.Pop();
                var newCtx = new ScopeContext(fraction.DenominatorTokens, fraction, ScopeRole.Denominator);
                newCtx.CursorIndex = 0;
                _scopeStack.Push(newCtx);
            }
            else if (direction == NavDirection.Left)
            {
                _scopeStack.Pop();
                PositionCursorAtParentToken(fraction, before: true);
            }
            else if (direction == NavDirection.Right)
            {
                _scopeStack.Pop();
                PositionCursorAtParentToken(fraction, before: false);
            }
        }

        private void HandleDenominatorNavigation(NavDirection direction, ScopeContext context)
        {
            var fraction = context.ParentToken as FractionToken;
            if (fraction == null) return;

            if (direction == NavDirection.Up)
            {
                _scopeStack.Pop();
                var newCtx = new ScopeContext(fraction.NumeratorTokens, fraction, ScopeRole.Numerator);
                newCtx.CursorIndex = fraction.NumeratorTokens.Count; // coming from below, land at the end
                _scopeStack.Push(newCtx);
            }
            else if (direction == NavDirection.Left)
            {
                _scopeStack.Pop();
                PositionCursorAtParentToken(fraction, before: true);
            }
            else if (direction == NavDirection.Right)
            {
                _scopeStack.Pop();
                PositionCursorAtParentToken(fraction, before: false);
            }
        }

        // exponents, radicands, function arguments: everything that reads as one line, so only
        // Left and Right can leave it and Up/Down are ignored
        private void HandleDefaultLinearNavigation(NavDirection direction, ScopeContext context)
        {
            MathToken parent = context.ParentToken;

            if (direction == NavDirection.Left)
            {
                _scopeStack.Pop();
                PositionCursorAtParentToken(parent, before: true);
            }
            else if (direction == NavDirection.Right)
            {
                _scopeStack.Pop();
                PositionCursorAtParentToken(parent, before: false);
            }
        }

        // after a pop, drop the cursor either directly before or directly after the token that owned the
        // scope we just left, so it comes out on the side it was heading towards
        private void PositionCursorAtParentToken(MathToken parentToken, bool before)
        {
            var parentCtx = CurrentContext;
            int parentTokenIndex = parentCtx.Tokens.IndexOf(parentToken);
            if (parentTokenIndex == -1) return; // safety, a scope always sits in its parents list

            if (before)
            {
                parentCtx.CursorIndex = parentTokenIndex;
            }
            else
            {
                parentCtx.CursorIndex = parentTokenIndex + 1;
            }
        }


        // === plain input ===

        // digits grow the number token left of the cursor instead of piling up one token per keystroke,
        // which is what keeps "125" a single number rather than three
        public void AddNumber(string digit)
        {
            var ctx = CurrentContext;

            MathToken tokenLeftOfCursor;
            if (ctx.CursorIndex > 0) { tokenLeftOfCursor = ctx.Tokens[ctx.CursorIndex - 1]; }
            else { tokenLeftOfCursor = null; }

            if (tokenLeftOfCursor != null && tokenLeftOfCursor.Type == TokenType.Number)
            {
                if (digit == "." && tokenLeftOfCursor.Value.Contains(".")) return; // one decimal point per number
                tokenLeftOfCursor.Value += digit;
                // no cursor change, a character was appended inside a token that was already there
            }
            else
            {
                ctx.Tokens.Insert(ctx.CursorIndex, new MathToken(TokenType.Number, digit));
                ctx.CursorIndex++;
            }
        }

        public void AddOperator(string op)
        {
            var ctx = CurrentContext;

            MathToken tokenLeftOfCursor;
            if (ctx.CursorIndex > 0) { tokenLeftOfCursor = ctx.Tokens[ctx.CursorIndex - 1]; }
            else { tokenLeftOfCursor = null; }

            // an operator needs something on its left to work on; the sole exception is a minus, which
            // reads as a sign there and is the only way a negative number can be typed at all
            if (tokenLeftOfCursor == null || tokenLeftOfCursor.Type == TokenType.BracketOpen)
            {
                if (op != "-") return;

                ctx.Tokens.Insert(ctx.CursorIndex, new MathToken(TokenType.Operator, op));
                ctx.CursorIndex++;
                return;
            }

            if (tokenLeftOfCursor.Type == TokenType.Operator)
            {
                // two operators in a row is a correction, not an input; the newer one wins
                tokenLeftOfCursor.Value = op;
            }
            else
            {
                ctx.Tokens.Insert(ctx.CursorIndex, new MathToken(TokenType.Operator, op));
                ctx.CursorIndex++;
            }
        }

        public void AddConstant(string name)
        {
            var ctx = CurrentContext;

            ctx.Tokens.Insert(ctx.CursorIndex, new ConstantToken(name));
            ctx.CursorIndex++;
        }

        // brackets stay flat tokens in the list rather than a scope of their own, the way they do on a
        // pocket calculator; the evaluator is what pairs them up again
        public void AddBracket(bool open)
        {
            var ctx = CurrentContext;

            MathToken bracket;
            if (open) { bracket = new MathToken(TokenType.BracketOpen, "("); }
            else { bracket = new MathToken(TokenType.BracketClose, ")"); }

            ctx.Tokens.Insert(ctx.CursorIndex, bracket);
            ctx.CursorIndex++;
        }


        // === structured input ===

        // every StartXXX below inserts its token at the cursor, steps the cursor over it, and then pushes
        // the slot the user is expected to fill next; Backspace relies on exactly that order

        public void StartPower()
        {
            var ctx = CurrentContext;
            var powerToken = new PowerToken();

            // a power typed after a number should raise that number rather than open an empty base, so
            // the token to the left is pulled out of the parent scope and becomes the base
            MathToken tokenLeftOfCursor;
            if (ctx.CursorIndex > 0) { tokenLeftOfCursor = ctx.Tokens[ctx.CursorIndex - 1]; }
            else { tokenLeftOfCursor = null; }

            if (tokenLeftOfCursor != null && tokenLeftOfCursor.Type == TokenType.BracketClose)
            {
                // the base of (1+2) squared is the whole group, so the search runs back to the partner
                // bracket; an unmatched closing bracket leaves the base empty rather than eating the list
                int groupStart = FindMatchingBracketOpen(ctx.Tokens, ctx.CursorIndex - 1);
                if (groupStart >= 0)
                {
                    int groupLength = ctx.CursorIndex - groupStart;
                    powerToken.BaseTokens.AddRange(ctx.Tokens.GetRange(groupStart, groupLength));
                    ctx.Tokens.RemoveRange(groupStart, groupLength);
                    ctx.CursorIndex -= groupLength;
                }
            }
            else if (tokenLeftOfCursor != null && (tokenLeftOfCursor.Type == TokenType.Number || tokenLeftOfCursor.Type == TokenType.Constant))
            {
                powerToken.BaseTokens.Add(tokenLeftOfCursor);
                ctx.Tokens.RemoveAt(ctx.CursorIndex - 1);
                ctx.CursorIndex--; // the parent scope just lost a token left of the cursor
            }

            ctx.Tokens.Insert(ctx.CursorIndex, powerToken);
            ctx.CursorIndex++;

            _scopeStack.Push(new ScopeContext(powerToken.ExponentTokens, powerToken, ScopeRole.Exponent));
        }

        // walks left from a closing bracket to its partner so the whole group can be treated as one
        // operand; returns -1 when the opening bracket was never typed
        private static int FindMatchingBracketOpen(List<MathToken> tokens, int closeIndex)
        {
            int depth = 0;

            for (int i = closeIndex; i >= 0; i--)
            {
                if (tokens[i].Type == TokenType.BracketClose)
                {
                    depth++;
                }
                else if (tokens[i].Type == TokenType.BracketOpen)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }

            return -1;
        }

        // customIndex picks which slot the user lands in: the index of an nth root, or the radicand of a
        // plain square root
        public void StartRoot(bool customIndex)
        {
            var ctx = CurrentContext;
            var rootToken = new RootToken();

            ctx.Tokens.Insert(ctx.CursorIndex, rootToken);
            ctx.CursorIndex++;

            if (customIndex)
            {
                _scopeStack.Push(new ScopeContext(rootToken.IndexTokens, rootToken, ScopeRole.RootIndex));
            }
            else
            {
                _scopeStack.Push(new ScopeContext(rootToken.RadicandTokens, rootToken, ScopeRole.RootRadicand));
            }
        }

        // sin, cos, tan, ln; the name is passed straight through to LaTeX as a command
        public void StartFunction(string name)
        {
            var ctx = CurrentContext;
            var funcToken = new FunctionToken(name);

            ctx.Tokens.Insert(ctx.CursorIndex, funcToken);
            ctx.CursorIndex++;

            _scopeStack.Push(new ScopeContext(funcToken.ParameterTokens, funcToken, ScopeRole.FunctionParameter));
        }

        public void StartFraction()
        {
            var ctx = CurrentContext;
            var fracToken = new FractionToken();

            ctx.Tokens.Insert(ctx.CursorIndex, fracToken);
            ctx.CursorIndex++;

            _scopeStack.Push(new ScopeContext(fracToken.NumeratorTokens, fracToken, ScopeRole.Numerator));
        }

        // customBase starts in the subscript for log_b(x), otherwise straight in the argument
        public void StartLogarithm(bool customBase)
        {
            var ctx = CurrentContext;
            var logToken = new LogarithmToken();

            ctx.Tokens.Insert(ctx.CursorIndex, logToken);
            ctx.CursorIndex++;

            if (customBase)
            {
                _scopeStack.Push(new ScopeContext(logToken.BaseTokens, logToken, ScopeRole.LogBase));
            }
            else
            {
                _scopeStack.Push(new ScopeContext(logToken.ParameterTokens, logToken, ScopeRole.LogParameter));
            }
        }


        // === editing ===

        // deleting backwards out of an empty structure has to remove the structure itself, otherwise a
        // mistyped fraction could never be undone from inside it
        public void Backspace()
        {
            var ctx = CurrentContext;

            // at the start of a sub-scope: leave it and delete the token that owned it
            if (ctx.CursorIndex == 0 && ctx.Role != ScopeRole.Root)
            {
                MathToken parentToken = ctx.ParentToken;
                _scopeStack.Pop();

                // the owning token sits directly left of the cursor in the parent scope, because every
                // StartXXX inserted it there and then stepped over it
                var parentCtx = CurrentContext;
                if (parentCtx.CursorIndex > 0 && parentCtx.Tokens[parentCtx.CursorIndex - 1] == parentToken)
                {
                    parentCtx.Tokens.RemoveAt(parentCtx.CursorIndex - 1);
                    parentCtx.CursorIndex--;
                }
                return;
            }

            // at the start of the root scope there is nothing left to delete
            if (ctx.CursorIndex == 0) return;

            MathToken tokenLeftOfCursor = ctx.Tokens[ctx.CursorIndex - 1];

            // a multi-digit number loses one character and stays a token, mirroring how AddNumber built it
            if (tokenLeftOfCursor.Type == TokenType.Number && tokenLeftOfCursor.Value.Length > 1)
            {
                tokenLeftOfCursor.Value = tokenLeftOfCursor.Value.Substring(0, tokenLeftOfCursor.Value.Length - 1);
            }
            else
            {
                // single digit, operator or a whole structure; goes away in one piece
                ctx.Tokens.RemoveAt(ctx.CursorIndex - 1);
                ctx.CursorIndex--;
            }
        }

        public void Clear()
        {
            _rootTokens.Clear();
            _scopeStack.Clear();
            _scopeStack.Push(_rootContext);
            _rootContext.CursorIndex = 0;
        }

        // drops everything and leaves a single number behind, which is how a finished result is carried
        // into the calculation that continues from it
        public void SeedWithValue(string numberText)
        {
            Clear();
            _rootTokens.Add(new MathToken(TokenType.Number, numberText));
            _rootContext.CursorIndex = _rootTokens.Count;
        }


        // === output ===

        // read-only view of the tree for the evaluator; this class stays the only thing that mutates it
        public IReadOnlyList<MathToken> RootTokens => _rootTokens;

        // empty input renders as "0" so the display is never blank
        //
        // the cursor belongs to the line being typed, so the history line asks for the same formula
        // without it
        public string GetLatexString(bool withCursor = true)
        {
            ScopeContext? activeScope = null;
            if (withCursor) activeScope = CurrentContext;

            if (_rootTokens.Count == 0)
            {
                if (withCursor) return "0" + LatexHelper.CursorLatex;
                return "0";
            }

            return LatexHelper.GetListLatex(_rootTokens, activeScope);
        }
    }
}
