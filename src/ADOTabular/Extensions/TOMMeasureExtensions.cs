using System;
using Microsoft.AnalysisServices.Tabular;

namespace ADOTabular.Extensions
{
    public static class TOMMeasureExtensions
    {
        private static readonly Action<Measure, DataType> DataTypeSetter = (Action<Measure, DataType>)Delegate.CreateDelegate(typeof(Action<Measure, DataType>), null, typeof(Measure).GetProperty("DataType")?.GetSetMethod(true)!);
        // uses a static delegate to set the DataType property on a Measure which has an internal setter
        public static void SetDataType(this Measure measure, DataType dataType)
        {
            if (measure == null) return;
            DataTypeSetter(measure, dataType);
        }
    }
}
