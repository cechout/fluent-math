using System.Collections.Generic;

namespace FluentMath.Persistence.Models
{
    // what the pages leave behind that is not a setting, saved to page-state.json
    public class PageStateData
    {
        public const string DefaultCurrencyFrom = "EUR";
        public const string DefaultCurrencyTo = "USD";

        // the size each page was last dragged to while compact, keyed by its type name
        public Dictionary<string, CompactSize> CompactSizes { get; set; } = new Dictionary<string, CompactSize>();

        // the scientific display row, in DIP; null until the splitter is first dragged
        public double? ScientificDisplayHeight { get; set; }

        // the two currency pickers, as ISO codes
        public string CurrencyFrom { get; set; } = DefaultCurrencyFrom;
        public string CurrencyTo { get; set; } = DefaultCurrencyTo;
    }


    // in DIP, so a screen with another scale gets the same look
    public class CompactSize
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
