using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular
{
    public interface IADOTabularColumn:IADOTabularObject
    {
        string MinValue { get; }
        string MaxValue { get; }
        long DistinctValues { get; }
        void UpdateBasicStats(ADOTabularConnection connection);
        List<string> GetSampleData(ADOTabularConnection connection, int sampleSize);
    }
}
