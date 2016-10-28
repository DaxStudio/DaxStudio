using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using System.Threading.Tasks;
using DaxStudio.UI.Views;
using DaxStudio.UI.Utils;
using Serilog;
using System.Text.RegularExpressions;
using System.Globalization;

namespace DaxStudio.UI.ViewModels
{
    class ConnectionDialogViewModel : Screen
        , IHandle<ApplicationActivatedEvent>
    {

        private readonly IEventAggregator _eventAggregator;
        private readonly string _connectionString;
        private readonly DocumentViewModel _activeDocument;
        private readonly Regex _ppvtRegex;
        public ConnectionDialogViewModel(string connectionString
            , IDaxStudioHost host
            , IEventAggregator eventAggregator
            , bool hasPowerPivotModel
            , DocumentViewModel document ) 
        {
            try
            {
                _eventAggregator = eventAggregator;
                _eventAggregator.Subscribe(this);
                _connectionString = connectionString;
                _activeDocument = document;
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
            
            PowerBIHelper.Refresh();
            PowerBIDesignerDetected = PowerBIHelper.Instances.Count > 0;
            if (PowerBIHelper.Instances.Count > 0)
            {
                if (SelectedPowerBIInstance == null) SelectedPowerBIInstance = PowerBIHelper.Instances[0];
            } else
            {
                if (PowerBIModeSelected) ServerModeSelected = true;
                SelectedPowerBIInstance = null;
            }
            // update bound properties
            NotifyOfPropertyChange(() => PowerBIDesignerDetected);
            NotifyOfPropertyChange(() => PowerBIDesignerInstances);
            NotifyOfPropertyChange(() => SelectedPowerBIInstance);
            
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
                        if (_connectionProperties["Application Name"].StartsWith("DAX Studio (PowerBI)"))
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
                            default:
                                AdditionalProperties.Append(string.Format("{0}={1};", p.Key, p.Value));
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

        public string AdditionalOptions { get; set; }

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
        public string EffectiveUserName { get; set; }
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

        StringBuilder _additionalProperties = new StringBuilder();
        public StringBuilder AdditionalProperties { get {return _additionalProperties;  }}

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
            get {var list = RegistryHelper.GetServerMRUListFromRegistry();
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
            return DirectQueryMode == string.Empty || DirectQueryMode.ToLower() == "default" ? string.Empty : string.Format("DirectQueryMode={0};", DirectQueryMode);
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
            return string.Format("Data Source=localhost:{0};{1}{2}{3}{4}{5}{6};{7};Application Name={8}", SelectedPowerBIInstance.Port
                                 , GetMdxCompatibilityMode()
                                 , GetDirectQueryMode()
                                 , GetRolesProperty()
                                 , GetLocaleIdentifier()
                                 , GetEffectiveUserName()
                                 , AdditionalProperties
                                 , AdditionalOptions
                                 , GetApplicationName("Power BI"));
        }

        private object GetEffectiveUserName()
        {
            return EffectiveUserName == string.Empty ? string.Empty : string.Format("EffectiveUserName={0};", EffectiveUserName);
        }

        private string BuildServerConnection()
        {
            //OLEDB;Provider=MSOLAP.5;Persist Security Info=True;Data Source=.\SQL2012TABULAR;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue
            return string.Format("Data Source={0};{1}{2}{3}{4}{5}{6};{7};Application Name={8}", DataSource
                                 , GetMdxCompatibilityMode()     //1
                                 , GetDirectQueryMode()          //2
                                 , GetRolesProperty()            //3
                                 , GetLocaleIdentifier()         //4
                                 , GetEffectiveUserName()        //5
                                 , AdditionalProperties          //6
                                 , AdditionalOptions             //7
                                 , GetApplicationName("SSAS"));  //8
        }

        private string GetApplicationName(string connectionType)
        {
            return string.Format("DAX Studio ({0}) - {1}", connectionType, _activeDocument.UniqueID);
        }

        /*
        * Note: The ability to connect to PowerPivot from DAX Studio (or any third party application) is not
        * supported by Microsoft. Microsoft does not support connecting to PowerPivot models using the APIs 
        * used here and as such the behaviour may change or stop working without notice in future releases. 
        * This functionality is provided on an "as-is" basis.
        */
        private string BuildPowerPivotConnection()
        {    
            return Host.Proxy.GetPowerPivotConnection(GetApplicationName("Power Pivot")).ConnectionString;
            
        }

        public void Connect()
        {
            try
            {
                var vw = (Window)this.GetView();
                vw.Visibility = Visibility.Hidden;
                using (var c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices))
                {
                    c.Open();
                }
                if (ServerModeSelected)
                {
                    RegistryHelper.SaveServerMRUListToRegistry(DataSource, RecentServers);
                }
                var connEvent = new ConnectEvent(ConnectionString, PowerPivotModeSelected, WorkbookName, GetApplicationName(ConnectionType),"");
                if (PowerBIModeSelected)
                {
                    connEvent.PowerBIFileName = SelectedPowerBIInstance.Name;
                }
                _eventAggregator.PublishOnUIThread(connEvent);
            }
            catch (Exception ex)
            {
                _activeDocument.OutputError(String.Format("Could not connect to '{0}': {1}", PowerPivotModeSelected?"Power Pivot model":DataSource, ex.Message));
                _eventAggregator.PublishOnUIThread(new CancelConnectEvent());
            }
            finally
            {
                _eventAggregator.Unsubscribe(this);
                SelectedServerSetFocus = false;
                TryClose(true);
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

        public bool PowerBIDesignerDetected { get; private set; }

        public List<PowerBIInstance> PowerBIDesignerInstances { get { return PowerBIHelper.Instances; } }
        public bool PowerBIModeSelected { get; set; }

        public PowerBIInstance SelectedPowerBIInstance { get; set; }

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
                return string.Format("Locale Identifier={0}", Locale.LCID);
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
