using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FluentMath.Models;

namespace FluentMath.Engines
{
    // the input model:
    // owns the token tree and the cursor, and is the only thing that mutates either; every editable slot
    // is a ScopeContext on a stack whose top is the slot being typed into
    //
    // the input is a sandbox: nothing typed is refused, corrected or rearranged, however broken; the one
    // place a formula is judged is MathEvaluator on =, and a broken one is a Syntax ERROR to walk back into
    public class MathInputManager
    {
        // === fields ===

        private readonly List<MathToken> _rootTokens = new List<MathToken>();
        private readonly Stack<ScopeContext> _scopeStack = new Stack<ScopeContext>();

        // the outermost scope, kept so a reset pushes the same instance back
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

        // moving inside the current scope wins; at a scope edge, or for Up and Down, the scope role decides
        // (the call after the move keeps the cursor off a position drawn on top of its neighbour)
        public void Move(NavDirection direction)
        {
            MoveOnce(direction);

            EnterTokensThatBeginWithTheirFirstSlot();
        }

        private void MoveOnce(NavDirection direction)
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

            // step 2: at a scope edge, or Up/Down, so hand over to the slot walker
            if (ctx.Role == ScopeRole.Root) return; // root has nowhere to exit to

            HandleSlotNavigation(direction, ctx);
        }

        // both helpers below push the slot the cursor lands in, at the end it arrives from; a fraction too,
        // so both halves are reachable without Up and Down

        // cursor sits right of the token and the user pressed Left
        // returns false when the token should be treated as atomic and simply stepped over
        private bool TryEnterTokenFromRight(MathToken token)
        {
            List<TokenSlot> slots = GetSlots(token);
            if (slots.Count == 0) return false;

            PushScope(slots[slots.Count - 1], token, atEnd: true);
            return true;
        }

        // cursor sits left of the token and the user pressed Right
        private bool TryEnterTokenFromLeft(MathToken token)
        {
            List<TokenSlot> slots = GetSlots(token);
            if (slots.Count == 0) return false;

            PushScope(slots[0], token, atEnd: false);
            return true;
        }

        // a power and a mixed fraction draw nothing in front of their first slot, so the place before the
        // token and the first place inside are one spot on screen
        // (MathLayoutEngine.BuildPower and BuildMixedFraction make that true and move together with this)
        private static bool BeginsWithItsFirstSlot(MathToken token)
        {
            return token is PowerToken || token is MixedFractionToken;
        }

        // the cursor never stands in front of such a token but in its slot, where typing joins the number
        // on screen
        private void EnterTokensThatBeginWithTheirFirstSlot()
        {
            while (true)
            {
                var ctx = CurrentContext;
                if (ctx.CursorIndex >= ctx.Tokens.Count) return;

                MathToken token = ctx.Tokens[ctx.CursorIndex];
                if (!BeginsWithItsFirstSlot(token)) return;
                if (!TryEnterTokenFromLeft(token)) return;
            }
        }

        // Left and Right walk to the neighbouring slot of the same token first, so a root index, a log base
        // or the far half of a fraction is reachable with the arrow keys alone
        private void HandleSlotNavigation(NavDirection direction, ScopeContext context)
        {
            if (direction == NavDirection.Left)
            {
                if (TryMoveToNeighbourSlot(context, next: false)) return;

                _scopeStack.Pop();
                PositionCursorAtParentToken(context.ParentToken, before: true);

                // out of a first slot the token begins with, that is the place just left, so the move carries on
                if (BeginsWithItsFirstSlot(context.ParentToken)) MoveOnce(NavDirection.Left);
                return;
            }

            if (direction == NavDirection.Right)
            {
                if (TryMoveToNeighbourSlot(context, next: true)) return;

                _scopeStack.Pop();
                PositionCursorAtParentToken(context.ParentToken, before: false);
                return;
            }

            TrySwitchVerticalSlot(direction, context);
        }

