using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Model;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;

namespace DaxStudio.UI.ViewModels
{

    [Export]
    public class AutoSaveRecoveryDialogViewModel : Screen
    {
        private OpenDialogResult _dialogResult = OpenDialogResult.Cancel;
        [ImportingConstructor]
        public AutoSaveRecoveryDialogViewModel() { }

        public ObservableCollection<AutoSaveIndexEntry> Files {get; set;}

        public void Open() {

            // TODO - implement open logic

            //foreach (var doc in Documents)
            //{
            //    if (doc.ShouldSave)
            //        doc.Save();
            //    else
            //        doc.IsDirty = false;
            //}
            _dialogResult = OpenDialogResult.Open;
            TryClose(true);
        }
        
        public void Cancel() {
            _dialogResult = OpenDialogResult.Cancel;
            //TryClose(false);
        }

        public void ToggleShouldOpen(IOpenable item)
        {
            // it's possible to click on the edge of the listview
            // and generate this event with a null item
            // in this case we ignore the click and return immediately
            if (item == null) return;

            item.ShouldOpen = !item.ShouldOpen;
        }

        private bool _selectAll = true;
        public bool SelectAll {
            get { return _selectAll; }
            set
            {
                _selectAll = value;
                foreach (var f in Files) { f.ShouldOpen = _selectAll; }
                NotifyOfPropertyChange(() => SelectAll);
            } }

        public OpenDialogResult Result => _dialogResult;
    }
}
