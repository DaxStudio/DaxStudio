using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Common.Extensions
{
    public static class ExceptionExtensions
    {
        public static string GetInnerExceptionMessages(this Exception ex)
        {
            var innerEx = ex;
            var msg = "";
            while (innerEx != null)
            {
                msg = innerEx.Message + "\n";
                innerEx = innerEx.InnerException;
            }
            return msg;
        }

        public static string GetAllMessages(this AggregateException ex)
        {
            if (ex == null) return string.Empty;
            var sb = new StringBuilder();
            foreach (var inner in ex.Flatten().InnerExceptions)
            {
                sb.AppendLine(inner.Message);
                if (inner is AggregateException aggEx)
                {
                    sb.AppendLine(aggEx.GetAllMessages());
                }
            }
            return sb.ToString();
        }
    }
}
