using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using DaxStudio.Properties;
using Excel = Microsoft.Office.Interop.Excel;
using ADOTabular;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

namespace DaxStudio
{
    public partial class DaxStudioForm : Form, IOutputWindow
    {

        private enum QueryType
        {
            ToTable = 0,
            ToStatic =1,
            NoResults=2
        }
        private readonly BackgroundWorker _bgQuery = new BackgroundWorker();
        private readonly BackgroundWorker _bgMetadata = new BackgroundWorker();
        private bool _refreshingMetadata = false;
        private ADOTabularConnection _conn;
        private Excel.Application _app;
        private QueryType _defaultQueryType = QueryType.ToTable;
        private Excel.Workbook _workbook;

        public string CurrentConnectionString
        {
            get
            {
                return _conn.ConnectionString;
            }
        }

        public DaxStudioForm()
        {
            InitializeComponent();          
            // hook up the KeyUp event of the DaxEditor (wpf) control
            var ctr = (elementHost1.Child as DaxEditorUserControl);
            if (ctr == null)
                return;
            ctr.KeyUp += DaxEditorKeyUp;
            _bgMetadata.DoWork += (sender, args) => RefreshTabularMetadata();
        }

        public Excel.Application Application
        {
            get { return _app; }
            set { _app = value; }
        }

        private string GetTextToExecute()
        {
            // if text is selected try to execute that
            return ucDaxEditor.daxEditor.SelectionLength == 0 ? ucDaxEditor.daxEditor.Text : ucDaxEditor.daxEditor.SelectedText;
        }


