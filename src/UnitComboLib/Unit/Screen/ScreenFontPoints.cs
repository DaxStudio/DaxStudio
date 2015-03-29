namespace UnitComboLib.Unit.Screen
{
  using System;

  /// <summary>
  /// Class to convert from font sizes to other unit based values.
  /// </summary>
  public class ScreenFontPoints
  {
    private double mValue = 0;

    #region constructor
    /// <summary>
    /// Class constructor
    /// </summary>
    /// <param name="value"></param>
    public ScreenFontPoints(double value)
    {
      this.mValue = value;
    }

    private ScreenFontPoints()
    {      
    }
    #endregion constructor

    #region methods
    /// <summary>
    /// Convert a font size to other values.
    /// </summary>
    /// <param name="inputValue"></param>
    /// <param name="targetUnit"></param>
    /// <returns></returns>
    public static double ToUnit(double inputValue, Itemkey targetUnit)
    {
      ScreenFontPoints d = new ScreenFontPoints(inputValue);

      return d.ToUnit(targetUnit);
    }

    /// <summary>
    /// Convert a font size to other values.
    /// </summary>
    /// <param name="targetUnit"></param>
    /// <returns></returns>
    public double ToUnit(Itemkey targetUnit)
    {
      switch (targetUnit)
      {
        case Itemkey.ScreenPercent:
          return this.mValue * (100 / ScreenConverter.OneHundretPercentFont);

        case Itemkey.ScreenFontPoints:
          return this.mValue;

        default:
          throw new NotImplementedException(targetUnit.ToString());
      }
    }
    #endregion methods
  }
}
