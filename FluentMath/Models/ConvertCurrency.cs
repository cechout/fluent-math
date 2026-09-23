using System;
using System.Collections.Generic;
using System.Globalization;

namespace FluentMath.Models
{
    // converts between any two currencies in the ECB rate table
    //
    // the table is EUR-based, so every conversion goes through the euro; there is no direct rate for
    // a pair like USD to JPY and none is needed
    class ConvertCurrency
    {
        public Dictionary<string, double> ExchangeRates { get; private set; }

        // rates are fetched once per instance; CurrencyViewModel recreates the object to refresh them
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

            double result = (amountCurrency1 / fromRate) * toRate;
            result = Math.Round(result, 2);

            return result.ToString(CultureInfo.InvariantCulture);
        }

        // what a single unit of currency1 is worth in currency2; shown as the small rate line under
        // the converter, so it carries more decimals than a converted amount
        public string GetCurrencyRate(string currency1, string currency2)
        {
            if (!ExchangeRates.ContainsKey(currency1) || !ExchangeRates.ContainsKey(currency2))
                return "Error";

            double fromRate = ExchangeRates[currency1];
            double toRate = ExchangeRates[currency2];

            double rate = (1.0 / fromRate) * toRate;
            rate = Math.Round(rate, 4);

            return rate.ToString(CultureInfo.InvariantCulture);
        }
    }
}
