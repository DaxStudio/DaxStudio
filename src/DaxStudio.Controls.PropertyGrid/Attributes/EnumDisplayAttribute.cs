using System;

namespace DaxStudio.Controls.PropertyGrid
{
    [AttributeUsage(AttributeTargets.Property)]
    public class EnumDisplayAttribute: Attribute
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
