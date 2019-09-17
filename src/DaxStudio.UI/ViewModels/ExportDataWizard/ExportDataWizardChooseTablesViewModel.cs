using Caliburn.Micro;
using DaxStudio.UI.Enums;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
            Icons.Add(ExportStatus.Cancelled, new StatusIcon("ExclamationTriangle", "goldenrod"));
        }
        public static Dictionary<ExportStatus, StatusIcon> Icons { get; private set; }
    }

    public class SelectedTable : PropertyChangedBase
    {
        public SelectedTable(string name, string caption)
        {
            DaxName = name;
            Caption = caption;
            
        }
        public string DaxName { get;  }
        public string Caption { get;  }
        private bool _isSelected = true;
        public bool IsSelected { get { return _isSelected; }
            set {
                _isSelected = value;
                NotifyOfPropertyChange(() => IsSelected);
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

        private long _rowCount = 0;
        public long RowCount
        {
            get { return _rowCount; }
            set
            {
                _rowCount = value;
                NotifyOfPropertyChange(() => RowCount);
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
                    case ExportStatus.Exporting:
                        return $"{RowCount:N0} rows exported";
                    case ExportStatus.Ready:
                        return "Waiting...";
                    case ExportStatus.Error:
                        return "Error - check output pane";
                    case ExportStatus.Cancelled:
                        return "Cancelled";
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
        }

        public void Next()
        {
            NextPage = ExportDataWizardPage.ExportStatus;
            TryClose();
        }

        public bool SelectAll { get; set; }

        public void SelectAllChecked()
        {
            foreach (var t in Tables)
            {
                t.IsSelected = SelectAll;
            }            
        }

        public ObservableCollection<SelectedTable> Tables
        {
            get { return Wizard.Tables; }
        }

    }
}
