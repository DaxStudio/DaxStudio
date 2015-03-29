namespace UnitComboLib.Unit
{
  /// <summary>
  /// Enumeration keys for each unit
  /// </summary>
  public enum Itemkey
  {
    /// <summary>
    /// Units of computer font screen dimensions
    /// </summary>
    ScreenFontPoints = 5,

    /// <summary>
    /// A percentage of the <seealso cref="ScreenFontPoints"/> with 12 being equivalent to 100%.
    /// </summary>
    ScreenPercent = 6
  }

  /// <summary>
  /// Abstract converter class definition to convert values from one unit to the other.
  /// </summary>
  public abstract class Converter
  {
    /// <summary>
    /// Converter method to convert a value from one unit to the other.
    /// </summary>
    /// <param name="inputUnit">Unit of <paramref name="inputValue"/></param>
    /// <param name="inputValue">Amount of value to convert</param>
    /// <param name="outputUnit">Expected Unit of value to be converted to.</param>
    /// <returns>Converted value.</returns>
    public abstract double Convert(Itemkey inputUnit, double inputValue, Itemkey outputUnit);
  }
}
