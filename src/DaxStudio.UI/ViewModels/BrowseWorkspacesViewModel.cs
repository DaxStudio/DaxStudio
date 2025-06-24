using Caliburn.Micro;
using DaxStudio.UI.Utils;
using DaxStudio.UI.Extensions;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Windows;
using Microsoft.Identity.Client;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Diagnostics;
using DaxStudio.Interfaces;
using DaxStudio.Common;

namespace DaxStudio.UI.ViewModels
{
    public class BrowseWorkspacesViewModel : Screen
    {
        private AuthenticationResult _authResult;
        private IntPtr? _viewHwnd;
        public BrowseWorkspacesViewModel(IGlobalOptions options)
        {
            Options = options;
            WorkspacesView = CollectionViewSource.GetDefaultView(Workspaces);
            WorkspacesView.Filter = UserFilter;
            WorkspacesView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
        }

        public IGlobalOptions Options { get; }
        public ICollectionView WorkspacesView { get; }

        public Workspace SelectedWorkspace { get; set; }

        public AuthenticationResult AuthenticationResult { get => _authResult; }

        public bool UserFilter(object db)
        {
            if (String.IsNullOrEmpty(SearchCriteria))
                return true;
            else
                return (((Workspace)db).Name.Contains(SearchCriteria, StringComparison.OrdinalIgnoreCase));
        }

        public bool HasSearchCriteria => !string.IsNullOrEmpty(SearchCriteria);

        public void ClearSearchCriteria()
        {
            SearchCriteria = string.Empty;
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
                WorkspacesView.Refresh();
            }
        }

        protected override void OnViewReady(object view)
        {
            base.OnViewReady(view);
            _viewHwnd = GetHwnd((ContentControl)view);
            GetWorkspacesAsync(_viewHwnd,false).FireAndForget();
        }

        private async Task GetWorkspacesAsync(IntPtr? hwnd, bool switchAccount)
        {
            
            // first get the authentication token
            AccountStatus = "Connecting...";
            // getting workspaces only requires PowerBI scope, so we can use the same token for switching accounts
            _authResult =  switchAccount ? await EntraIdHelper.SwitchAccountAsync(hwnd, Options, AccessTokenScope.PowerBI) : await EntraIdHelper.AcquireTokenAsync(hwnd, Options, AccessTokenScope.PowerBI) ;
            AccountStatus = string.Empty;
            if (_authResult == null) {
                // if the user cancelled we should exit here
                AccountStatus = string.Empty;
                return; 
            }


            IsBusy = true;
            Workspaces.Clear();
            AccountName = _authResult.Account.Username;

            // then get the workspaces
            var ws = await PbiServiceHelper.GetWorkspacesAsync(_authResult);
            var orderedList = ws.OrderBy(ws => ws.Name);
            foreach (var w in orderedList)
            {
                Workspaces.Add(w);
            }
            IsBusy = false;
        }

        public ObservableCollection<Workspace> Workspaces { get; set; } = new ObservableCollection<Workspace>();
        private string _accountName = string.Empty;
        public string AccountName
        {
            get => _accountName;
            private set
            {
                _accountName = value;
                NotifyOfPropertyChange();
            }
        }

        public async void SwitchAccountAsync()
        {
            AccountName = string.Empty;
            AccountStatus = "Signing Out...";
            //await WorkspaceHelper.SignOutAsync();
            // prompt the user to sign in again and refresh the workspaces list 

            await GetWorkspacesAsync(_viewHwnd, true);
        }

        private IntPtr? GetHwnd(ContentControl view)
        {
            HwndSource hwnd = PresentationSource.FromVisual(view) as HwndSource;
            return hwnd?.Handle;
        }

        public void SetFocusToWorkspaces()
        {
            Debug.WriteLine("Setting focus to Databases");
            FocusManager.SetFocus(this, nameof(WorkspacesView));
        }

        public System.Windows.Forms.DialogResult Result { get; private set; }

        public void Connect()
        {
            //if (SelectedWorkspace == null) return;
            Result = System.Windows.Forms.DialogResult.OK;
            this.TryCloseAsync();
        }

        public void Cancel()
        {
            Result = System.Windows.Forms.DialogResult.Cancel;
            this.TryCloseAsync();
        }
        private bool _isBusy = false;
        public bool IsBusy { get => _isBusy; private set { _isBusy = value;NotifyOfPropertyChange(); } }
        public bool IsConnecting { get => !string.IsNullOrEmpty(AccountStatus); }
        private string _accountStatus = "Connecting...";
        public string AccountStatus { get => _accountStatus; 
            private set { _accountStatus = value; 
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(IsConnecting));
            } 
        }
    }
}
