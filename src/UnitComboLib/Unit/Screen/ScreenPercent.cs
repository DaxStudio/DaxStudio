namespace UnitComboLib.Unit.Screen
{
  using System;

  /// <summary>
  /// Class to convert from percentage values into other units.
  /// </summary>
  public class ScreenPercent
  {
    private double mValue = 0;

    #region constructor
    /// <summary>
    /// Class constructor.
    /// </summary>
    /// <param name="value"></param>
    public ScreenPercent(double value)
    {
      this.mValue = value;
    }

    private ScreenPercent()
    {
    }
    #endregion constructor

    #region methods
    /// <summary>
    /// Convert percentage unit based value into another unit based value.
    /// </summary>
    /// <param name="inputValue"></param>
    /// <param name="targetUnit"></param>
    /// <returns></returns>
    public static double ToUnit(double inputValue, Itemkey targetUnit)
    {
      ScreenPercent d = new ScreenPercent(inputValue);

      return d.ToUnit(targetUnit);
    }

    /// <summary>
    /// Convert percentage unit based value into another unit based value.
    /// </summary>
    /// <param name="targetUnit"></param>
    /// <returns></returns>
    public double ToUnit(Itemkey targetUnit)
    {
      switch (targetUnit)
      {
        case Itemkey.ScreenPercent:
          return this.mValue;

        case Itemkey.ScreenFontPoints:
          return (this.mValue * ScreenConverter.OneHundretPercentFont) / 100.0;

        default:
          throw new NotImplementedException(targetUnit.ToString());
      }
    }
    #endregion methods
  }
}
