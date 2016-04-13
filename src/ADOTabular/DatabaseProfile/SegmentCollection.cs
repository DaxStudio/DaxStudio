using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.DatabaseProfile
{
    public class SegmentCollection
    {
        public SegmentCollection()
        {
            Segments = new List<Segment>();
        }
        public long Count
        {
            get { return Segments.Count; }
        }

        public long TotalRowCount
        {
            get { return Segments.Sum(s => s.RowCount); }
        }

        public float TotalUsedSize
        {
            get { return Segments.Sum(s => (float)s.UsedSize); }
        }
        public void Add(Segment segment)
        {
            Segments.Add(segment);
        }

        public List<Segment> Segments { get; internal set; }
    }
}
