using System.Collections.Generic;

namespace FluentMath.Persistence.Models
{
    // what the pages leave behind that is not a setting, saved to page-state.json
    public class PageStateData
    {
        // the size each page was last dragged to while compact, keyed by its type name
        public Dictionary<string, CompactSize> CompactSizes { get; set; } = new Dictionary<string, CompactSize>();

        // the scientific display row, in DIP; null until the splitter is first dragged
        public double? ScientificDisplayHeight { get; set; }

        // the two pickers of each converter, keyed by its name; a converter not in here starts on its default
        public Dictionary<string, UnitPair> ConverterPairs { get; set; } = new Dictionary<string, UnitPair>();
    }


    // unit ids, e.g. ISO currency codes
    public class UnitPair
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
    }


    // in DIP, so a screen with another scale gets the same look
    public class CompactSize
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
