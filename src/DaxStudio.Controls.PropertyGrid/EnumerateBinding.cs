using System;
using System.Windows.Data;

namespace DaxStudio.Controls.PropertyGrid
{
    public sealed class EnumerateBinding : Binding
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        public EnumerateBinding(string path) : base(path)
        {
            Converter = Converter<object, object>.New
            (
                input => input?.GetType().GetEnumValues(),
                input => throw new NotSupportedException()
            );
            Mode = BindingMode.OneTime;
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
        }
    }
}
