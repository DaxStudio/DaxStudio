// Decompiled with JetBrains decompiler
// Type: DAXDebugOutput.AS.DAXEvalAndLogData
// Assembly: DAXDebugOutput, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D4D903F6-0D72-4E27-92E8-23EAA55A70E4
// Assembly location: C:\Program Files\DAX Debug Output\DAXDebugOutput.dll

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

#nullable enable
namespace DAXDebugOutput.AS
{
    internal enum DAXDataType
    {
        NULL,
        INTEGER,
        CURRENCY,
        DOUBLE,
        DATETIME,
        BOOLEAN,
        STRING,
        VARIANT,
    }

    internal class DAXEvalAndLogData
    {
        private string _expression;
        private bool _isScalar = true;
        private string? _label;
        private int _inputColumnCount;
        private int _outputColumnCount;
        private int _specialColumnCount;
        private List<string> _columns = new List<string>();
        private List<int> _columnSizes = new List<int>();
        private List<DAXDataType> _types = new List<DAXDataType>();
        private List<List<object?>> _values = new List<List<object>>();
        private string? _notice;

        internal string Expression => this._expression;

        internal bool IsScalar => this._isScalar;

        internal string? Label => this._label;

        internal int InputColumnCount => this._inputColumnCount;

        internal int OutputColumnCount => this._outputColumnCount;

        internal int SpecialColumnCount => this._specialColumnCount;

        internal int TotalColumnCount
        {
            get => this.InputColumnCount + this.OutputColumnCount + this.SpecialColumnCount;
        }

        internal List<string> Columns => this._columns;

        internal List<int> ColumnSizes => this._columnSizes;

        internal List<DAXDataType> Types => this._types;

        internal List<List<object?>> Values => this._values;

        internal string? Notice => this._notice;

        internal DAXEvalAndLogData(string textData)
        {
            JsonElement rootElement = JsonDocument.Parse(textData).RootElement;
            this._expression = rootElement.GetProperty("expression").GetString();
            JsonElement jsonElement1;
            if (rootElement.TryGetProperty("label", out jsonElement1))
                this._label = jsonElement1.GetString();
            JsonElement jsonElement2;
            if (rootElement.TryGetProperty("notice", out jsonElement2))
            {
                this._notice = jsonElement2.GetString();
                if (this.Notice == "EvaluateAndLog function is not executed due to optimization.")
                    return;
            }
            JsonElement jsonElement3;
            if (rootElement.TryGetProperty("inputs", out jsonElement3))
            {
                foreach (JsonElement enumerate in jsonElement3.EnumerateArray())
                    this.AddColumn(enumerate.GetString(), 0, DAXDataType.NULL);
                this._inputColumnCount = this._columns.Count;
            }
            JsonElement jsonElement4;
            if (rootElement.TryGetProperty("outputs", out jsonElement4))
            {
                this._isScalar = false;
                foreach (JsonElement enumerate in jsonElement4.EnumerateArray())
                    this.AddColumn(enumerate.GetString(), 0, DAXDataType.NULL);
            }
            else
            {
                this.AddColumn("Value", 0, DAXDataType.NULL);
                this._outputColumnCount = 1;
            }
            this._outputColumnCount = this._columns.Count - this.InputColumnCount;
            if (!this.IsScalar && this.InputColumnCount > 0)
            {
                this.AddColumn("Table Number", 0, DAXDataType.INTEGER);
                this.AddColumn("Row Number", 0, DAXDataType.INTEGER);
                this.AddColumn("Row Count", 0, DAXDataType.INTEGER);
                this._specialColumnCount = 3;
            }
            JsonElement jsonElement5;
            if (!rootElement.TryGetProperty("data", out jsonElement5))
                return;
            int x1 = 1;
            foreach (JsonElement enumerate1 in jsonElement5.EnumerateArray())
            {
                int x2 = 1;
                List<object> collection = new List<object>();
                if (this.InputColumnCount > 0)
                {
                    JsonElement property = enumerate1.GetProperty("input");
                    for (int index = 0; index < property.GetArrayLength(); ++index)
                    {
                        (object obj, DAXDataType type2, int num) = DAXEvalAndLogData.ParseValue(property[index]);
                        collection.Add(obj);
                        if (this._types.Count < this.TotalColumnCount)
                            this._types.Add(type2);
                        else
                            this._types[index] = this.GetCommonType(this._types[index], type2);
                        if (num > this._columnSizes[index])
                            this._columnSizes[index] = num;
                    }
                }
                if (this.IsScalar)
                {
                    (object obj, DAXDataType type2, int num) = DAXEvalAndLogData.ParseValue(enumerate1.GetProperty("output"));
                    this._values.Add(new List<object>((IEnumerable<object>)collection)
          {
            obj
          });
                    this._types[this.InputColumnCount] = this.GetCommonType(this._types[this.InputColumnCount], type2);
                    if (num > this._columnSizes[this.InputColumnCount])
                        this._columnSizes[this.InputColumnCount] = num;
                }
                else
                {
                    int int32 = enumerate1.GetProperty("rowCount").GetInt32();
                    foreach (JsonElement enumerate2 in enumerate1.GetProperty("output").EnumerateArray())
                    {
                        List<object> objectList = new List<object>((IEnumerable<object>)collection);
                        for (int index1 = 0; index1 < this.OutputColumnCount; ++index1)
                        {
                            (object obj, DAXDataType type2, int num) = DAXEvalAndLogData.ParseValue(enumerate2[index1]);
                            objectList.Add(obj);
                            int index2 = this.InputColumnCount + index1;
                            this._types[index2] = this.GetCommonType(this._types[index2], type2);
                            if (num > this._columnSizes[index2])
                                this._columnSizes[index2] = num;
                        }
                        if (this.SpecialColumnCount > 0)
                        {
                            int index3 = this.InputColumnCount + this.OutputColumnCount;
                            objectList.Add((object)x1);
                            int integerLength1 = DAXEvalAndLogData.GetIntegerLength((long)x1);
                            if (integerLength1 > this._columnSizes[index3])
                                this._columnSizes[index3] = integerLength1;
                            int index4 = index3 + 1;
                            objectList.Add((object)x2++);
                            int integerLength2 = DAXEvalAndLogData.GetIntegerLength((long)x2);
                            if (integerLength2 > this._columnSizes[index4])
                                this._columnSizes[index4] = integerLength2;
                            int index5 = index4 + 1;
                            objectList.Add((object)int32);
                            int integerLength3 = DAXEvalAndLogData.GetIntegerLength((long)int32);
                            if (integerLength3 > this._columnSizes[index5])
                                this._columnSizes[index5] = integerLength3;
                        }
                        this._values.Add(objectList);
                    }
                    ++x1;
                }
            }
        }

