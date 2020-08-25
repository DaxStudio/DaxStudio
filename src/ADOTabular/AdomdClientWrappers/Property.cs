using System;
using System.Collections.Generic;

namespace ADOTabular.AdomdClientWrappers
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "This is just wrapping the ADOMD Property class")]
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "This is just wrapping the ADOMD PropertyCollection class")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2237:Mark ISerializable types with serializable", Justification = "<Pending>")]
    public class PropertyCollection : Dictionary<string, Property>
    {
    }

}
