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