        private void DaxStudioFormKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyData)
            {
                case Keys.F5:
                    RunDefaultQueryType();
                    break;
            }
        }

        private void TspExportMetadataClick(object sender, EventArgs e)
        {
            /*
            Excel.Workbook excelWorkbook = app.ActiveWorkbook;

            // Create a new Sheet
            Excel.Worksheet excelSheet = (Excel.Worksheet)excelWorkbook.Sheets.Add(
                Type.Missing, excelWorkbook.Sheets.get_Item(excelWorkbook.Sheets.Count)
                , 1, Excel.XlSheetType.xlWorksheet);

            Microsoft.AnalysisServices.Server svr = new Microsoft.AnalysisServices.Server();

            svr.Connect("$embedded$");
            Microsoft.AnalysisServices.Database db = svr.Databases[0];

            //"Type`tTable`tColumn`tSource"
            // Foreach dimension loop through each attribute and output the source
            foreach (Microsoft.AnalysisServices.Dimension dim in db.Dimensions)
            {
                //#"Dimension: $($dim.Name)"
                string tableSrc = ((Microsoft.AnalysisServices.QueryBinding)db.Cubes["Model"].MeasureGroups[dim.ID].Partitions[0].Source).QueryDefinition
                //"TABLE`t$($dim.Name)`t`t$tsrc"
                foreach (Microsoft.AnalysisServices.DimensionAttribute att in dim.Attributes)
                {

                    if (att.Name != "RowNumber")  // ## don't show the internal RowNumber column
                    {
                        foreach (Microsoft.AnalysisServices.Binding col in att.KeyColumns)
                        {
                            if (col is Microsoft.AnalysisServices.ExpressionBinding)
                            {
                                //"CALCULATED COLUMN`t$($dim.Name)`t$($att.Name)`t$($col.source.Expression)"
                            }
                            else
                            {
                                //"COLUMN`t$($dim.Name)`t$($att.Name)`t$($col.source.ColumnID)"
                            }
                        }
                    }
                }
            }

            svr.Disconnect();
             */
        }

        private ExcelHelper _xlHelper;
        private void DaxStudioFormLoad(object sender, EventArgs e)
        {
            ucDaxEditor.AllowDrop = true;
            ucDaxEditor.Drop += UcDaxEditorDrop;
            _xlHelper = new ExcelHelper(_app, tcbOutputTo);
            _workbook = Application.ActiveWorkbook;
        }

        void UcDaxEditorDrop(object sender, System.Windows.DragEventArgs e)
        {
            ucDaxEditor.daxEditor.SelectedText = e.Data.ToString();
        }

        private void TvwItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(((TreeNode)e.Item).Name, DragDropEffects.Move);
        }

        private void RunStaticResultsToolStripMenuItemClick(object sender, EventArgs e)
        {
            RunQuery(QueryType.ToStatic);
        }

        private void RunDsicardResultsToolStripMenuItemClick(object sender, EventArgs e)
        {
            RunQuery(QueryType.NoResults);
        }

        private void RunQueryTableToolStripMenuItemClick(object sender, EventArgs e)
        {
            RunQuery(QueryType.ToTable);
        }

        private void ToolStripCmdModelsClick(object sender, EventArgs e)
        {
            var conDialog = new ConnectionDialog(Application.ActiveWorkbook, _conn.ConnectionString, _xlHelper);
            if (conDialog.ShowDialog() == DialogResult.Cancel) return;

            _conn = conDialog.Connection;
            RefreshDatabaseList();
            //RefreshTabularMetadata();
        }

        private void RefreshTabularMetadataAsync()
        {
            tspStatus.Text = Resources.Refreshing_Metadata;
            _bgMetadata.RunWorkerAsync();
            tspStatus.Text = Resources.Status_Ready;
        }

        private void RefreshTabularMetadata()
        {
            _refreshingMetadata = true;
            try
            {
                //populate metadata tabs
                TabularMetadata.PopulateConnectionMetadata(_conn, tvwMetadata, tvwFunctions, listDMV, cboModel.Text);
                
                // update status bar
                
                tspConnection.Text = _conn.ServerName;
                tspVersion.Text = _conn.ServerVersion;
                tspSpid.Text = _conn.SPID.ToString(CultureInfo.InvariantCulture);
            }
            finally
            {
                _refreshingMetadata = false;
            }
        }

        private void RunLastQueryType(object sender, EventArgs e) //object sender, EventArgs e)
        {
            RunDefaultQueryType();

        }

        private void RunDefaultQueryType() //object sender, EventArgs e)
        {
            RunQuery(_defaultQueryType);
        }

        private string BuildPowerPivotConnection()
        {
            return string.Format("Data Source=$Embedded$;Location={0}", _workbook.FullName);
        }

        private void DaxStudioFormShown(object sender, EventArgs e)
        {
            var wb = _app.ActiveWorkbook;
            if (_xlHelper.HasPowerPivotData())
            {
                // if current workbook has PowerPivot data ensure it is loaded into memory
                _xlHelper.EnsurePowerPivotDataIsLoaded();
                _conn = new ADOTabularConnection(BuildPowerPivotConnection());
                RefreshDatabaseList();
                //RefreshTabularMetadata();
            }
            else
            {
                var connDialog = new ConnectionDialog(wb,"",_xlHelper);
                if (connDialog.ShowDialog() == DialogResult.OK)
                {
                    _conn = new ADOTabularConnection(connDialog.ConnectionString);
                    RefreshDatabaseList();
                    //cboDatabase.SelectedIndex = 0;
                    //RefreshTabularMetadata();
                }
            }
        }

        private void CboDatabasesSelectionChangeCommitted(object sender, EventArgs e)
        {
            if (!_refreshingMetadata)
            {
                _conn.ChangeDatabase(cboDatabase.Text);
                RefreshModelList();
                
            }
        }

        private void ListDmvItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(((ListViewItem)e.Item).Name, DragDropEffects.Move);
        }

        private void DaxStudioFormActivated(object sender, EventArgs e)
        {
            if (Application.ActiveWorkbook == _workbook) return;
            _workbook = Application.ActiveWorkbook;
            if (_conn.ServerName != "$Embedded$") return;
            if ( _xlHelper.HasPowerPivotData())
            {
                //change connection
                RefreshTabularMetadata();
            }
            else
            {
                // prompt for new connection
                var connDialog = new ConnectionDialog(_workbook, null, _xlHelper);
                if (connDialog.ShowDialog() == DialogResult.OK)
                {
                    _conn = connDialog.Connection;
                    RefreshTabularMetadata();
                }
                else
                {
                    ClearTabularMetadata();
                }
            }
        }

        public void ClearTabularMetadata()
        {
            tvwFunctions.Nodes.Clear();
            tvwMetadata.Nodes.Clear();
            listDMV.Items.Clear();
        }

        public void RefreshDatabaseList()
        {
            cboDatabase.Items.Clear();
            //populate db dropdown
            foreach (var database in _conn.Databases)
            {
                cboDatabase.Items.Add( database);
            }
            //select first db
            if (cboDatabase.Items.Count >= 1) cboDatabase.SelectedIndex = 0;
            //_conn.ChangeDatabase(cboDatabase.Text);

            
        }

        public void RefreshModelList()
        {
            // populate model tab
            cboModel.Items.Clear();
            foreach (var model in _conn.Database.Models)
            {
                cboModel.Items.Add(model.Name);
            }
            // select first model
            if (cboModel.Items.Count > 0)
            {
                cboModel.Text = cboModel.Items[0].ToString();
            }
            RefreshTabularMetadata();
        }


        #region IOutputWindow methods

        public void ClearOutput()
        {
            rtbOutput.Clear();
            rtbOutput.ForeColor = Color.Black;
        }

        public void WriteOutputMessage(string message)
        {
            rtbOutput.ForeColor = Color.Black;
            rtbOutput.AppendText(message + "\n");
        }

        public void WriteOutputError(string message)
        {
            rtbOutput.ForeColor = Color.Red;
            rtbOutput.Text = message;
        }
        
        #endregion

        private void CboModelSelectionChangeCommitted(object sender, EventArgs e)
        {
            RefreshTabularMetadata();
        }

        private void DocumentationToolStripMenuItemClick(object sender, EventArgs e)
        {
            Process.Start(Resources.DocumentationUrl);
        }

        private void AboutToolStripMenuItemClick(object sender, EventArgs e)
        {
            var aboutBox = new AboutDaxStudio();
            aboutBox.ShowDialog();
        }

        private void MsdnForumsToolStripMenuItemClick(object sender, EventArgs e)
        {
            Process.Start(Resources.MsdnForumsUrl);
        }

        private void analysisServicesToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void ElementHostChildChanged(object sender, ChildChangedEventArgs e)
        {
            var ctr = (elementHost1.Child as DaxEditorUserControl);
            if (ctr == null)
                return;
            ctr.KeyUp += DaxEditorKeyUp;
        }

        private void RunQuery(QueryType queryType)
        {
            _defaultQueryType = queryType;
            btnRun.Image = GetQueryImage(queryType);
            switch (queryType)
            {
                case QueryType.ToTable:
                    DaxQueryHelpers.DaxQueryTable(_xlHelper.SelectedOutput, CurrentConnectionString, GetTextToExecute(), this);
                    break;
                case QueryType.ToStatic:
                    DaxQueryHelpers.DaxQueryStaticResult(_xlHelper.SelectedOutput, CurrentConnectionString, GetTextToExecute(), this, _xlHelper);
                    break;
                case QueryType.NoResults:
                    DaxQueryHelpers.DaxQueryDiscardResults(_conn, GetTextToExecute(), this);
                    break;
            }
        }

        private Image GetQueryImage(QueryType queryType)
        {
            switch (queryType)
            {
                case QueryType.ToTable:
                    return (Image)runQueryTableToolStripMenuItem.Image;    
                case QueryType.ToStatic:
                    return (Image)runStaticResultsToolStripMenuItem.Image;
                case QueryType.NoResults:
                    return (Image)runDiscardResultsToolStripMenuItem.Image;
                default:
                    return (Image)runQueryTableToolStripMenuItem.Image;
            }
        }

        void DaxEditorKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var wpfKey = e.Key == Key.System ? e.SystemKey : e.Key;
            var formsKey = (Keys)KeyInterop.VirtualKeyFromKey(wpfKey);
            var e2 = new KeyEventArgs(formsKey);
            
            // "forward" the event to the form's KeyUp handler
            //if (e2.KeyData == Key.F5 || e.KeyboardDevice.Modifiers != System.Windows.Input.ModifierKeys.None)
                DaxStudioFormKeyUp(sender, e2);
        }

        private void clearCacheToolStripMenuItem_Click(object sender, EventArgs e) {
            DaxQueryHelpers.DaxClearCache(_conn, this);
        }
    }
}
