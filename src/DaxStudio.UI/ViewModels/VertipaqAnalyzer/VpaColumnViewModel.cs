using Dax.ViewModel;
using DaxStudio.Interfaces;
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
        private IVpaOptions _options;

        public VpaColumnViewModel(VpaColumn col, IVpaOptions options)
        {
            _col = col;
            _options = options;
        }
        public VpaColumnViewModel(VpaColumn col, VpaTableViewModel table, IVpaOptions options)
        {
            _col = col;
            Table = table;
            _options = options;
        }
        public VpaTableViewModel Table { get; }
        public string TableColumnName => _col.TableColumnName;
        public string TableName => _col.Table.TableName;
        public string ColumnName => _col.ColumnName;
        public long TableRowsCount => _col.TableRowsCount;
        public long TableSize => _col.Table.TableSize;
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
        public double? SegmentsAverageTemperature => _col.SegmentsAverageTemperature * ((bool)_options?.VpaxAdjustSegmentsMetrics ? 1000 : 1);
        public DateTime? SegmentsLastAccessed => _col.SegmentsLastAccessed;
        public long ReferentialIntegrityViolationCount => Table?.ReferentialIntegrityViolationCount??0;
        public long UserHierarchiesSize => Table?.UserHierarchiesSize??0;
        public long RelationshipsSize => Table?.RelationshipSize??0;

        public long ColumnsNumber => 1;
        //public long MaxColumnCardinality { get; set; }
        public long MaxColumnTotalSize { get; set; }
        public double PercentOfMaxTotalSize => Table == null ? 0 : TotalSize / (double)Table.ColumnMaxTotalSize;
        public double PercentOfMaxCardinality => Table == null ? 0 : ColumnCardinality / (double)Table.ColumnsMaxCardinality;
        public double PercentOfMaxTotalDBSize => MaxColumnTotalSize == 0 ? 0 : TotalSize / (double)MaxColumnTotalSize;

        public bool IsNotResident => (_col.SegmentsResident ?? 1) == 0;
    }
}
