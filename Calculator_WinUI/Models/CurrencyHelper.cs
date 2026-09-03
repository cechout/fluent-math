using System.Collections.Generic;
using System.Globalization;

namespace Calculator_WinUI.Models
{
    // this holds the raw code for the logic and the beautiful name for the ui
    public class CurrencyInfo
    {
        public string Code { get; set; }
        public string DisplayName { get; set; }
    }

    public static class CurrencyHelper
    {
        private static Dictionary<string, string> _currencyNames;

        public static CurrencyInfo GetInfo(string code)
        {
            // build the dictionary only once when it is needed for the first time
            if (_currencyNames == null)
            {
                BuildCurrencyMap();
            }

            // if windows knows the currency, use the nice name, otherwise fallback to the raw code
            string displayName = _currencyNames.ContainsKey(code) ? _currencyNames[code] : $"{code} - {code}";
            return new CurrencyInfo { Code = code, DisplayName = displayName };
        }

        private static void BuildCurrencyMap()
        {
            _currencyNames = new Dictionary<string, string>();

            // get all specific cultures known to windows (e.g., en-US, de-DE)
            var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);

            foreach (var culture in cultures)
            {
                try
                {
                    var region = new RegionInfo(culture.Name);
                    string isoCode = region.ISOCurrencySymbol;

                    // we only add the first country we find for each currency
                    if (!_currencyNames.ContainsKey(isoCode))
                    {
                        // creates strings like: "United States - US Dollar" or "Japan - Japanese Yen"
                        _currencyNames[isoCode] = $"{region.EnglishName} - {region.CurrencyEnglishName}";
                    }
                }
                catch
                {
                    // some exotic cultures might throw an exception, just skip them
                }
            }

            // small override for the euro to make it look clean
            _currencyNames["EUR"] = "Europe - Euro";
        }
    }
}
