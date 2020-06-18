using ADOTabular;
using DaxStudio.UI.Model;
using Moq;
using System;

namespace DaxStudio.Tests.Mocks
{
    public class MockColumn 
    {

        public static QueryBuilderColumn Create(string caption, string daxName, Type dataType, ADOTabularObjectType objectType, bool isModelItem = true)
        {
            var col = new Mock<IADOTabularColumn>();
            col.SetupGet(x => x.Caption).Returns(caption);
            col.SetupGet(x => x.DaxName).Returns(daxName);
            col.SetupGet(x => x.DataType).Returns(dataType);
            col.SetupGet(x => x.ObjectType).Returns(objectType);

            var col2 = new QueryBuilderColumn(col.Object, isModelItem);

            return col2;
        }

    }
}
