using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using ADOTabular;
using Microsoft.Win32;
using Microsoft.Windows.Controls.Ribbon;
using DaxStudio.Properties;

namespace DaxStudio
{
    /// <summary>
    /// Interaction logic for DaxStudioWindow.xaml
    /// </summary>
    public partial class DaxStudioWindow : RibbonWindow
    {
        private enum QueryType
        {
            ToTable = 0,
            ToStatic = 1,
            NoResults = 2,
            ToGrid = 3
        }

        private bool _refreshingMetadata;
        private ADOTabularConnection _conn;
        private Microsoft.Office.Interop.Excel.Application _app;
        private QueryType _defaultQueryType = QueryType.ToTable;
        private Microsoft.Office.Interop.Excel.Workbook _workbook;
        private readonly ExcelHelper _xlHelper;

        public DaxStudioWindow(Microsoft.Office.Interop.Excel.Application app)
        {
            InitializeComponent();
            Application = app;
            // Insert code required on object creation below this point.
            daxEditorUserControl1.AllowDrop = true;
            daxEditorUserControl1.Drop += UcDaxEditorDrop;
            _xlHelper = new ExcelHelper(_app, cboOutputTo);
            _workbook = Application.ActiveWorkbook;
        }

        public Microsoft.Office.Interop.Excel.Application Application
        {
            get { return _app; }
            set { _app = value; }
        }


        private string _fileName = string.Empty;
        public string FileName
        {
            get { return _fileName; }
            set
            {
                _fileName = value;
                Title = string.Format("{0} - {1}", Properties.Resources.DAX_Studio_Caption, _fileName);
            }
        }

        private string GetTextToExecute()
        {
            // if text is selected try to execute that
            return daxEditorUserControl1.daxEditor.SelectionLength == 0 ? daxEditorUserControl1.daxEditor.Text : daxEditorUserControl1.daxEditor.SelectedText;
        }


