using System.Collections.Generic;

namespace FluentMath.Models.Converters
{
    // what one converter brings to the shared ConverterViewModel; everything else is the same for all of them
    public interface IUnitSource
    {
        // the converter in page-state.json, e.g. "Currency"
        string Key { get; }

        // empty while the converter has nothing to convert with
        IReadOnlyList<UnitInfo> Units { get; }

        // ids; the pair a converter starts on before anything was picked
        string DefaultFrom { get; }
        string DefaultTo { get; }

        // money rounds to decimal places, a measure to significant digits
        bool RoundsToDecimals { get; }

        // the rate line while Units is empty
        string EmptyText { get; }

        // the left half of the rate line; empty for a converter whose units never change
        string DateText { get; }

        // reloads Units and DateText; (false = no refresh key)
        bool CanRefresh { get; }
        void Refresh();

        // null when either id is unknown
        double? Convert(string fromId, string toId, double value);
    }
}
