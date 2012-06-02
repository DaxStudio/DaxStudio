namespace DaxStudio
{
    partial class DaxStudioForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DaxStudioForm));
            this.stsDax = new System.Windows.Forms.StatusStrip();
            this.tspStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.tspConnection = new System.Windows.Forms.ToolStripStatusLabel();
            this.tspVersion = new System.Windows.Forms.ToolStripStatusLabel();
            this.tspSpid = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripDropDownButton1 = new System.Windows.Forms.ToolStripDropDownButton();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveAsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.btnRun = new System.Windows.Forms.ToolStripSplitButton();
            this.runQueryTableToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runStaticResultsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runDiscardResultsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.clearCacheToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripCmdModels = new System.Windows.Forms.ToolStripButton();
            this.cboDatabase = new System.Windows.Forms.ToolStripComboBox();
            this.tspExportMetadata = new System.Windows.Forms.ToolStripButton();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.tcbOutputTo = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.btnHelp = new System.Windows.Forms.ToolStripDropDownButton();
            this.documentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mSDNForumsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.pnlModel = new System.Windows.Forms.Panel();
            this.cboModel = new System.Windows.Forms.ComboBox();
            this.tabMetadataBrowser = new System.Windows.Forms.TabControl();
            this.tabMetadata = new System.Windows.Forms.TabPage();
            this.tvwMetadata = new System.Windows.Forms.TreeView();
            this.imgListTree = new System.Windows.Forms.ImageList(this.components);
            this.tabFunctions = new System.Windows.Forms.TabPage();
            this.tvwFunctions = new System.Windows.Forms.TreeView();
            this.tabDMV = new System.Windows.Forms.TabPage();
            this.listDMV = new System.Windows.Forms.ListView();
            this.colDmv = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.elementHost1 = new System.Windows.Forms.Integration.ElementHost();
            this.ucDaxEditor = new DaxStudio.DaxEditorUserControl();
            this.rtbOutput = new System.Windows.Forms.RichTextBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.stsDax.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.pnlModel.SuspendLayout();
            this.tabMetadataBrowser.SuspendLayout();
            this.tabMetadata.SuspendLayout();
            this.tabFunctions.SuspendLayout();
            this.tabDMV.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.SuspendLayout();
            // 
            // stsDax
            // 
            this.stsDax.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tspStatus,
            this.tspConnection,
            this.tspVersion,
            this.tspSpid});
            this.stsDax.Location = new System.Drawing.Point(0, 425);
            this.stsDax.Name = "stsDax";
            this.stsDax.ShowItemToolTips = true;
            this.stsDax.Size = new System.Drawing.Size(777, 24);
            this.stsDax.TabIndex = 0;
            this.stsDax.Text = "statusStrip1";
            // 
            // tspStatus
            // 
            this.tspStatus.Name = "tspStatus";
            this.tspStatus.Size = new System.Drawing.Size(636, 19);
            this.tspStatus.Spring = true;
            this.tspStatus.Text = "Ready";
            this.tspStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // tspConnection
            // 
            this.tspConnection.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
            this.tspConnection.MergeIndex = 1;
            this.tspConnection.Name = "tspConnection";
            this.tspConnection.Size = new System.Drawing.Size(92, 19);
            this.tspConnection.Text = "Not Connected";
            this.tspConnection.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.tspConnection.ToolTipText = "ServerName";
            // 
            // tspVersion
            // 
            this.tspVersion.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
            this.tspVersion.Name = "tspVersion";
            this.tspVersion.Size = new System.Drawing.Size(17, 19);
            this.tspVersion.Text = "0";
            this.tspVersion.ToolTipText = "Server Version";
            // 
            // tspSpid
            // 
            this.tspSpid.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
            this.tspSpid.Name = "tspSpid";
            this.tspSpid.Size = new System.Drawing.Size(17, 19);
            this.tspSpid.Text = "0";
            this.tspSpid.ToolTipText = "Connection SPID";
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripDropDownButton1,
            this.btnRun,
            this.toolStripSeparator1,
            this.toolStripCmdModels,
            this.cboDatabase,
            this.tspExportMetadata,
            this.toolStripLabel1,
            this.tcbOutputTo,
            this.toolStripSeparator2,
            this.btnHelp});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(777, 25);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripDropDownButton1
            // 
            this.toolStripDropDownButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripDropDownButton1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.saveToolStripMenuItem,
            this.saveAsToolStripMenuItem,
            this.toolStripMenuItem1,
            this.exitToolStripMenuItem});
            this.toolStripDropDownButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripDropDownButton1.Image")));
            this.toolStripDropDownButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripDropDownButton1.Name = "toolStripDropDownButton1";
            this.toolStripDropDownButton1.Size = new System.Drawing.Size(38, 22);
            this.toolStripDropDownButton1.Text = "File";
            this.toolStripDropDownButton1.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.openToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            this.openToolStripMenuItem.Text = "Open...";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.OpenToolStripMenuItemClick);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.SaveToolStripMenuItemClick);
            // 
            // saveAsToolStripMenuItem
            // 
            this.saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            this.saveAsToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.S)));
            this.saveAsToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            this.saveAsToolStripMenuItem.Text = "Save As";
            this.saveAsToolStripMenuItem.Click += new System.EventHandler(this.SaveAsToolStripMenuItemClick);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(183, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            // 
            // btnRun
            // 
            this.btnRun.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnRun.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.runQueryTableToolStripMenuItem,
            this.runStaticResultsToolStripMenuItem,
            this.runDiscardResultsToolStripMenuItem,
            this.toolStripSeparator3,
            this.clearCacheToolStripMenuItem});
            this.btnRun.Image = global::DaxStudio.Properties.Resources.PlayTable1;
            this.btnRun.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRun.Name = "btnRun";
            this.btnRun.Size = new System.Drawing.Size(32, 22);
            this.btnRun.Text = "toolStripSplitButton1";
            this.btnRun.ToolTipText = "Run Query (F5)";
            this.btnRun.ButtonClick += new System.EventHandler(this.RunLastQueryType);
            // 
            // runQueryTableToolStripMenuItem
            // 
            this.runQueryTableToolStripMenuItem.Image = global::DaxStudio.Properties.Resources.PlayTable1;
            this.runQueryTableToolStripMenuItem.Name = "runQueryTableToolStripMenuItem";
            this.runQueryTableToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F5)));
            this.runQueryTableToolStripMenuItem.Size = new System.Drawing.Size(227, 22);
            this.runQueryTableToolStripMenuItem.Text = "Run (Query Table)";
            this.runQueryTableToolStripMenuItem.Click += new System.EventHandler(this.RunQueryTableToolStripMenuItemClick);
            // 
            // runStaticResultsToolStripMenuItem
            // 
            this.runStaticResultsToolStripMenuItem.Image = global::DaxStudio.Properties.Resources.PlayStatic;
            this.runStaticResultsToolStripMenuItem.Name = "runStaticResultsToolStripMenuItem";
            this.runStaticResultsToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.F5)));
            this.runStaticResultsToolStripMenuItem.Size = new System.Drawing.Size(227, 22);
            this.runStaticResultsToolStripMenuItem.Text = "Run (Static Results)";
            this.runStaticResultsToolStripMenuItem.Click += new System.EventHandler(this.RunStaticResultsToolStripMenuItemClick);
            // 
            // runDiscardResultsToolStripMenuItem
            // 
            this.runDiscardResultsToolStripMenuItem.Image = global::DaxStudio.Properties.Resources.PlayDiscard;
            this.runDiscardResultsToolStripMenuItem.Name = "runDiscardResultsToolStripMenuItem";
            this.runDiscardResultsToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.F5)));
            this.runDiscardResultsToolStripMenuItem.Size = new System.Drawing.Size(227, 22);
            this.runDiscardResultsToolStripMenuItem.Text = "Run (Discard Results)";
            this.runDiscardResultsToolStripMenuItem.Click += new System.EventHandler(this.RunDsicardResultsToolStripMenuItemClick);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(224, 6);
            // 
            // clearCacheToolStripMenuItem
            // 
            this.clearCacheToolStripMenuItem.Name = "clearCacheToolStripMenuItem";
            this.clearCacheToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F2;
            this.clearCacheToolStripMenuItem.Size = new System.Drawing.Size(227, 22);
            this.clearCacheToolStripMenuItem.Text = "Clear Cache";
            this.clearCacheToolStripMenuItem.Click += new System.EventHandler(this.ClearCacheToolStripMenuItemClick);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // toolStripCmdModels
            // 
            this.toolStripCmdModels.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripCmdModels.Image = global::DaxStudio.Properties.Resources.DataSource;
            this.toolStripCmdModels.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripCmdModels.Name = "toolStripCmdModels";
            this.toolStripCmdModels.Size = new System.Drawing.Size(23, 22);
            this.toolStripCmdModels.Text = "Models";
            this.toolStripCmdModels.ToolTipText = "Change Connection";
            this.toolStripCmdModels.Click += new System.EventHandler(this.ToolStripCmdModelsClick);
            // 
            // cboDatabase
            // 
            this.cboDatabase.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboDatabase.Name = "cboDatabase";
            this.cboDatabase.Size = new System.Drawing.Size(180, 25);
            this.cboDatabase.ToolTipText = "Database Name";
            this.cboDatabase.SelectedIndexChanged += new System.EventHandler(this.CboDatabasesSelectionChangeCommitted);
            // 
            // tspExportMetadata
            // 
            this.tspExportMetadata.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tspExportMetadata.Image = ((System.Drawing.Image)(resources.GetObject("tspExportMetadata.Image")));
            this.tspExportMetadata.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tspExportMetadata.Name = "tspExportMetadata";
            this.tspExportMetadata.Size = new System.Drawing.Size(23, 22);
            this.tspExportMetadata.Text = "Export Metadata";
            this.tspExportMetadata.Visible = false;
            this.tspExportMetadata.Click += new System.EventHandler(this.TspExportMetadataClick);
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.Name = "toolStripLabel1";
            this.toolStripLabel1.Size = new System.Drawing.Size(62, 22);
            this.toolStripLabel1.Text = "Output to:";
            // 
            // tcbOutputTo
            // 
            this.tcbOutputTo.Name = "tcbOutputTo";
            this.tcbOutputTo.Size = new System.Drawing.Size(150, 25);
            this.tcbOutputTo.ToolTipText = "Output Target";
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // btnHelp
            // 
            this.btnHelp.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnHelp.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.documentationToolStripMenuItem,
            this.mSDNForumsToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.btnHelp.Image = global::DaxStudio.Properties.Resources.question_button;
            this.btnHelp.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnHelp.Name = "btnHelp";
            this.btnHelp.Size = new System.Drawing.Size(29, 22);
            this.btnHelp.Text = "toolStripButton1";
            this.btnHelp.ToolTipText = "Help";
            // 
            // documentationToolStripMenuItem
            // 
            this.documentationToolStripMenuItem.Name = "documentationToolStripMenuItem";
            this.documentationToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F1;
            this.documentationToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.documentationToolStripMenuItem.Text = "Documentation";
            this.documentationToolStripMenuItem.Click += new System.EventHandler(this.DocumentationToolStripMenuItemClick);
            // 
            // mSDNForumsToolStripMenuItem
            // 
            this.mSDNForumsToolStripMenuItem.Name = "mSDNForumsToolStripMenuItem";
            this.mSDNForumsToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.F1)));
            this.mSDNForumsToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.mSDNForumsToolStripMenuItem.Text = "MSDN Forums";
            this.mSDNForumsToolStripMenuItem.Click += new System.EventHandler(this.MsdnForumsToolStripMenuItemClick);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.aboutToolStripMenuItem.Text = "About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.AboutToolStripMenuItemClick);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.pnlModel);
            this.splitContainer1.Panel1.Controls.Add(this.tabMetadataBrowser);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(777, 400);
            this.splitContainer1.SplitterDistance = 257;
            this.splitContainer1.TabIndex = 2;
            // 
            // pnlModel
            // 
            this.pnlModel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlModel.Controls.Add(this.cboModel);
            this.pnlModel.Location = new System.Drawing.Point(4, 4);
            this.pnlModel.Name = "pnlModel";
            this.pnlModel.Size = new System.Drawing.Size(246, 23);
            this.pnlModel.TabIndex = 1;
            // 
            // cboModel
            // 
            this.cboModel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cboModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboModel.FormattingEnabled = true;
            this.cboModel.Location = new System.Drawing.Point(0, 0);
            this.cboModel.Name = "cboModel";
            this.cboModel.Size = new System.Drawing.Size(246, 21);
            this.cboModel.TabIndex = 0;
            this.cboModel.SelectionChangeCommitted += new System.EventHandler(this.CboModelSelectionChangeCommitted);
            // 
            // tabMetadataBrowser
            // 
            this.tabMetadataBrowser.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabMetadataBrowser.Controls.Add(this.tabMetadata);
            this.tabMetadataBrowser.Controls.Add(this.tabFunctions);
            this.tabMetadataBrowser.Controls.Add(this.tabDMV);
            this.tabMetadataBrowser.Location = new System.Drawing.Point(0, 31);
            this.tabMetadataBrowser.Name = "tabMetadataBrowser";
            this.tabMetadataBrowser.SelectedIndex = 0;
            this.tabMetadataBrowser.Size = new System.Drawing.Size(257, 369);
            this.tabMetadataBrowser.TabIndex = 0;
            // 
            // tabMetadata
            // 
            this.tabMetadata.Controls.Add(this.tvwMetadata);
            this.tabMetadata.Location = new System.Drawing.Point(4, 22);
            this.tabMetadata.Name = "tabMetadata";
            this.tabMetadata.Padding = new System.Windows.Forms.Padding(3);
            this.tabMetadata.Size = new System.Drawing.Size(249, 343);
            this.tabMetadata.TabIndex = 0;
            this.tabMetadata.Text = "Metadata";
            this.tabMetadata.UseVisualStyleBackColor = true;
            // 
            // tvwMetadata
            // 
            this.tvwMetadata.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tvwMetadata.ImageIndex = 5;
            this.tvwMetadata.ImageList = this.imgListTree;
            this.tvwMetadata.ItemHeight = 18;
            this.tvwMetadata.Location = new System.Drawing.Point(3, 3);
            this.tvwMetadata.Name = "tvwMetadata";
            this.tvwMetadata.SelectedImageIndex = 0;
            this.tvwMetadata.ShowNodeToolTips = true;
            this.tvwMetadata.Size = new System.Drawing.Size(243, 337);
            this.tvwMetadata.TabIndex = 0;
            this.tvwMetadata.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.TvwItemDrag);
            // 
            // imgListTree
            // 
            this.imgListTree.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imgListTree.ImageStream")));
            this.imgListTree.TransparentColor = System.Drawing.Color.Transparent;
            this.imgListTree.Images.SetKeyName(0, "Table.png");
            this.imgListTree.Images.SetKeyName(1, "HiddenTable.png");
            this.imgListTree.Images.SetKeyName(2, "Column2.png");
            this.imgListTree.Images.SetKeyName(3, "HiddenColumn2.png");
            this.imgListTree.Images.SetKeyName(4, "Measure.png");
            this.imgListTree.Images.SetKeyName(5, "HiddenMeasure.png");
            this.imgListTree.Images.SetKeyName(6, "Folder");
            this.imgListTree.Images.SetKeyName(7, "Function.png");
            this.imgListTree.Images.SetKeyName(8, "DmvTable.png");
            this.imgListTree.Images.SetKeyName(9, "Column.png");
            this.imgListTree.Images.SetKeyName(10, "HiddenColumn.png");
            // 
            // tabFunctions
            // 
            this.tabFunctions.Controls.Add(this.tvwFunctions);
            this.tabFunctions.Location = new System.Drawing.Point(4, 22);
            this.tabFunctions.Name = "tabFunctions";
            this.tabFunctions.Padding = new System.Windows.Forms.Padding(3);
            this.tabFunctions.Size = new System.Drawing.Size(249, 343);
            this.tabFunctions.TabIndex = 1;
            this.tabFunctions.Text = "Functions";
            this.tabFunctions.UseVisualStyleBackColor = true;
            // 
            // tvwFunctions
            // 
            this.tvwFunctions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tvwFunctions.ImageIndex = 0;
            this.tvwFunctions.ImageList = this.imgListTree;
            this.tvwFunctions.ItemHeight = 18;
            this.tvwFunctions.Location = new System.Drawing.Point(3, 3);
            this.tvwFunctions.Name = "tvwFunctions";
            this.tvwFunctions.SelectedImageIndex = 0;
            this.tvwFunctions.Size = new System.Drawing.Size(243, 337);
            this.tvwFunctions.TabIndex = 0;
            this.tvwFunctions.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.TvwItemDrag);
            // 
            // tabDMV
            // 
            this.tabDMV.Controls.Add(this.listDMV);
            this.tabDMV.Location = new System.Drawing.Point(4, 22);
            this.tabDMV.Name = "tabDMV";
            this.tabDMV.Padding = new System.Windows.Forms.Padding(3);
            this.tabDMV.Size = new System.Drawing.Size(249, 343);
            this.tabDMV.TabIndex = 2;
            this.tabDMV.Text = "DMV";
            this.tabDMV.UseVisualStyleBackColor = true;
            // 
            // listDMV
            // 
            this.listDMV.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colDmv});
            this.listDMV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listDMV.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listDMV.Location = new System.Drawing.Point(3, 3);
            this.listDMV.MultiSelect = false;
            this.listDMV.Name = "listDMV";
            this.listDMV.Size = new System.Drawing.Size(243, 337);
            this.listDMV.SmallImageList = this.imgListTree;
            this.listDMV.TabIndex = 0;
            this.listDMV.UseCompatibleStateImageBehavior = false;
            this.listDMV.View = System.Windows.Forms.View.SmallIcon;
            this.listDMV.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.ListDmvItemDrag);
            // 
            // colDmv
            // 
            this.colDmv.Text = "DMV";
            this.colDmv.Width = 239;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.elementHost1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.rtbOutput);
            this.splitContainer2.Size = new System.Drawing.Size(516, 400);
            this.splitContainer2.SplitterDistance = 308;
            this.splitContainer2.TabIndex = 0;
            // 
            // elementHost1
            // 
            this.elementHost1.AllowDrop = true;
            this.elementHost1.AutoSize = true;
            this.elementHost1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.elementHost1.Location = new System.Drawing.Point(0, 0);
            this.elementHost1.Name = "elementHost1";
            this.elementHost1.Size = new System.Drawing.Size(516, 308);
            this.elementHost1.TabIndex = 0;
            this.elementHost1.Text = "elementHost1";
            this.elementHost1.Child = this.ucDaxEditor;
            // 
            // rtbOutput
            // 
            this.rtbOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbOutput.Location = new System.Drawing.Point(0, 0);
            this.rtbOutput.Name = "rtbOutput";
            this.rtbOutput.Size = new System.Drawing.Size(516, 88);
            this.rtbOutput.TabIndex = 0;
            this.rtbOutput.Text = "";
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            this.openFileDialog1.Filter = "Dax Files|*.dax|Text Files|*.txt|All files|*.*";
            this.openFileDialog1.Title = "Open DAX file";
            // 
            // saveFileDialog1
            // 
            this.saveFileDialog1.Filter = "Dax Files|*.dax|Text Files|*.txt|All files|*.*";
            // 
            // DaxStudioForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(777, 449);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.stsDax);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Name = "DaxStudioForm";
            this.Text = "DAX Studio";
            this.Activated += new System.EventHandler(this.DaxStudioFormActivated);
            this.Load += new System.EventHandler(this.DaxStudioFormLoad);
            this.Shown += new System.EventHandler(this.DaxStudioFormShown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.DaxStudioFormKeyUp);
            this.stsDax.ResumeLayout(false);
            this.stsDax.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.pnlModel.ResumeLayout(false);
            this.tabMetadataBrowser.ResumeLayout(false);
            this.tabMetadata.ResumeLayout(false);
            this.tabFunctions.ResumeLayout(false);
            this.tabDMV.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel1.PerformLayout();
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.StatusStrip stsDax;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TabControl tabMetadataBrowser;
        private System.Windows.Forms.TabPage tabMetadata;
        private System.Windows.Forms.TabPage tabFunctions;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.Integration.ElementHost elementHost1;
        private System.Windows.Forms.RichTextBox rtbOutput;
        //private DaxEditorUserControl userControl11;
        private DaxEditorUserControl ucDaxEditor;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton tspExportMetadata;
        private System.Windows.Forms.TreeView tvwMetadata;
        private System.Windows.Forms.TreeView tvwFunctions;
        private System.Windows.Forms.ToolStripSplitButton btnRun;
        private System.Windows.Forms.ToolStripMenuItem runQueryTableToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runStaticResultsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runDiscardResultsToolStripMenuItem;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripComboBox tcbOutputTo;
        private System.Windows.Forms.ImageList imgListTree;
        private System.Windows.Forms.ToolStripButton toolStripCmdModels;
        private System.Windows.Forms.ToolStripStatusLabel tspStatus;
        private System.Windows.Forms.Panel pnlModel;
        private System.Windows.Forms.ComboBox cboModel;
        private System.Windows.Forms.ToolStripStatusLabel tspConnection;
        private System.Windows.Forms.TabPage tabDMV;
        private System.Windows.Forms.ListView listDMV;
        private System.Windows.Forms.ColumnHeader colDmv;
        private System.Windows.Forms.ToolStripStatusLabel tspVersion;
        private System.Windows.Forms.ToolStripStatusLabel tspSpid;
        private System.Windows.Forms.ToolStripComboBox cboDatabase;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem clearCacheToolStripMenuItem;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButton1;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveAsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.ToolStripDropDownButton btnHelp;
        private System.Windows.Forms.ToolStripMenuItem documentationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem mSDNForumsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;

    }
}
