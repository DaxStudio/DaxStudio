using Caliburn.Micro;
using DaxStudio.UI.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DaxStudio.UI.ViewModels
{
    public class StatusIcon
    {
        public StatusIcon(string icon, string color):this(icon,color,false) { }

        public StatusIcon( string icon, string color, bool spin)
        {
            Icon = icon;
            Color = color;
            Spin = spin;
        }

        public string Icon { get; set; }
        public string Color { get; set; }
        public bool Spin { get; set; }
    }

    public static class StatusIcons
    {
        static StatusIcons()
        {
            Icons = new Dictionary<ExportStatus, StatusIcon>();
            Icons.Add(ExportStatus.Done, new StatusIcon("CheckCircle", "green"));
            Icons.Add(ExportStatus.Exporting, new StatusIcon("Refresh", "royalblue", true));
            Icons.Add(ExportStatus.Error, new StatusIcon("TimesCircle", "red"));
            Icons.Add(ExportStatus.Ready, new StatusIcon("ChevronCircleRight", "lightgray"));
            Icons.Add(ExportStatus.Skipped, new StatusIcon("ChevronCircleRight", "lightgray"));
            Icons.Add(ExportStatus.Cancelled, new StatusIcon("ExclamationTriangle", "goldenrod"));
        }
        public static Dictionary<ExportStatus, StatusIcon> Icons { get; private set; }
    }

    public class SelectedTable : PropertyChangedBase
    {
        public SelectedTable(string name, string caption, bool isVisible, bool isprivate, bool showAsVariationsOnly)
        {
            DaxName = name;
            Caption = caption;
            IsVisible = isVisible;
            Private = isprivate;
            ShowAsVariationsOnly = showAsVariationsOnly;
        }
        public string DaxName { get;  }
        public string Caption { get;  }
        public bool IsVisible { get; }
        public bool Private { get; }
        public bool ShowAsVariationsOnly { get; }
        public ExportDataWizardChooseTablesViewModel Parent { get; set; }

        private bool _isSelected = true;
        public bool IsSelected { get { return _isSelected; }
            set {
                _isSelected = value;
                NotifyOfPropertyChange(() => IsSelected);
                Parent?.UpdateCanNext();
            }
        }

        private ExportStatus _status = ExportStatus.Ready;
        public ExportStatus Status { get { return _status; }
            set { _status = value;
                NotifyOfPropertyChange(() => Status);
                NotifyOfPropertyChange(() => StatusMessage);
                NotifyOfPropertyChange(() => Icon);
                NotifyOfPropertyChange(() => IconColor);
                NotifyOfPropertyChange(() => IconSpin);
            }
        }

        public string Icon => StatusIcons.Icons[Status].Icon;

        public string IconColor => StatusIcons.Icons[Status].Color;

        public bool IconSpin => StatusIcons.Icons[Status].Spin;

        private long _rowCount;
        public long RowCount
        {
            get { return _rowCount; }
            set
            {
                _rowCount = value;
                NotifyOfPropertyChange(() => RowCount);
                NotifyOfPropertyChange(() => StatusMessage);
                NotifyOfPropertyChange(() => ProgressPercentage);
            }
        }

        public double ProgressPercentage => TotalRows == 0 ? 0 : (Double)RowCount / TotalRows;

        private long _totalRows;
        public long TotalRows
        {
            get { return _totalRows; }
            set
            {
                _totalRows = value;
                NotifyOfPropertyChange(() => TotalRows);
                NotifyOfPropertyChange(() => StatusMessage);
            }
        }
        public string StatusMessage
        {
            get
            {
                switch (Status)
                {
                    case ExportStatus.Done:
                        return $"{RowCount:N0} rows exported";
                    case ExportStatus.Exporting:
                        return $"{RowCount:N0} of {TotalRows:N0} rows exported";
                    case ExportStatus.Ready:
                        return "Waiting...";
                    case ExportStatus.Error:
                        return "Error - check output pane";
                    case ExportStatus.Cancelled:
                        return "Cancelled";
                    case ExportStatus.Skipped:
                        return "Skipped";
                    default:
                        return Caption;
                }
            }
        }
    }

    public class ExportDataWizardChooseTablesViewModel : ExportDataWizardBasePageViewModel
    {


        public ExportDataWizardChooseTablesViewModel(ExportDataWizardViewModel wizard) : base(wizard)
        {
            SelectAll = true;
            foreach (var t in Tables)
            {
                t.Parent = this;
            }
        }

        public void Next()
        {
            NextPage = ExportDataWizardPage.ExportStatus;
            TryClose();
        }

        public bool CanNext
        {
            get { return Wizard.Tables.Count(t => t.IsSelected) > 0; }
        }

        public void UpdateCanNext()
        {
            NotifyOfPropertyChange(() => CanNext);
        }

        public bool SelectAll { get; set; }

        public void SelectAllChecked()
        {
            foreach (var t in Tables)
            {
                t.IsSelected = SelectAll;
            }            
        }

        public IEnumerable<SelectedTable> Tables
        {
            get { foreach (var t in Wizard.Tables) {
                    
                    if ((t.IsVisible || IncludeHiddenTables)
                        && (!t.ShowAsVariationsOnly || IncludeInternalTables))
                    {
                        t.IsSelected = true;
                        yield return t;
                    }
                    else
                    {
                        t.IsSelected = false;
                    }
                }
            }
        }
        private bool _includeHidden = true;
        public bool IncludeHiddenTables { get { return _includeHidden; }
            set {
                _includeHidden = value;
                NotifyOfPropertyChange(nameof(Tables));
            }
        }

        private bool _includeInternalTables;
        public bool IncludeInternalTables { get { return _includeInternalTables; }
            set {
                _includeInternalTables = value;
                NotifyOfPropertyChange(nameof(Tables));
            }
        } 

    }
}
