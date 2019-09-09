namespace DaxStudio.UI.ViewModels
{
    public class ExportDataWizardChooseTypeViewModel: ExportDataWizardBasePageViewModel
    {


        public ExportDataWizardChooseTypeViewModel(ExportDataWizardViewModel wizard):base(wizard)
        {

        }

        public void ExportToCsv() {
            Wizard.ExportType = Enums.ExportDataType.CsvFolder;
            NextPage = ExportDataWizardPage.ChooseCsvFolder;
            TryClose();
        }
        public void ExportToSql() {
            Wizard.ExportType = Enums.ExportDataType.SqlTables;
            NextPage = ExportDataWizardPage.BuildSqlConnection;
            TryClose();
        }
    }
}
