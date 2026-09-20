using System.Collections.Generic;
using System.Globalization;
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
                case FractionToken:
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
        private static List<TokenSlot> GetSlots(MathToken token)
        {
            switch (token)
            {
                case FractionToken fraction:
                    return new List<TokenSlot>
                    {
                        new TokenSlot(fraction.NumeratorTokens, ScopeRole.Numerator),
                        new TokenSlot(fraction.DenominatorTokens, ScopeRole.Denominator)
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

                case FunctionToken function:
                    return new List<TokenSlot>
                    {
                        new TokenSlot(function.ParameterTokens, ScopeRole.FunctionParameter)
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
        public void AddNumber(string digit)
        {
            var ctx = CurrentContext;

            if (digit == "." && NumberRunHasDecimalPoint(ctx)) return; // one decimal point per number

            ctx.Tokens.Insert(ctx.CursorIndex, new MathToken(TokenType.Number, digit));
            ctx.CursorIndex++;
        }

        // the number tokens on both sides of the cursor read as one number, so a second decimal point
        // anywhere in that run has to be refused, not only one sitting directly left of the cursor
        private static bool NumberRunHasDecimalPoint(ScopeContext context)
        {
            for (int i = context.CursorIndex - 1; i >= 0; i--)
            {
                if (context.Tokens[i].Type != TokenType.Number) break;
                if (context.Tokens[i].Value == ".") return true;
            }

            for (int i = context.CursorIndex; i < context.Tokens.Count; i++)
            {
                if (context.Tokens[i].Type != TokenType.Number) break;
                if (context.Tokens[i].Value == ".") return true;
            }

            return false;
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
            MoveOperandIntoSlot(ctx, powerToken.BaseTokens);

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

        // sin, cos, tan, ln; the name is passed straight through to LaTeX as a command
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
            if (ctx.CursorIndex == 0 && ctx.Role != ScopeRole.Root && ctx.ParentToken != null)
            {
                // as long as only one slot of the token is still in use, the structure can be dropped
                // without losing anything, so that always wins; this is what stops an emptied exponent
                // from leaving a box behind that no further Backspace can reach
                if (CountFilledSlots(ctx.ParentToken) <= 1)
                {
                    DissolveStructure(ctx.ParentToken);
                    return;
                }

                // more than one slot is in use, so fall back into a previous one that holds something
                // rather than deleting the lot
                //
                // an empty previous slot is skipped on purpose, falling back into nothing would leave
                // Backspace looking stuck
                TokenSlot? previousSlot = GetNeighbourSlot(ctx, next: false);
                if (previousSlot != null && previousSlot.Tokens.Count > 0)
                {
                    SwitchToSlot(previousSlot, ctx.ParentToken, atEnd: true);
                    return;
                }

                RemoveStructure(ctx.ParentToken);
                return;
            }

            // at the start of the root scope there is nothing left to delete
            if (ctx.CursorIndex == 0) return;

            // digit, operator or a whole structure; everything left of the cursor is one token now, so
            // it all goes away in one piece
            ctx.Tokens.RemoveAt(ctx.CursorIndex - 1);
            ctx.CursorIndex--;
        }

        // how many slots of a structured token still hold something
        //
        // the salvage below decides on this rather than on the slot the cursor happens to be in: with a
        // single slot in use, putting its contents back can never run two separate numbers into one
        private static int CountFilledSlots(MathToken token)
        {
            int filled = 0;
            foreach (TokenSlot slot in GetSlots(token))
            {
                if (slot.Tokens.Count > 0) filled++;
            }

            return filled;
        }

        // drops the structure but keeps what was already typed into it, by putting the contents of its
        // one remaining slot back into the parent scope where the token itself stood
        //
        // deleting the 7 out of 5^7 twice therefore leaves the 5 rather than taking it along; the slots
        // are walked in reading order, though only one of them can contribute anything, since the caller
        // only gets here while at most one is in use
        private void DissolveStructure(MathToken structureToken)
        {
            _scopeStack.Pop();

            var parentCtx = CurrentContext;
            int tokenIndex = parentCtx.Tokens.IndexOf(structureToken);
            if (tokenIndex == -1) return; // safety, a scope always sits in its parents list

            var salvaged = new List<MathToken>();
            foreach (TokenSlot slot in GetSlots(structureToken))
            {
                salvaged.AddRange(slot.Tokens);
            }

            parentCtx.Tokens.RemoveAt(tokenIndex);
            parentCtx.Tokens.InsertRange(tokenIndex, salvaged);
            parentCtx.CursorIndex = tokenIndex + salvaged.Count;
        }

        // deletes the structure with everything still in it, for the case where there is nothing left
        // to fall back into and salvaging the slots would run two separate numbers into one
        //
        // the token is looked up rather than assumed to sit left of the cursor: entering a structure
        // from the left leaves the parent cursor on the token instead of after it, and the old
        // assumption made Backspace silently do nothing in exactly that case
        private void RemoveStructure(MathToken structureToken)
        {
            _scopeStack.Pop();

            var parentCtx = CurrentContext;
            int tokenIndex = parentCtx.Tokens.IndexOf(structureToken);
            if (tokenIndex == -1) return;

            parentCtx.Tokens.RemoveAt(tokenIndex);
            parentCtx.CursorIndex = tokenIndex;
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

        // drops everything and leaves a single number behind, which is how a finished result is carried
        // into the calculation that continues from it
        public void SeedWithValue(string numberText)
        {
            Clear();

            // one token per character, exactly what typing the same number by hand would leave behind;
            // a leading minus is a sign rather than a digit, so it goes in as the operator the evaluator
            // already reads that way
            foreach (char character in numberText)
            {
                if (character == '-')
                {
                    _rootTokens.Add(new MathToken(TokenType.Operator, "-"));
                    continue;
                }

                _rootTokens.Add(new MathToken(TokenType.Number, character.ToString()));
            }

            _rootContext.CursorIndex = _rootTokens.Count;
        }


        // === output ===

        // read-only view of the tree for the evaluator; this class stays the only thing that mutates it
        public IReadOnlyList<MathToken> RootTokens => _rootTokens;

        // empty input renders as "0" so the display is never blank
        //
        // the cursor belongs to the line being typed, so the history line asks for the same formula
        // without it, and without the addresses that make a formula clickable
        public string GetLatexString(bool withCursor = true, bool withAddresses = false)
        {
            ScopeContext? activeScope = null;
            if (withCursor) activeScope = CurrentContext;

            if (_rootTokens.Count == 0)
            {
                if (withCursor) return "0" + LatexHelper.CursorLatex;
                return "0";
            }

            return LatexHelper.GetListLatex(_rootTokens, new LatexRenderContext(activeScope, withAddresses));
        }
    }
}