        // which two slots sit above each other differs per token: an exponent is above its base, but a
        // root index is above its radicand and a logarithm base below its argument
        private void TrySwitchVerticalSlot(NavDirection direction, ScopeContext context)
        {
            ScopeRole upper;
            ScopeRole lower;

            switch (context.ParentToken)
            {
                // the whole part of a mixed fraction has nothing above or below it, the same as the number
                // in front of a plain fraction
                case FractionToken:
                case MixedFractionToken:
                    upper = ScopeRole.Numerator;
                    lower = ScopeRole.Denominator;
                    break;

                case PowerToken:
                    upper = ScopeRole.Exponent;
                    lower = ScopeRole.PowerBase;
                    break;

                case RootToken:
                    upper = ScopeRole.RootIndex;
                    lower = ScopeRole.RootRadicand;
                    break;

                case LogarithmToken:
                    upper = ScopeRole.LogParameter;
                    lower = ScopeRole.LogBase;
                    break;

                // the body of Σ, Π and the integral stands beside both bounds, so it has nothing above or
                // below it either
                case LargeOperatorToken:
                    upper = ScopeRole.UpperBound;
                    lower = ScopeRole.LowerBound;
                    break;

                case DerivativeToken:
                    upper = ScopeRole.CalculusBody;
                    lower = ScopeRole.DerivativePoint;
                    break;

                default:
                    return; // a function argument stands alone, so Up and Down do nothing there
            }

            ScopeRole target;
            if (direction == NavDirection.Up && context.Role == lower) { target = upper; }
            else if (direction == NavDirection.Down && context.Role == upper) { target = lower; }
            else { return; }

            TokenSlot? slot = GetSlots(context.ParentToken).Find(candidate => candidate.Role == target);
            if (slot == null) return;

            // coming from below lands at the end of the slot above, the same way the fraction halves behave
            SwitchToSlot(slot, context.ParentToken, atEnd: direction == NavDirection.Up);
        }


        // === slots ===

        // the slots of a structured token in reading order, the one list every slot walk reads
        // (the order is the slot index of a click address, which MathToken and the layout hardcode per token)
        internal static List<TokenSlot> GetSlots(MathToken token)
        {
            switch (token)
            {
                case FractionToken fraction:
                    return new List<TokenSlot>
                    {
                        new TokenSlot(fraction.NumeratorTokens, ScopeRole.Numerator),
                        new TokenSlot(fraction.DenominatorTokens, ScopeRole.Denominator)
                    };

                case MixedFractionToken mixed:
                    return new List<TokenSlot>
                    {
                        new TokenSlot(mixed.WholeTokens, ScopeRole.WholePart),
                        new TokenSlot(mixed.NumeratorTokens, ScopeRole.Numerator),
                        new TokenSlot(mixed.DenominatorTokens, ScopeRole.Denominator)
                    };

                case PowerToken power:
                    return new List<TokenSlot>
                    {
                        new TokenSlot(power.BaseTokens, ScopeRole.PowerBase),
                        new TokenSlot(power.ExponentTokens, ScopeRole.Exponent)
                    };

                case RootToken root:
                    return new List<TokenSlot>
                    {
                        new TokenSlot(root.IndexTokens, ScopeRole.RootIndex),
                        new TokenSlot(root.RadicandTokens, ScopeRole.RootRadicand)
                    };

                case LogarithmToken logarithm:
                    return new List<TokenSlot>
                    {
                        new TokenSlot(logarithm.BaseTokens, ScopeRole.LogBase),
                        new TokenSlot(logarithm.ParameterTokens, ScopeRole.LogParameter)
                    };

                // one slot per argument, side by side; Left and Right walk them and Up and Down have
                // nothing to cross
                case FunctionToken function:
                    List<TokenSlot> arguments = new List<TokenSlot>();
                    foreach (List<MathToken> argument in function.Arguments)
                    {
                        arguments.Add(new TokenSlot(argument, ScopeRole.FunctionParameter));
                    }
                    return arguments;

                case LargeOperatorToken largeOperator:
                    return new List<TokenSlot>
                    {
                        new TokenSlot(largeOperator.LowerTokens, ScopeRole.LowerBound),
                        new TokenSlot(largeOperator.UpperTokens, ScopeRole.UpperBound),
                        new TokenSlot(largeOperator.BodyTokens, ScopeRole.CalculusBody)
                    };

                case DerivativeToken derivative:
                    return new List<TokenSlot>
                    {
                        new TokenSlot(derivative.FunctionTokens, ScopeRole.CalculusBody),
                        new TokenSlot(derivative.PointTokens, ScopeRole.DerivativePoint)
                    };
            }

            return new List<TokenSlot>();
        }

