using System;
using System.Collections.Generic;

namespace Calculator_WinUI.Models
{
    public enum ScopeRole
    {
        Root,
        Numerator,
        Denominator,
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

    public class ScopeContext
    {
        public List<MathToken> Tokens { get; }
        public MathToken ParentToken { get; }
        public ScopeRole Role { get; }

        // cursor position within Tokens; valid range: 0 - Tokens.Count
        // index i means: cursor sits before Tokens[i], index Tokens.Count means: cursor at very end
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
