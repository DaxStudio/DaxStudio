namespace DaxStudio
{
    partial class ConnectionDialog
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
            this.radPowerPivot = new System.Windows.Forms.RadioButton();
            this.lblWorkbook = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.txtEffectiveUserName = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.txtAdditionalOptions = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.txtRoles = new System.Windows.Forms.TextBox();
            this.cboMdxCompat = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.cboDirectQuery = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.radServer = new System.Windows.Forms.RadioButton();
            this.cboServers = new System.Windows.Forms.ComboBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnConnect = new System.Windows.Forms.Button();
            this.lblPowerPivotUnavailable = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // radPowerPivot
            // 
            this.radPowerPivot.AutoSize = true;
            this.radPowerPivot.Location = new System.Drawing.Point(13, 13);
            this.radPowerPivot.Name = "radPowerPivot";
            this.radPowerPivot.Size = new System.Drawing.Size(114, 17);
            this.radPowerPivot.TabIndex = 0;
            this.radPowerPivot.Text = "PowerPivot Model:";
            this.radPowerPivot.UseVisualStyleBackColor = true;
            // 
            // lblWorkbook
            // 
            this.lblWorkbook.AutoSize = true;
            this.lblWorkbook.Location = new System.Drawing.Point(130, 15);
            this.lblWorkbook.Name = "lblWorkbook";
            this.lblWorkbook.Size = new System.Drawing.Size(35, 13);
            this.lblWorkbook.TabIndex = 2;
            this.lblWorkbook.Text = "label1";
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.txtEffectiveUserName);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.txtAdditionalOptions);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.txtRoles);
            this.groupBox1.Controls.Add(this.cboMdxCompat);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.cboDirectQuery);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Location = new System.Drawing.Point(12, 95);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(353, 247);
            this.groupBox1.TabIndex = 5;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Server Options";
            // 
            // txtEffectiveUserName
            // 
            this.txtEffectiveUserName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtEffectiveUserName.Location = new System.Drawing.Point(118, 80);
            this.txtEffectiveUserName.Name = "txtEffectiveUserName";
            this.txtEffectiveUserName.Size = new System.Drawing.Size(226, 20);
            this.txtEffectiveUserName.TabIndex = 17;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 138);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(95, 13);
            this.label1.TabIndex = 16;
            this.label1.Text = "Additional Options:";
            // 
            // txtAdditionalOptions
            // 
            this.txtAdditionalOptions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtAdditionalOptions.Location = new System.Drawing.Point(121, 135);
            this.txtAdditionalOptions.Multiline = true;
            this.txtAdditionalOptions.Name = "txtAdditionalOptions";
            this.txtAdditionalOptions.Size = new System.Drawing.Size(223, 106);
            this.txtAdditionalOptions.TabIndex = 15;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 111);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(37, 13);
            this.label5.TabIndex = 14;
            this.label5.Text = "Roles:";
            // 
            // txtRoles
            // 
            this.txtRoles.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtRoles.Location = new System.Drawing.Point(118, 108);
            this.txtRoles.Name = "txtRoles";
            this.txtRoles.Size = new System.Drawing.Size(226, 20);
            this.txtRoles.TabIndex = 13;
            // 
            // cboMdxCompat
            // 
            this.cboMdxCompat.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cboMdxCompat.FormattingEnabled = true;
            this.cboMdxCompat.Items.AddRange(new object[] {
            "0 - Equivalent to 1",
            "1 - Placeholder members are exposed",
            "2 - Placeholder members are not exposed",
            "3- (Default) Placeholder members are not exposed"});
            this.cboMdxCompat.Location = new System.Drawing.Point(118, 52);
            this.cboMdxCompat.Name = "cboMdxCompat";
            this.cboMdxCompat.Size = new System.Drawing.Size(226, 21);
            this.cboMdxCompat.TabIndex = 11;
            this.cboMdxCompat.Text = "3- (Default) Placeholder members are not exposed";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 83);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(108, 13);
            this.label4.TabIndex = 10;
            this.label4.Text = "Effective User Name:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 55);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(99, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "MDX Compatability:";
            // 
            // cboDirectQuery
            // 
            this.cboDirectQuery.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cboDirectQuery.FormattingEnabled = true;
            this.cboDirectQuery.Items.AddRange(new object[] {
            "Default",
            "DirectQuery",
            "In-Memory"});
            this.cboDirectQuery.Location = new System.Drawing.Point(118, 24);
            this.cboDirectQuery.Name = "cboDirectQuery";
            this.cboDirectQuery.Size = new System.Drawing.Size(226, 21);
            this.cboDirectQuery.TabIndex = 8;
            this.cboDirectQuery.Text = "Default";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(4, 27);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(99, 13);
            this.label2.TabIndex = 7;
            this.label2.Text = "Direct Query Mode:";
            // 
            // radServer
            // 
            this.radServer.AutoSize = true;
            this.radServer.Location = new System.Drawing.Point(13, 59);
            this.radServer.Name = "radServer";
            this.radServer.Size = new System.Drawing.Size(95, 17);
            this.radServer.TabIndex = 7;
            this.radServer.Text = "Tabular Server";
            this.radServer.UseVisualStyleBackColor = true;
            // 
            // cboServers
            // 
            this.cboServers.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cboServers.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.cboServers.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.cboServers.FormattingEnabled = true;
            this.cboServers.Location = new System.Drawing.Point(133, 59);
            this.cboServers.Name = "cboServers";
            this.cboServers.Size = new System.Drawing.Size(232, 21);
            this.cboServers.TabIndex = 0;
            this.cboServers.SelectedIndexChanged += new System.EventHandler(this.CboServersSelectedIndexChanged);
            this.cboServers.TextUpdate += new System.EventHandler(this.CboServersTextUpdate);
            this.cboServers.Enter += new System.EventHandler(this.CboServersEnter);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(290, 352);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.BtnCancelClick);
            // 
            // btnConnect
            // 
            this.btnConnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnConnect.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnConnect.Location = new System.Drawing.Point(209, 352);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(75, 23);
            this.btnConnect.TabIndex = 10;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.BtnConnectClick);
            // 
            // lblPowerPivotUnavailable
            // 
            this.lblPowerPivotUnavailable.AutoSize = true;
            this.lblPowerPivotUnavailable.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.lblPowerPivotUnavailable.Location = new System.Drawing.Point(29, 36);
            this.lblPowerPivotUnavailable.Name = "lblPowerPivotUnavailable";
            this.lblPowerPivotUnavailable.Size = new System.Drawing.Size(287, 13);
            this.lblPowerPivotUnavailable.TabIndex = 11;
            this.lblPowerPivotUnavailable.Text = "The active workbook does not contain a PowerPivot model";
            this.lblPowerPivotUnavailable.Visible = false;
            // 
            // ConnectionDialog
            // 
            this.AcceptButton = this.btnConnect;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(377, 387);
            this.Controls.Add(this.lblPowerPivotUnavailable);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.radServer);
            this.Controls.Add(this.cboServers);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.lblWorkbook);
            this.Controls.Add(this.radPowerPivot);
            this.Name = "ConnectionDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Connect To";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ConnectionDialogClosing);
            this.Load += new System.EventHandler(this.ConnectionDialogLoad);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RadioButton radPowerPivot;
        private System.Windows.Forms.Label lblWorkbook;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox txtRoles;
        private System.Windows.Forms.ComboBox cboMdxCompat;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cboDirectQuery;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.RadioButton radServer;
        private System.Windows.Forms.ComboBox cboServers;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.TextBox txtAdditionalOptions;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtEffectiveUserName;
        private System.Windows.Forms.Label lblPowerPivotUnavailable;
    }
}