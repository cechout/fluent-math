using System;
using System.Collections.Generic;
using System.Globalization;

namespace FluentMath.Models
{
    // converts between any two currencies in the ECB rate table, always through the euro
    class ConvertCurrency
    {
        public Dictionary<string, double> ExchangeRates { get; private set; }

        // the rates come from CurrencyViewModel, live or saved; a refresh makes a new converter
        public ConvertCurrency(Dictionary<string, double> exchangeRates)
        {
            ExchangeRates = exchangeRates;
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

        // one unit of currency1 in currency2, for the rate line; (more decimals than an amount)
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
