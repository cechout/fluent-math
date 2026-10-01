using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace FluentMath.Models
{
    // pulls the daily reference rates published by the European Central Bank
    // feed reference: https://www.ecb.europa.eu/stats/policy_and_exchange_rates/euro_reference_exchange_rates/html/index.en.html
    class GetCurrencyData
    {
        public static RateTable FetchAllRates()
        {
            var table = new RateTable();
            Dictionary<string, double> rates = table.Rates;

            // the feed quotes everything against the euro and therefore never lists EUR itself
            rates.Add("EUR", 1.0);

            string url = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml";

            try
            {
                using (XmlReader reader = XmlReader.Create(url))
                {
                    while (reader.Read())
                    {
                        // three levels of Cube elements; only the innermost carry the attributes of a rate
                        // (the middle one carries the day they are for)
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "Cube" && reader.HasAttributes)
                        {
                            string time = reader.GetAttribute("time");
                            if (DateOnly.TryParseExact(time, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
                            {
                                table.Date = date;
                            }

                            string currency = reader.GetAttribute("currency");
                            string rateString = reader.GetAttribute("rate");

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

            return table;
        }
    }
}
