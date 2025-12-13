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
using Serilog;
using System.Collections.Generic;

namespace DaxStudio.UI.ViewModels
{
    class BrowseWorkspacesViewModel : BaseDialogViewModel
    {
        private AuthenticationResult _authResult;
        private IntPtr? _viewHwnd;
        private PowerBIEnvironment _environment;
        
        public BrowseWorkspacesViewModel(IGlobalOptions options)
        {
            Options = options;
            WorkspacesView = CollectionViewSource.GetDefaultView(Workspaces);
            WorkspacesView.Filter = UserFilter;
            WorkspacesView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            
            // Detect Power BI environment (Public, GCC, China, etc.)
            _environment = PowerBIEnvironment.Public; // Default to public cloud
        }

        public IGlobalOptions Options { get; }
        public ICollectionView WorkspacesView { get; }

        private Workspace _selectedWorkspace;
        public Workspace SelectedWorkspace { get => _selectedWorkspace; set {
                _selectedWorkspace = value;
                NotifyOfPropertyChange(nameof(CanConnect));
            } 
        }

        public bool IsListEnabled { get; set; } = true;

        public AuthenticationResult AuthenticationResult { get => _authResult; }
        
        public PowerBIEnvironment Environment 
        { 
            get => _environment;
            private set
            {
                _environment = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(EnvironmentName));
            }
        }
        
        public string EnvironmentName => _environment?.Name ?? "Power BI";
        
