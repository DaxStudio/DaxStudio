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
        private static PowerBIInstance _pbiNoneInstance = new PowerBIInstance("<none found>", -1, EmbeddedSSASIcon.Loading);
        private ISettingProvider SettingProvider;


        public ConnectionDialogViewModel(string connectionString
            , IDaxStudioHost host
            , IEventAggregator eventAggregator
            , bool hasPowerPivotModel
            , DocumentViewModel document
            , ISettingProvider settingProvider) 
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
                _powerBIInstances = PowerBIHelper.GetLocalInstances();


                if (PowerBIInstanceDetected)
                {
                    if (SelectedPowerBIInstance == null) SelectedPowerBIInstance = _powerBIInstances[0];
                } else
                {
                    if (PowerBIModeSelected) ServerModeSelected = true;
                    SelectedPowerBIInstance = null;
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

            bool res = await Task.FromResult<bool>(Host.Proxy.HasPowerPivotModel);
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
                    NotifyOfPropertyChange(() => ServerModeSelected);
                    NotifyOfPropertyChange(nameof(IsRolesEnabled));
                    NotifyOfPropertyChange(nameof(IsEffectiveUserNameEnabled));
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
                    NotifyOfPropertyChange(nameof(IsRolesEnabled));
                    NotifyOfPropertyChange(nameof(IsEffectiveUserNameEnabled));
                }
            }
        }

        private void ParseConnectionString()
        {
            if (_connectionString != String.Empty)
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
                        if (_connectionProperties["Application Name"].StartsWith("DAX Studio (Power BI)"))
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
                                MdxCompatibility = MdxCompatibilityOptions.Find(x => x.StartsWith(p.Value));
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

        private string _additionalOptions = string.Empty;
        public string AdditionalOptions {
            get { if (_additionalOptions.Trim().EndsWith(";"))
                    return _additionalOptions;
                else
                    return _additionalOptions.Trim() + ";";
            }
            set { _additionalOptions = value; }
        }

        private string _dataSource;
        public string DataSource { 
            get { 
                if (RecentServers.Count > 0 && String.IsNullOrWhiteSpace(_dataSource))
                { _dataSource = RecentServers[0]; }
                return  _dataSource; } 
            set{ _dataSource=value;
                NotifyOfPropertyChange(()=> DataSource);
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
                return _roles.Trim();
            }
            set { _roles = value; }
        }

        public bool IsRolesEnabled { get { return true; } }
        public string EffectiveUserName { get; set; }
        public bool IsEffectiveUserNameEnabled { get { return true; } }
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

        //StringBuilder _additionalProperties = new StringBuilder();
        //public StringBuilder AdditionalProperties { get {return _additionalProperties;  }}

        private Dictionary<string, string> SplitConnectionString(string connectionString)
        {
            var props = new Dictionary<string, string>();
            foreach (var prop in connectionString.Split(';'))
            {
                if (prop.Trim().Length == 0) continue;
                var p = prop.Split('=');

                props.Add(p[0], p[1]);
            }
            return props;
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

        public ObservableCollection<string> RecentServers
        {
            get {var list = SettingProvider.GetServerMRUList();
                return list;
            }
        }

        public bool PowerPivotEnabled { get; private set; }

        public string WorkbookName { get; private set; }

        private string GetRolesProperty()
        {
            return Roles == string.Empty ? string.Empty : string.Format("Roles={0};", Roles);
        }

        private string GetDirectQueryMode()
        {
            if (DirectQueryMode == string.Empty || DirectQueryMode.ToLower() == "default")
                return string.Empty;
            else
                return string.Format("DirectQueryMode={0};", DirectQueryMode);
        }

        private string GetMdxCompatibilityMode()
        {
            return string.Format("MDX Compatibility={0};", MdxCompatibility.Substring(0, 1));
        }

        private string _mdxCompatibility;
        private bool _selectedServerSetFocus;
        public string MdxCompatibility { 
            get { 
                if (string.IsNullOrWhiteSpace(_mdxCompatibility))
                    {_mdxCompatibility = MdxCompatibilityOptions.Find(x=> x.StartsWith("3"));}
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
            return string.Format("Data Source=localhost:{0};{1}{2}{3}{4}{5}{6}{7}"
                        , SelectedPowerBIInstance.Port // 0
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
            return string.Format("Data Source={0};{1}{2}{3}{4}{5}{6};{7}", DataSource
                                 , GetMdxCompatibilityMode()     //1
                                 , GetDirectQueryMode()          //2
                                 , GetRolesProperty()            //3
                                 , GetLocaleIdentifier()         //4
                                 , GetEffectiveUserName()        //5
                                 , AdditionalOptions             //6
                                 , GetApplicationName("SSAS"));  //7
        }

        private string GetApplicationName(string connectionType)
        {
            return string.Format("Application Name=DAX Studio ({0}) - {1}", connectionType, _activeDocument.UniqueID);
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

        public void Connect()
        {
            try
            {
                string serverType=null;
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
                    serverType = "SSAS";
                }
                if (PowerPivotModeSelected) { serverType = "PowerPivot"; }
                if (PowerBIModeSelected)
                {
                    powerBIFileName = SelectedPowerBIInstance.Name;
                    switch (SelectedPowerBIInstance.Icon)
                    {
                        case EmbeddedSSASIcon.Devenv:
                            serverType = "SSDT";
                            break;
                        case EmbeddedSSASIcon.PowerBI:
                            serverType = "PBI Desktop";
                            break;
                        case EmbeddedSSASIcon.PowerBIReportServer:
                            serverType = "PBI Report Server";
                            break;
                    }
                }
                var connEvent = new ConnectEvent(ConnectionString, PowerPivotModeSelected, WorkbookName, GetApplicationName(ConnectionType),powerBIFileName, serverType);
                _eventAggregator.PublishOnUIThread(connEvent);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} Error Connecting using: {connStr}", "ConnectionDialogViewModel", "Connect", ConnectionString);
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
                                              || (_powerBIInstances.Count == 1 && _powerBIInstances[0] != _pbiLoadingInstance);

        public List<PowerBIInstance> PowerBIDesignerInstances { get { return _powerBIInstances; } }
        public bool PowerBIModeSelected { get => _powerBIModeSelected; set {
                _powerBIModeSelected = value;
                NotifyOfPropertyChange(nameof(IsRolesEnabled));
                NotifyOfPropertyChange(nameof(IsEffectiveUserNameEnabled));
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
                    foreach (var ci in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
                    {
                        _locales.Add(ci.DisplayName, new LocaleIdentifier() { 
                            DisplayName = string.Format("{0} - {1}",ci.DisplayName,ci.LCID) , 
                            LCID = ci.LCID });
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
    }
     
    public class LocaleIdentifier
    {
        public string DisplayName {get;set;}
        public int LCID { get; set; }
        public override string ToString() { return DisplayName; }
    }
}
