using ADOTabular.Interfaces;
using System;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;

namespace ADOTabular
{

    public class ADOTabularLevel: IADOTabularObject, IADOTabularColumn
    {
        

        public ADOTabularLevel( ADOTabularColumn column)
        {
            Column = column;
        }

        public ADOTabularColumn Column {get; }

        public string LevelName { get; set; }
        private string _caption;
        public string Caption { 
            get => string.IsNullOrEmpty(_caption) ? LevelName : _caption;
            set => _caption = value;
        }
        public string Name => Column.Name;
        public string DaxName => Column.DaxName;
        public ADOTabularObjectType ObjectType => Column.ObjectType;
        public bool IsVisible => true;
        public string Description => Column.Description;

        public string MinValue => Column.MinValue;

        public string MaxValue => Column.MaxValue;

        public long DistinctValues => Column.DistinctValues;

        public Type SystemType => Column.SystemType;

        public Microsoft.AnalysisServices.Tabular.DataType DataType => Column.DataType;

        public MetadataImages MetadataImage => Column.MetadataImage;

        public string MeasureExpression => string.Empty;

        public string TableName => Column.TableName;

        public void UpdateBasicStats(ADOTabularConnection connection)
        {
            Column.UpdateBasicStats(connection);
        }

        public List<string> GetSampleData(ADOTabularConnection connection, int sampleSize)
        {
            return Column.GetSampleData(connection, sampleSize);
        }
    }
}
