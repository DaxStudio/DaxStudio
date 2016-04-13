using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADOTabular.DatabaseProfile
{
    public class Column
    {
        public Column()
        {
            DataSegments = new SegmentCollection();
            HierarchySegments = new SegmentCollection();
        }
        public string Name { get; set; }

        public long RecordCount { get; set; }
        public long Cardinality { get; set; }
        public string Id { get; set; }

        public string DataType { get; set; }

        public ulong DictionarySize { get; set; }

        public SegmentCollection DataSegments { get; internal set; }
        public SegmentCollection HierarchySegments { get; internal set; }

    }
}
