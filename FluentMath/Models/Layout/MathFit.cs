using System;

namespace FluentMath.Models.Layout
{
    // how far a formula is scaled down to fit its box; here rather than in the panel, so the tests reach it
    public static class MathFit
    {
        public static double ScaleFor(double contentHeight, double availableHeight, double minScale)
        {
            // an unbounded or unknown box asks for nothing
            if (double.IsNaN(availableHeight) || double.IsInfinity(availableHeight)) return 1;
            if (availableHeight <= 0 || contentHeight <= 0) return 1;

            // it already fits, and a formula is never scaled up to fill the box
            if (contentHeight <= availableHeight) return 1;

            // past the floor it is clipped after all, because unreadable is worse than cut off
            return Math.Max(minScale, availableHeight / contentHeight);
        }
    }
}
