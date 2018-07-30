namespace UnitComboLib.Unit.Screen
{
  using System;

  /// <summary>
  /// Class to convert from font sizes to other unit based values.
  /// </summary>
  public static class ScreenFontPoints
  {

    /// <summary>
    /// Convert a font size to other values.
    /// </summary>
    /// <param name="targetUnit"></param>
    /// <returns></returns>
    public static double ToUnit(double inputValue, Itemkey targetUnit, double oneHundredPercentFontSize)
    {
      switch (targetUnit)
      {
        case Itemkey.ScreenPercent:
          return inputValue * (100 / oneHundredPercentFontSize);

        case Itemkey.ScreenFontPoints:
          return inputValue;

        default:
          throw new NotImplementedException(targetUnit.ToString());
      }
    }

  }
}
