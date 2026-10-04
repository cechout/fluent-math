namespace FluentMath.Models.Converters
{
    // the volume converter; the units and the order of the Windows Calculator, in litres
    // (the US and the imperial measures are exact by definition: the US gallon is 231 cubic inches, the
    // imperial one 4.54609 litres)
    public static class VolumeUnits
    {
        public static LinearUnitSource Create()
        {
            return new LinearUnitSource("Volume", "l", "gal_us", new[]
            {
                ("ml", "mL", "Milliliters", 0.001),
                ("cm3", "cm³", "Cubic Centimeters", 0.001),
                ("l", "L", "Liters", 1.0),
                ("m3", "m³", "Cubic Meters", 1000.0),
                ("tsp_us", "tsp (US)", "Teaspoons (US)", 0.00492892159375),
                ("tbsp_us", "tbsp (US)", "Tablespoons (US)", 0.01478676478125),
                ("floz_us", "fl oz (US)", "Fluid Ounces (US)", 0.0295735295625),
                ("cup_us", "cup (US)", "Cups (US)", 0.2365882365),
                ("pt_us", "pt (US)", "Pints (US)", 0.473176473),
                ("qt_us", "qt (US)", "Quarts (US)", 0.946352946),
                ("gal_us", "gal (US)", "Gallons (US)", 3.785411784),
                ("in3", "in³", "Cubic Inches", 0.016387064),
                ("ft3", "ft³", "Cubic Feet", 28.316846592),
                ("yd3", "yd³", "Cubic Yards", 764.554857984),
                ("tsp_uk", "tsp (UK)", "Teaspoons (UK)", 0.00591938802083333),
                ("tbsp_uk", "tbsp (UK)", "Tablespoons (UK)", 0.0177581640625),
                ("floz_uk", "fl oz (UK)", "Fluid Ounces (UK)", 0.0284130625),
                ("pt_uk", "pt (UK)", "Pints (UK)", 0.56826125),
                ("qt_uk", "qt (UK)", "Quarts (UK)", 1.1365225),
                ("gal_uk", "gal (UK)", "Gallons (UK)", 4.54609)
            });
        }
    }
}
