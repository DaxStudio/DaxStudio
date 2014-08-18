using DaxStudio.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.ExcelAddin.Xmla
{
    public class StaticQueryResult:IStaticQueryResult
    {
        [JsonConverter(typeof(Newtonsoft.Json.Converters.DataTableConverter))]
        public System.Data.DataTable QueryResults {get;set;}

        public string TargetSheet { get; set; }
        
    }
}
