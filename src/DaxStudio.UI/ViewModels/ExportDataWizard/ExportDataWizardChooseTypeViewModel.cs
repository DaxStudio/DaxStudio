namespace DaxStudio.UI.ViewModels
{
    public class ExportDataWizardChooseTypeViewModel: ExportDataWizardBasePageViewModel
    {


        public ExportDataWizardChooseTypeViewModel(ExportDataWizardViewModel wizard):base(wizard)
        {

        }

        public async void ExportToCsv() {
            Wizard.ExportType = Enums.ExportDataType.CsvFolder;
            NextPage = ExportDataWizardPage.ChooseCsvFolder;
            await TryCloseAsync();
        }
        public async void ExportToSql()
        {
            Wizard.ExportType = Enums.ExportDataType.SqlTables;
            NextPage = ExportDataWizardPage.BuildSqlConnection;
            await TryCloseAsync ();
        }
    }
}
