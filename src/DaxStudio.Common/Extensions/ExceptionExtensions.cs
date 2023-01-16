using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Common.Extensions
{
    public static class ExceptionExtensions
    {
        public static string GetAllExceptionMessages(this Exception ex)
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
    }
}
