using System;
using System.Collections.Generic;

namespace ADOTabular.Interfaces
{
    public interface IADOTabularColumn:IADOTabularObject
    {
        string MinValue { get; }
        string MaxValue { get; }
        long DistinctValues { get; }
        void UpdateBasicStats(ADOTabularConnection connection);
        List<string> GetSampleData(ADOTabularConnection connection, int sampleSize);
        Type DataType { get; }

        MetadataImages MetadataImage { get; }

        string MeasureExpression { get; }

        string TableName { get; }
    }
}
