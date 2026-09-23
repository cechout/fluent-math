using System.Collections.Generic;

namespace FluentMath.Models
{
    // which slot of a structured token a scope belongs to
    // this is what decides how the cursor leaves a scope; Down out of a numerator lands in the
    // denominator, Down out of an exponent lands in the base it belongs to
    public enum ScopeRole
    {
        Root,
        Numerator,
        Denominator,
        PowerBase,
        Exponent,
        RootIndex,
        RootRadicand,
        FunctionParameter,
        LogBase,
        LogParameter
    }

    public enum NavDirection
    {
        Left,
        Right,
        Up,
        Down
    }


    // one slot of a structured token before it becomes a scope; MathInputManager lists these in reading
    // order to answer what sits before or after the slot the cursor is in
    public class TokenSlot
    {
        public List<MathToken> Tokens { get; }
        public ScopeRole Role { get; }

        public TokenSlot(List<MathToken> tokens, ScopeRole role)
        {
            Tokens = tokens;
            Role = role;
        }
    }


    // one editable slot: the token list being typed into, the token that owns that list, and where the
    // cursor currently sits inside it
    //
    // MathInputManager keeps these on a stack, so entering a fraction or an exponent is a push and
    // leaving it is a pop; nesting needs no other bookkeeping
    public class ScopeContext
    {
        public List<MathToken> Tokens { get; }
        public MathToken ParentToken { get; }
        public ScopeRole Role { get; }

        // cursor position within Tokens, valid from 0 to Tokens.Count
        // index i means the cursor sits before Tokens[i], Tokens.Count means it sits at the very end
        public int CursorIndex { get; set; }

        public ScopeContext(List<MathToken> tokens, MathToken parentToken, ScopeRole role, int cursorIndex = 0)
        {
            Tokens = tokens;
            ParentToken = parentToken;
            Role = role;
            CursorIndex = cursorIndex;
        }
    }
}
