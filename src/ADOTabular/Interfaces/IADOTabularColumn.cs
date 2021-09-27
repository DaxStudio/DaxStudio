using System;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;

namespace ADOTabular.Interfaces
{
    public interface IADOTabularColumn:IADOTabularObject
    {
        string MinValue { get; }
        string MaxValue { get; }
        long DistinctValues { get; }
        void UpdateBasicStats(ADOTabularConnection connection);
        List<string> GetSampleData(ADOTabularConnection connection, int sampleSize);
        Type SystemType { get; }
        DataType DataType { get; }
        MetadataImages MetadataImage { get; }

        string MeasureExpression { get; }

        string TableName { get; }

    }
}
