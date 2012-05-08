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
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripSplitButton1 = new System.Windows.Forms.ToolStripSplitButton();
            this.runQueryTableToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runStaticResultsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runDsicardResultsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.tspExportMetadata = new System.Windows.Forms.ToolStripButton();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.tcbOutputTo = new System.Windows.Forms.ToolStripComboBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabMetadata = new System.Windows.Forms.TabPage();
            this.tvwMetadata = new System.Windows.Forms.TreeView();
            this.imgListTree = new System.Windows.Forms.ImageList(this.components);
            this.tabFunctions = new System.Windows.Forms.TabPage();
            this.tvwFunctions = new System.Windows.Forms.TreeView();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.rtbOutput = new System.Windows.Forms.RichTextBox();
            this.elementHost1 = new System.Windows.Forms.Integration.ElementHost();
            this.userControl12 = new DaxStudio.UserControl1();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabMetadata.SuspendLayout();
            this.tabFunctions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.SuspendLayout();
            // 
            // stsDax
            // 
            this.stsDax.Location = new System.Drawing.Point(0, 427);
            this.stsDax.Name = "stsDax";
            this.stsDax.Size = new System.Drawing.Size(777, 22);
            this.stsDax.TabIndex = 0;
            this.stsDax.Text = "statusStrip1";
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripSplitButton1,
            this.toolStripSeparator1,
            this.tspExportMetadata,
            this.toolStripLabel1,
            this.tcbOutputTo});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(777, 25);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripSplitButton1
            // 
            this.toolStripSplitButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripSplitButton1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.runQueryTableToolStripMenuItem,
            this.runStaticResultsToolStripMenuItem,
            this.runDsicardResultsToolStripMenuItem});
            this.toolStripSplitButton1.Image = global::DaxStudio.Properties.Resources.play;
            this.toolStripSplitButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripSplitButton1.Name = "toolStripSplitButton1";
            this.toolStripSplitButton1.Size = new System.Drawing.Size(32, 22);
            this.toolStripSplitButton1.Text = "toolStripSplitButton1";
            // 
            // runQueryTableToolStripMenuItem
            // 
            this.runQueryTableToolStripMenuItem.Name = "runQueryTableToolStripMenuItem";
            this.runQueryTableToolStripMenuItem.Size = new System.Drawing.Size(185, 22);
            this.runQueryTableToolStripMenuItem.Text = "Run (Query Table)";
            this.runQueryTableToolStripMenuItem.Click += new System.EventHandler(this.runQueryTableToolStripMenuItem_Click);
            // 
            // runStaticResultsToolStripMenuItem
            // 
            this.runStaticResultsToolStripMenuItem.Name = "runStaticResultsToolStripMenuItem";
            this.runStaticResultsToolStripMenuItem.Size = new System.Drawing.Size(185, 22);
            this.runStaticResultsToolStripMenuItem.Text = "Run (Static Results)";
            this.runStaticResultsToolStripMenuItem.Click += new System.EventHandler(this.runStaticResultsToolStripMenuItem_Click);
            // 
            // runDsicardResultsToolStripMenuItem
            // 
            this.runDsicardResultsToolStripMenuItem.Name = "runDsicardResultsToolStripMenuItem";
            this.runDsicardResultsToolStripMenuItem.Size = new System.Drawing.Size(185, 22);
            this.runDsicardResultsToolStripMenuItem.Text = "Run (Dsicard Results)";
            this.runDsicardResultsToolStripMenuItem.Click += new System.EventHandler(this.runDsicardResultsToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // tspExportMetadata
            // 
            this.tspExportMetadata.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tspExportMetadata.Image = ((System.Drawing.Image)(resources.GetObject("tspExportMetadata.Image")));
            this.tspExportMetadata.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tspExportMetadata.Name = "tspExportMetadata";
            this.tspExportMetadata.Size = new System.Drawing.Size(23, 22);
            this.tspExportMetadata.Text = "Export Metadata";
            this.tspExportMetadata.Click += new System.EventHandler(this.tspExportMetadata_Click);
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
            this.tcbOutputTo.Size = new System.Drawing.Size(121, 25);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tabControl1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(777, 402);
            this.splitContainer1.SplitterDistance = 257;
            this.splitContainer1.TabIndex = 2;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabMetadata);
            this.tabControl1.Controls.Add(this.tabFunctions);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(257, 402);
            this.tabControl1.TabIndex = 0;
            // 
            // tabMetadata
            // 
            this.tabMetadata.Controls.Add(this.tvwMetadata);
            this.tabMetadata.Location = new System.Drawing.Point(4, 22);
            this.tabMetadata.Name = "tabMetadata";
            this.tabMetadata.Padding = new System.Windows.Forms.Padding(3);
            this.tabMetadata.Size = new System.Drawing.Size(249, 376);
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
            this.tvwMetadata.Size = new System.Drawing.Size(243, 370);
            this.tvwMetadata.TabIndex = 0;
            this.tvwMetadata.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.tvw_ItemDrag);
            // 
            // imgListTree
            // 
            this.imgListTree.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imgListTree.ImageStream")));
            this.imgListTree.TransparentColor = System.Drawing.Color.Transparent;
            this.imgListTree.Images.SetKeyName(0, "Table.png");
            this.imgListTree.Images.SetKeyName(1, "Column.png");
            this.imgListTree.Images.SetKeyName(2, "HiddenColumn.png");
            this.imgListTree.Images.SetKeyName(3, "Measure.png");
            this.imgListTree.Images.SetKeyName(4, "HiddenMeasure.png");
            this.imgListTree.Images.SetKeyName(5, "Folder");
            this.imgListTree.Images.SetKeyName(6, "Function.png");
            // 
            // tabFunctions
            // 
            this.tabFunctions.Controls.Add(this.tvwFunctions);
            this.tabFunctions.Location = new System.Drawing.Point(4, 22);
            this.tabFunctions.Name = "tabFunctions";
            this.tabFunctions.Padding = new System.Windows.Forms.Padding(3);
            this.tabFunctions.Size = new System.Drawing.Size(249, 376);
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
            this.tvwFunctions.Size = new System.Drawing.Size(243, 370);
            this.tvwFunctions.TabIndex = 0;
            this.tvwFunctions.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.tvw_ItemDrag);
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
            this.splitContainer2.Size = new System.Drawing.Size(516, 402);
            this.splitContainer2.SplitterDistance = 310;
            this.splitContainer2.TabIndex = 0;
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
            // elementHost1
            // 
            this.elementHost1.AllowDrop = true;
            this.elementHost1.AutoSize = true;
            this.elementHost1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.elementHost1.Location = new System.Drawing.Point(0, 0);
            this.elementHost1.Name = "elementHost1";
            this.elementHost1.Size = new System.Drawing.Size(516, 310);
            this.elementHost1.TabIndex = 0;
            this.elementHost1.Text = "elementHost1";
            this.elementHost1.ChildChanged += new System.EventHandler<System.Windows.Forms.Integration.ChildChangedEventArgs>(this.elementHost1_ChildChanged);
            this.elementHost1.Child = this.userControl12;
            // 
            // DaxStudioForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(777, 449);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.stsDax);
            this.KeyPreview = true;
            this.Name = "DaxStudioForm";
            this.Text = "DAX Studio";
            this.Load += new System.EventHandler(this.DaxStudioForm_Load);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.DaxStudioForm_KeyUp);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabMetadata.ResumeLayout(false);
            this.tabFunctions.ResumeLayout(false);
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
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabMetadata;
        private System.Windows.Forms.TabPage tabFunctions;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.Integration.ElementHost elementHost1;
        private System.Windows.Forms.RichTextBox rtbOutput;
        //private UserControl1 userControl11;
        private UserControl1 userControl12;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton tspExportMetadata;
        private System.Windows.Forms.TreeView tvwMetadata;
        private System.Windows.Forms.TreeView tvwFunctions;
        private System.Windows.Forms.ToolStripSplitButton toolStripSplitButton1;
        private System.Windows.Forms.ToolStripMenuItem runQueryTableToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runStaticResultsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runDsicardResultsToolStripMenuItem;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripComboBox tcbOutputTo;
        private System.Windows.Forms.ImageList imgListTree;

    }
}