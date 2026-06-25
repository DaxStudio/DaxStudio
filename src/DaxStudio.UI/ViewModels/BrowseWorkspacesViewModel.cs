using Caliburn.Micro;
using DaxStudio.Core.Extensions;
using DaxStudio.UI.Utils;
using DaxStudio.UI.Extensions;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
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
        // Default Power BI server name used to resolve authentication information when prompting
        // for an account (matches the default used by EntraIdHelper.CreateDefaultContext).
        private const string PowerBIServerName = "powerbi://api.powerbi.com";

        private AuthenticationResult _authResult;
        private AccessTokenContext _authContext;
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

        // Workspace is a value type (struct), so this must be nullable - otherwise the ListView's
        // SelectedItem binding throws when it tries to write null (no selection), which WPF surfaces
        // as a red validation adorner around the list.
        private Workspace? _selectedWorkspace;
        public Workspace? SelectedWorkspace { get => _selectedWorkspace; set {
                _selectedWorkspace = value;
                NotifyOfPropertyChange(nameof(CanConnect));
            } 
        }

        public bool IsListEnabled { get; set; } = true;

        public AuthenticationResult AuthenticationResult { get => _authResult; }

        /// <summary>
        /// The token context used to authenticate the workspace list. This is exposed so the caller
        /// can reuse the same account/token to establish the connection without prompting again.
        /// </summary>
        public AccessTokenContext AuthenticationContext { get => _authContext; }
        
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
        
        private ImageSource _userAvatar;
        public ImageSource UserAvatar
        {
            get => _userAvatar;
            private set
            {
                _userAvatar = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(HasAvatar));
            }
        }
        
        public bool HasAvatar => UserAvatar != null;

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
            // Clear any avatar from a previous account so we fall back to the default icon while
            // (re)connecting. LoadUserAvatarAsync will repopulate it only if the new photo loads.
            UserAvatar = null;
            AccessTokenContext context = EntraIdHelper.CreateDefaultContext(AccessTokenScope.PowerBI);
            // getting workspaces only requires PowerBI scope, so we can use the same token for switching accounts
            if (switchAccount)
            {
                try
                {
                    // PromptForAccountAsync requires a valid server name to resolve the authentication
                    // information - passing an empty string causes 'new Uri(string.Empty)' to throw and
                    // the interactive sign-in prompt is never shown. Use the default Power BI server name.
                    (_authResult, context) = await EntraIdHelper.PromptForAccountAsync(hwnd, Options, AccessTokenScope.PowerBI, PowerBIServerName);
                }
                catch (Exception ex)
                {
                    // the user cancelled the sign-in prompt (or it failed) - stay on the current view
                    Log.Warning(ex, Constants.LogMessageTemplate, nameof(BrowseWorkspacesViewModel), nameof(GetWorkspacesAsync),
                        "Interactive sign-in was cancelled or failed while switching accounts");
                    AccountStatus = string.Empty;
                    AccountName = _authResult?.Account?.Username ?? string.Empty;
                    return;
                }
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

            // remember the context that authenticated this account so the caller can reuse the
            // same token to connect without prompting the user a second time.
            _authContext = context;


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

                var avatarBytes = await PbiServiceHelper.GetAccountAvatarAsync(_authResult.Account);
                
                if (avatarBytes != null && avatarBytes.Length > 0)
                {
                    UserAvatar = CreateImageSource(avatarBytes);
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

        private static ImageSource CreateImageSource(byte[] imageBytes)
        {
            // Build a frozen BitmapImage so it can be safely assigned from a background thread.
            // BitmapCacheOption.OnLoad fully loads the image during EndInit so the stream can be disposed.
            using (var stream = new MemoryStream(imageBytes))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                // Decode at a higher resolution than the 32px display size so the image stays sharp
                // on high-DPI displays instead of being upscaled from a small thumbnail.
                bitmap.DecodePixelWidth = 96;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
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
            if (SelectedWorkspace == null || SelectedWorkspace.Value.Id == Guid.Empty || string.IsNullOrEmpty(SelectedWorkspace.Value.Name))
            {
                ErrorMessage = "Please select a workspace to connect to.";
                return;
            }

            // Log the connection string that will be generated
            var connectionString = SelectedWorkspace.Value.GetConnectionString(_environment);
            Log.Information(Constants.LogMessageTemplate, nameof(BrowseWorkspacesViewModel), nameof(Connect), 
                $"Connecting to workspace '{SelectedWorkspace.Value.Name}' using connection string: {connectionString}");

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
                if (SelectedWorkspace == null || SelectedWorkspace.Value.Id == Guid.Empty || string.IsNullOrEmpty(SelectedWorkspace.Value.Name))
                    return string.Empty;

                return SelectedWorkspace.Value.GetConnectionString(_environment);
            }
        }

        public async void RefreshWorkspaces()
        {
            ErrorMessage = string.Empty;
            await GetWorkspacesAsync(_viewHwnd, false);
        }

        public bool CanConnect => SelectedWorkspace.HasValue && SelectedWorkspace.Value.Id != Guid.Empty && !string.IsNullOrEmpty(SelectedWorkspace.Value.Name);

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
