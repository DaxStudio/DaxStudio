using ADOTabular;
using DaxStudio.UI.Model;
using Moq;
using System;
using ADOTabular.Interfaces;

namespace DaxStudio.Tests.Mocks
{
    public class MockColumn 
    {

        public static IADOTabularColumn CreateADOTabularColumn(string caption, string daxName, Type dataType, ADOTabularObjectType objectType, bool isModelItem = true, SortDirection sortDirection = SortDirection.ASC)
        {
            var col = new Mock<IADOTabularColumn>();
            col.SetupGet(x => x.Caption).Returns(caption);
            col.SetupGet(x => x.DaxName).Returns(daxName);
            col.SetupGet(x => x.SystemType).Returns(dataType);
            col.SetupGet(x => x.ObjectType).Returns(objectType);
            return col.Object;
        }

        public static QueryBuilderColumn Create(string caption, string daxName, Type dataType, ADOTabularObjectType objectType, bool isModelItem = true, SortDirection sortDirection = SortDirection.ASC)
        {
            var col = CreateADOTabularColumn(caption, daxName, dataType, objectType, isModelItem, sortDirection);

            var col2 = new QueryBuilderColumn(col, isModelItem, new MockEventAggregator());

            return col2;
        }

    }
}
