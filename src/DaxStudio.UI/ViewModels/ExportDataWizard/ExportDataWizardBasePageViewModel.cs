using Caliburn.Micro;

namespace DaxStudio.UI.ViewModels
{
    public class ExportDataWizardBasePageViewModel :Screen
    {
        public ExportDataWizardBasePageViewModel(ExportDataWizardViewModel wizard)
        {
            Wizard = wizard;
        }

        public ExportDataWizardViewModel Wizard { get; }
        public ExportDataWizardPage NextPage { get; set; }
        public void Cancel()
        {
            Wizard.Cancel();
        }
        public bool BackClicked { get; set; }

        public void Back()
        {
            BackClicked = true;
            TryClose();
        }
    }


}
