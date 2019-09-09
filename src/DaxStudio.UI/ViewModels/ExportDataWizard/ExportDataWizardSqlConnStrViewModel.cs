namespace DaxStudio.UI.ViewModels
{
    public class ExportDataWizardSqlConnStrViewModel : ExportDataWizardBasePageViewModel
    {

        public ExportDataWizardSqlConnStrViewModel(ExportDataWizardViewModel wizard): base(wizard)
        {

        }

        public string SqlConnectionString
        {
            get { return Wizard.SqlConnectionString; }
            set { Wizard.SqlConnectionString = value; }
        }

        public bool TruncateTables
        {
            get { return Wizard.TruncateTables; }
            set { Wizard.TruncateTables = value; }
        }

        public string Schema
        {
            get { return Wizard.Schema; }
            set { Wizard.Schema = value; }
        }

        public void Next()
        {
            NextPage = ExportDataWizardPage.ChooseTables;
            TryClose();
        }
    }
}
