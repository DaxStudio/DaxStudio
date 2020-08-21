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
    public class VpaPartitionViewModel : IComparable
    {
        private readonly VpaPartition _partition;
        private readonly VertiPaqAnalyzerViewModel _parentViewModel;
        public VpaPartitionViewModel(VpaPartition partition, VpaTableViewModel table, VertiPaqAnalyzerViewModel parentViewModel)
        {
            _partition = partition;
            _parentViewModel = parentViewModel;
            Table = table;
        }

        public VpaTableViewModel Table { get; }
        public string PartitionName => _partition.PartitionName;

        public string TableAndPartitionName => Table.TableName + "~" + _partition.PartitionName;

        public long RowsCount => _partition.RowsCount;
        public long DataSize => _partition.DataSize;
        public long PartitionsNumber => 1;
        public long SegmentsNumber => _partition.SegmentsNumber;

        public int CompareTo(object obj)
        {
            var objPartition = (VpaPartitionViewModel)obj;
            switch (_parentViewModel.PartitionSortColumn)
            {
                case "ColumnCardinality":
                    return RowsCount.CompareTo(objPartition.RowsCount) * _parentViewModel.PartitionSortDirection;
                case "DataSize":
                    return DataSize.CompareTo(objPartition.DataSize) * _parentViewModel.PartitionSortDirection;
                default:
                    return PartitionName.CompareTo(objPartition.PartitionName) * _parentViewModel.PartitionSortDirection;
            }
        }

        public bool IsExpanded { get; set; }

        public double PercentOfTableRows => (Table == null ? 0 : RowsCount / (double)Table.RowsCount); 
        public double PercentOfTableSize => Table == null ? 0 : DataSize / (double)Table.DataSize;

    }
}
