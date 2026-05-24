using System;

namespace DaxStudio.Interfaces.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SubcategoryAttribute: Attribute
    {
        public SubcategoryAttribute(string subcategory)
        {
            Subcategory = subcategory;
        }

        public string Subcategory { get; }
    }
}
