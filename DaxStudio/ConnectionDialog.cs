using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ADOTabular;
using Microsoft.Office.Interop.Excel;


namespace DaxStudio
{
    public partial class ConnectionDialog : Form
    {
        public ConnectionDialog()
        {
            InitializeComponent();
        }

        private readonly Workbook _workbook;
        private readonly string _connectionString = "";
        private Dictionary<String, String> _connectionProperties;
        private readonly ExcelHelper _xlHelper;

        public ConnectionDialog(Workbook wbk,String currentConnection, ExcelHelper xlHelper)
        {
            InitializeComponent();
            _workbook = wbk;
            _connectionString = currentConnection;
            _xlHelper = xlHelper;
        }

        private void ConnectionDialogLoad(object sender, EventArgs e)
        {
            lblWorkbook.Text = _workbook.Name;
            lblPowerPivotUnavailable.Visible = false;
            RegistryHelper.LoadServerMRUListFromRegistry(cboServers);
            // if current workbook does not have PPvt data disable that option
            if (!_xlHelper.HasPowerPivotData())
            {
                radPowerPivot.Enabled = false;
                lblPowerPivotUnavailable.Visible = true;
            }

            // if connection string is not blank, split it into it's pieces
            // and populate UI 
            if (_connectionString != String.Empty)
            {
                _connectionProperties = SplitConnectionString(_connectionString);
                // if data source = $Embedded$ then mark Ppvt option as selected 
                if (_connectionProperties["Data Source"] == "$Embedded$")
                    radPowerPivot.Checked = true;
                else
                {
                    radServer.Checked = true;
                    // set dialog box properties
                    var sbOther = new StringBuilder();
                    foreach (var p in _connectionProperties)
                    {
                        switch (p.Key.ToLower())
                        {
                            case "data source" :
                                cboServers.Text = p.Value;
                                break;
                            case "roles" :
                                txtRoles.Text = p.Value;
                                break;
                            case "effectiveusername" :
                                txtEffectiveUserName.Text = p.Value;
                                break;
                            case "mdx compatibility" :
                                KeyValuePair<string, string> p1 = p;
                                foreach (var compat in cboMdxCompat.Items.Cast<string>().Where(compat => compat.StartsWith(p1.Value)))
                                {
                                    cboMdxCompat.Text = compat;
                                }
                                break;
                            case "directquerymode" :
                                cboDirectQuery.Text = p.Value;
                                break;
                            default:
                                sbOther.Append(string.Format("{0}={1};",p.Key,p.Value));
                                break;
                        }   
                    }   
                }
            }
            else
            {
                if (!radPowerPivot.Enabled )
                {
                    radServer.Checked = true;
                    cboServers.Select();
                }
            }


        }

        private Dictionary<string,string> SplitConnectionString(string connectionString)
        {
            var props = new Dictionary<string, string>();
            foreach (var prop in connectionString.Split(';') )
            {
                if (prop.Trim().Length == 0) continue;
                var p = prop.Split('=');
                props.Add(p[0],p[1]);
            }
            return props;
        }


        /*
         * Note: The ability to connect to PowerPivot from DAX Studio (or any third party application) is not
         * supported by Microsoft. Microsoft does not support connecting to PowerPivot models using the APIs 
         * used here and as such the behaviour may change or stop working without notice in future releases. 
         * This functionality is provided on an "as-is" basis.
         */
        private string BuildPowerPivotConnection()
        {
            return string.Format("Data Source=$Embedded$;Location={0}", _workbook.FullName);
        }

        private string BuildServerConnection()
        {
            //OLEDB;Provider=MSOLAP.5;Persist Security Info=True;Data Source=.\SQL2012TABULAR;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue
            return string.Format("Data Source={0};{1}{2}{3}{4}", cboServers.Text
                                 , GetMdxCompatibilityMode()
                                 , GetDirectQueryMode()
                                 , GetRolesProperty()
                                 , txtAdditionalOptions.Text);
        }

        private string GetRolesProperty()
        {
            return txtRoles.Text == string.Empty ? string.Empty:string.Format("Roles={0};", txtRoles.Text);
        }
        private string GetDirectQueryMode()
        {
            return cboDirectQuery.SelectedIndex == 0 ? string.Empty : string.Format("DirectQueryMode={0}",cboDirectQuery.Text);
        }
        private string GetMdxCompatibilityMode()
        {
            return string.Format("MDX Compatibility={0};", cboMdxCompat.Text.Substring(0, 1));
        }

        public string ConnectionString
        {
            get
            {
                return radPowerPivot.Checked ? BuildPowerPivotConnection() : BuildServerConnection();
            }
        }

        private void BtnConnectClick(object sender, EventArgs e)
        {
            try
            {
                Connection = new ADOTabularConnection(ConnectionString);
                RegistryHelper.SaveServerMRUListToRegistry(cboServers);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Error Connecting: {0}", ex.Message));
            }
        }

        public ADOTabularConnection Connection { get; private set; }

        private void ConnectionDialogClosing(object sender, FormClosingEventArgs e)
        {
            if (Connection == null) e.Cancel = true;
        }

        private void BtnCancelClick(object sender, EventArgs e)
        {
            Visible = false;
        }

        private void CboServersEnter(object sender, EventArgs e)
        {
            radServer.Checked = true;
        }

        private void CboServersSelectedIndexChanged(object sender, EventArgs e)
        {
            radServer.Checked = true;
        }

        private void CboServersTextUpdate(object sender, EventArgs e)
        {
            radServer.Checked = true;
        }


    }
}
