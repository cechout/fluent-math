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

        public ScopeContext(List<MathToken> tokens, MathToken parentToken, ScopeRole role)
        {
            Tokens = tokens;
            ParentToken = parentToken;
            Role = role;
        }
    }
}