        // the slot next to the one the cursor is in, or null when the current slot is the outermost one
        // on that side and the cursor has to leave the token instead
        private static TokenSlot? GetNeighbourSlot(ScopeContext context, bool next)
        {
            if (context.ParentToken == null) return null;

            List<TokenSlot> slots = GetSlots(context.ParentToken);
            int index = slots.FindIndex(slot => ReferenceEquals(slot.Tokens, context.Tokens));
            if (index < 0) return null;

            int neighbourIndex = next ? index + 1 : index - 1;
            if (neighbourIndex < 0 || neighbourIndex >= slots.Count) return null;

            return slots[neighbourIndex];
        }

        // an empty neighbour is not skipped; it is the only way into a blank root index or log base
        private bool TryMoveToNeighbourSlot(ScopeContext context, bool next)
        {
            TokenSlot? neighbour = GetNeighbourSlot(context, next);
            if (neighbour == null || context.ParentToken == null) return false;

            SwitchToSlot(neighbour, context.ParentToken, atEnd: !next);
            return true;
        }

        // pushes a slot as the scope being typed into, with the cursor parked at the end the cursor is
        // arriving from
        private void PushScope(TokenSlot slot, MathToken parentToken, bool atEnd)
        {
            var context = new ScopeContext(slot.Tokens, parentToken, slot.Role);
            if (atEnd) context.CursorIndex = slot.Tokens.Count;

            _scopeStack.Push(context);
        }

