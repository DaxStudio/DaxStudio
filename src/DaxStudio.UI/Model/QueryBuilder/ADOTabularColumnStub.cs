using System;
using System.Collections.Generic;
using ADOTabular;
using ADOTabular.Interfaces;
using Microsoft.AnalysisServices.Tabular;

namespace DaxStudio.UI.Model
{
    public class ADOTabularColumnStub: IADOTabularColumn
    {
        public string Caption { get;  set; }
        public string DaxName { get;  set; }
        public string Name { get;  set; }
        public string Description { get;  set; }
        public bool IsVisible { get;  set; }
        public ADOTabularObjectType ObjectType { get;  set; }
        public string MinValue { get;  set; }
        public string MaxValue { get;  set; }
        public long DistinctValues { get;  set; }
        public void UpdateBasicStats(ADOTabularConnection connection)
        {
            throw new NotImplementedException();
        }

        public List<string> GetSampleData(ADOTabularConnection connection, int sampleSize)
        {
            throw new NotImplementedException();
        }

        public MetadataImages MetadataImage { get;  set; }
        public string MeasureExpression { get;  set; }
        public string TableName { get;  set; }

        public Type SystemType { get; set; }

        public DataType DataType { get; set; }
    }
}
