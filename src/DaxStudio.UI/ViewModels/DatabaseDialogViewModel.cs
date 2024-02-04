using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Utils;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;

namespace DaxStudio.UI.ViewModels
{
    public class DatabaseDialogViewModel:Screen
    {

        public DatabaseDialogViewModel(ADOTabularDatabaseCollection databases)
        {
            Databases = databases;
            DatabasesView = CollectionViewSource.GetDefaultView(Databases);
            DatabasesView.Filter = UserFilter;
            DatabasesView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            SelecteFirstDatabase();
        }

        public DatabaseDetails SelectedDatabase { get; set; }
        public int SelectedIndex { get; set; } = -1;
        public System.Windows.Forms.DialogResult Result { get; set; }
        public ADOTabularDatabaseCollection Databases { get; }

        public async void Ok()
        {
            Result = System.Windows.Forms.DialogResult.OK;
            await TryCloseAsync();
        }

        public ICollectionView DatabasesView { get; }

        public bool UserFilter(object db)
        {
            if (String.IsNullOrEmpty(SearchCriteria))
                return true;
            else
                return (((DatabaseDetails)db).Caption.IndexOf(SearchCriteria, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        private string _searchCriteria;
        public string SearchCriteria
        {
            get { return _searchCriteria; }
            set
            {
                _searchCriteria = value;
                NotifyOfPropertyChange(nameof(SearchCriteria));
                NotifyOfPropertyChange(nameof(HasSearchCriteria));
                DatabasesView.Refresh();
                SelecteFirstDatabase();
            }
        }

        private void SelecteFirstDatabase()
        {
            DatabasesView.MoveCurrentToFirst();
            SelectedDatabase = (DatabaseDetails)DatabasesView.CurrentItem;
            NotifyOfPropertyChange(nameof(SelectedDatabase));
        }

        public bool HasSearchCriteria => !string.IsNullOrEmpty(SearchCriteria);

        public void ClearSearchCriteria()
        {
            SearchCriteria = string.Empty;
        }

        public void SetFocusToDatabases()
        {
            Debug.WriteLine("Setting focus to Databases");
            FocusManager.SetFocus(this, nameof(DatabasesView));
        }
    }
}
