using System;

namespace DaxStudio.Interfaces.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SubcategoryAttribute: Attribute
    {
        public SubcategoryAttribute(string subcategory)
        {
            Subcategory = subcategory;
        }

        public string Subcategory { get; }
    }
}
