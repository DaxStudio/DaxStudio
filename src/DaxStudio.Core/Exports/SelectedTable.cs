using Caliburn.Micro;
using System;
using System.Collections.Generic;

namespace DaxStudio.Core.Exports
{
    public class StatusIcon
    {
        public StatusIcon(string icon, string color) : this(icon, color, false) { }

        public StatusIcon(string icon, string color, bool spin)
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
            Icons = new Dictionary<ExportStatus, StatusIcon>
            {
                { ExportStatus.Done, new StatusIcon("successDrawingImage", "green") },
                { ExportStatus.Exporting, new StatusIcon("refresh_toolbarDrawingImage", "royalblue", true) },
                { ExportStatus.Error, new StatusIcon("failDrawingImage", "red") },
                { ExportStatus.Ready, new StatusIcon("select_arrow_rightDrawingImage", "lightgray") },
                { ExportStatus.Skipped, new StatusIcon("select_arrow_rightDrawingImage", "lightgray") },
                { ExportStatus.Cancelled, new StatusIcon("warningDrawingImage", "goldenrod") }
            };
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
        public string DaxName { get; }
        public string Caption { get; }
        public bool IsVisible { get; }
        public bool Private { get; }
        public bool ShowAsVariationsOnly { get; }

        // Callback raised when IsSelected changes so a UI host (e.g. the wizard
        // ChooseTables page) can refresh its CanNext state without SelectedTable
        // having a direct reference to a UI ViewModel.
        public System.Action OnSelectionChanged { get; set; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                NotifyOfPropertyChange(() => IsSelected);
                OnSelectionChanged?.Invoke();
            }
        }

        private ExportStatus _status = ExportStatus.Ready;
        public ExportStatus Status
        {
            get { return _status; }
            set
            {
                _status = value;
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
}
