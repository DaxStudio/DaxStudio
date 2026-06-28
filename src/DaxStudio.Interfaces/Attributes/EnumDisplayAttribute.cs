using System;

namespace DaxStudio.Interfaces.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EnumDisplayAttribute: Attribute
    {
        public EnumDisplayAttribute(EnumDisplayOptions enumDisplay)
        {
            EnumDisplay = enumDisplay;
        }

        public EnumDisplayOptions EnumDisplay { get; }
    }

    public enum EnumDisplayOptions
    {
        Description,
        Value
    }
}
