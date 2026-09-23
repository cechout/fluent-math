using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FluentMath.Models;

namespace FluentMath.Engines
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
    //
    // the input is a sandbox: nothing typed is ever refused, corrected or rearranged, however broken the
    // formula looks along the way
    // a leading times sign, three operators in a row, two decimal points in one number, a lone factorial,
    // an exponent with no number in front of it: all of it goes in as typed and stands there
    // the single place a formula is judged is MathEvaluator, on = , and a broken one comes back as a
    // Syntax ERROR that the user can walk back into and repair
    // the guards that used to live here read as helpfulness and were not: they silently threw a keypress
    // away, which is worse than an error message, because nothing on screen says why
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
        //
        // one press has to move the caret somewhere the eye can follow, which is what the call below the
        // move is for: it keeps the cursor off a position that is drawn where the one beside it is drawn
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

        // both helpers below follow the same shape for every structured token: push the sub-scope the
        // cursor should land in and park the cursor at the far end, so entering from the right starts at
        // the end of the slot and entering from the left starts at its beginning
        //
        // a fraction takes part like everything else: arriving from the left lands at the start of the
        // numerator and arriving from the right at the end of the denominator, so both halves are
        // reachable without switching to the Up and Down keys; the cost is that merely walking past a
        // fraction now goes through it rather than over it

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

        // a power draws nothing in front of its base, so the position in front of the token and the first
        // position inside the base are one place on screen; every other structured token draws something
        // there, a bar with the numerator centred over it, a radical sign, or a name
        //
        // `MathLayoutEngine.BuildPower` is what makes that true, and the two have to move together
        // a mixed fraction is the same case with its whole part, see `MathLayoutEngine.BuildMixedFraction`
        private static bool BeginsWithItsFirstSlot(MathToken token)
        {
            return token is PowerToken || token is MixedFractionToken;
        }

        // the cursor never stands in front of such a token, it stands in the slot instead
        //
        // standing on both costs a press of an arrow key that changes nothing the eye can see, in either
        // direction. The inner one is the one that is kept, because what is typed there joins the number
        // that is on screen rather than landing beside a structure the display draws no boundary for
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

        // Left and Right first walk to the neighbouring slot of the same token and only leave the token
        // once there is no neighbour left, which is what makes a root index, a logarithm base or the far
        // half of a fraction reachable with the arrow keys alone
        private void HandleSlotNavigation(NavDirection direction, ScopeContext context)
        {
            if (direction == NavDirection.Left)
            {
                if (TryMoveToNeighbourSlot(context, next: false)) return;

                _scopeStack.Pop();
                PositionCursorAtParentToken(context.ParentToken, before: true);

                // that position is the place the cursor just left when the token begins with the slot it
                // came out of, so the move carries straight on out of it rather than stopping there
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

        // the slots of a structured token in reading order
        //
        // everything that walks between slots reads its answer out of this one list, so a further
        // structured token needs one case here instead of a branch in each of the four callers
        //
        // the order is also the slot index a click address carries, which MathToken hardcodes per token
        // when it renders; reordering a token here without reordering it there sends a click into the
        // wrong half of a structure
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

        // an empty neighbour is deliberately not skipped here; stepping into it is the only way to fill
        // the index of a plain square root or the base of a logarithm that was left blank
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

        // swaps the top of the stack for another slot of the same token, the way the two fraction halves
        // already trade places
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

        // one token per digit, so the cursor can stand between any two characters of a number the same
        // way it stands between any two tokens; "125" is three tokens and the evaluator is the one place
        // that reads such a run back as a single value
        //
        // a second decimal point in the same number is accepted like anything else, see the sandbox note
        // on the class
        public void AddNumber(string digit)
        {
            var ctx = CurrentContext;

            ctx.Tokens.Insert(ctx.CursorIndex, new MathToken(TokenType.Number, digit));
            ctx.CursorIndex++;
        }

        // every operator goes in where the cursor is, however many of them are already there and
        // whatever stands beside them
        //
        // it used to refuse one with nothing on its left and to overwrite the one before it, both of
        // which quietly changed what was typed; a leading minus needs no special case either, since
        // nothing is refused for it to be an exception to
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

        public void AddPostfix(string kind)
        {
            var ctx = CurrentContext;

            ctx.Tokens.Insert(ctx.CursorIndex, new PostfixToken(kind));
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
            MoveOperandIntoSlot(ctx, powerToken.BaseTokens);

            ctx.Tokens.Insert(ctx.CursorIndex, powerToken);
            ctx.CursorIndex++;

            _scopeStack.Push(new ScopeContext(powerToken.ExponentTokens, powerToken, ScopeRole.Exponent));
        }

        // the e to the x key; unlike StartPower it deliberately leaves whatever stands to its left
        // alone, because the base is the thing the key already names
        //
        // there was a matching ten to the n key beside it and it was taken out: it sat next to the
        // keypads own x10^n, looked almost the same and bound quite differently, which is a trap rather
        // than a choice
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

        // the run of tokens directly left of the cursor that reads as one operand: everything back to
        // the nearest operator, opening bracket or start of the scope, with a bracket group counted as
        // one piece, so the base of (1+2) squared is the whole group
        //
        // returns cursorIndex itself when there is nothing to take, which is what leaves an empty slot
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

        // moves that operand out of the scope and into the slot a structured token is about to own,
        // which is what lets the fraction and the power keys continue from what is already typed
        //
        // says whether anything was taken; that is what decides which half the fraction key leaves the
        // cursor in
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

        // the EXP key, which is the work taken off you for typing times, one, zero, power and nothing
        // more; it builds exactly what those four keys build, so everything downstream treats it as what
        // it is rather than as a shape of its own
        //
        // it used to be a token with a single slot, and that cost a cursor position: there was nowhere to
        // stand between the ten and the exponent, because the ten was drawn rather than typed, so walking
        // left out of the exponent left the whole times ten to the n behind in one step
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

        // the numerator continues from whatever already stands left of the cursor, the way a Casio does
        // it: 5 + 45 followed by the fraction key lifts the 45 over the bar and drops the cursor into the
        // denominator, which is the half still waiting to be typed
        //
        // with nothing to lift there is no half to continue into either, so an empty fraction opens in
        // the numerator instead
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

        // the mixed fraction continues the same way: 2 followed by the key makes the 2 the whole part and
        // drops the cursor into the empty numerator, which is where a Casio puts it
        //
        // a minus in front stays outside, since the operand stops at it, and negates the whole mixed
        // number; with nothing to lift the template opens in the whole part
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

        // every Backspace deletes something
        //
        // at the start of a sub-scope there is no character left to take, so the structure itself goes
        // and everything typed into it stays standing where the structure stood
        //
        // that can run two numbers together, and deliberately does: backspacing into the denominator of
        // 1/2 leaves 12, and deleting the sin out of 2sin(30) leaves 230
        // it used to step into the slot before instead, which meant a keypress that visibly did nothing
        // and a second one needed to delete a single character
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

            // digit, operator or a whole structure; everything left of the cursor is one token now, so
            // it all goes away in one piece
            ctx.Tokens.RemoveAt(ctx.CursorIndex - 1);
            ctx.CursorIndex--;
        }

        // drops the structure but keeps what was already typed into it, by putting the contents of its
        // one remaining slot back into the parent scope where the token itself stood
        //
        // deleting the 7 out of 5^7 twice therefore leaves the 5 rather than taking it along; the slots
        // are walked in reading order, though only one of them can contribute anything, since the caller
        // only gets here while at most one is in use
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

        // what a dissolved structure leaves behind, and where in it the cursor lands
        //
        // the cursor stays at the point the slot it was standing in used to begin, so it does not jump
        // over content it was in front of: backspacing out of the argument of 2sin(30) leaves the caret
        // between the 2 and the 30, and out of the exponent of 5^7 leaves it behind the 5
        //
        // a scientific token also draws a times sign and a ten that were never tokens of their own, so
        // those go back in as well; without them 3x10^5 would collapse to 35, a different number with
        // nothing on screen saying so
        // an untouched one has nothing on screen worth keeping and disappears whole
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

        // puts the cursor where a click in the display landed, from the address the renderer wrote onto
        // the token that was hit: a chain of tokenIndex.slotIndex steps and the position within the last
        // of them, for example 2.0/5.1@3
        //
        // everything here arrives from the browser, so none of it is trusted; a malformed address or an
        // index that no longer exists leaves the cursor exactly where it was, which is also what happens
        // when a stale render is clicked after the tree has already changed
        public bool SetCursorPosition(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;

            int separator = address.IndexOf('@');
            if (separator < 0) return false;

            if (!TryParseIndex(address.Substring(separator + 1), out int cursorIndex)) return false;

            // the walk builds the new stack beside the live one, so a bad step half way down cannot
            // strand the cursor in a scope nobody asked for
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

                    // the scope being left parks its cursor on the token, the same way entering a
                    // structure from the left does, so Backspace out of it finds the token where it
                    // expects to
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

            // an address in front of a token that begins with its own first slot names a place the caret
            // is never drawn on its own, so it is resolved to the one inside the slot
            EnterTokensThatBeginWithTheirFirstSlot();

            return true;
        }

        // invariant culture on purpose, the address is machine written and a German system would
        // otherwise read a grouping separator into it
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

        // drops everything and leaves the finished result behind as ordinary tokens, which is how it is
        // carried into the calculation that continues from it
        //
        // the digits rather than an Ans token: the display is still showing that number, and swapping it
        // for a word reads as the formula having been thrown away; it also keeps the result editable
        // digit by digit
        // the cost is that only the twelve significant digits on screen survive into the next step,
        // which is what the Ans key in the flyout is there for
        public void SeedWithValue(string numberText)
        {
            Clear();

            FillWithDigits(_rootTokens, numberText);
            _rootContext.CursorIndex = _rootTokens.Count;
        }

        // the same for a result the S to D key is showing as a fraction, so the display keeps the shape
        // it had
        public void SeedWithFraction(long numerator, long denominator)
        {
            Clear();

            var fraction = new FractionToken();
            FillWithDigits(fraction.NumeratorTokens, numerator.ToString(CultureInfo.InvariantCulture));
            FillWithDigits(fraction.DenominatorTokens, denominator.ToString(CultureInfo.InvariantCulture));

            _rootTokens.Add(fraction);
            _rootContext.CursorIndex = _rootTokens.Count;
        }

        // and as a mixed number, which goes back in as the structure it is drawn as
        //
        // the sign rides on the whole part, where the sign rule of the mixed fraction makes it the sign of
        // the whole number; a minus in front of the token would do the same until x squared lifts only
        // the token and leaves the minus behind
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

        // one token per character, exactly what typing the same number by hand would leave behind; a
        // leading minus is a sign rather than a digit, so it goes in as the operator the evaluator
        // already reads that way
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

        // where the caret stands, for a display that draws it itself
        //
        // the list is handed out by reference on purpose: that identity is what tells one empty slot from
        // another, and two of them compare equal by contents
        public IReadOnlyList<MathToken> ActiveTokens => CurrentContext.Tokens;
        public int ActiveCursorIndex => CurrentContext.CursorIndex;

        // empty input renders as "0" so the display is never blank
        //
        // the cursor belongs to the line being typed, so the history line asks for the same formula
        // without it, and without the addresses that make a formula clickable
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
