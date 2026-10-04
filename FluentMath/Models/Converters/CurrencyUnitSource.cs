using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FluentMath.Models.Converters
{
    // the currency converter:
    // the ECB reference rates, every conversion through the euro;
    // where the rates come from is handed in, so the models stay out of persistence
    public class CurrencyUnitSource : IUnitSource
    {
        // === fields ===

        private readonly Func<RateTable?> _loadRates; // null when there are no rates at all
        private Dictionary<string, double> _rates = new Dictionary<string, double>(); // against the euro


        // === unit source ===

        public string Key => "Currency";
        public IReadOnlyList<UnitInfo> Units { get; private set; } = new List<UnitInfo>();
        public string DefaultFrom => "EUR";
        public string DefaultTo => "USD";
        public bool RoundsToDecimals => true;
        public string EmptyText => "No rates available";
        public string DateText { get; private set; } = "";
        public bool CanRefresh => true;


        // === constructor ===

        public CurrencyUnitSource(Func<RateTable?> loadRates)
        {
            _loadRates = loadRates;
            ApplyRates(_loadRates());
        }


        // === rates ===

        // a refresh goes the way the start does
        public void Refresh()
        {
            ApplyRates(_loadRates());
        }

        // the unit list is whatever the rates contain; the date is in the region format
        private void ApplyRates(RateTable? rates)
        {
            _rates = rates?.Rates ?? new Dictionary<string, double>();
            DateText = rates?.Date?.ToString("d", CultureInfo.CurrentCulture) ?? "";
            Units = _rates.Keys.Select(CurrencyHelper.GetInfo).ToList();
        }

        public double? Convert(string fromId, string toId, double value)
        {
            if (!_rates.TryGetValue(fromId, out double fromRate) || !_rates.TryGetValue(toId, out double toRate))
                return null;

            return value / fromRate * toRate;
        }
    }
}
