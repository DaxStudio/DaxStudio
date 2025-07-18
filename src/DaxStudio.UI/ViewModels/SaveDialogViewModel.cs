using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{

    [Export]
    public class SaveDialogViewModel : BaseDialogViewModel
    {

        [ImportingConstructor]
        public SaveDialogViewModel() { }

        public ObservableCollection<ISaveable> Documents {get; set;}

        public async Task Save() {
            foreach (var doc in Documents)
            {
                if (doc.ShouldSave)
                    doc.Save();
                else
                    doc.IsDirty = false;
            }
            Result = SaveDialogResult.Save;
            await TryCloseAsync(true);
        }
        public async Task DontSave()
        {
            foreach (var doc in Documents)
            {
                doc.IsDirty = false;
            }
            Result = SaveDialogResult.DontSave;
            await TryCloseAsync (true);
        }
        public override void Close() {
            Result = SaveDialogResult.Cancel;
            TryCloseAsync();
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

        public SaveDialogResult Result { get; private set; } = SaveDialogResult.Cancel;
    }
}
