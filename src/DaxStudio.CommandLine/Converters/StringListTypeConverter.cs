using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine.Converters
{
    
    internal class StringListTypeConverter:TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return true;
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var rawList = ((string)value).Split(',').ToList();
            var finalList = new List<string>();
            for (int i = 0; i < rawList.Count; i++)
            {
                if (i < rawList.Count - 2 && rawList[i + 1].Length == 0) {
                    finalList.Add((rawList[i] + "," + rawList[i + 2]).Trim());
                    i += 2;
                } else {
                    finalList.Add(rawList[i].Trim()); 
                }
            }
            return finalList;   
        }
    }
}
