using System;
using System.Collections.Generic;
using System.Text;

namespace DaxStudio.AdomdClientWrappers
{
    public class AdomdParameter
    {
        public AdomdParameter(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; set; }
        public object Value { get; set; }
    }

    public class AdomdParameterCollection : List<AdomdParameter>
    {
    }

}
