using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    public static class ExceptionExtensions
    {
        public static string GetAllMessages(this Exception ex)
        {
            var msg = ex.Message;
            var innerEx = ex.InnerException;
            var indent = 1;
            while (innerEx != null)
            {
                var indentStr = new string('\t', indent);
                msg = msg + $"\n{indentStr}{innerEx.Message}";
                innerEx = innerEx.InnerException;
                indent++;
            }
            return msg;
        }

        public static Exception GetLeafException(this Exception ex)
        {
            var innerEx = ex;
            while (innerEx.InnerException != null)
            {
                innerEx = innerEx.InnerException;
            }
            return innerEx;
        }
    }
}
