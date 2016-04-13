using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.DatabaseProfile
{
    public class UserHierarchy
    {
        public UserHierarchy()
        {
            Segments = new SegmentCollection();
        }
        public string Name { get; set; }
        public SegmentCollection Segments { get; internal set; }
    }
}
