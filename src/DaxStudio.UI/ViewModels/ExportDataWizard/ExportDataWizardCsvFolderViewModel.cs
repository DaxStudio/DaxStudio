using Caliburn.Micro;

namespace DaxStudio.UI.ViewModels
{
    public class ExportDataWizardCsvFolderViewModel : ExportDataWizardBasePageViewModel
    {
        

        public ExportDataWizardCsvFolderViewModel(ExportDataWizardViewModel wizard):base(wizard)
        {
        }

        public string CsvFolder
        {
            get { return Wizard.CsvFolder; }
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

        private bool _useCommaDelimiter = false;
        public bool UseCommaDelimiter { get => _useCommaDelimiter;
            set {
                _useCommaDelimiter = value;
                if (_useCommaDelimiter )
                {
                    CsvDelimiter = ",";
                }
            }
        }

        private bool _useTabDelimiter = false;
        public bool UseTabDelimiter { get => _useTabDelimiter;
            set { _useTabDelimiter = value;
                if (_useTabDelimiter)
                {
                    CsvDelimiter = "\t";
                }
            }

        }

        private bool _useOtherDelimiter = false;
        public bool UseOtherDelimiter { get => _useOtherDelimiter;
            set { _useOtherDelimiter = value;
                
            }
        }

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

        public void Next()
        {
            NextPage = ExportDataWizardPage.ChooseTables;
            TryClose();
        }

        public bool CanNext
        {
            get { return CsvFolder.Length > 0; }
        }
    }
}
