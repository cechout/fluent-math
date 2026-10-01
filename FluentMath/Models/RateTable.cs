using System;
using System.Collections.Generic;

namespace FluentMath.Models
{
    // one publication of the ECB reference rates: the day they are for, and every rate against the euro
    // (the euro itself included, at 1)
    public class RateTable
    {
        public DateOnly? Date { get; set; }
        public Dictionary<string, double> Rates { get; set; } = new Dictionary<string, double>();
    }
}
