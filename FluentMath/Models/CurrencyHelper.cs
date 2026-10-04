using System.Collections.Generic;
using System.Globalization;
using FluentMath.Models.Converters;

namespace FluentMath.Models
{
    // the display name of an ISO currency code, from the regions Windows knows
    public static class CurrencyHelper
    {
        private static Dictionary<string, string> _currencyNames;

        public static UnitInfo GetInfo(string code)
        {
            if (_currencyNames == null)
            {
                BuildCurrencyMap();
            }

            // no region for it: the raw code on both halves of the label
            string displayName = _currencyNames.ContainsKey(code) ? _currencyNames[code] : $"{code} - {code}";
            return new UnitInfo { Id = code, Symbol = code, DisplayName = displayName };
        }

        // built once, lazily; enumerating every culture is not cheap
        private static void BuildCurrencyMap()
        {
            _currencyNames = new Dictionary<string, string>();

            var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);

            foreach (var culture in cultures)
            {
                try
                {
                    var region = new RegionInfo(culture.Name);
                    string isoCode = region.ISOCurrencySymbol;

                    // the first region that claims a code wins
                    if (!_currencyNames.ContainsKey(isoCode))
                    {
                        // reads as "United States - US Dollar" or "Japan - Japanese Yen"
                        _currencyNames[isoCode] = $"{region.EnglishName} - {region.CurrencyEnglishName}";
                    }
                }
                catch { /* a culture without a matching region cannot contribute a name, skip it */ }
            }

            // the euro would otherwise be labelled by whichever eurozone country came first
            _currencyNames["EUR"] = "Europe - Euro";
        }
    }
}
