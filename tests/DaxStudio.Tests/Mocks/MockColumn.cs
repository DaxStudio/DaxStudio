using ADOTabular;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests.Mocks
{
    public class MockColumn : IADOTabularColumn
    {
        public MockColumn(string caption, string daxName, Type dataType, ADOTabularObjectType objectType)
        {
            Caption = caption;
            DaxName = daxName;
            DataType = dataType;
            ObjectType = objectType;
        }

        public string MinValue => throw new NotImplementedException();

        public string MaxValue => throw new NotImplementedException();

        public long DistinctValues => throw new NotImplementedException();

        public Type DataType { get; }

        public string Caption { get; }

        public string DaxName { get; }

        public string Name => throw new NotImplementedException();

        public bool IsVisible => throw new NotImplementedException();

        public ADOTabularObjectType ObjectType { get; }

        public MetadataImages MetadataImage => throw new NotImplementedException();

        public List<string> GetSampleData(ADOTabularConnection connection, int sampleSize)
        {
            throw new NotImplementedException();
        }

        public void UpdateBasicStats(ADOTabularConnection connection)
        {
            throw new NotImplementedException();
        }


    }
}
