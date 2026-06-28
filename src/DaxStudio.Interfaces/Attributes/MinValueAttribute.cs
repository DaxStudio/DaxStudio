using System;

namespace DaxStudio.Interfaces.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MinValueAttribute: Attribute
    {
        public MinValueAttribute(double minValue)
        {
            MinValue = minValue;
        }

        public double MinValue { get; }
    }
}
