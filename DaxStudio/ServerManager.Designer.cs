namespace DaxStudio
{
    partial class ServerManager
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ServerManager));
            this.lblServer = new System.Windows.Forms.Label();
            this.cmboModels = new System.Windows.Forms.ComboBox();
            this.lblModels = new System.Windows.Forms.Label();
            this.cmdAddModel = new System.Windows.Forms.Button();
            this.cmdClose = new System.Windows.Forms.Button();
            this.cmboServer = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // lblServer
            // 
            this.lblServer.AutoSize = true;
            this.lblServer.Location = new System.Drawing.Point(13, 19);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(38, 13);
            this.lblServer.TabIndex = 1;
            this.lblServer.Text = "Server";
            // 
            // cmboModels
            // 
            this.cmboModels.FormattingEnabled = true;
            this.cmboModels.Location = new System.Drawing.Point(69, 40);
            this.cmboModels.Name = "cmboModels";
            this.cmboModels.Size = new System.Drawing.Size(203, 21);
            this.cmboModels.TabIndex = 1;
            this.cmboModels.SelectedIndexChanged += new System.EventHandler(this.cmboModels_SelectedIndexChanged);
            // 
            // lblModels
            // 
            this.lblModels.AutoSize = true;
            this.lblModels.Location = new System.Drawing.Point(13, 48);
            this.lblModels.Name = "lblModels";
            this.lblModels.Size = new System.Drawing.Size(41, 13);
            this.lblModels.TabIndex = 3;
            this.lblModels.Text = "Models";
            // 
            // cmdAddModel
            // 
            this.cmdAddModel.Location = new System.Drawing.Point(116, 67);
            this.cmdAddModel.Name = "cmdAddModel";
            this.cmdAddModel.Size = new System.Drawing.Size(75, 23);
            this.cmdAddModel.TabIndex = 2;
            this.cmdAddModel.Text = "Add Model";
            this.cmdAddModel.UseVisualStyleBackColor = true;
            this.cmdAddModel.Click += new System.EventHandler(this.cmdAddModel_Click);
            // 
            // cmdClose
            // 
            this.cmdClose.Location = new System.Drawing.Point(197, 67);
            this.cmdClose.Name = "cmdClose";
            this.cmdClose.Size = new System.Drawing.Size(75, 23);
            this.cmdClose.TabIndex = 3;
            this.cmdClose.Text = "Return";
            this.cmdClose.UseVisualStyleBackColor = true;
            this.cmdClose.Click += new System.EventHandler(this.cmdClose_Click);
            // 
            // cmboServer
            // 
            this.cmboServer.FormattingEnabled = true;
            this.cmboServer.Location = new System.Drawing.Point(70, 16);
            this.cmboServer.Name = "cmboServer";
            this.cmboServer.Size = new System.Drawing.Size(202, 21);
            this.cmboServer.TabIndex = 0;
            this.cmboServer.SelectedIndexChanged += new System.EventHandler(this.cmboServer_SelectedIndexChanged);
            this.cmboServer.TextUpdate += new System.EventHandler(this.cmboServer_TextUpdate);
            this.cmboServer.Leave += new System.EventHandler(this.cmboServer_Leave);
            // 
            // ServerManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 100);
            this.Controls.Add(this.cmboServer);
            this.Controls.Add(this.cmdClose);
            this.Controls.Add(this.cmdAddModel);
            this.Controls.Add(this.lblModels);
            this.Controls.Add(this.cmboModels);
            this.Controls.Add(this.lblServer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ServerManager";
            this.ShowInTaskbar = false;
            this.Text = "Tabular Models";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.ComboBox cmboModels;
        private System.Windows.Forms.Label lblModels;
        private System.Windows.Forms.Button cmdAddModel;
        private System.Windows.Forms.Button cmdClose;
        private System.Windows.Forms.ComboBox cmboServer;
    }
}