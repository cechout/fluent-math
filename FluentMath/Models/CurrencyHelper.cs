using System.Collections.Generic;
using System.Globalization;

namespace FluentMath.Models
{
    // pairs the ISO code the conversion math runs on with the readable name the dropdown shows
    public class CurrencyInfo
    {
        public string Code { get; set; }
        public string DisplayName { get; set; }
    }


    // turns an ISO currency code into a display name, using what Windows already knows about regions
    // instead of shipping our own currency name table
    public static class CurrencyHelper
    {
        private static Dictionary<string, string> _currencyNames;

        public static CurrencyInfo GetInfo(string code)
        {
            if (_currencyNames == null)
            {
                BuildCurrencyMap();
            }

            // a currency Windows has no region for still has to appear in the list, so fall back to
            // the raw code on both halves of the label
            string displayName = _currencyNames.ContainsKey(code) ? _currencyNames[code] : $"{code} - {code}";
            return new CurrencyInfo { Code = code, DisplayName = displayName };
        }

        // built lazily and kept for the process lifetime; enumerating every culture is not cheap and
        // the result never changes
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

                    // many countries share one currency, so the first region that claims a code wins
                    // and every later one is dropped
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
