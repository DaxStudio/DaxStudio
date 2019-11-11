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
          OneHundredPercentFontSize = baseFontSize;
      }

    /// <summary>
    /// A font size of 12 is equivalent to 100% (percent) display size.
    /// </summary>
      public  double OneHundredPercentFontSize { get; set; }


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
          return ScreenFontPoints.ToUnit(inputValue, outputUnit, OneHundredPercentFontSize);

        case Itemkey.ScreenPercent:
          return ScreenPercent.ToUnit(inputValue, outputUnit, OneHundredPercentFontSize);

        default:
          throw new NotImplementedException(outputUnit.ToString());
       }
    }
  }
}
