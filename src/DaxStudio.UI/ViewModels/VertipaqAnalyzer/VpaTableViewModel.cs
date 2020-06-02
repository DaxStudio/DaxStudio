using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;
using System.Windows.Data;
using System;
using System.ComponentModel;
using Serilog;
using System.Windows.Input;
using DaxStudio.Interfaces;
using Dax.ViewModel;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Windows.Navigation;

namespace DaxStudio.UI.ViewModels
{
    public class VpaTableViewModel : IComparable
    {
        private readonly VpaTable _table;
        private readonly VertiPaqAnalyzerViewModel _parentViewModel;
        public VpaTableViewModel(VpaTable table, VertiPaqAnalyzerViewModel parentViewModel)
        {
            _table = table;
            _parentViewModel = parentViewModel;
            Columns = _table.Columns.Select(c => new VpaColumnViewModel(c, this));
            ColumnMaxTotalSize = Columns.Max(c => c.TotalSize);
            ColumnsMaxCardinality = Columns.Max(c => c.ColumnCardinality);
            RelationshipsFrom = _table.RelationshipsFrom.Select(r => new VpaRelationshipViewModel(r, this));
            if (RelationshipsFrom.Count() > 0)
            {
                RelationshipMaxFromCardinality = RelationshipsFrom.Max(r => r.FromColumnCardinality);
                RelationshipMaxToCardinality = RelationshipsFrom.Max(r => r.ToColumnCardinality);
                RelationshipFromMissingKeys = RelationshipsFrom.Sum(r => r.MissingKeys);
            }
        }

        public string TableName => _table.TableName;
        public long TableSize => _table.TableSize;
        public string ColumnsEncoding => _table.ColumnsEncoding;
        public string ColumnsTypeName => "-";
        public double PercentageDatabase => _table.PercentageDatabase;
        public long ColumnsTotalSize => _table.ColumnsTotalSize;
        public long ColumnsDataSize => _table.ColumnsDataSize;
        public long ColumnsDictionarySize => _table.ColumnsDictionarySize;
        public long ColumnsHierarchySize => _table.ColumnsHierarchiesSize;
        public long RelationshipSize => _table.RelationshipsSize;
        public long UserHierarchiesSize => _table.UserHierarchiesSize;
        public long ColumnsNumber => _table.ColumnsNumber;
        public long RowsCount => _table.RowsCount;
        public long SegmentsNumber => _table.SegmentsNumber;
        public long PartitionsNumber => _table.PartitionsNumber;
        public long ReferentialIntegrityViolationCount => _table.ReferentialIntegrityViolationCount;


        public IEnumerable<VpaColumnViewModel> Columns { get; }
        public IEnumerable<VpaRelationshipViewModel> RelationshipsFrom { get; }
        public long RelationshipMaxFromCardinality { get; }
        public long RelationshipFromMissingKeys { get; }
        public long ColumnMaxTotalSize { get; }
        public long ColumnsMaxCardinality { get; }

        public int CompareTo(object obj)
        {
            var objTable = (VpaTableViewModel)obj;
            switch (_parentViewModel.SortColumn)
            {
                case "ColumnCardinality":
                    return RowsCount.CompareTo(objTable.RowsCount) * _parentViewModel.SortDirection;
                case "TotalSize":
                    return ColumnsTotalSize.CompareTo(objTable.ColumnsTotalSize) * _parentViewModel.SortDirection;
                case "DictionarySize":
                    return ColumnsDictionarySize.CompareTo(objTable.ColumnsDictionarySize) * _parentViewModel.SortDirection;
                case "DataSize":
                    return ColumnsDataSize.CompareTo(objTable.ColumnsDataSize) * _parentViewModel.SortDirection;
                case "HierarchiesSize":
                    return ColumnsHierarchySize.CompareTo(objTable.ColumnsHierarchySize) * _parentViewModel.SortDirection;
                default:
                    return TableName.CompareTo(objTable.TableName) * _parentViewModel.SortDirection;
            }

        }

        public bool IsExpanded { get; set; }
        public long RelationshipMaxToCardinality { get; }

    }
}
