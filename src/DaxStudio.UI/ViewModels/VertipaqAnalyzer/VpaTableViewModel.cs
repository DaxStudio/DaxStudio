using System;
using Dax.ViewModel;
using System.Collections.Generic;
using System.Linq;
using DaxStudio.Interfaces;
using Caliburn.Micro;
using DaxStudio.UI.Events;

namespace DaxStudio.UI.ViewModels
{
    public enum VpaSort
    {
        Table,
        Partition,
        Relationship,
    }

    public class VpaTableViewModel : IComparable
    {
        private readonly VpaTable _table;
        private readonly VertiPaqAnalyzerViewModel _parentViewModel;
        private readonly VpaSort _sort;
        private IGlobalOptions _options;
        private IEventAggregator _eventAggregator;
        public VpaTableViewModel(VpaTable table, VertiPaqAnalyzerViewModel parentViewModel, VpaSort sort, IGlobalOptions options, IEventAggregator eventAggregator)
        {
            _options = options;
            _eventAggregator = eventAggregator;
            _table = table;
            _parentViewModel = parentViewModel;
            Columns = _table.Columns.Select(c => new VpaColumnViewModel(c, this, options));
            if (Columns.Any()) {
                ColumnMaxTotalSize = Columns.Max(c => c.TotalSize);
                ColumnsMaxCardinality = Columns.Max(c => c.ColumnCardinality);
            }
            RelationshipsFrom = _table.RelationshipsFrom.Select(r => new VpaRelationshipViewModel(r, this));
            if (RelationshipsFrom.Any())
            {
                RelationshipMaxFromCardinality = RelationshipsFrom.Max(r => r.FromColumnCardinality);
                RelationshipMaxToCardinality = RelationshipsFrom.Max(r => r.ToColumnCardinality);
                RelationshipMaxOneToManyRatio = RelationshipsFrom.Max(r => r.OneToManyRatio);
                RelationshipFromMissingKeys = RelationshipsFrom.Sum(r => r.MissingKeys);
                RelationshipInvalidRows = RelationshipsFrom.Sum(r => r.InvalidRows);
            }
            _sort = sort;

        }

        public int SortDirection {

            get {
                switch (_sort)
                {
                    case VpaSort.Table: return _parentViewModel.TableSortDirection;
                    //case VpaSort.Column: return _parentViewModel.ColumnSortDirection;
                    case VpaSort.Partition: return _parentViewModel.PartitionSortDirection;
                    case VpaSort.Relationship: return _parentViewModel.RelationshipSortDirection;
                }
                return _parentViewModel.TableSortDirection;
            }
            set {
                switch (_sort)
                {
                    case VpaSort.Table: _parentViewModel.TableSortDirection = value; break;
                    //case VpaSort.Column:  _parentViewModel.ColumnSortDirection = value; break;
                    case VpaSort.Partition: _parentViewModel.PartitionSortDirection = value; break;
                    case VpaSort.Relationship: _parentViewModel.RelationshipSortDirection = value; break;
                }
            }
        }

        public string SortColumn
        {

            get
            {
                switch (_sort)
                {
                    case VpaSort.Table: return _parentViewModel.TableSortColumn;
                    //case VpaSort.Column: return _parentViewModel.ColumnSortColumn;
                    case VpaSort.Partition: return _parentViewModel.PartitionSortColumn;
                    case VpaSort.Relationship: return _parentViewModel.RelationshipSortColumn;
                }
                return _parentViewModel.TableSortColumn;
            }
            set
            {
                switch (_sort)
                {
                    case VpaSort.Table: _parentViewModel.TableSortColumn = value; break;
                    //case VpaSort.Column: _parentViewModel.ColumnSortColumn = value; break;
                    case VpaSort.Partition: _parentViewModel.PartitionSortColumn = value; break;
                    case VpaSort.Relationship: _parentViewModel.RelationshipSortColumn = value; break;
                }
            }
        }

        public string TableName => _table.TableName;
        public long TableSize => _table.TableSize;
        public string ColumnsEncoding =>
            (_table.HasDualPartitions == true) ? "Dual" :
            (_table.HasDirectQueryPartitions == true) ? "DQ" :
            _table.ColumnsEncoding;
        public string ColumnsTypeName => "-";
        public double PercentageDatabase => _table.PercentageDatabase;

        public long TotalSize => _table.ColumnsTotalSize;
        public long DataSize => _table.ColumnsDataSize;
        public long DictionarySize => _table.ColumnsDictionarySize;
        public long HierarchiesSize => _table.ColumnsHierarchiesSize;
        public long ColumnCardinality => _table.RowsCount;

