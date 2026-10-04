namespace FluentMath.Models.Converters
{
    // one unit a converter offers:
    // the id is what the math runs on and what is saved, the symbol what the rate line shows
    public class UnitInfo
    {
        public string Id { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }
}
