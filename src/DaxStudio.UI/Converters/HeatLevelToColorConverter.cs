using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DaxStudio.UI.Converters
{
    /// <summary>
    /// Converts a heat level (0.0 - 1.0) to a gradient color from cool (blue) to hot (red/orange).
    /// </summary>
    public class HeatLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double heatLevel)
            {
                // Clamp to 0-1 range
                heatLevel = Math.Max(0, Math.Min(1, heatLevel));

                // Create a gradient from blue (cold) through green/yellow to orange/red (hot)
                // Using HSL-like interpolation for better visual gradient
                Color color;

                if (heatLevel < 0.5)
                {
                    // Blue to Green (cold to warm)
                    double t = heatLevel * 2; // 0 to 1
                    color = InterpolateColor(
                        Color.FromRgb(66, 133, 244),   // Cool blue
                        Color.FromRgb(52, 168, 83),    // Green
                        t);
                }
                else
                {
                    // Green to Orange/Red (warm to hot)
                    double t = (heatLevel - 0.5) * 2; // 0 to 1
                    color = InterpolateColor(
                        Color.FromRgb(52, 168, 83),    // Green
                        Color.FromRgb(234, 67, 53),    // Red/Orange
                        t);
                }

                return new SolidColorBrush(color);
            }

            // Default to theme accent if not a valid heat level
            return Binding.DoNothing;
        }

        private Color InterpolateColor(Color from, Color to, double t)
        {
            return Color.FromRgb(
                (byte)(from.R + (to.R - from.R) * t),
                (byte)(from.G + (to.G - from.G) * t),
                (byte)(from.B + (to.B - from.B) * t));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