        // swaps the top of the stack for another slot of the same token
        private void SwitchToSlot(TokenSlot slot, MathToken parentToken, bool atEnd)
        {
            _scopeStack.Pop();
            PushScope(slot, parentToken, atEnd);
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

        // one token per digit, so the cursor can stand between any two; the evaluator reads the run as one value
        public void AddNumber(string digit)
        {
            var ctx = CurrentContext;

            ctx.Tokens.Insert(ctx.CursorIndex, new MathToken(TokenType.Number, digit));
            ctx.CursorIndex++;
        }

        // every operator goes in where the cursor is, whatever stands beside it
        public void AddOperator(string op)
        {
            var ctx = CurrentContext;

            ctx.Tokens.Insert(ctx.CursorIndex, new MathToken(TokenType.Operator, op));
            ctx.CursorIndex++;
        }

        public void AddConstant(string name)
        {
            var ctx = CurrentContext;

            ctx.Tokens.Insert(ctx.CursorIndex, new ConstantToken(name));
            ctx.CursorIndex++;
        }

        public void AddAns()
        {
            var ctx = CurrentContext;

            ctx.Tokens.Insert(ctx.CursorIndex, new AnsToken());
            ctx.CursorIndex++;
        }

        public void AddRandom()
        {
            var ctx = CurrentContext;

            ctx.Tokens.Insert(ctx.CursorIndex, new RandomToken());
            ctx.CursorIndex++;
        }

        public void AddVariable()
        {
            var ctx = CurrentContext;

            ctx.Tokens.Insert(ctx.CursorIndex, new VariableToken());
            ctx.CursorIndex++;
        }

        public void AddPostfix(string kind)
        {
            var ctx = CurrentContext;

            ctx.Tokens.Insert(ctx.CursorIndex, new PostfixToken(kind));
            ctx.CursorIndex++;
        }

        // the °′″ key, one key for all three markers the way a Casio types 2°30′15″: a number standing
        // behind degrees gets minutes, one behind minutes gets seconds, anything else degrees
        //
        // the first marker is therefore always degrees, so minutes alone are typed as 0°39′
        public void AddSexagesimalMarker()
        {
            var ctx = CurrentContext;

            int start = ctx.CursorIndex;
            while (start > 0 && ctx.Tokens[start - 1].Type == TokenType.Number) start--;

            string kind = "degrees";
            if (start < ctx.CursorIndex && start > 0 && ctx.Tokens[start - 1] is PostfixToken marker)
            {
                if (marker.Value == "degrees") kind = "minutes";
                else if (marker.Value == "minutes") kind = "seconds";
            }

            AddPostfix(kind);
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

            // the operand on the left becomes the base
            MoveOperandIntoSlot(ctx, powerToken.BaseTokens);

            ctx.Tokens.Insert(ctx.CursorIndex, powerToken);
            ctx.CursorIndex++;

            _scopeStack.Push(new ScopeContext(powerToken.ExponentTokens, powerToken, ScopeRole.Exponent));
        }

        // the e to the x key; unlike StartPower it leaves the left alone, since the key names its base
        public void StartPowerOfE()
        {
            var ctx = CurrentContext;
            var powerToken = new PowerToken();

            powerToken.BaseTokens.Add(new ConstantToken("e"));

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

        // the run of tokens left of the cursor that reads as one operand, back to the nearest operator,
        // opening bracket or scope start; a bracket group counts as one piece
        // (cursorIndex itself when there is nothing to take)
        private static int FindOperandStart(List<MathToken> tokens, int cursorIndex)
        {
            int start = cursorIndex;

            while (start > 0)
            {
                MathToken token = tokens[start - 1];

                if (token.Type == TokenType.Operator || token.Type == TokenType.BracketOpen) break;

                if (token.Type == TokenType.BracketClose)
                {
                    // an unmatched closing bracket stops the search rather than eating the whole list
                    int groupStart = FindMatchingBracketOpen(tokens, start - 1);
                    if (groupStart < 0) break;

                    start = groupStart;
                    continue;
                }

                start--;
            }

            return start;
        }

        // moves that operand into the slot a structured token is about to own; returns whether it took any
        private static bool MoveOperandIntoSlot(ScopeContext context, List<MathToken> slot)
        {
            int start = FindOperandStart(context.Tokens, context.CursorIndex);
            int length = context.CursorIndex - start;
            if (length == 0) return false;

            slot.AddRange(context.Tokens.GetRange(start, length));
            context.Tokens.RemoveRange(start, length);
            context.CursorIndex = start; // the parent scope just lost everything left of the cursor

            return true;
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

        // the EXP key: builds exactly what times, one, zero and power build
        public void StartScientific()
        {
            var ctx = CurrentContext;

            ctx.Tokens.Insert(ctx.CursorIndex, new MathToken(TokenType.Operator, "*"));
            ctx.CursorIndex++;
            ctx.Tokens.Insert(ctx.CursorIndex, new MathToken(TokenType.Number, "1"));
            ctx.CursorIndex++;
            ctx.Tokens.Insert(ctx.CursorIndex, new MathToken(TokenType.Number, "0"));
            ctx.CursorIndex++;

            // pulls the ten it just typed into the base, the same way it would pull a hand typed one
            StartPower();
        }

        // sin, cos, tan, ln and every other named function; the cursor opens in the first argument, and
        // FunctionToken decides from the name how many there are and how the function is drawn
        public void StartFunction(string name)
        {
            var ctx = CurrentContext;
            var funcToken = new FunctionToken(name);

            ctx.Tokens.Insert(ctx.CursorIndex, funcToken);
            ctx.CursorIndex++;

            _scopeStack.Push(new ScopeContext(funcToken.ParameterTokens, funcToken, ScopeRole.FunctionParameter));
        }

        // the operand on the left becomes the numerator and the cursor drops into the denominator, as on
        // a Casio (5 + 45 lifts the 45); with nothing to lift it opens in the numerator
        public void StartFraction()
        {
            var ctx = CurrentContext;
            var fracToken = new FractionToken();

            bool captured = MoveOperandIntoSlot(ctx, fracToken.NumeratorTokens);

            ctx.Tokens.Insert(ctx.CursorIndex, fracToken);
            ctx.CursorIndex++;

            if (captured)
            {
                _scopeStack.Push(new ScopeContext(fracToken.DenominatorTokens, fracToken, ScopeRole.Denominator));
                return;
            }

            _scopeStack.Push(new ScopeContext(fracToken.NumeratorTokens, fracToken, ScopeRole.Numerator));
        }

        // the same with the whole part: 2 and the key make the 2 the whole part, the cursor in the numerator
        // (a minus in front stays outside; with nothing to lift it opens in the whole part)
        public void StartMixedFraction()
        {
            var ctx = CurrentContext;
            var mixedToken = new MixedFractionToken();

            bool captured = MoveOperandIntoSlot(ctx, mixedToken.WholeTokens);

            ctx.Tokens.Insert(ctx.CursorIndex, mixedToken);
            ctx.CursorIndex++;

            if (captured)
            {
                _scopeStack.Push(new ScopeContext(mixedToken.NumeratorTokens, mixedToken, ScopeRole.Numerator));
                return;
            }

            _scopeStack.Push(new ScopeContext(mixedToken.WholeTokens, mixedToken, ScopeRole.WholePart));
        }

        // Σ, Π and the integral open in the lower bound, the first slot they are walked through; nothing on
        // the left is lifted, since none of them reads an operand
        public void StartLargeOperator(LargeOperatorKind kind)
        {
            var ctx = CurrentContext;
            var largeOperator = new LargeOperatorToken(kind);

            ctx.Tokens.Insert(ctx.CursorIndex, largeOperator);
            ctx.CursorIndex++;

            _scopeStack.Push(new ScopeContext(largeOperator.LowerTokens, largeOperator, ScopeRole.LowerBound));
        }

        // the derivative opens in the function, and the point comes after it
        public void StartDerivative()
        {
            var ctx = CurrentContext;
            var derivative = new DerivativeToken();

            ctx.Tokens.Insert(ctx.CursorIndex, derivative);
            ctx.CursorIndex++;

            _scopeStack.Push(new ScopeContext(derivative.FunctionTokens, derivative, ScopeRole.CalculusBody));
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

        // every Backspace deletes something; at the start of a slot the structure goes and its contents stay
        // (which can run two numbers together on purpose: 2sin(30) leaves 230)
        public void Backspace()
        {
            var ctx = CurrentContext;

            if (ctx.CursorIndex == 0 && ctx.Role != ScopeRole.Root && ctx.ParentToken != null)
            {
                DissolveStructure(ctx.ParentToken, ctx.Tokens);
                return;
            }

            // at the start of the root scope there is nothing left to delete
            if (ctx.CursorIndex == 0) return;

            // a digit, an operator or a whole structure, one token either way
            ctx.Tokens.RemoveAt(ctx.CursorIndex - 1);
            ctx.CursorIndex--;
        }

        // drops the structure and puts the contents of its slots back where it stood, so 5^7 leaves the 5
        private void DissolveStructure(MathToken structureToken, List<MathToken> leavingSlot)
        {
            _scopeStack.Pop();

            var parentCtx = CurrentContext;
            int tokenIndex = parentCtx.Tokens.IndexOf(structureToken);
            if (tokenIndex == -1) return; // safety, a scope always sits in its parents list

            List<MathToken> salvaged = SalvagedTokens(structureToken, leavingSlot, out int cursorOffset);

            parentCtx.Tokens.RemoveAt(tokenIndex);
            parentCtx.Tokens.InsertRange(tokenIndex, salvaged);
            parentCtx.CursorIndex = tokenIndex + cursorOffset;
        }

        // what a dissolved structure leaves behind, and where the cursor lands: where its slot began, so
        // out of 2sin(30) the caret stands between the 2 and the 30
        private static List<MathToken> SalvagedTokens(MathToken structureToken, List<MathToken> leavingSlot,
            out int cursorOffset)
        {
            var salvaged = new List<MathToken>();

            cursorOffset = 0;
            bool reachedLeavingSlot = false;

            foreach (TokenSlot slot in GetSlots(structureToken))
            {
                if (ReferenceEquals(slot.Tokens, leavingSlot)) reachedLeavingSlot = true;
                if (!reachedLeavingSlot) cursorOffset += slot.Tokens.Count;

                salvaged.AddRange(slot.Tokens);
            }

            return salvaged;
        }

        // puts the cursor where a click landed, from an address like 2.0/5.1@3 (tokenIndex.slotIndex steps
        // and the position in the last); a malformed or stale one leaves the cursor where it was
        public bool SetCursorPosition(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;

            int separator = address.IndexOf('@');
            if (separator < 0) return false;

            if (!TryParseIndex(address.Substring(separator + 1), out int cursorIndex)) return false;

            // built beside the live stack, so a bad step cannot strand the cursor
            var scopes = new List<ScopeContext> { _rootContext };
            List<MathToken> tokens = _rootTokens;

            string path = address.Substring(0, separator);
            if (path.Length > 0)
            {
                foreach (string step in path.Split('/'))
                {
                    string[] parts = step.Split('.');
                    if (parts.Length != 2) return false;

                    if (!TryParseIndex(parts[0], out int tokenIndex)) return false;
                    if (!TryParseIndex(parts[1], out int slotIndex)) return false;
                    if (tokenIndex >= tokens.Count) return false;

                    MathToken owner = tokens[tokenIndex];
                    List<TokenSlot> slots = GetSlots(owner);
                    if (slotIndex >= slots.Count) return false;

                    // the scope being left parks its cursor on the token, as entering from the left does
                    scopes[scopes.Count - 1].CursorIndex = tokenIndex;

                    TokenSlot slot = slots[slotIndex];
                    scopes.Add(new ScopeContext(slot.Tokens, owner, slot.Role));
                    tokens = slot.Tokens;
                }
            }

            if (cursorIndex > tokens.Count) cursorIndex = tokens.Count;

            _scopeStack.Clear();
            foreach (ScopeContext scope in scopes)
            {
                _scopeStack.Push(scope);
            }

            CurrentContext.CursorIndex = cursorIndex;

            // an address in front of a token that begins with its first slot resolves into the slot
            EnterTokensThatBeginWithTheirFirstSlot();

            return true;
        }

        // invariant culture; the address is machine written
        private static bool TryParseIndex(string text, out int value)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return false;

            return value >= 0;
        }

        public void Clear()
        {
            _rootTokens.Clear();
            _scopeStack.Clear();
            _scopeStack.Push(_rootContext);
            _rootContext.CursorIndex = 0;
        }

        // replaces everything with the result as ordinary digits, editable one by one, to continue from
        // (fullValue rides on the digits until one is edited, see SeededValue)
        public void SeedWithValue(string numberText, MathValue? fullValue = null)
        {
            Clear();

            FillWithDigits(_rootTokens, numberText);
            _rootContext.CursorIndex = _rootTokens.Count;

            if (fullValue is MathValue value) CarryFullValue(value);
        }

        // every digit of the seed shares the one full value, its magnitude only
        private void CarryFullValue(MathValue value)
        {
            List<MathToken> digits = _rootTokens.FindAll(token => token.Type == TokenType.Number);
            SeededValue seed = new SeededValue(new MathValue(Math.Abs(value.Value), ExactValue.Abs(value.Exact)), digits.Count);
            foreach (MathToken digit in digits) digit.Seed = seed;
        }

        // the same for a result shown as a fraction, so it keeps its shape
        public void SeedWithFraction(long numerator, long denominator)
        {
            Clear();

            var fraction = new FractionToken();
            FillWithDigits(fraction.NumeratorTokens, numerator.ToString(CultureInfo.InvariantCulture));
            FillWithDigits(fraction.DenominatorTokens, denominator.ToString(CultureInfo.InvariantCulture));

            _rootTokens.Add(fraction);
            _rootContext.CursorIndex = _rootTokens.Count;
        }

        // and as a mixed number; the sign rides on the whole part, so x² lifts it along
        public void SeedWithMixedFraction(long whole, long numerator, long denominator)
        {
            Clear();

            var mixed = new MixedFractionToken();
            FillWithDigits(mixed.WholeTokens, whole.ToString(CultureInfo.InvariantCulture));
            FillWithDigits(mixed.NumeratorTokens, numerator.ToString(CultureInfo.InvariantCulture));
            FillWithDigits(mixed.DenominatorTokens, denominator.ToString(CultureInfo.InvariantCulture));

            _rootTokens.Add(mixed);
            _rootContext.CursorIndex = _rootTokens.Count;
        }

        // and as the tokens a result is drawn as, when those are real tokens: prime factors, an exact form,
        // an angle (whose digits carry the full value, the seconds being rounded)
        public void SeedWithTokens(IEnumerable<MathToken> tokens, MathValue? fullValue = null)
        {
            Clear();

            _rootTokens.AddRange(tokens);
            _rootContext.CursorIndex = _rootTokens.Count;

            if (fullValue is MathValue value) CarryFullValue(value);
        }

        // and as a number with a power of ten behind it, typed the way the EXP key types one; the digits
        // carry the full mantissa, the power is exact as it stands
        public void SeedWithScientific(string mantissaText, MathValue fullMantissa, int exponent)
        {
            SeedWithValue(mantissaText, fullMantissa);

            var power = new PowerToken();
            FillWithDigits(power.BaseTokens, "10");
            FillWithDigits(power.ExponentTokens, exponent.ToString(CultureInfo.InvariantCulture));

            _rootTokens.Add(new MathToken(TokenType.Operator, "*"));
            _rootTokens.Add(power);
            _rootContext.CursorIndex = _rootTokens.Count;
        }

        // and as a number with a decimal prefix behind it, the way the ENG view writes one
        public void SeedWithPrefix(string mantissaText, MathValue fullMantissa, string prefix)
        {
            SeedWithValue(mantissaText, fullMantissa);

            _rootTokens.Add(new PostfixToken(prefix));
            _rootContext.CursorIndex = _rootTokens.Count;
        }

        // brackets a seeded result that is more than one operand (a negative number, a power of ten, prime
        // factors, a sum of roots), so −5 squared is 25, as a Casio squares Ans
        public void EncloseIfCompound()
        {
            if (FindOperandStart(_rootTokens, _rootTokens.Count) == 0) return;

            _rootTokens.Insert(0, new MathToken(TokenType.BracketOpen, "("));
            _rootTokens.Add(new MathToken(TokenType.BracketClose, ")"));
            _rootContext.CursorIndex = _rootTokens.Count;
        }

        // one token per character, as typing it would leave; a leading minus goes in as the operator
        private static void FillWithDigits(List<MathToken> tokens, string numberText)
        {
            foreach (char character in numberText)
            {
                if (character == '-')
                {
                    tokens.Add(new MathToken(TokenType.Operator, "-"));
                    continue;
                }

                tokens.Add(new MathToken(TokenType.Number, character.ToString()));
            }
        }


        // === output ===

        // read-only view of the tree for the evaluator; this class stays the only thing that mutates it
        public IReadOnlyList<MathToken> RootTokens => _rootTokens;

        // where the caret stands; the list by reference, which tells two empty slots apart
        public IReadOnlyList<MathToken> ActiveTokens => CurrentContext.Tokens;
        public int ActiveCursorIndex => CurrentContext.CursorIndex;

        // empty input renders as "0"; the history line asks without cursor and addresses
        public string GetLatexString(bool withCursor = true, bool withAddresses = false,
            bool displayFractions = false)
        {
            ScopeContext? activeScope = null;
            if (withCursor) activeScope = CurrentContext;

            if (_rootTokens.Count == 0)
            {
                if (withCursor) return "0" + LatexHelper.CursorLatex;
                return "0";
            }

            var context = new LatexRenderContext(activeScope, withAddresses, displayFractions);
            return LatexHelper.GetListLatex(_rootTokens, context);
        }
    }
}
