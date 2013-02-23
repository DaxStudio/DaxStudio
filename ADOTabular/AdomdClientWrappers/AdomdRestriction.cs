using System;
using System.Collections.Generic;
using System.Text;

namespace DaxStudio.AdomdClientWrappers
{
    public class AdomdRestriction
    {
        public AdomdRestriction(string name, object restrictionValue)
        {
            Name = name;
            Value = restrictionValue;
        }

        public string Name { get; set; }
        public object Value { get; set; }
    }

    public class AdomdRestrictionCollection : List<AdomdRestriction>
    {
        public void Add(string propertyName, object value)
        {
            this.Add(new AdomdRestriction(propertyName,value));
        }
    }

}