        private string _userAvatar;
        public string UserAvatar
        {
            get => _userAvatar;
            private set
            {
                _userAvatar = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(HasAvatar));
            }
        }
        
        public bool HasAvatar => !string.IsNullOrEmpty(UserAvatar);

        public bool UserFilter(object db)
        {
            var workspace = (Workspace)db;
            
            // Filter by search criteria
            if (!String.IsNullOrEmpty(SearchCriteria))
            {
                if (workspace.Name.IndexOf(SearchCriteria, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }
            
            // Filter by premium capacity if requested
            if (ShowPremiumOnly)
            {
                if (!(workspace.IsOnPremiumCapacity ?? false))
                    return false;
            }
            
            return true;
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
            AccessTokenContext context = EntraIdHelper.CreateDefaultContext(AccessTokenScope.PowerBI);
            // getting workspaces only requires PowerBI scope, so we can use the same token for switching accounts
            if (switchAccount)
            {
                (_authResult, context) = await EntraIdHelper.PromptForAccountAsync(hwnd, Options, AccessTokenScope.PowerBI, string.Empty);
            }
            else
            {
                _authResult = await EntraIdHelper.AcquireTokenAsync(hwnd, Options, AccessTokenScope.PowerBI, context);
            }
            AccountStatus = string.Empty;
            if (_authResult == null) {
                // if the user cancelled we should exit here
                AccountStatus = string.Empty;
                return; 
            }


            IsBusy = true;
            Workspaces.Clear();
            AccountName = _authResult.Account.Username;

            // Load user avatar asynchronously (don't block on this)
            _ = LoadUserAvatarAsync();

            try
            {
                List<Workspace> ws;
                
                // Detect Power BI environment from the endpoint
                var clusterEndpoint = _environment?.ServiceEndpoint ?? "https://api.powerbi.com";
                
                // Use direct REST API approach (similar to Bravo) for more detailed information
                // Fallback to SDK if direct API fails
                try
                {
                    Log.Debug(Constants.LogMessageTemplate, nameof(BrowseWorkspacesViewModel), nameof(GetWorkspacesAsync), 
                        $"Attempting to get workspaces using direct REST API for {_environment?.Name ?? "Public Cloud"}");
                    ws = await PbiServiceHelper.GetWorkspacesDirectAsync(_authResult, clusterEndpoint);
                }
                catch (Exception directApiEx)
                {
                    Log.Warning(directApiEx, Constants.LogMessageTemplate, nameof(BrowseWorkspacesViewModel), nameof(GetWorkspacesAsync), 
                        "Direct REST API failed, falling back to SDK");
                    ws = await PbiServiceHelper.GetWorkspacesAsync(_authResult);
                }
                
                // Filter out My Workspace and empty/invalid entries, then sort by name
                var orderedList = ws.Where(w => !string.IsNullOrEmpty(w.Name) 
                                              && w.Id != Guid.Empty
                                              && w.Name != "My Workspace")
                                    .OrderBy(w => w.Name);
                
                foreach (var w in orderedList)
                {
                    Workspaces.Add(w);
                }
                
                if (Workspaces.Count == 0)
                {
                    ErrorMessage = "No workspaces found. You may not have access to any Power BI workspaces, or only have access to 'My Workspace'.";
                }
                else
                {
                    Log.Information(Constants.LogMessageTemplate, nameof(BrowseWorkspacesViewModel), nameof(GetWorkspacesAsync), 
                        $"Successfully loaded {Workspaces.Count} workspaces from {_environment?.Name ?? "Power BI"}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(BrowseWorkspacesViewModel), nameof(GetWorkspacesAsync), 
                    $"Error loading workspaces: {ex.Message}");
                ErrorMessage = $"Error loading workspaces: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        private async Task LoadUserAvatarAsync()
        {
            try
            {
                if (_authResult == null || string.IsNullOrEmpty(_authResult.Account?.Username))
                    return;
                
                Log.Debug(Constants.LogMessageTemplate, nameof(BrowseWorkspacesViewModel), nameof(LoadUserAvatarAsync), 
                    $"Loading avatar for {_authResult.Account.Username}");
                
                var avatar = await PbiServiceHelper.GetAccountAvatarAsync(
                    _authResult, 
                    _authResult.Account.Username, 
                    _environment);
                
                if (!string.IsNullOrEmpty(avatar))
                {
                    UserAvatar = avatar;
                    Log.Debug(Constants.LogMessageTemplate, nameof(BrowseWorkspacesViewModel), nameof(LoadUserAvatarAsync), 
                        "Successfully loaded user avatar");
                }
            }
            catch (Exception ex)
            {
                // Don't fail if avatar can't be loaded, just log it
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(BrowseWorkspacesViewModel), nameof(LoadUserAvatarAsync), 
                    $"Failed to load user avatar: {ex.Message}");
            }
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

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                _errorMessage = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        private bool _showPremiumOnly = false;
        public bool ShowPremiumOnly
        {
            get => _showPremiumOnly;
            set
            {
                _showPremiumOnly = value;
                NotifyOfPropertyChange();
                WorkspacesView.Refresh();
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
            if (SelectedWorkspace.Id == Guid.Empty || string.IsNullOrEmpty(SelectedWorkspace.Name))
            {
                ErrorMessage = "Please select a workspace to connect to.";
                return;
            }
            
            // Log the connection string that will be generated
            var connectionString = SelectedWorkspace.GetConnectionString(_environment);
            Log.Information(Constants.LogMessageTemplate, nameof(BrowseWorkspacesViewModel), nameof(Connect), 
                $"Connecting to workspace '{SelectedWorkspace.Name}' using connection string: {connectionString}");
            
            Result = System.Windows.Forms.DialogResult.OK;
            this.TryCloseAsync();
        }
        
        /// <summary>
        /// Gets the Power BI connection string for the selected workspace
        /// </summary>
        public string SelectedWorkspaceConnectionString
        {
            get
            {
                if (SelectedWorkspace.Id == Guid.Empty || string.IsNullOrEmpty(SelectedWorkspace.Name))
                    return string.Empty;
                
                return SelectedWorkspace.GetConnectionString(_environment);
            }
        }

        public async void RefreshWorkspaces()
        {
            ErrorMessage = string.Empty;
            await GetWorkspacesAsync(_viewHwnd, false);
        }

        public bool CanConnect => SelectedWorkspace.Id != Guid.Empty && !string.IsNullOrEmpty(SelectedWorkspace.Name);

        public override void Close()
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
