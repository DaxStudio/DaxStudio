using Dax.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    public class VpaColumnViewModel
    {
        readonly VpaColumn _col;

        public VpaColumnViewModel(VpaColumn col)
        {
            _col = col;
        }
        public VpaColumnViewModel(VpaColumn col, VpaTableViewModel table)
        {
            _col = col;
            Table = table;
        }
        public VpaTableViewModel Table { get; }
        public string TableColumnName => _col.TableColumnName;
        public long TableRowsCount => _col.TableRowsCount;
        public long TableSize => Table.TableSize;
        public string ColumnName => _col.ColumnName;
        public string TypeName => _col.TypeName;
        public long ColumnCardinality => _col.ColumnCardinality;
        public string Encoding => _col.Encoding;
        public long DataSize => _col.DataSize;
        public long DictionarySize => _col.DictionarySize;
        public long HierarchiesSize => _col.HierarchiesSize;
        public long TotalSize => _col.TotalSize;
        public double PercentageDatabase => _col.PercentageDatabase;
        public double PercentageTable => _col.PercentageTable;
        public long SegmentsNumber => _col.SegmentsNumber;
        public long PartitionsNumber => _col.PartitionsNumber;
        public int? SegmentsPageable => _col.SegmentsPageable;
        public int? SegmentsResident => _col.SegmentsResident;
        public double? SegmentsAverageTemperature => _col.SegmentsAverageTemperature * 1000;
        public DateTime? SegmentsLastAccessed => _col.SegmentsLastAccessed;
        public long ReferentialIntegrityViolationCount => Table.ReferentialIntegrityViolationCount;
        public long UserHierarchiesSize => Table.UserHierarchiesSize;
        public long RelationshipsSize => Table.RelationshipSize;

        public long ColumnsNumber => 1;
        //public long MaxColumnCardinality { get; set; }
        public long MaxColumnTotalSize { get; set; }
        public double PercentOfMaxTotalSize => Table == null ? 0 : TotalSize / (double)Table.ColumnMaxTotalSize;
        public double PercentOfMaxCardinality => Table == null ? 0 : ColumnCardinality / (double)Table.ColumnsMaxCardinality;
        public double PercentOfMaxTotalDBSize => MaxColumnTotalSize == 0 ? 0 : TotalSize / (double)MaxColumnTotalSize;
    }
}
