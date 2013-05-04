using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using ADOTabular;
using DaxStudio;
using DaxStudio.AdomdClientWrappers;

namespace DaxStudio.ViewModel
{
    class ConnectionViewModel : INotifyPropertyChanged
    {

#region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        // Create the OnPropertyChanged method to raise the event 
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
#endregion

        public ConnectionViewModel(ADOTabular.ADOTabularConnection conn)
        {
            Connection = conn;
            PowerPivotEnabled = false;
            ServerModeSelected = true;
            WorkbookName = "";
            OnPropertyChanged("WorkbookName");
            OnPropertyChanged("PowerPivotEnabled");
            OnPropertyChanged("ServerModeSelected");
        }

        public ConnectionViewModel(ADOTabular.ADOTabularConnection conn, string workbookName)
        {
            Connection = conn;
            PowerPivotEnabled = true;
            WorkbookName = workbookName;
            OnPropertyChanged("WorkbookName");
            OnPropertyChanged("PowerPivotEnabled");
            OnPropertyChanged("ServerModeSelected");
            OnPropertyChanged("PowerPivotUnavailableVisibility");
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

        private bool _serverModeSelected;
        private bool _powerPivotModeSelected;
        private Dictionary<string, string> _connectionProperties;

        public bool ServerModeSelected
        {
            get { return _serverModeSelected; }
            private set
            {
                if (value != _serverModeSelected)
                {
                    _serverModeSelected = value;
                    OnPropertyChanged("ServerModeSelected");
                }
            }
        }

        public bool PowerPivotModeSelected
        {
            get { return _powerPivotModeSelected; }
            private set
            {
                if (value != _powerPivotModeSelected)
                {
                    _powerPivotModeSelected = value;
                    OnPropertyChanged("PowerPivotModeSelected");
                }
            }
        }

        private void ConnectionDialogLoad(object sender, EventArgs e)
        {
        //    lblWorkbook.Text = _workbook.Name;
        //    lblPowerPivotUnavailable.Visible = false;
            
            // if current workbook does not have PPvt data disable that option
        /*
            if (!_xlHelper.HasPowerPivotData())
            {
                radPowerPivot.Enabled = false;
                lblPowerPivotUnavailable.Visible = true;
            }
         */ 

            // if connection string is not blank, split it into it's pieces
            // and populate UI 
            if (Connection == null)
            {
            }
            else
            {


                if (Connection.ConnectionString != String.Empty)
                {
                    _connectionProperties = SplitConnectionString(Connection.ConnectionString);
                    // if data source = $Embedded$ then mark Ppvt option as selected 
                    if (_connectionProperties["Data Source"] == "$Embedded$")
                    {
                        //           radPowerPivot.Checked = true;
                        PowerPivotModeSelected = true;
                    }
                    else
                    {
                        //           radServer.Checked = true;
                        // set dialog box properties
                        //var sbOther = new StringBuilder();
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

        }

        public string DataSource { get; set; }
        public string Roles { get; set; }
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

        public ADOTabularConnection Connection { get; set; }   

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
            return string.Format("Data Source=$Embedded$;Location={0}", WorkbookName);
        }


        public AdomdType ConnectionType
        {
            get { return PowerPivotModeSelected ? AdomdType.Excel : AdomdType.AnalysisServices; }
        }


    }
        
}
