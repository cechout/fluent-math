namespace FluentMath.Models.Converters
{
    // the length converter; the units and the order of the Windows Calculator, in metres
    public static class LengthUnits
    {
        public static LinearUnitSource Create()
        {
            return new LinearUnitSource("Length", "m", "ft", new[]
            {
                ("nm", "nm", "Nanometers", 1e-9),
                ("um", "µm", "Microns", 1e-6),
                ("mm", "mm", "Millimeters", 0.001),
                ("cm", "cm", "Centimeters", 0.01),
                ("m", "m", "Meters", 1.0),
                ("km", "km", "Kilometers", 1000.0),
                ("in", "in", "Inches", 0.0254),
                ("ft", "ft", "Feet", 0.3048),
                ("yd", "yd", "Yards", 0.9144),
                ("mi", "mi", "Miles", 1609.344),
                ("nmi", "nmi", "Nautical Miles", 1852.0)
            });
        }
    }
}
