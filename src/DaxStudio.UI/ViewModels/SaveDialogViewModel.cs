using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;

namespace DaxStudio.UI.ViewModels
{

    [Export]
    public class SaveDialogViewModel : Screen
    {
        private SaveDialogResult _dialogResult = SaveDialogResult.Cancel;
        [ImportingConstructor]
        public SaveDialogViewModel() { }

        public ObservableCollection<ISaveable> Documents {get; set;}

        public void Save() {
            foreach (var doc in Documents)
            {
                if (doc.ShouldSave)
                    doc.Save();
                else
                    doc.IsDirty = false;
            }
            _dialogResult = SaveDialogResult.Save;
            TryClose(true);
        }
        public void DontSave() {
            foreach (var doc in Documents)
            {
                doc.IsDirty = false;
            }
            _dialogResult = SaveDialogResult.DontSave;
            TryClose(true);
        }
        public void Cancel() {
            _dialogResult = SaveDialogResult.Cancel;
            //TryClose(false);
        }

        public void ToggleShouldSave(ISaveable item)
        {
            // it's possible to click on the edge of the listview
            // and generate this event with a null item
            // in this case we ignore the click and return immediately
            if (item == null) return;

            item.ShouldSave = !item.ShouldSave;
        }

        private bool _selectAll = true;
        public bool SelectAll {
            get { return _selectAll; }
            set
            {
                _selectAll = value;
                foreach (var doc in Documents) { doc.ShouldSave = _selectAll; }
                NotifyOfPropertyChange(() => SelectAll);
            } }

        public SaveDialogResult Result { get { return _dialogResult; } }
    }
}
