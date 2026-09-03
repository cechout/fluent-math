using System.Collections.Generic;
using System.Linq;
using Calculator_WinUI.Models;

namespace Calculator_WinUI.Engines
{
    public class MathInputManager
    {
        private readonly List<MathToken> _rootTokens = new List<MathToken>();
        private readonly Stack<ScopeContext> _scopeStack = new Stack<ScopeContext>();
        private readonly ScopeContext _rootContext;
        private List<MathToken> CurrentScope => _scopeStack.Peek().Tokens; // the token list we are currently writing into
        private ScopeContext CurrentContext => _scopeStack.Peek(); // the full context object (list + role + cursor position)


        // constructor
        public MathInputManager()
        {
            _rootContext = new ScopeContext(_rootTokens, parentToken: null, ScopeRole.Root);
            ResetToRoot();
        }


        // reset
        public void ResetToRoot()
        {
            _scopeStack.Clear();
            _scopeStack.Push(_rootContext);
            _rootContext.CursorIndex = _rootTokens.Count; // cursor at the end by default
        }

        // modular movement handler 
        // delegates to specific handlers based on the current scope role
        public void Move(NavDirection direction)
        {
            var ctx = CurrentContext;

            // step 1: try to move WITHIN the current scope first
            if (direction == NavDirection.Left)
            {
                // is there a complex token we should step INTO instead of just stepping over?
                if (ctx.CursorIndex > 0)
                {
                    MathToken tokenLeftOfCursor = ctx.Tokens[ctx.CursorIndex - 1];
                    if (TryEnterTokenFromRight(tokenLeftOfCursor)) return;

                    // no enter -> just move cursor one position left
                    ctx.CursorIndex--;
                    return;
                }
                // else: cursor at index 0, fall through to scope-exit logic
            }
            else if (direction == NavDirection.Right)
            {
                if (ctx.CursorIndex < ctx.Tokens.Count)
                {
                    MathToken tokenRightOfCursor = ctx.Tokens[ctx.CursorIndex];
                    if (TryEnterTokenFromLeft(tokenRightOfCursor)) return;

                    // no enter -> just move cursor one position right
                    ctx.CursorIndex++;
                    return;
                }
                // else: cursor at end of list, fall through to scope-exit logic
            }

            // step 2: cursor at edge (or Up/Down) -> use scope-role-specific logic
            if (ctx.Role == ScopeRole.Root) return; // nowhere to exit to from root

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
        // move helper
        // called when cursor sits right of `token` and user pressed Left.
        // returns true if we entered a sub-scope of the token (cursor lands at its END).
        // returns false if the token should be treated as atomic and just skipped over.
        private bool TryEnterTokenFromRight(MathToken token)
        {
            // fractions are atomic for left/right navigation (only Up/Down enters them)
            if (token is FractionToken) return false;

            if (token is PowerToken powerToken)
            {
                var newCtx = new ScopeContext(powerToken.ExponentTokens, powerToken, ScopeRole.Exponent);
                newCtx.CursorIndex = powerToken.ExponentTokens.Count; // land at the end
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

        // called when cursor sits left of `token` and user pressed Right.
        // returns true if we entered a sub-scope of the token (cursor lands at its START).
        private bool TryEnterTokenFromLeft(MathToken token)
        {
            if (token is FractionToken) return false;

            if (token is PowerToken powerToken)
            {
                var newCtx = new ScopeContext(powerToken.ExponentTokens, powerToken, ScopeRole.Exponent);
                newCtx.CursorIndex = 0; // land at the start
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

        // behavior: in numerator
        private void HandleNumeratorNavigation(NavDirection direction, ScopeContext context)
        {
            var fraction = context.ParentToken as FractionToken;
            if (fraction == null) return;

            if (direction == NavDirection.Down)
            {
                // leave numerator, enter denominator of the same fraction
                _scopeStack.Pop();
                var newCtx = new ScopeContext(fraction.DenominatorTokens, fraction, ScopeRole.Denominator);
                newCtx.CursorIndex = 0;
                _scopeStack.Push(newCtx);
            }
            else if (direction == NavDirection.Left)
            {
                // exit fraction to the LEFT: cursor lands BEFORE the fraction token in parent
                _scopeStack.Pop();
                PositionCursorAtParentToken(fraction, before: true);
            }
            else if (direction == NavDirection.Right)
            {
                // exit fraction to the RIGHT: cursor lands AFTER the fraction token in parent
                _scopeStack.Pop();
                PositionCursorAtParentToken(fraction, before: false);
            }
        }
        // behavior: in denominator
        private void HandleDenominatorNavigation(NavDirection direction, ScopeContext context)
        {
            var fraction = context.ParentToken as FractionToken;
            if (fraction == null) return;

            if (direction == NavDirection.Up)
            {
                // leave denominator, enter numerator
                _scopeStack.Pop();
                var newCtx = new ScopeContext(fraction.NumeratorTokens, fraction, ScopeRole.Numerator);
                newCtx.CursorIndex = fraction.NumeratorTokens.Count; // land at end of numerator
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
        // behavior: standard nesting (power, logarithm etc.)
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
            // Up and Down: no effect for linear sub-scopes (like exponent, root radicand)
        }
        // after popping a sub-scope, place the cursor either right before or right after
        // the parent token in the (now current) parent scope.
        private void PositionCursorAtParentToken(MathToken parentToken, bool before)
        {
            var parentCtx = CurrentContext;
            int parentTokenIndex = parentCtx.Tokens.IndexOf(parentToken);
            if (parentTokenIndex == -1) return; // safety: shouldn't happen

            if (before)
            {
                parentCtx.CursorIndex = parentTokenIndex;
            }
            else
            {
                parentCtx.CursorIndex = parentTokenIndex + 1;
            }
        }

        public void AddNumber(string digit)
        {
            var ctx = CurrentContext;

            // check if there is a number token direcly left of the cursor to append to
            MathToken tokenLeftOfCursor;
            if (ctx.CursorIndex > 0) { tokenLeftOfCursor = ctx.Tokens[ctx.CursorIndex - 1]; }
            else { tokenLeftOfCursor = null; }

            if (tokenLeftOfCursor != null && tokenLeftOfCursor.Type == TokenType.Number)
            {
                if (digit == "." && tokenLeftOfCursor.Value.Contains(".")) return;
                tokenLeftOfCursor.Value += digit;
                // cursor index does not change, we just appended a char to an existing token
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

            // don't allow an operator as the first token in a scope (leading + or *)
            if (tokenLeftOfCursor == null) return;

            if (tokenLeftOfCursor.Type == TokenType.Operator)
            {
                // replace: user typed one operator and immediately another; second one wins
                tokenLeftOfCursor.Value = op;
            }
            else
            {
                ctx.Tokens.Insert(ctx.CursorIndex, new MathToken(TokenType.Operator, op));
                ctx.CursorIndex++;
            }
        }

        // creates a power x^y
        public void StartPower()
        {
            var ctx = CurrentContext;
            var powerToken = new PowerToken();

            // check token directly left of cursor, thats the base candidate
            MathToken tokenLeftOfCursor;
            if (ctx.CursorIndex > 0) { tokenLeftOfCursor = ctx.Tokens[ctx.CursorIndex - 1]; }
            else { tokenLeftOfCursor = null; }

            if (tokenLeftOfCursor != null && (tokenLeftOfCursor.Type == TokenType.Number || tokenLeftOfCursor.Type == TokenType.BracketClose))
            {
                // move the token from the parent scope into the powers BaseTokens
                powerToken.BaseTokens.Add(tokenLeftOfCursor);
                ctx.Tokens.RemoveAt(ctx.CursorIndex - 1);
                ctx.CursorIndex--; // parent scope shrank by 1 to the left of cursor
            }

            // insert the power token at the current cursor position and step over it
            ctx.Tokens.Insert(ctx.CursorIndex, powerToken);
            ctx.CursorIndex++;

            // enter the exponent scope, cursor at position 0 (empty)
            _scopeStack.Push(new ScopeContext(powerToken.ExponentTokens, powerToken, ScopeRole.Exponent));
        }
        // creates a root
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
        // creates sin, cos, tan, ln
        public void StartFunction(string name)
        {
            var ctx = CurrentContext;
            var funcToken = new FunctionToken(name);

            ctx.Tokens.Insert(ctx.CursorIndex, funcToken);
            ctx.CursorIndex++;

            _scopeStack.Push(new ScopeContext(funcToken.ParameterTokens, funcToken, ScopeRole.FunctionParameter));
        }
        // creates a fraction
        public void StartFraction()
        {
            var ctx = CurrentContext;
            var fracToken = new FractionToken();

            ctx.Tokens.Insert(ctx.CursorIndex, fracToken);
            ctx.CursorIndex++;

            _scopeStack.Push(new ScopeContext(fracToken.NumeratorTokens, fracToken, ScopeRole.Numerator));
        }
        // creates a logarithm
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


        public void Backspace()
        {
            var ctx = CurrentContext;

            // case 1: cursor is at the very start of a sub-scope (not root)
            // leave the scope and delete the parent token that owned this scope
            if (ctx.CursorIndex == 0 && ctx.Role != ScopeRole.Root)
            {
                MathToken parentToken = ctx.ParentToken;
                _scopeStack.Pop();

                // now we are in the parent scope; the parent token sits right at (cursor - 1)
                // because StartXXX inserted it and stepped the cursor over it.
                var parentCtx = CurrentContext;
                if (parentCtx.CursorIndex > 0 && parentCtx.Tokens[parentCtx.CursorIndex - 1] == parentToken)
                {
                    parentCtx.Tokens.RemoveAt(parentCtx.CursorIndex - 1);
                    parentCtx.CursorIndex--;
                }
                return;
            }

            // case 2: cursor is at start of root scope; nothing to delete
            if (ctx.CursorIndex == 0) return;

            // case 3: normal case, delete/shrink the token directly left of the cursor
            MathToken tokenLeftOfCursor = ctx.Tokens[ctx.CursorIndex - 1];

            // multi-digit number: just shrink the value by one char
            if (tokenLeftOfCursor.Type == TokenType.Number && tokenLeftOfCursor.Value.Length > 1)
            {
                tokenLeftOfCursor.Value = tokenLeftOfCursor.Value.Substring(0, tokenLeftOfCursor.Value.Length - 1);
                // cursor stays where it is
            }
            else
            {
                // single-digit number, operator, or complex token; remove entirely
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

        public string GetLatexString()
        {
            if (_rootTokens.Count == 0) return "0";
            return LatexHelper.GetListLatex(_rootTokens);
        }
    }
}