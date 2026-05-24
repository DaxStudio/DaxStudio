using System.Collections.Generic;
using System;
using DaxStudio.Core.Exports;

namespace DaxStudio.UI.ViewModels
{
    public class ExportDataWizardChooseTypeViewModel: ExportDataWizardBasePageViewModel
    {


        public ExportDataWizardChooseTypeViewModel(ExportDataWizardViewModel wizard):base(wizard)
        {
            ExportTypes = new List<ExportTypes>();
            ExportTypes.Add(new ViewModels.ExportTypes() { Name = "CSV Files", ExportType = ExportDataType.CsvFolder, ImageResource = "csvDrawingImage" });
            ExportTypes.Add(new ViewModels.ExportTypes() { Name = "Parquet Files", ExportType = ExportDataType.ParquetFolder, ImageResource = "parquetDrawingImage" });
            ExportTypes.Add(new ViewModels.ExportTypes() { Name = "SQL Tables", ExportType = ExportDataType.SqlTables, ImageResource = "results_tableDrawingImage" });
        }

        public List<ExportTypes> ExportTypes { get; }

        private ExportTypes _selectedItem;
        public ExportTypes SelectedItem {
            get => _selectedItem;
            set
            {
                Set(ref _selectedItem, value);
                NotifyOfPropertyChange(nameof(CanNext));
                Next(); // move to the next page
            }
        }


        public bool CanNext => SelectedItem != null;
        public async void Next()
        {
            Wizard.ExportType = SelectedItem.ExportType;
            switch (Wizard.ExportType)
            {
                case ExportDataType.CsvFolder:
                    NextPage = ExportDataWizardPage.ChooseCsvFolder;
                    break;
                case ExportDataType.ParquetFolder:
                    NextPage = ExportDataWizardPage.ChooseParquetFolder;
                    break;
                case ExportDataType.SqlTables:
                    NextPage = ExportDataWizardPage.BuildSqlConnection;
                    break;
                default:
                    throw new InvalidOperationException("Attempting to export to an unknown export type");

            }
            await TryCloseAsync();
        }

        public async void ExportToCsv() {
            Wizard.ExportType = ExportDataType.CsvFolder;
            NextPage = ExportDataWizardPage.ChooseCsvFolder;
            await TryCloseAsync();
        }
        public async void ExportToSql()
        {
            Wizard.ExportType = ExportDataType.SqlTables;
            NextPage = ExportDataWizardPage.BuildSqlConnection;
            await TryCloseAsync ();
        }
    }

    public class ExportTypes
    {
        public string Name { get; set; }
        public string ImageResource { get; set; }
        public ExportDataType ExportType { get; set; }

    }
}