        private static (object?, DAXDataType, int) ParseValue(JsonElement elem)
        {
            switch (elem.ValueKind)
            {
                case JsonValueKind.String:
                    string s = elem.GetString();
                    if (s.Length == 10)
                    {
                        try
                        {
                            return ((object)DateTime.ParseExact(s, "yyyy-MM-dd", (IFormatProvider)CultureInfo.InvariantCulture), DAXDataType.DATETIME, 10);
                        }
                        catch (FormatException ex)
                        {
                        }
                    }
                    else if (s.Length == 19)
                    {
                        try
                        {
                            return ((object)DateTime.ParseExact(s, "yyyy-MM-ddThh:mm:tt", (IFormatProvider)CultureInfo.InvariantCulture), DAXDataType.DATETIME, 19);
                        }
                        catch (FormatException ex)
                        {
                        }
                    }
                    return ((object)s, DAXDataType.STRING, s.Length);
                case JsonValueKind.Number:
                    long x1;
                    if (elem.TryGetInt64(out x1))
                        return ((object)x1, DAXDataType.INTEGER, DAXEvalAndLogData.GetIntegerLength(x1));
                    Decimal x2;
                    return elem.TryGetDecimal(out x2) ? ((object)x2, DAXDataType.CURRENCY, DAXEvalAndLogData.GetDecimalLength(x2)) : ((object)elem.GetDouble(), DAXDataType.DOUBLE, DAXEvalAndLogData.GetDoubleLength(elem.GetDouble()));
                case JsonValueKind.True:
                    return ((object)true, DAXDataType.BOOLEAN, 4);
                case JsonValueKind.False:
                    return ((object)false, DAXDataType.BOOLEAN, 5);
                case JsonValueKind.Null:
                    return ((object)null, DAXDataType.NULL, 0);
                default:
                    return ((object)"INVALID VALUE", DAXDataType.VARIANT, 100);
            }
        }

        private DAXDataType GetCommonType(DAXDataType type1, DAXDataType type2)
        {
            if (type1 == DAXDataType.VARIANT || type2 == DAXDataType.VARIANT)
                return DAXDataType.VARIANT;
            if (type1 == type2)
                return type1;
            if (type1 == DAXDataType.NULL)
                return type2;
            if (type2 == DAXDataType.NULL)
                return type1;
            if (!DAXEvalAndLogData.IsNumericType(type1) || !DAXEvalAndLogData.IsNumericType(type2))
                return DAXDataType.VARIANT;
            return type1 == DAXDataType.INTEGER || type2 != DAXDataType.INTEGER && type1 == DAXDataType.CURRENCY ? type2 : type1;
        }

        private static int GetIntegerLength(long x)
        {
            int integerLength = 0;
            if (x < 0L)
            {
                ++integerLength;
                x = -x;
            }
            while (x > 0L)
            {
                x /= 10L;
                ++integerLength;
            }
            return integerLength;
        }

        private static int GetDecimalLength(Decimal x)
        {
            return DAXEvalAndLogData.GetIntegerLength((long)x) + 4;
        }

        private static int GetDoubleLength(double x)
        {
            return DAXEvalAndLogData.GetIntegerLength((long)x) + 2;
        }

        internal static bool IsNumericType(DAXDataType type)
        {
            return type == DAXDataType.INTEGER || type == DAXDataType.CURRENCY || type == DAXDataType.DOUBLE;
        }

        private void AddColumn(string columnName, int columnSize, DAXDataType dataType)
        {
            this._columns.Add(columnName);
            this._columnSizes.Add(columnSize);
            this._types.Add(dataType);
        }
    }
}
