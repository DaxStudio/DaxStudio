using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    class ConnectionDialogViewModel : Screen  
    {

        private readonly IEventAggregator _eventAggregator;
        private readonly string _connectionString;
        public ConnectionDialogViewModel(string connectionString, IDaxStudioHost host, IEventAggregator eventAggregator, bool hasPowerPivotModel )
        {
            _eventAggregator = eventAggregator;
            _connectionString = connectionString;
            PowerPivotEnabled = true;
            Host = host;
            ServerModeSelected = true;
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
                    }
                   
                //}
            }
            
            WorkbookName = host.Proxy.WorkbookName;
            DisplayName = "Connect To";
            MdxCompatibility = "3- (Default) Placeholder members are not exposed";
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

        private void ConnectionDialogLoad(object sender, EventArgs e)
        {
            if (_connectionString != String.Empty)
            {
                _connectionProperties = SplitConnectionString(_connectionString);
                // if data source = $Embedded$ then mark Ppvt option as selected 
                if (_connectionProperties["Data Source"] == "$Embedded$")
                {
                    PowerPivotModeSelected = true;
                }
                else
                {
                    foreach (var p in _connectionProperties)
                    {
                        switch (p.Key.ToLower())
                        {
                            case "data source":
                                DataSource = p.Value;
                                break;
                            case "roles":
                                Roles = p.Value;
                                break;
                            case "effectiveusername":
                                EffectiveUserName = p.Value;
                                break;
                                /*
                        case "mdx compatibility":
                            KeyValuePair<string, string> p1 = p;
                            foreach (
                                var compat in
                                    cboMdxCompat.Items.Cast<string>().Where(
                                        compat => compat.StartsWith(p1.Value)))
                            {
                                cboMdxCompat.Text = compat;
                            }
                            break;
                                */
                            case "directquerymode":
                                DirectQueryMode = p.Value;
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
            }           
        }

        public string DataSource { get; set; }
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
        public string DirectQueryMode { get; set; }
        public StringBuilder AdditionalProperties { get; set; }

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
            return DirectQueryMode == string.Empty ? string.Empty : string.Format("DirectQueryMode={0}", DirectQueryMode);
        }

        private string GetMdxCompatibilityMode()
        {
            return string.Format("MDX Compatibility={0};", MdxCompatibility.Substring(0, 1));
        }

        public string MdxCompatibility { get; set; }

        public string ConnectionString
        {
            get
            {
                return PowerPivotModeSelected ? BuildPowerPivotConnection() : BuildServerConnection();
            }
        }

        private string BuildServerConnection()
        {
            //OLEDB;Provider=MSOLAP.5;Persist Security Info=True;Data Source=.\SQL2012TABULAR;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue
            return string.Format("Data Source={0};{1}{2}{3}{4}", DataSource
                                 , GetMdxCompatibilityMode()
                                 , GetDirectQueryMode()
                                 , GetRolesProperty()
                                 , AdditionalProperties);
        }

        /*
        * Note: The ability to connect to PowerPivot from DAX Studio (or any third party application) is not
        * supported by Microsoft. Microsoft does not support connecting to PowerPivot models using the APIs 
        * used here and as such the behaviour may change or stop working without notice in future releases. 
        * This functionality is provided on an "as-is" basis.
        */
        private string BuildPowerPivotConnection()
        {    
            // TODO - need Full workbook name, not just the display name
            //return string.Format("Data Source=$Embedded$;Location={0}", WorkbookName);
            return Host.Proxy.GetPowerPivotConnection().ConnectionString;
            
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
                _eventAggregator.Publish(new ConnectEvent(ConnectionString, PowerPivotModeSelected, WorkbookName));
            }
            catch (Exception ex)
            {
                _eventAggregator.Publish(
                    new OutputMessage(MessageType.Error
                        , String.Format("Could not connect to '{0}': {1}", DataSource, ex.Message))
                    );
            }
            finally
            {
                TryClose(true);
            }
        }

        public void Cancel()
        {
            _eventAggregator.Publish(new CancelConnectEvent());
        }
    }
        
}
