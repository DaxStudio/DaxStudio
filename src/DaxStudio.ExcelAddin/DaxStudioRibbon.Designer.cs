namespace DaxStudio.ExcelAddin
{
    partial class DaxStudioRibbon : Microsoft.Office.Tools.Ribbon.RibbonBase
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        public DaxStudioRibbon()
            : base(Globals.Factory.GetRibbonFactory())
        {
            InitializeComponent();
        }

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

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tab1 = this.Factory.CreateRibbonTab();
            this.group1 = this.Factory.CreateRibbonGroup();
            this.btnDax = this.Factory.CreateRibbonButton();
            this.btnTest = this.Factory.CreateRibbonButton();
            this.tab1.SuspendLayout();
            this.group1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tab1
            // 
            this.tab1.ControlId.ControlIdType = Microsoft.Office.Tools.Ribbon.RibbonControlIdType.Office;
            this.tab1.Groups.Add(this.group1);
            this.tab1.Label = "TabAddIns";
            this.tab1.Name = "tab1";
            // 
            // group1
            // 
            this.group1.Items.Add(this.btnDax);
            this.group1.Items.Add(this.btnTest);
            this.group1.Name = "group1";
            // 
            // btnDax
            // 
            this.btnDax.ControlSize = Microsoft.Office.Core.RibbonControlSize.RibbonControlSizeLarge;
            this.btnDax.Image = global::DaxStudio.Properties.Resources.daxstudio_logo_32x32;
            this.btnDax.Label = "DAX Studio";
            this.btnDax.Name = "btnDax";
            this.btnDax.ScreenTip = "Launch DAX Studio";
            this.btnDax.ShowImage = true;
            this.btnDax.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.BtnDaxClick);
            // 
            // btnTest
            // 
            this.btnTest.Label = "";
            this.btnTest.Name = "btnTest";
            // 
            // DaxStudioRibbon
            // 
            this.Name = "DaxStudioRibbon";
            this.RibbonType = "Microsoft.Excel.Workbook";
            this.Tabs.Add(this.tab1);
            this.Close += new System.EventHandler(this.DaxStudioRibbon_Close);
            this.Load += new Microsoft.Office.Tools.Ribbon.RibbonUIEventHandler(this.Ribbon1Load);
            this.tab1.ResumeLayout(false);
            this.tab1.PerformLayout();
            this.group1.ResumeLayout(false);
            this.group1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        internal Microsoft.Office.Tools.Ribbon.RibbonTab tab1;
        internal Microsoft.Office.Tools.Ribbon.RibbonGroup group1;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton btnDax;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton btnTest;
    }

    partial class ThisRibbonCollection
    {
        internal DaxStudioRibbon Ribbon1
        {
            get { return this.GetRibbon<DaxStudioRibbon>(); }
        }
    }
}
