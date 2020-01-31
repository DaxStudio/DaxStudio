using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{

    public sealed class EnumDescriptionConverter : IValueConverter
        {
            private string GetEnumDescription(Enum enumObj)
            {
                try
                {
                    FieldInfo fieldInfo = enumObj.GetType().GetField(enumObj.ToString());

                    object[] attribArray = fieldInfo.GetCustomAttributes(false);

                    if (attribArray.Length == 0)
                    {
                        return enumObj.ToString();
                    }
                    else
                    {
                        DescriptionAttribute attrib = attribArray[0] as DescriptionAttribute;
                        return attrib.Description;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Assert(false, "Error in GetEnumDescription", ex.Message);
                    return string.Empty;
                }
            }

            object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (string.IsNullOrEmpty(value?.ToString())) return Binding.DoNothing;

                Enum myEnum = (Enum)value;
                string description = GetEnumDescription(myEnum);
                return description;
            }

            object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return string.Empty;
            }
        }

    
}
