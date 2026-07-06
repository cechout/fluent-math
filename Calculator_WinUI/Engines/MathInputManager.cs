using System.Collections.Generic;
using System.Linq;
using Calculator_WinUI.Models;

namespace Calculator_WinUI.Engines
{
    public class MathInputManager
    {
        private readonly List<MathToken> _rootTokens = new List<MathToken>();

        // the stack that keeps track of which sublist we are currently writing to
        // the stack now manages context objects instead of just lists of tokens
        private readonly Stack<ScopeContext> _scopeStack = new Stack<ScopeContext>();

        // returns the top List<MathToken> from _scopeStack via Peek() if _scopeStack is not empty.
        // otherwise, returns _rootTokens when no nested scopes are active
        private List<MathToken> CurrentScope
        {
            get
            {
                if (_scopeStack.Count > 0)
                {
                    return _scopeStack.Peek().Tokens;
                }
                else
                {
                    return _rootTokens;
                }
            }
        }

        // return last token from current scope
        private MathToken LastTokenInScope
        {
            get
            {
                return CurrentScope.LastOrDefault();
            }
        }


        // constructor
        public MathInputManager()
        {
            ResetToRoot();
        }


        // reset
        public void ResetToRoot()
        {
            _scopeStack.Clear();
        }

        // modular movement handler 
        // delegates to specific handlers based on the current scope role
        public void Move(NavDirection direction)
        {
            if (_scopeStack.Count == 0) return; // at root level there is no sub-scope to leave

            var currentContext = _scopeStack.Peek();

            // modular movement handler; delegates to specific handlers based on the current scope role
            switch (currentContext.Role)
            {
                case ScopeRole.Numerator:
                    HandleNumeratorNavigation(direction, currentContext);
                    break;

                case ScopeRole.Denominator:
                    HandleDenominatorNavigation(direction, currentContext);
                    break;

                // all standard types (powers, roots, logarithms) behave movement-wise linearly
                case ScopeRole.Exponent:
                case ScopeRole.RootRadicand:
                case ScopeRole.RootIndex:
                case ScopeRole.FunctionParameter:
                case ScopeRole.LogBase:
                case ScopeRole.LogParameter:
                    HandleDefaultLinearNavigation(direction, currentContext);
                    break;
            }
        }
        // behavior: in numerator
        private void HandleNumeratorNavigation(NavDirection direction, ScopeContext context)
        {
            var fraction = context.ParentToken as FractionToken;
            if (fraction == null) return;

            if (direction == NavDirection.Down)
            {
                // new pointer: leave numerator, enter denominator of the same fraction
                _scopeStack.Pop();
                _scopeStack.Push(new ScopeContext(fraction.DenominatorTokens, fraction, ScopeRole.Denominator));
            }
            else if (direction == NavDirection.Right)
            {
                // movement to right just leaves the whole fraction
                _scopeStack.Pop();
            }
            else if (direction == NavDirection.Left)
            {
                _scopeStack.Pop();
            }
        }
        // behavior: in denominator
        private void HandleDenominatorNavigation(NavDirection direction, ScopeContext context)
        {
            var fraction = context.ParentToken as FractionToken;
            if (fraction == null) return;

            if (direction == NavDirection.Up)
            {
                // new pointer: leave denominator, enter numerator of the same fraction
                _scopeStack.Pop();
                _scopeStack.Push(new ScopeContext(fraction.NumeratorTokens, fraction, ScopeRole.Numerator));
            }
            else if (direction == NavDirection.Right || direction == NavDirection.Left)
            {
                // linearly leave the fraction to the right or left
                _scopeStack.Pop();
            }
        }
        // behavior: standard nesting (power, logarithm etc.)
        private void HandleDefaultLinearNavigation(NavDirection direction, ScopeContext context)
        {
            // since powers/roots are built horizontally, they only react to horizontal vectors
            if (direction == NavDirection.Right || direction == NavDirection.Left)
            {
                _scopeStack.Pop(); // leaves the exponent / parameter scope
            }
        }

        public void AddNumber(string digit)
        {
            if (LastTokenInScope != null && LastTokenInScope.Type == TokenType.Number)
            {
                if (digit == "." && LastTokenInScope.Value.Contains(".")) return;
                LastTokenInScope.Value += digit;
            }
            else
            {
                CurrentScope.Add(new MathToken(TokenType.Number, digit));
            }
        }
        public void AddOperator(string op)
        {
            if (LastTokenInScope == null) return;

            if (LastTokenInScope.Type == TokenType.Operator)
            {
                LastTokenInScope.Value = op;
            }
            else
            {
                CurrentScope.Add(new MathToken(TokenType.Operator, op));
            }
        }

        // creates a power x^y
        public void StartPower()
        {
            var powerToken = new PowerToken();
            if (LastTokenInScope != null && (LastTokenInScope.Type == TokenType.Number || LastTokenInScope.Type == TokenType.BracketClose))
            {
                powerToken.BaseTokens.Add(LastTokenInScope);
                CurrentScope.RemoveAt(CurrentScope.Count - 1);
            }
            CurrentScope.Add(powerToken);

            // push with type as exponent; so that we can handle navigation correctly
            _scopeStack.Push(new ScopeContext(powerToken.ExponentTokens, powerToken, ScopeRole.Exponent));
        }
        // creates a root
        public void StartRoot(bool customIndex)
        {
            var rootToken = new RootToken();
            CurrentScope.Add(rootToken);

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
            var funcToken = new FunctionToken(name);
            CurrentScope.Add(funcToken);
            _scopeStack.Push(new ScopeContext(funcToken.ParameterTokens, funcToken, ScopeRole.FunctionParameter));
        }
        // creates a fraction
        public void StartFraction()
        {
            var fracToken = new FractionToken();
            CurrentScope.Add(fracToken);

            // focus first on numerator
            _scopeStack.Push(new ScopeContext(fracToken.NumeratorTokens, fracToken, ScopeRole.Numerator));
        }
        // creates a logarithm
        public void StartLogarithm(bool customBase)
        {
            var logToken = new LogarithmToken();
            CurrentScope.Add(logToken);

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
            // case 1: we are in an empty subsection (empty exponent or smth)
            // we delete the whole math token
            if (CurrentScope.Count == 0 && _scopeStack.Count > 0)
            {
                _scopeStack.Pop();
                if (CurrentScope.Count > 0)
                {
                    CurrentScope.RemoveAt(CurrentScope.Count - 1);
                }
                return;
            }

            // case 2: the current section has tokens 
            if (CurrentScope.Count > 0)
            {
                var lastToken = LastTokenInScope;

                // if token is multi-digit number
                if (lastToken.Type == TokenType.Number && lastToken.Value.Length > 1)
                {
                    lastToken.Value = lastToken.Value.Substring(0, lastToken.Value.Length - 1);
                }
                else
                {
                    // if token is single-digit number of function or whatever
                    CurrentScope.RemoveAt(CurrentScope.Count - 1);
                }
            }
        }

        public void Clear()
        {
            _rootTokens.Clear();
            _scopeStack.Clear();
        }

        public string GetLatexString()
        {
            if (_rootTokens.Count == 0) return "0";
            return LatexHelper.GetListLatex(_rootTokens);
        }
    }
}