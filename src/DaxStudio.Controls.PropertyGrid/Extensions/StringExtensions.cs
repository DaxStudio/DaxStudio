using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Controls.PropertyGrid
{
    public static class StringExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ToEvaluate"></param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(this string ToEvaluate)
            => string.IsNullOrEmpty(ToEvaluate);

        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source != null && toCheck != null && source.IndexOf(toCheck, comp) >= 0;
        }
    }
}
