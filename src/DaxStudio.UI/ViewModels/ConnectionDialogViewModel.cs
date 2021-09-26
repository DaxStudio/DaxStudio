using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using System.Threading.Tasks;
using DaxStudio.UI.Views;
using DaxStudio.UI.Utils;
using Serilog;
using System.Text.RegularExpressions;
using System.Globalization;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using ADOTabular.Enums;
using ADOTabular;
using System.Windows.Input;
using System.Linq.Expressions;
using DaxStudio.Controls.PropertyGrid;

namespace DaxStudio.UI.ViewModels
{
    class ConnectionDialogViewModel : Screen
        , IHandle<ApplicationActivatedEvent>
    {

        private readonly IEventAggregator _eventAggregator;
        private readonly string _connectionString;
        private readonly DocumentViewModel _activeDocument;
        private readonly Regex _ppvtRegex;
        private static PowerBIInstance _pbiLoadingInstance = new PowerBIInstance("Loading...", -1, EmbeddedSSASIcon.Loading);
        private static PowerBIInstance _pbiNoneInstance = new PowerBIInstance("<no running PBI/SSDT windows found>", -1, EmbeddedSSASIcon.None);
        private ISettingProvider SettingProvider { get; }


        public ConnectionDialogViewModel(string connectionString
            , IDaxStudioHost host
            , IEventAggregator eventAggregator
            , bool hasPowerPivotModel
            , DocumentViewModel document
            , ISettingProvider settingProvider
            , IGlobalOptions options) 
        {
            try
            {
                _eventAggregator = eventAggregator;
                _eventAggregator.Subscribe(this);
                _connectionString = connectionString;
                _activeDocument = document;
                SettingProvider = settingProvider;
                _ppvtRegex = new Regex(@"http://localhost:\d{4}/xmla", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                PowerPivotEnabled = true;
                Host = host;
                ServerModeSelected = true;
                Options = options;

                RefreshPowerBIInstances();

                ParseConnectionString(); // load up dialog with values from ConnStr

                if (Host.IsExcel)
                {
                    //using (new StatusBarMessage("Checking for PowerPivot model 2..."))
                    //{
                    //bool hasPpvt = false;
                    //HasPowerPivotModelAsync().ContinueWith(t => hasPpvt = t.Result).Wait(); 

                    if (hasPowerPivotModel)
                    {
                        ServerModeSelected = false;
                        PowerPivotModeSelected = true;
                        HasPowerPivotModel = true;
                    }

                    //}
                }

                WorkbookName = host.Proxy.WorkbookName;
                DisplayName = "Connect To";
                //MdxCompatibility = "3 - (Default) Placeholder members are not exposed";
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "ConnectionDialogViewModel", "ctor", ex.Message, ex.StackTrace);
            }
        }

        public IGlobalOptions Options { get; }

        private void RefreshPowerBIInstances()
        {

            _powerBIInstances = new List<PowerBIInstance>() { _pbiLoadingInstance };
            SelectedPowerBIInstance = _pbiLoadingInstance;

            Task.Run(() =>{

                // display the "loading..." message
                _powerBIInstances.Clear();
                _powerBIInstances.Add(_pbiLoadingInstance);
                NotifyOfPropertyChange(() => PowerBIDesignerInstances);
                NotifyOfPropertyChange(() => PowerBIInstanceDetected);

                // look for local workspace instances
                _powerBIInstances = PowerBIHelper.GetLocalInstances(false);

                if (_powerBIInstances.Count == 0 )
                {
                    // Add the none found 'fake' instance
                    _powerBIInstances.Add(_pbiNoneInstance);
                }

                if (PowerBIInstanceDetected)
                {
                    if (SelectedPowerBIInstance == null || SelectedPowerBIInstance?.Port == -1) 
                    { SelectedPowerBIInstance = _powerBIInstances[0]; }
                } 
                else
                {
                    if (PowerBIModeSelected) ServerModeSelected = true;
                    SelectedPowerBIInstance = _pbiNoneInstance;
                }
                // update bound properties
                NotifyOfPropertyChange(() => PowerBIInstanceDetected);
                NotifyOfPropertyChange(() => PowerBIDesignerInstances);
                NotifyOfPropertyChange(() => SelectedPowerBIInstance);
            }).ContinueWith(t => {
                // we should only come here if we got an exception
                Log.Error(t.Exception, "Error getting PowerBI/SSDT instances: {message}", t.Exception.Message);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error getting PowerBI/SSDT instances: {t.Exception.Message}"));
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public bool HostIsExcel { get { return Host.IsExcel; } }
     
        public async Task<bool> HasPowerPivotModelAsync() {

            bool res = await Task.FromResult<bool>(Host.Proxy.HasPowerPivotModel).ConfigureAwait(false);
            return res;
            
        }

        public bool ShowMissingModelWarning{ 
            get
            {
                if (!HostIsExcel)
                    return false;
                var hasPpvtModel = HasPowerPivotModelAsync().Result;
                return !hasPpvtModel;
            }
        }


        public Visibility PowerPivotUnavailableVisibility
        {
            get
            {
                if (!PowerPivotEnabled)
                {
                    return Visibility.Visible;
                }
                return  Visibility.Hidden;
            }
            
        }

        public IDaxStudioHost Host { get; set; }

        private bool _serverModeSelected;
        private bool _powerPivotModeSelected;
        private Dictionary<string, string> _connectionProperties;

        public bool ServerModeSelected
        {
            get { return _serverModeSelected; }
            set
            {
                if (value != _serverModeSelected)
                {
                    _serverModeSelected = value;
                    InitialCatalog = string.Empty;
                    NotifyOfPropertyChange(() => ServerModeSelected);
                    NotifyOfPropertyChange(nameof(IsRolesEnabled));
                    NotifyOfPropertyChange(nameof(IsEffectiveUserNameEnabled));
                    NotifyOfPropertyChange(nameof(CanConnect));
                }
            }
        }

        public bool PowerPivotModeSelected
        {
            get { return _powerPivotModeSelected; }
            set
            {
                if (value != _powerPivotModeSelected)
                {
                    _powerPivotModeSelected = value;
                    InitialCatalog = string.Empty;
                    NotifyOfPropertyChange(nameof(IsRolesEnabled));
                    NotifyOfPropertyChange(nameof(IsEffectiveUserNameEnabled));
                    NotifyOfPropertyChange(nameof(CanConnect));
                }
            }
        }

        private void ParseConnectionString()
        {
            if (! string.IsNullOrEmpty(_connectionString))
            {
                _connectionProperties = SplitConnectionString(_connectionString);
                // if data source = $Embedded$ then mark Ppvt option as selected 
                var dataSrc = _connectionProperties["Data Source"];

                if (_ppvtRegex.Match(dataSrc).Success) // if we are connected to PowerPivot
                {
                    PowerBIModeSelected = false;
                    ServerModeSelected = false;
                    PowerPivotModeSelected = true;
                    NotifyOfPropertyChange(() => PowerPivotModeSelected);
                }
                else
                {
                    if (_connectionProperties.ContainsKey("Application Name"))
                    {
                        if (_connectionProperties["Application Name"].StartsWith("DAX Studio (Power BI)", StringComparison.OrdinalIgnoreCase)
                            || _connectionProperties["Data Source"].StartsWith("localhost:", StringComparison.OrdinalIgnoreCase))
                        {
                            PowerPivotModeSelected = false;
                            ServerModeSelected = false;
                            PowerBIModeSelected = true;
                            NotifyOfPropertyChange(() => PowerBIModeSelected);
                        }
                    }
                    foreach (var p in _connectionProperties)
                    {
                        switch (p.Key.ToLower())
                        {
                            case "data source":
                                DataSource = PowerBIModeSelected ? "": p.Value;
                                break;
                            case "roles":
                                Roles = p.Value;
                                break;
                            case "effectiveusername":
                                EffectiveUserName = p.Value;
                                break;
                            case "mdx compatibility":
                                MdxCompatibility = MdxCompatibilityOptions.Find(x => x.StartsWith(p.Value, StringComparison.OrdinalIgnoreCase));
                                break;
                            case "directquerymode":
                                DirectQueryMode = p.Value;
                                break;
                            case "application name":
                                ApplicationName = p.Value;
                                break;
                            case "locale identifier":
                                Locale = LocaleOptions.GetByLcid(int.Parse( p.Value));
                                break;
                            case "show hidden cubes":
                                // do nothing
                                break;
                            case "initial catalog":
                                InitialCatalog = p.Value;
                                break;
                            default:
                                AdditionalOptions += string.Format("{0}={1};", p.Key, p.Value);
                                break;
                        }
                    }
                }
            }
            else
            {
                if (!PowerPivotModeSelected)
                {
                    ServerModeSelected = true;
                }
                if (RecentServers.Count > 0)
                { DataSource = RecentServers[0]; }
            }           
        }

        private Dictionary<string, string> SplitConnectionString(string connectionString)
        {
            return ADOTabular.Utils.ConnectionStringParser.Parse(connectionString);
        }

        private string _additionalOptions = string.Empty;
        public string AdditionalOptions {
            get { if (_additionalOptions.Trim().EndsWith(";", StringComparison.OrdinalIgnoreCase))
                    return _additionalOptions;
                else
                    return _additionalOptions.Trim() + ";";
            }
            set { _additionalOptions = value; }
        }

        private string _dataSource = string.Empty;
        public string DataSource { 
            get { 
                if (RecentServers.Count > 0 && String.IsNullOrWhiteSpace(_dataSource))
                { _dataSource = RecentServers[0]; }
                return  _dataSource; } 
            set{ _dataSource=CleanDataSourceName(value);
                NotifyOfPropertyChange(nameof( DataSource));
                NotifyOfPropertyChange(nameof(ShowConnectionWarning));
                NotifyOfPropertyChange(nameof(CanConnect));
                InitialCatalog = string.Empty;
                SelectedServerSetFocus = true;
            }
        }

        private string _roles;
        public string Roles {
            get {
                if (_roles == null)
                {
                    return string.Empty;
                }
                return _roles;
            }
            set => _roles = value;
        }

        public bool IsRolesEnabled => true;
        public string EffectiveUserName { get; set; }
        public bool IsEffectiveUserNameEnabled => true;
        public string ApplicationName { get; set; }

        private string _directQueryMode;
        public string DirectQueryMode { 
            get { 
                if (string.IsNullOrWhiteSpace(_directQueryMode))
                    {_directQueryMode = "Default";}
                return _directQueryMode;
            }
            set { 
                _directQueryMode = value;
                NotifyOfPropertyChange(() => DirectQueryMode);
            }
        }


        private List<string> _directQueryModeOptions;
        public List<string> DirectQueryModeOptions
        {
            get
            {
                if (_directQueryModeOptions == null)
                {
                    _directQueryModeOptions = new List<string>();
                    _directQueryModeOptions.Add("Default");
                    _directQueryModeOptions.Add("InMemory");
                    _directQueryModeOptions.Add("DirectQuery");
                }
                return _directQueryModeOptions;
            }
        }

        private List<string> _mdxCompatibilityOptions;
        public List<string> MdxCompatibilityOptions
        {
            get { 
                if (_mdxCompatibilityOptions == null)
                {   
                    _mdxCompatibilityOptions = new List<string>() ;
                    _mdxCompatibilityOptions.Add("0 - Equivalent to 1");
                    _mdxCompatibilityOptions.Add("1 - Placeholder members are exposed");
                    _mdxCompatibilityOptions.Add("2 - Placeholder members are not exposed");
                    _mdxCompatibilityOptions.Add("3 - (Default) Placeholder members are not exposed");        
                }
                return _mdxCompatibilityOptions;
            }
        }

        public ObservableCollection<string> RecentServers => Options.RecentServers;

        public bool PowerPivotEnabled { get; private set; }

        public string WorkbookName { get; private set; }

        private string GetRolesProperty()
        {
            return Roles.Length == 0 ? string.Empty : $"Roles={Roles};";
        }

        private string GetDirectQueryMode()
        {
            if (string.IsNullOrEmpty(DirectQueryMode) || DirectQueryMode.ToLower() == "default")
                return string.Empty;
            else
                return $"DirectQueryMode={DirectQueryMode};";
        }

        private string GetMdxCompatibilityMode()
        {
            return $"MDX Compatibility={MdxCompatibility.Substring(0, 1)};";
        }

        private string _mdxCompatibility;
        private bool _selectedServerSetFocus;
        public string MdxCompatibility { 
            get { 
                if (string.IsNullOrWhiteSpace(_mdxCompatibility))
                    {_mdxCompatibility = MdxCompatibilityOptions.Find(x=> x.StartsWith("3", StringComparison.OrdinalIgnoreCase));}
                return _mdxCompatibility;
            }
            set {
                _mdxCompatibility = value;
                 NotifyOfPropertyChange(()=> MdxCompatibility);
           }
        }

        public string ConnectionString
        {
            get
            {

                if (PowerPivotModeSelected) return BuildPowerPivotConnection();
                if (PowerBIModeSelected) return BuildPowerBIDesignerConnection();
                return BuildServerConnection();
                
            }
        }

        private string BuildPowerBIDesignerConnection()
        {
            var port = -1;
            try
            {
                port = SelectedPowerBIInstance.Port;
            }
            catch (Exception ex)
            {
                Log.Error(ex,"{class} {method} {message}",nameof(ConnectionDialogViewModel),nameof(BuildPowerBIDesignerConnection),ex.Message);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"The following error occured while trying to connect to Power BI Desktop/SSDT : {ex.Message}"));
            }

            return string.Format("Data Source=localhost:{0};{1}{2}{3}{4}{5}{6}{7}"
                            , port // 0
                            , GetMdxCompatibilityMode()    // 1
                            , GetDirectQueryMode()         // 2 
                            , GetRolesProperty()           // 3
                            , GetLocaleIdentifier()        // 4
                            , GetEffectiveUserName()       // 5
                            , AdditionalOptions            // 6
                            , GetApplicationName("Power BI")); // 7
            
        }

        private object GetEffectiveUserName()
        {
            if (string.IsNullOrWhiteSpace(EffectiveUserName))
                return string.Empty;
            else
                return string.Format("EffectiveUserName={0};", EffectiveUserName);
        }

        private string BuildServerConnection()
        {
            //OLEDB;Provider=MSOLAP.5;Persist Security Info=True;Data Source=.\SQL2012TABULAR;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue
            return string.Format("Data Source=\"{0}\";{1}{2}{3}{4}{5}{6}{7}{8}", DataSource
                                 , GetMdxCompatibilityMode()     //1
                                 , GetDirectQueryMode()          //2
                                 , GetRolesProperty()            //3
                                 , GetLocaleIdentifier()         //4
                                 , GetEffectiveUserName()        //5
                                 , GetApplicationName("SSAS")    //6
                                 , GetInitialCatalog()           //7
                                 , AdditionalOptions             //8
                                 );  
        }

        private string GetInitialCatalog()
        {
            if (string.IsNullOrEmpty(InitialCatalog)) return string.Empty;
            if (InitialCatalog == "<default>") return string.Empty;
            if (InitialCatalog == "<not connected>") return string.Empty;
            if (InitialCatalog == "<loading...>") return string.Empty;
            return $"Initial Catalog={InitialCatalog};";
        }

        private string GetApplicationName(string connectionType)
        {
            return string.Format("Application Name=DAX Studio ({0}) - {1};", connectionType, _activeDocument.UniqueID);
        }

        /*
        * Note: The ability to connect to PowerPivot from DAX Studio (or any third party application) is not
        * supported by Microsoft. Microsoft does not support connecting to PowerPivot models using the APIs 
        * used here and as such the behaviour may change or stop working without notice in future releases. 
        * This functionality is provided on an "as-is" basis.
        */
        private string BuildPowerPivotConnection()
        {    
            return Host.Proxy.GetPowerPivotConnection(GetApplicationName("Power Pivot"), string.Format("Location=\"{0}\";Extended Properties='Location=\"{1}\"';Workstation ID=\"{0}\"",WorkbookName, WorkbookName.Replace("'","''"))).ConnectionString;
            
        }

        public bool CanConnect
        {
            get
            {
                if (ServerModeSelected && DataSource.IsNullOrEmpty()) return false;
                return true;
            }
        }
        public void Connect()
        {
            string connectionString = string.Empty;
            try
            {
                ServerType serverType= ServerType.AnalysisServices;
                string powerBIFileName = "";
                var vw = (Window)this.GetView();
                vw.Visibility = Visibility.Hidden;
                //using (var c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices))
                //{
                //    c.Open();
                //}
                
                if (ServerModeSelected)
                {
                    SettingProvider.SaveServerMRUList(DataSource, RecentServers);
                    serverType = ServerType.AnalysisServices;
                }
                if (PowerPivotModeSelected) { serverType = ServerType.PowerPivot; }
                if (PowerBIModeSelected)
                {
                    powerBIFileName = SelectedPowerBIInstance.Name;
                    switch (SelectedPowerBIInstance.Icon)
                    {
                        case EmbeddedSSASIcon.Devenv:
                            serverType = ServerType.SSDT;
                            break;
                        case EmbeddedSSASIcon.PowerBI:
                            serverType = ServerType.PowerBIDesktop;
                            break;
                        case EmbeddedSSASIcon.PowerBIReportServer:
                            serverType = ServerType.PowerBIReportServer;
                            break;
                    }
                }
                // we cache this to a local variable in case there are any exceptions thrown while building the ConnectionString
                connectionString = ConnectionString;
                var connEvent = new ConnectEvent(connectionString, PowerPivotModeSelected, WorkbookName, GetApplicationName(ConnectionType),powerBIFileName, serverType, false);
                Log.Debug("{Class} {Method} {@ConnectEvent}", "ConnectionDialogViewModel", "Connect", connEvent);
                _eventAggregator.PublishOnUIThread(connEvent);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} Error Connecting using: {connStr}", "ConnectionDialogViewModel", "Connect", connectionString);
                _activeDocument.OutputError(String.Format("Could not connect to '{0}': {1}", PowerPivotModeSelected?"Power Pivot model":DataSource, ex.Message));
                _eventAggregator.PublishOnUIThread(new CancelConnectEvent());
            }
            finally
            {
                _eventAggregator.Unsubscribe(this);
                SelectedServerSetFocus = false;
                this.TryClose();
            //    TryClose(true);
            }
        }

        public void Cancel()
        {
            _eventAggregator.Unsubscribe(this);
            _eventAggregator.PublishOnUIThread(new CancelConnectEvent());
        }

        protected override void OnViewLoaded(object view)
        {
 	        base.OnViewLoaded(view);
            //((UserControl)view).MoveFocus(new  TraversalRequest( FocusNavigationDirection.Next));
            ((ConnectionDialogView)view).cboServers.Focus();
        }

        // This property is used to trigger all the text to be selected in the server name
        // combobox when the connection dialog is first shown.
        public bool SelectedServerSetFocus
        {
            get { return _selectedServerSetFocus; }
            set
            {
                _selectedServerSetFocus = value;
                NotifyOfPropertyChange(() => SelectedServerSetFocus);
            }
        }

        // readonly property
        // do we have more than one detected instance of a local workspace
        // or we have 1 instance that is not the "Loading..." instance 
        public bool PowerBIInstanceDetected => _powerBIInstances.Count > 1 
                                              || (_powerBIInstances.Count == 1 && ( _powerBIInstances[0] != _pbiLoadingInstance && _powerBIInstances[0] != _pbiNoneInstance));

        public List<PowerBIInstance> PowerBIDesignerInstances { get { return _powerBIInstances; } }
        public bool PowerBIModeSelected { get => _powerBIModeSelected; set {
                _powerBIModeSelected = value;
                // clear any previously set InitialCatalog
                InitialCatalog = string.Empty;
                NotifyOfPropertyChange(nameof(IsRolesEnabled));
                NotifyOfPropertyChange(nameof(IsEffectiveUserNameEnabled));
                NotifyOfPropertyChange(nameof(CanConnect));
            }
        }

        private PowerBIInstance _selectedPowerBIInstance;
        public PowerBIInstance SelectedPowerBIInstance {
            get { return _selectedPowerBIInstance; }
            set {
                _selectedPowerBIInstance = value;
                NotifyOfPropertyChange(() => SelectedPowerBIInstance);
            }
        }

        public string ConnectionType { get {
            if (ServerModeSelected) return "SSAS";
            if (PowerBIModeSelected) return "Power BI";
            if (PowerPivotModeSelected) return "Power Pivot";
            return "Unknown";  } 
        }

        public string GetLocaleIdentifier()
        {
            if (Locale.LCID != -1)
            {
                return string.Format("Locale Identifier={0};", Locale.LCID);
            }
            return "";
        }

        public void Handle(ApplicationActivatedEvent message)
        {
            RefreshPowerBIInstances();
        }

        private LocaleIdentifier _locale;
        public LocaleIdentifier Locale
        {
            get
            {
                if (_locale == null) { _locale = LocaleOptions["<Default>"]; }
                return _locale;
            }
            set { _locale = value;
            NotifyOfPropertyChange(() => Locale);
            }
        }

        private SortedList<string, LocaleIdentifier> _locales;
        private bool _hasPowerPivotModel;
        private List<PowerBIInstance> _powerBIInstances = new List<PowerBIInstance> { _pbiLoadingInstance };
        private bool _powerBIModeSelected;

        public SortedList<string, LocaleIdentifier> LocaleOptions
        {
            get
            {
                if (_locales == null)
                {
                    _locales = new SortedList<string, LocaleIdentifier>();
                    _locales.Add("<Default>", new LocaleIdentifier() { DisplayName = "<Default>", LCID = -1 });
                    try
                    {
                        foreach (var ci in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
                        {
                            _locales.Add(ci.DisplayName, new LocaleIdentifier()
                            {
                                DisplayName = string.Format("{0} - {1}", ci.DisplayName, ci.LCID),
                                LCID = ci.LCID
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        // it looks like it's possible for people to add a custom culture to windows which can cause issues
                        // when enumerating cultures
                        // see: https://social.msdn.microsoft.com/Forums/ie/en-US/671bc463-932d-4a9e-bba1-3e5898b9100d/culture-4096-0x1000-is-an-invalid-culture-identifier-culturenotfoundexception?forum=csharpgeneral
                        Log.Warning(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionDialogViewModel), nameof(LocaleOptions), ex.Message);
                        _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, $"An error occurred reading the system locales: {ex.Message}"));
                    }
                }
                return _locales;
            }
        }

        public bool HasPowerPivotModel {
            get { return _hasPowerPivotModel; }
            private set { _hasPowerPivotModel = value;
                NotifyOfPropertyChange(() => HasPowerPivotModel);
            }
        }


        public void ClearDatabases()
        {
            CheckDataSource();
            Log.Verbose(Common.Constants.LogMessageTemplate, nameof(ConnectionDialogViewModel), nameof(ClearDatabases), "Clearing Database Collection");
            Databases.Clear();
            Databases.Add("<default>");
        }

        public void PasteDataSource(object sender, DataObjectPastingEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("pasting data source");

        }


        public DataObjectPastingEventHandler DataSourcePasted => OnDataSourcePasted;

        public void OnDataSourcePasted(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                try {
                    string text = Convert.ToString(e.DataObject.GetData(DataFormats.Text));

                    if (text.Contains(";")) {
                        var msg = "Detected paste of a string with semi-colons, attempting to parse out the \"Data Source\" and \"Initial Catalog\" properties";
                        Log.Information(Common.Constants.LogMessageTemplate, nameof(ConnectionDialogViewModel), nameof(OnDataSourcePasted), msg);
                        _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, msg));

                        var props = SplitConnectionString(text);

                        // update the DataSource property if we found a "Data Source=" in the pasted string
                        if (props.ContainsKey("Data Source"))
                        {
                            DataSource = props["Data Source"];
                            e.CancelCommand();
                        }
                        // update the InitialCatalog property if we found a "Initial Cataloge=" in the pasted string
                        if (props.ContainsKey("Initial Catalog"))
                        {
                            _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, $"Setting the \"Initial Catalog\" property in the Advanced Options to \"{ props["Initial Catalog"]}\""));
                            InitialCatalog = props["Initial Catalog"];
                            e.CancelCommand();
                        }
                        //TODO - should we attempt to assign other properties?
                    }
                }
                catch (Exception ex)
                {
                    var msg = $"Error processing paste into DataSource: {ex.Message}";
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionDialogViewModel), nameof(OnDataSourcePasted),msg);
                    _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, msg));
                }
            }
            else
                e.CancelCommand(); // invalid clipboard format
        }

        private void CheckDataSource()
        {
            if (DataSource.Trim().Length == 0)
            {
                ShowConnectionWarning = true;
                ConnectionWarning = "Server Name is blank";
                return;
            }

            if (DataSource.Contains(";"))
            {
                ShowConnectionWarning = true;
                ConnectionWarning = "The Server Name cannot contain semi-colon(;) characters";
                return;
            }

            ShowConnectionWarning = false;
            ConnectionWarning = string.Empty;
        }

        public async void RefreshDatabases()
        {
            // exit here if the database collection is already populated
            if (Databases.Count > 1) return;

            // exit here if no server name is specified
            CheckDataSource();
            if (ShowConnectionWarning) return;

            Log.Information(Common.Constants.LogMessageTemplate, nameof(ConnectionDialogViewModel), nameof(RefreshDatabases), $"Refreshing Databases for: {ConnectionString}");

            try
            {
                IsLoadingDatabases = true;
                if(!Databases.Contains("<loading...>")) Databases.Add("<loading...>");
                NotifyOfPropertyChange(() => Databases);
                //InitialCatalog = "<loading...>";
                
                // populate temporary database list async
                SortedSet<string> tmpDatabases = await GetDatabasesFromConnectionAsync();
                
                if (tmpDatabases.Count > 0) { 
                    //Databases.Clear();
                    //InitialCatalog = "";
                    tmpDatabases.Apply(db => Databases.Add(db));
                    Databases.Remove("<loading...>");
                    NotifyOfPropertyChange(() => Databases);
                    Log.Information(Common.Constants.LogMessageTemplate, nameof(ConnectionDialogViewModel), nameof(RefreshDatabases), $"Finished Loading Databases");
                }
                else
                {
                    Databases.Remove("<default>");
                    Databases.Add("<not connected>");
                }
                //}
                NotifyOfPropertyChange(nameof(Databases));
            }
            catch (Exception ex)
            {
                Databases.Remove("<loading...>");
                InitialCatalog = "";
                ShowConnectionWarning = true;
                ConnectionWarning = $"Error connecting to server: {ex.Message}";
                var msg = $"Error refreshing database list for Initial Catalog: {ex.Message}";
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionDialogViewModel), nameof(RefreshDatabases), msg);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, msg));
            } 
            finally
            {
                IsLoadingDatabases = false;
            }
        }

        private async Task<SortedSet<string>> GetDatabasesFromConnectionAsync()
        {
            return await Task.Factory.StartNew<SortedSet<string>>(() =>
            {
                SortedSet<string> tmpDatabases = new SortedSet<string>();

                using (var conn = new ADOTabular.ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices))
                {

                    conn.Open();
                    if (conn.State == System.Data.ConnectionState.Open)
                    {
                        conn.Databases.Apply(db => tmpDatabases.Add(db.Name));
                    }
                }
                return tmpDatabases;

            });
        }

        private string _initialCatalog = string.Empty;//= "<default>";
        public string InitialCatalog { get => _initialCatalog;
            set {
                _initialCatalog = value;
                NotifyOfPropertyChange(nameof(InitialCatalog));
            } 
        }

        private bool _showConnectionWarning;
        public bool ShowConnectionWarning
        {
            get => _showConnectionWarning;
            set
            {
                _showConnectionWarning = value;
                NotifyOfPropertyChange(nameof(ShowConnectionWarning));
            }
        }

        private string _connectionWarning = string.Empty;
        public string ConnectionWarning
        {
            get => _connectionWarning;
            set
            {
                _connectionWarning = value;
                NotifyOfPropertyChange(nameof(ConnectionWarning));
            }
        }

        private bool _isLoadingDatabases;
        public bool IsLoadingDatabases
        {
            get => _isLoadingDatabases;
            set
            {
                _isLoadingDatabases = value;
                NotifyOfPropertyChange(nameof(IsLoadingDatabases));
            }
        }

        public ObservableCollection<string> Databases { get; set; } = new ObservableCollection<string>();

        private string CleanDataSourceName(string datasource)
        {
            var trimmedName = datasource.Trim().TrimStart('"').TrimEnd('"');
            return trimmedName;
        }
    } 
     
    public class LocaleIdentifier
    {
        public string DisplayName {get;set;}
        public int LCID { get; set; }
        public override string ToString() { return DisplayName; }


    }
}
