using System;
using System.Collections.Generic;

namespace ADOTabular.AdomdClientWrappers
{
    public class Property
    {
        public Property(string name, object restrictionValue, Type type)
        {
            Name = name;
            Value = restrictionValue;
            Type = type;
        }

        public string Name { get; set; }
        public object Value { get; set; }
        public Type Type { get; set; }
    }

    public class PropertyCollection : Dictionary<string, Property>
    {
    }

}
