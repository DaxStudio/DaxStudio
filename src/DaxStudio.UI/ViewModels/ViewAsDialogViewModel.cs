using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Interfaces;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;

namespace DaxStudio.UI.ViewModels
{
    public class ViewAsRole:PropertyChangedBase
    {

        public ViewAsRole (string name)
        {
            Name = name;
        }
        public string Name { get; }
        private bool _selected;
        public bool Selected { get => _selected; 
            set { 
                _selected = value; 
                NotifyOfPropertyChange(); 
            } 
        }
    }

    [Export]
    public class ViewAsDialogViewModel : Screen
    {
        private DialogResult _dialogResult = DialogResult.Cancel;
        private IMetadataProvider _connectionManager;
        private int _selectedRoles = 0;

        [ImportingConstructor]
        public ViewAsDialogViewModel(IMetadataProvider connectionManager) {
            _connectionManager = connectionManager;
            // populate the role collection
            RoleList = new ObservableCollection<ViewAsRole>();
            var roles = _connectionManager.GetRoles();
            foreach (var r in roles) { RoleList.Add(new ViewAsRole(r)); }
        }

        public ObservableCollection<ViewAsRole> RoleList { get; set; }

        private bool _unrestricted = true;
        public bool Unrestricted
        {
            get => _unrestricted;
            set {
                if (_unrestricted) return;
                _unrestricted = true;
                OtherUser = false;
                Roles = false;
                RoleList.Apply(r => r.Selected = false);
                NotifyOfPropertyChange();
            }
        }

        private bool _otherUser;
        public bool OtherUser { get => _otherUser; 
            set {
                _otherUser = value;
                if (_otherUser) _unrestricted = false;
                if (!_roles && !_otherUser) _unrestricted = true;
                if (!_otherUser) OtherUserName = string.Empty;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(Unrestricted));
            }
        }

        private bool _roles;
        public bool Roles
        {
            get => _roles;
            set
            {
                _roles = value;
                if (_roles) _unrestricted = false;
                if (!_roles && !_otherUser) _unrestricted = true;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(Unrestricted));
            }
        }

        public string OtherUserName { get; set; } = string.Empty;

        public void Cancel()
        {
            Log.Information(Constants.LogMessageTemplate, nameof(ViewAsDialogViewModel), nameof(Cancel), $"Cancelling ViewAs Dialog");
            _dialogResult = DialogResult.Cancel;
            TryClose(true);
        }

        public void Ok()
        {
            Log.Information(Constants.LogMessageTemplate, nameof(ViewAsDialogViewModel), nameof(Ok), $"Setting ViewAs");
            _dialogResult = DialogResult.OK;
            TryClose(true);
        }

        public DialogResult Result => _dialogResult;

        public void SelectRole(bool role)
        {
            if (role) _selectedRoles++;
            else _selectedRoles--;

            Roles = _selectedRoles > 0;
        }

    }
}
