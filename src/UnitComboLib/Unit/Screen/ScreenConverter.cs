namespace UnitComboLib.Unit.Screen
{
  using System;
  
  /// <summary>
  /// Classs to convert font size values into a percentage value and back.
  /// </summary>
  public class ScreenConverter : Converter
  {
      /// <summary>
      /// Default constructor to set the base font size to 12 point
      /// </summary>
      public ScreenConverter():this(12.0)  {  }
      /// <summary>
      /// Optional constuctor that allows for the overriding fo the base font size
      /// </summary>
      /// <param name="baseFontSize"></param>
      public ScreenConverter(double baseFontSize)
      {
          OneHundretPercentFont = baseFontSize;
      }

    /// <summary>
    /// A font size of 12 is equivalent to 100% (percent) display size.
    /// </summary>
      public static double OneHundretPercentFont { get; set; }

    /// <summary>
    /// This is the standard value to scale against when using percent instead of fontsize.
    /// </summary>
    public const double OneHundretPercent = 100.0;

    /// <summary>
    /// Convert between different units of screen resolutions.
    /// </summary>
    /// <param name="inputUnit"></param>
    /// <param name="inputValue"></param>
    /// <param name="outputUnit"></param>
    /// <returns></returns>
    public override double Convert(Itemkey inputUnit, double inputValue, Itemkey outputUnit)
    {
      switch (inputUnit)
      {
        case Itemkey.ScreenFontPoints:
          return ScreenFontPoints.ToUnit(inputValue, outputUnit);

        case Itemkey.ScreenPercent:
          return ScreenPercent.ToUnit(inputValue, outputUnit);

        default:
          throw new NotImplementedException(outputUnit.ToString());
       }
    }
  }
}
