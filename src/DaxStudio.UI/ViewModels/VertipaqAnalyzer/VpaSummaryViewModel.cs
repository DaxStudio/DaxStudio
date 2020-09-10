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
    public class VpaSummaryViewModel
    {
        public VpaSummaryViewModel(VertiPaqAnalyzerViewModel parent)
        {
            ParentViewModel = parent;
            TableCount = parent.ViewModel.Tables.Count();
            ColumnCount = parent.ViewModel.Columns.Count();
            CompatibilityLevel = parent.ViewModel.Model.CompatibilityLevel;
            TotalSize = parent.ViewModel.Tables.Sum(t => t.TableSize);
            DataSource = parent.ViewModel.Model.ServerName?.Name ?? "<Unknown>";
            ModelName = parent.ViewModel.Model.ModelName.Name;
            LastDataRefresh = parent.ViewModel.Model.LastDataRefresh;
            ExtractionDate = parent.ViewModel.Model.ExtractionDate;
        }

        public VertiPaqAnalyzerViewModel ParentViewModel { get; }
        public int TableCount { get; }
        public int ColumnCount { get; }
        public int CompatibilityLevel { get; }
        public long TotalSize { get; }
        public string FormattedTotalSize {
            get {
                switch (TotalSize)
                {
                    case long size when size < 1024: return TotalSize.ToString("N0") + " Bytes";
                    case long size when size < (Math.Pow(1024, 2)): return (TotalSize / (double)(1024)).ToString("N2") + " KB";
                    case long size when size < (Math.Pow(1024, 3)): return (TotalSize / (double)(Math.Pow(1024, 2))).ToString("N2") + " MB";
                    default: return (TotalSize / (double)(Math.Pow(1024, 3))).ToString("N2") + " GB";
                }
            }
        }
        public string DataSource { get; }
        public string ModelName { get; }
        public DateTime LastDataRefresh { get; }
        public DateTime ExtractionDate { get; }
    }
}
