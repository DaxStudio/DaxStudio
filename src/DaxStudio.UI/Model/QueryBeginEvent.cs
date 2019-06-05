using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    public class QueryBeginEvent
    {
        public string RequestID { get; set; }
        public string EffectiveUsername { get; set; }
        public string Query { get; set; }
        public string Parameters { get; set; }
        public string RequestParameters { get; set; }
        public string RequestProperties { get; set; }

    }
}