        public long RelationshipSize => _table.RelationshipsSize;
        public long UserHierarchiesSize => _table.UserHierarchiesSize;
        public long ColumnsNumber => _table.ColumnsNumber;
        public long RowsCount => _table.RowsCount;
        public int SegmentsNumber => _table.SegmentsNumber;
        public long PartitionsNumber => _table.PartitionsNumber;
        public long SegmentsTotalNumber => _table.SegmentsTotalNumber;
        public int? SegmentsPageable => _table.SegmentsPageable;
        public int? SegmentsResident => _table.SegmentsResident;
        public double? SegmentsAverageTemperature => _table.SegmentsAverageTemperature * ((bool)_options?.VpaxAdjustSegmentsMetrics ? 1000 : 1);
        public DateTime? SegmentsLastAccessed => _table.SegmentsLastAccessed;
        public long ReferentialIntegrityViolationCount => _table.ReferentialIntegrityViolationCount;


        public IEnumerable<VpaColumnViewModel> Columns { get; }
        public IEnumerable<VpaRelationshipViewModel> RelationshipsFrom { get; }
        public long RelationshipMaxFromCardinality { get; }
        public double RelationshipMaxOneToManyRatio { get; }
        public long RelationshipFromMissingKeys { get; }
        public long RelationshipInvalidRows { get; }
        public long ColumnMaxTotalSize { get; }
        public long ColumnsMaxCardinality { get; }

        public int CompareTo(object obj)
        {
            var objTable = (VpaTableViewModel)obj;
            switch (SortColumn)
            {
                case "ColumnCardinality":
                    return RowsCount.CompareTo(objTable.RowsCount) * SortDirection;
                case "TotalSize":
                    return TotalSize.CompareTo(objTable.TotalSize) * SortDirection;
                case "DictionarySize":
                    return DictionarySize.CompareTo(objTable.DictionarySize) * SortDirection;
                case "DataSize":
                    return DataSize.CompareTo(objTable.DataSize) * SortDirection;
                case "HierarchiesSize":
                    return HierarchiesSize.CompareTo(objTable.HierarchiesSize) * SortDirection;
                case "TableSize":
                    return TableSize.CompareTo(objTable.TableSize) * SortDirection;

                case "PercentageDatabase":
                    return PercentageDatabase.CompareTo(objTable.PercentageDatabase) * SortDirection;
                case "SegmentsNumber":
                    return SegmentsNumber.CompareTo(objTable.SegmentsNumber) * SortDirection;
                case "PartitionsNumber":
                    return PartitionsNumber.CompareTo(objTable.PartitionsNumber) * SortDirection;
                case "ColumnsNumber":
                    return ColumnsNumber.CompareTo(objTable.ColumnsNumber) * SortDirection;
                case "ReferentialIntegrityViolationCount":
                    return ReferentialIntegrityViolationCount.CompareTo(objTable.ReferentialIntegrityViolationCount) * SortDirection;
                case "UserHierarchiesSize":
                    return UserHierarchiesSize.CompareTo(objTable.UserHierarchiesSize) * SortDirection;
                case "RelationshipSize":
                    return RelationshipSize.CompareTo(objTable.RelationshipSize) * SortDirection;
                case "RowsCount":
                    return RowsCount.CompareTo(objTable.RowsCount) * SortDirection;
                case "MissingKeys":
                    return RelationshipFromMissingKeys.CompareTo(objTable.RelationshipFromMissingKeys) * SortDirection;
                case "FromColumnCardinality":
                    return RelationshipMaxFromCardinality.CompareTo(objTable.RelationshipMaxFromCardinality) * SortDirection;
                case "OneToManyRatio":
                    return RelationshipMaxOneToManyRatio.CompareTo(objTable.RelationshipMaxOneToManyRatio) * SortDirection;
                case "ToColumnCardinality":
                    return RelationshipMaxToCardinality.CompareTo(objTable.RelationshipMaxToCardinality) * SortDirection;
                case "InvalidRows":
                    return RelationshipInvalidRows.CompareTo(objTable.RelationshipInvalidRows) * SortDirection;
                case "UsedSize":
                    return RelationshipSize.CompareTo(objTable.RelationshipSize) * SortDirection;
                default:
                    return string.Compare(TableName, objTable.TableName, StringComparison.OrdinalIgnoreCase) * SortDirection;
            }

        }
        private bool _isExpanded = false;
        public bool IsExpanded { get => _isExpanded;
            set { _isExpanded = value;
                _eventAggregator.PublishOnUIThreadAsync(new VpaViewExpandedEvent());
            } }
        public long RelationshipMaxToCardinality { get; }

        string _storageMode = string.Empty;
        public string StorageMode {
            get
            {
                if (!string.IsNullOrEmpty(_storageMode)) return _storageMode;
                foreach (var p in _table.Partitions)
                {
                    if (string.IsNullOrEmpty(_storageMode))
                    {
                        _storageMode = p.PartitionMode;
                        continue;
                    }
                    if (p.PartitionMode != _storageMode)
                    {
                        _storageMode = "Hybrid";
                        break;
                    }
                }
                return _storageMode;

            }
            
        }
    }
}
