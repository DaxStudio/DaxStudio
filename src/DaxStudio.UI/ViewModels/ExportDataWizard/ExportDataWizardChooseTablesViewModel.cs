using DaxStudio.Core.Exports;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.UI.ViewModels
{
    public class ExportDataWizardChooseTablesViewModel : ExportDataWizardBasePageViewModel
    {


        public ExportDataWizardChooseTablesViewModel(ExportDataWizardViewModel wizard) : base(wizard)
        {
            SelectAll = true;
            foreach (var t in Tables)
            {
                t.OnSelectionChanged = UpdateCanNext;
            }
        }

        public async void Next()
        {
            NextPage = ExportDataWizardPage.ExportStatus;
            await TryCloseAsync();
        }

        public bool CanNext
        {
            get { return Wizard.Tables.Count(t => t.IsSelected) > 0; }
        }

        public void UpdateCanNext()
        {
            NotifyOfPropertyChange(() => CanNext);
        }

        public bool SelectAll { get; set; }

        public void SelectAllChecked()
        {
            foreach (var t in Tables)
            {
                t.IsSelected = SelectAll;
            }            
        }

        public IEnumerable<SelectedTable> Tables
        {
            get { foreach (var t in Wizard.Tables) {
                    
                    if ((t.IsVisible || IncludeHiddenTables)
                        && (!t.ShowAsVariationsOnly || IncludeInternalTables))
                    {
                        t.IsSelected = true;
                        yield return t;
                    }
                    else
                    {
                        t.IsSelected = false;
                    }
                }
            }
        }
        private bool _includeHidden = true;
        public bool IncludeHiddenTables { get { return _includeHidden; }
            set {
                _includeHidden = value;
                NotifyOfPropertyChange(nameof(Tables));
            }
        }

        private bool _includeInternalTables;
        public bool IncludeInternalTables { get { return _includeInternalTables; }
            set {
                _includeInternalTables = value;
                NotifyOfPropertyChange(nameof(Tables));
            }
        } 

    }
}
