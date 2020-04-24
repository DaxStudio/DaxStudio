using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    public static class EnumExtensions
    {
    //    public static string GetDescription(this T enumerationValue) where T : struct
    //    {
    //        var type = enumerationValue.GetType();
    //        if (!type.IsEnum)
    //        {
    //            throw new ArgumentException($"{nameof(enumerationValue)} must be of Enum type", nameof(enumerationValue));
    //        }
    //        var memberInfo = type.GetMember(enumerationValue.ToString());
    //        if (memberInfo.Length > 0)
    //        {
    //            var attrs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);

    //            if (attrs.Length > 0)
    //            {
    //                return ((DescriptionAttribute)attrs[0]).Description;
    //            }
    //        }
    //        return enumerationValue.ToString();
    //    }
    //}

    public static string GetDescription<T>(this T e) where T : IConvertible
    {
        if (e is Enum)
        {
            Type type = e.GetType();
            Array values = System.Enum.GetValues(type);

            foreach (int val in values)
            {
                if (val == e.ToInt32(CultureInfo.InvariantCulture))
                {
                    var memInfo = type.GetMember(type.GetEnumName(val));
                    var descriptionAttribute = memInfo[0]
                        .GetCustomAttributes(typeof(DescriptionAttribute), false)
                        .FirstOrDefault() as DescriptionAttribute;

                    if (descriptionAttribute != null)
                    {
                        return descriptionAttribute.Description;
                    }

                }
            }
        }

            return string.Empty; // null; // could also return string.Empty
    }

}

}
