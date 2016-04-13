using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.DatabaseProfile
{
    public class Table
    {
        public Table()
        {
            Columns = new ColumnCollection();
            RelationshipSegments = new SegmentCollection();
            UserHierarchies = new UserHierarchyCollection();
        }
        public string Name { get; set; }
        public string Id { get; set; }
        public long RowCount { get; set; }
        public long RiViolationCount { get; set; }
        public ColumnCollection Columns { get; internal set; }
        public SegmentCollection RelationshipSegments { get; internal set; }
        public UserHierarchyCollection UserHierarchies { get; internal set; }
    }
}
