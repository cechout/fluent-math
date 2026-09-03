using System;
using System.Collections.Generic;
using System.Globalization;

namespace Calculator_WinUI.Models
{
    class ConvertCurrency
    {
        public Dictionary<string, double> ExchangeRates { get; private set; }

        public ConvertCurrency()
        {
            ExchangeRates = GetCurrencyData.FetchAllRates();
        }

        public string GetAmountCurrency2(string currency1, string currency2, double amountCurrency1)
        {
            if (!ExchangeRates.ContainsKey(currency1) || !ExchangeRates.ContainsKey(currency2))
                return "Error";

            double fromRate = ExchangeRates[currency1];
            double toRate = ExchangeRates[currency2];

            // first convert to euro, then to the target currency
            double result = (amountCurrency1 / fromRate) * toRate;
            result = Math.Round(result, 2);

            return result.ToString(CultureInfo.InvariantCulture);
        }

        public string GetCurrencyRate(string currency1, string currency2)
        {
            if (!ExchangeRates.ContainsKey(currency1) || !ExchangeRates.ContainsKey(currency2))
                return "Error";

            double fromRate = ExchangeRates[currency1];
            double toRate = ExchangeRates[currency2];

            // here we simply calculate what 1 unit of currency1 is worth in target currency
            double rate = (1.0 / fromRate) * toRate;
            rate = Math.Round(rate, 4);

            return rate.ToString(CultureInfo.InvariantCulture);
        }
    }
}
