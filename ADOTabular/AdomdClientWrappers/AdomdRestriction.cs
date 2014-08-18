using System.Collections.Generic;

namespace ADOTabular.AdomdClientWrappers
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

        internal void Add(Microsoft.AnalysisServices.AdomdClient.AdomdRestriction adomdRestriction)
        {
            throw new System.NotImplementedException();
        }
    }

}
