using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace Calculator_WinUI.Models
{
    class GetCurrencyData
    {
        public static Dictionary<string, double> FetchAllRates()
        {
            // a dictionary to store the currency codes and their corresponding rates
            var rates = new Dictionary<string, double>();

            // euro ist the base currency
            rates.Add("EUR", 1.0);

            string url = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml";

            try
            {
                using (XmlReader reader = XmlReader.Create(url))
                {
                    while (reader.Read())
                    {
                        // we only search elements named "Cube" that have attributes, because the rates are stored in such elements
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "Cube" && reader.HasAttributes)
                        {
                            string currency = reader.GetAttribute("currency");
                            string rateString = reader.GetAttribute("rate");

                            // if we found a currency and a rate, we parse the rate and add it to our dictionary
                            if (!string.IsNullOrEmpty(currency) && !string.IsNullOrEmpty(rateString))
                            {
                                double rate = double.Parse(rateString, CultureInfo.InvariantCulture);
                                rates[currency] = rate;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw new Exception("error fetching currency rates");
            }

            return rates;
        }
    }
}
