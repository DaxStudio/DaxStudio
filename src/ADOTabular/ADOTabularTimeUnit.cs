using ADOTabular.Interfaces;
using System;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;

namespace ADOTabular
{

    public class ADOTabularTimeUnit: IADOTabularObject
    {
        

        public ADOTabularTimeUnit( ADOTabularColumn primaryColumn, ADOTabularColumn sortByColumn, List<ADOTabularColumn> associatedColumns)
        {
            Column = primaryColumn;
            SortByColumn = sortByColumn;
            AssociatedColumns = associatedColumns;
        }

        public ADOTabularColumn Column {get; }

        public ADOTabularColumn SortByColumn { get; }

        public List<ADOTabularColumn> AssociatedColumns { get; }

        public string Name => Column.Name;
        public string DaxName => Column.DaxName;
        public ADOTabularObjectType ObjectType => ADOTabularObjectType.TimeUnit;
        public bool IsVisible => true;
        public string Description => Column.Description;

        public Type SystemType => Column.SystemType;

        public Microsoft.AnalysisServices.Tabular.DataType DataType => Column.DataType;

        public MetadataImages MetadataImage => Column.MetadataImage;

        public string TableName => Column.TableName;
    }
}
