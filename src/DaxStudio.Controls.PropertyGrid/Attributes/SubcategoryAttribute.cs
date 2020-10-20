using System;

namespace DaxStudio.Controls.PropertyGrid
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
