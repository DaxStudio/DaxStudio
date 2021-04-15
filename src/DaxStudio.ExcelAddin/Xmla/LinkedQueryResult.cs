using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaxStudio.Common.Interfaces;

namespace DaxStudio.ExcelAddin.Xmla
{
    public class LinkedQueryResult : ILinkedQueryResult
    {
        public string DaxQuery { get; set; }
        public string TargetSheet { get; set; }
        public string ConnectionString { get; set; }
    }
}
