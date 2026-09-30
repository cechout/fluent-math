using System.Collections.Generic;

namespace FluentMath.Models
{
    // which slot of a structured token a scope belongs to; decides how the cursor leaves it
    // (Down out of a numerator lands in the denominator, out of an exponent in its base)
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
        LogParameter,
        WholePart,
        LowerBound,
        UpperBound,
        CalculusBody, // the body of Σ, Π and the integral, and the function of a derivative
        DerivativePoint
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


    // one editable slot: the token list, the token that owns it, and the cursor in it
    // (MathInputManager keeps these on a stack; entering a slot is a push, leaving it a pop)
    public class ScopeContext
    {
        public List<MathToken> Tokens { get; }
        public MathToken ParentToken { get; }
        public ScopeRole Role { get; }

        // 0 to Tokens.Count; i stands before Tokens[i], Count at the very end
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
