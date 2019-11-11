namespace UnitComboLib.Unit.Screen
{
    using System;

    /// <summary>
    /// Class to convert from percentage values into other units.
    /// </summary>
    public static class ScreenPercent
    {

        #region methods
        /// <summary>
        /// Convert percentage unit based value into another unit based value.
        /// </summary>
        /// <param name="inputValue"></param>
        /// <param name="targetUnit"></param>
        /// <param name="oneHundredPercentFontSize"></param>
        /// <returns></returns>
        public static double ToUnit(double inputValue, Itemkey targetUnit, double oneHundredPercentFontSize)
        {
            switch (targetUnit)
            {
                case Itemkey.ScreenPercent:
                    return inputValue;

                case Itemkey.ScreenFontPoints:
                    return (inputValue * oneHundredPercentFontSize) / 100.0;

                default:
                    throw new NotImplementedException(targetUnit.ToString());
            }
        }
        #endregion methods
    }
}
