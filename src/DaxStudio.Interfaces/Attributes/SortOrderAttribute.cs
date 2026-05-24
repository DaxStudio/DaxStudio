using System;

namespace DaxStudio.Interfaces.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SortOrderAttribute: Attribute
    {
        public SortOrderAttribute(int sortOrder)
        {
            SortOrder = sortOrder;
        }

        public int SortOrder { get; }
    }
}
