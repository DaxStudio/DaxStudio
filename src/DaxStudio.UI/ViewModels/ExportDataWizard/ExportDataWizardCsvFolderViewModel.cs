using System;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.UI.ViewModels
{
    public class ExportDataWizardCsvFolderViewModel : ExportDataWizardBasePageViewModel
    {
        

        public ExportDataWizardCsvFolderViewModel(ExportDataWizardViewModel wizard):base(wizard)
        {
            // default to using culture default delimiter
            UseCultureDefaultDelimiter = true;
        }

        public string CsvFolder
        {
            get => Wizard.CsvFolder;
            set { Wizard.CsvFolder = value;
                NotifyOfPropertyChange(() => CsvFolder);
                NotifyOfPropertyChange(() => CanNext);
            }
        }

        public string CsvDelimiter
        {
            get => Wizard.CsvDelimiter;
            set { Wizard.CsvDelimiter = value;
                NotifyOfPropertyChange(() => CsvDelimiter);
            }
        }

        // ReSharper disable once UnusedMember.Global
        public bool CsvQuoteStrings
        {
            get => Wizard.CsvQuoteStrings;
            set => Wizard.CsvQuoteStrings = value;
        }

        private bool _useCultureDefaultDelimiter = true;
        public bool UseCultureDefaultDelimiter
        {
            get => _useCultureDefaultDelimiter;
            set {
                _useCultureDefaultDelimiter = value;
                if (_useCultureDefaultDelimiter)
                {
                    CsvDelimiter = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator;
                }
            }
        }

        private bool _useCommaDelimiter;
        // ReSharper disable once UnusedMember.Global
        public bool UseCommaDelimiter { get => _useCommaDelimiter;
            set {
                _useCommaDelimiter = value;
                if (_useCommaDelimiter )
                {
                    CsvDelimiter = ",";
                }
            }
        }

        private bool _useTabDelimiter;
        // ReSharper disable once UnusedMember.Global
        public bool UseTabDelimiter { get => _useTabDelimiter;
            set { _useTabDelimiter = value;
                if (_useTabDelimiter)
                {
                    CsvDelimiter = "\t";
                }
            }

        }

        // ReSharper disable once UnusedMember.Global
        public bool UseOtherDelimiter { get; set; }

        // ReSharper disable once UnusedMember.Global
        public void BrowseFolders()
        {
            // show browse folders
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    this.CsvFolder = dialog.SelectedPath;
                }
            }

        }

        public CsvEncoding CsvEncoding
        {
            get => Wizard.CsvEncoding; 
            set { 
                Wizard.CsvEncoding = value;
                NotifyOfPropertyChange();
            }
        }

        public IEnumerable<CsvEncoding> CsvEncodings
        {
            get
            {
                var items = Enum.GetValues(typeof(CsvEncoding)).Cast<CsvEncoding>();
                return items;
            }
        }

        public void Next()
        {
            NextPage = ExportDataWizardPage.ChooseTables;
            TryClose();
        }

        public bool CanNext => CsvFolder.Length > 0;
    }
}
