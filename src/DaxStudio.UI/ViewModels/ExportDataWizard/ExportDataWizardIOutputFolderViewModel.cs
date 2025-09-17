using System;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.UI.ViewModels
{
    public class ExportDataWizardOutputFolderViewModel : ExportDataWizardBasePageViewModel
    {
        

        public ExportDataWizardOutputFolderViewModel(ExportDataWizardViewModel wizard):base(wizard)
        {

        }

        public string OutputFolder
        {
            get => Wizard.OutputFolder;
            set { Wizard.OutputFolder = value;
                NotifyOfPropertyChange(() => OutputFolder);
                NotifyOfPropertyChange(() => CanNext);
            }
        }

       



        // ReSharper disable once UnusedMember.Global
        public void BrowseFolders()
        {
            // show browse folders
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.RootFolder = Environment.SpecialFolder.Desktop;
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    this.OutputFolder = dialog.SelectedPath;
                }
            }

        }




        public async void Next()
        {
            NextPage = ExportDataWizardPage.ChooseTables;
            await TryCloseAsync();
        }

        public bool CanNext => OutputFolder.Length > 0;
    }
}
