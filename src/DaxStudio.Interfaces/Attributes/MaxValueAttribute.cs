using System;

namespace DaxStudio.Interfaces.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MaxValueAttribute: Attribute
    {
        public MaxValueAttribute(double maxValue)
        {
            MaxValue = maxValue;
        }

        public double MaxValue { get; }
    }
}
