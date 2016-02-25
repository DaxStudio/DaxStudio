using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.DatabaseProfile
{
    public class Segment
    {
        public long SegmentIndex { get; set; }
        public long PartitionIndex { get; set; }
        public string PartitionName { get; set; }
        public long RowCount {get;set;}
        public ulong UsedSize {get;set;}
        public string CompressionType {get;set;}
        public long BitsCount {get;set;}
        public long BookmarkBitsCount {get;set;}
        public string VertipaqState {get;set;}
        public string SegmentType { get; set; }
    
    }

    public enum SegmentType
    {
        Hierarchy,
        Relationship,
        UserHierarchy,
        Data
    }
}
