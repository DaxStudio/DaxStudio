using Caliburn.Micro;
using DaxStudio.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Documents;

namespace DaxStudio.UI.Model
{
    public class QueryParameter: PropertyChangedBase
    {
        public QueryParameter(string name)
        {
            Name = name;
        }
        public QueryParameter(string name, object value,string typeName)
        {
            Name = name;
            Value = value;
            TypeName = typeName;
        }
        public string Name { get; set; }
        public object Value { get; set; }

        public string LiteralValue
        {
            get
            {
                if (string.IsNullOrEmpty(TypeName)) return "BLANK()";

                switch (TypeName)
                {
                    case "xsd:string" :
                        return $"\"{Value?.ToString()}\"";
                    case "xsd:dateTime":
                        try
                        {
                            var dt = (DateTime)Value;
                            return $"DATE({dt.Year},{dt.Month},{dt.Day})";
                        }
                        catch
                        {
                            return $"/* ERROR: Unable to convert the value '{Value?.ToString()}' to a datetime */ DATE(0,0,0)";
                        }
                    case "xsd:boolean":
                        return (bool)Value ? "TRUE()" : "FALSE()";
                    default:
                        if (string.IsNullOrEmpty(Value?.ToString())) return "BLANK()";
                        return Value?.ToString();
                }

            }
        }

        string _typeName = "string";
        public string TypeName { get => _typeName;
            set {
                _typeName = value;
            } 
        }

        private string _valueString;
        public string ValueString { get => _valueString;
            set {
                _valueString = value;
                try
                {
                    var valueType = DaxStudio.Common.XmlTypeMapper.GetSystemType($"xsd:{TypeName}");
                    if (_valueString.Contains("\n"))
                    {
                        Type t = typeof(List<>).MakeGenericType(valueType);
                        IList res = (IList)Activator.CreateInstance(t);
                        var valueList = _valueString.Split('\n');
                        foreach (var v in valueList)
                        {
                            res.Add(Convert.ChangeType(v, valueType));
                        }
                        IsValid = true;
                        ConversionError = string.Empty;
                    }
                    else
                    {
                        Value = Convert.ChangeType(_valueString, valueType);
                    }
                    IsValid = true;
                    ConversionError = string.Empty;
                }
                catch(Exception ex)
                {
                    IsValid = false;
                    ConversionError = ex.Message;
                }
            } 
        }

        private bool _isValid = true;
        public bool IsValid { get => _isValid; 
            set { 
                _isValid = value;
                NotifyOfPropertyChange(nameof(IsValid));
            } 
        }
        private string _conversionError = string.Empty;
        public string ConversionError { get => _conversionError;
            set {
                _conversionError = value;
                NotifyOfPropertyChange(nameof(ConversionError));
            } 
        }
    }
}
