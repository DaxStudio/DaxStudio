﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ADOTabular;
using ADOTabular.Interfaces;
using DaxStudio.UI.JsonConverters;
using Microsoft.AnalysisServices.Tabular;
using Newtonsoft.Json;

namespace DaxStudio.UI.Model
{
    public class ADOTabularColumnStub: IADOTabularColumn
    {
        private string _caption = string.Empty;
        public string Caption { get => _caption;
            set
            {
                _caption = value;
                DaxName = $"[{_caption.Replace("]", "]]")}]";
            }
        }
        public string DaxName { get; private set; }
        
        public string Name => Caption;
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
        [JsonConverter(typeof(QueryBuilderColumnDataTypeConverter ))]
        public DataType DataType { get; set; }

        public string ImageResource { get; set; }
        static Dictionary<DataType, Type> DataTypeLookup = new Dictionary<DataType, Type>()
        {
            {DataType.String, typeof(String) },
            {DataType.Boolean, typeof(Boolean) },
            {DataType.DateTime, typeof(DateTime) },
            {DataType.Decimal, typeof(Decimal) },
            {DataType.Double, typeof(Double) },
            {DataType.Int64, typeof(long) },
            {DataType.Unknown, typeof(object) },
            {DataType.Variant, typeof(object) },

        };

        [OnDeserialized]
        internal void OnSerializedMethod(StreamingContext context)
        {
            if (SystemType == null )
            {
                DataTypeLookup.TryGetValue(DataType, out var type);
                SystemType = type??typeof(object);
            } 
        }
    }
}
