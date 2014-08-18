using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Xmla
{
    public class LinkedQueryResult : ILinkedQueryResult
    {
        public string DaxQuery { get; set; }

        public string TargetSheet { get; set; }
    }
}