        void UcDaxEditorDrop(object sender, System.Windows.DragEventArgs e)
        {
            daxEditorUserControl1.daxEditor.SelectedText = e.Data.ToString();
        }
/*
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
            _conn.ShowHiddenObjects = true;
            RefreshDatabaseList();
            //RefreshTabularMetadata();
        }


        private void RefreshTabularMetadata()
        {
            _refreshingMetadata = true;
            try
            {
                tspStatus.Text = Resources.Refreshing_Metadata;
                Cursor = System.Windows.Forms.Cursors.WaitCursor;
                System.Windows.Forms.Application.DoEvents();
                //populate metadata tabs
                TabularMetadata.PopulateConnectionMetadata(_conn, tvwMetadata, tvwFunctions, listDMV, cboModel.Text);

                ResetCursor();
                // update status bar
                tspStatus.Text = Resources.Status_Ready;

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
                cboDatabase.Items.Add(database);
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
            Process.Start(Properties.Resources.DocumentationUrl);
        }

        private void AboutToolStripMenuItemClick(object sender, EventArgs e)
        {
            var aboutBox = new AboutDaxStudio();
            aboutBox.ShowDialog();
        }

        private void MsdnForumsToolStripMenuItemClick(object sender, EventArgs e)
        {
            Process.Start(Properties.Resources.MsdnForumsUrl);
        }

        private void RunQuery(QueryType queryType)
        {
            _defaultQueryType = queryType;
            btnRun.Image = GetQueryImage(queryType);
            switch (queryType)
            {
                case QueryType.ToTable:
                    DaxQueryHelpers.DaxQueryTable(_xlHelper.SelectedOutput, _conn, GetTextToExecute(), this);
                    break;
                case QueryType.ToStatic:
                    DaxQueryHelpers.DaxQueryStaticResult(_xlHelper.SelectedOutput, _conn, GetTextToExecute(), this, _xlHelper);
                    break;
                case QueryType.NoResults:
                    DaxQueryHelpers.DaxQueryDiscardResults(_conn, GetTextToExecute(), this);
                    break;
                case QueryType.ToGrid:
                    if (this._daxResultGrid.IsDisposed == true)
                        _daxResultGrid = new DaxResultGrid();
                    DaxQueryHelpers.DaxQueryGrid(_conn, GetTextToExecute(), this, _daxResultGrid);
                    break;
            }
        }

        private Image GetQueryImage(QueryType queryType)
        {
            switch (queryType)
            {
                case QueryType.ToTable:
                    return runQueryTableToolStripMenuItem.Image;
                case QueryType.ToStatic:
                    return runStaticResultsToolStripMenuItem.Image;
                case QueryType.NoResults:
                    return runDiscardResultsToolStripMenuItem.Image;
                case QueryType.ToGrid:
                    return runGridResultsToolStripMenuItem.Image;

                default:
                    return runQueryTableToolStripMenuItem.Image;
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

        private void ClearCacheToolStripMenuItemClick(object sender, EventArgs e)
        {
            DaxQueryHelpers.DaxClearCache(_conn, this);
        }

        #region file open/save

        private void OpenToolStripMenuItemClick(object sender, EventArgs e)
        {
            var openFileDialog1 = new OpenFileDialog();
            openFileDialog1.DefaultExt = "dax";
            openFileDialog1.Multiselect = false;
            //TODO 
            // file open dialog 
            if (openFileDialog1.ShowDialog() != true) return;
            // read file
            FileName = openFileDialog1.FileName;
            TextReader tr = new StreamReader(FileName, true);
            // put contents in edit window
            daxEditorUserControl1.daxEditor.Text = tr.ReadToEnd();
            tr.Close();
        }

        private void SaveAsToolStripMenuItemClick(object sender, EventArgs e)
        {
            var saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.DefaultExt = "dax";

            if (saveFileDialog1.ShowDialog() != true) return;
            FileName = saveFileDialog1.FileName;
            SaveDaxFile();
        }

        // save the current contents of the edit control to a unicode text file
        private void SaveDaxFile()
        {
            TextWriter tw = new StreamWriter(FileName, false, Encoding.Unicode);
            tw.Write(daxEditorUserControl1.daxEditor.Text);
            tw.Close();
        }

        private void SaveToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (FileName == string.Empty)
                SaveAsToolStripMenuItemClick(sender, e);
            else
                SaveDaxFile();
        }
        #endregion

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void runGridResultsToolStripMenuItem_Click(object sender, EventArgs e)
        {

            RunQuery(QueryType.ToGrid);
            //DaxQueryHelpers.DaxQueryGrid(_conn, GetTextToExecute(), this,  _daxResultGrid);

        }
*/

        public void RefreshDatabaseList()
        {
            cboDatabase.Items.Clear();
            //populate db dropdown
            foreach (var database in _conn.Databases)
            {
                cboDatabase.Items.Add(database);
            }
            //select first db
            // TODO - if (cboDatabase.Items.Count >= 1) cboDatabase.SelectedIndex = 0;
            //_conn.ChangeDatabase(cboDatabase.Text);


        }


        private string BuildPowerPivotConnection()
        {
            return string.Format("Data Source=$Embedded$;Location={0}", _workbook.FullName);
        }

        ///////////////////////////////////////////////

        private void DaxStudioWindowLoaded(object sender, RoutedEventArgs e)
        {
            var wb = _app.ActiveWorkbook;
            if (_xlHelper.HasPowerPivotData())
            {
                // if current workbook has PowerPivot data ensure it is loaded into memory
                _xlHelper.EnsurePowerPivotDataIsLoaded();
                _conn = new ADOTabularConnection(BuildPowerPivotConnection(), true);
                RefreshDatabaseList();
                //RefreshTabularMetadata();
            }
            else
            {
                var connDialog = new ConnectionDialog(wb, "", _xlHelper);
                if (connDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _conn = new ADOTabularConnection(connDialog.ConnectionString, true);
                    RefreshDatabaseList();
                    //cboDatabase.SelectedIndex = 0;
                    //RefreshTabularMetadata();
                }
            }
        }


    }
}
