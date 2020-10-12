using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Controls.PropertyGrid
{
    public static class EnumExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TAttribute"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static TAttribute GetAttribute<TAttribute>(this Enum source) where TAttribute : Attribute
        {
            var info = source.GetType().GetMember(source.ToString());

            foreach (var i in info[0].GetCustomAttributes(true))
            {
                if (i is TAttribute)
                    return (TAttribute)i;
            }

            return default(TAttribute);
        }
    }
}
