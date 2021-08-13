using Caliburn.Micro;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Interfaces;
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
        private OpenDialogResult _dialogResult = OpenDialogResult.Cancel;
        private IMetadataProvider _connectionManager;

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
                NotifyOfPropertyChange();
            }
        }

        private bool _otherUser;
        public bool OtherUser { get => _otherUser; 
            set {
                _otherUser = value;
                if (_otherUser) _unrestricted = false;
                if (!_roles && !_otherUser) _unrestricted = true;
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

        public string OtherUserName { get; set; }

        public void Cancel()
        {
            _dialogResult = OpenDialogResult.Cancel;
        }

        public void Ok()
        {
            _dialogResult = OpenDialogResult.Open;
            TryClose(true);
        }

        public OpenDialogResult Result => _dialogResult;

    }
}
