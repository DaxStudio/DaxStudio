using ADOTabular;
using DaxStudio.UI.Model;
using NSubstitute;
using System;
using ADOTabular.Interfaces;

namespace DaxStudio.Tests.Mocks
{
    public class MockColumn 
    {

        public static IADOTabularColumn CreateADOTabularColumn(string caption, string daxName, Type dataType, ADOTabularObjectType objectType, bool isModelItem = true, SortDirection sortDirection = SortDirection.ASC)
        {
            var col = Substitute.For<IADOTabularColumn>();
            col.Caption.Returns(caption);
            col.DaxName.Returns(daxName);
            col.SystemType.Returns(dataType);
            col.ObjectType.Returns(objectType);
            return col;
        }

        public static QueryBuilderColumn Create(string caption, string daxName, Type dataType, ADOTabularObjectType objectType, bool isModelItem = true, SortDirection sortDirection = SortDirection.ASC)
        {
            var col = CreateADOTabularColumn(caption, daxName, dataType, objectType, isModelItem, sortDirection);

            var col2 = new QueryBuilderColumn(col, isModelItem, new MockEventAggregator());

            return col2;
        }

    }
}
