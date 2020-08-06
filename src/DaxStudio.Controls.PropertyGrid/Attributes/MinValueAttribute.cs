using System;

namespace DaxStudio.Controls.PropertyGrid
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MinValueAttribute: Attribute
    {
        public MinValueAttribute(double minValue)
        {
            MinValue = minValue;
        }

        public double MinValue { get; }
    }
}
