using System.Collections.Generic;
using System.Linq;

namespace FluentMath.Models.Converters
{
    // a converter whose units are each a fixed multiple of one base unit, e.g. length in metres;
    // a new one of these is a table and nothing else
    public class LinearUnitSource : IUnitSource
    {
        // === fields ===

        private readonly Dictionary<string, double> _factors; // id to the base unit


        // === unit source ===

        public string Key { get; }
        public IReadOnlyList<UnitInfo> Units { get; }
        public string DefaultFrom { get; }
        public string DefaultTo { get; }
        public bool RoundsToDecimals => false;
        public string EmptyText => "";
        public string DateText => "";
        public bool CanRefresh => false;


        // === constructor ===

        // the units in picker order, each with its size in the base unit
        public LinearUnitSource(string key, string defaultFrom, string defaultTo,
            IEnumerable<(string Id, string Symbol, string DisplayName, double Factor)> units)
        {
            Key = key;
            DefaultFrom = defaultFrom;
            DefaultTo = defaultTo;

            var list = units.ToList();
            Units = list.Select(u => new UnitInfo { Id = u.Id, Symbol = u.Symbol, DisplayName = u.DisplayName }).ToList();
            _factors = list.ToDictionary(u => u.Id, u => u.Factor);
        }


        // === conversion ===

        public void Refresh() { }

        public double? Convert(string fromId, string toId, double value)
        {
            if (!_factors.TryGetValue(fromId, out double from) || !_factors.TryGetValue(toId, out double to))
                return null;

            return fromId == toId ? value : value * from / to;
        }
    }
}
