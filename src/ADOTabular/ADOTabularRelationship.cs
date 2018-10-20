using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular
{
    public class ADOTabularRelationship
    {
        public string InternalName { get; set; }
        public string FromTable { get; set; }
        public string ToTable { get; set; }
        public string FromColumn { get; set; }
        public string FromColumnMultiplicity { get; set; }
        public string ToColumnMultiplicity { get; set; }
        public string ToColumn { get; set; }
        public string CrossFilterDirection { get; set; }
    }
}
