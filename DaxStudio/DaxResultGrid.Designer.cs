namespace DaxStudio
{
    partial class DaxResultGrid
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DaxResultGrid));
            this.daxGrid = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.daxGrid)).BeginInit();
            this.SuspendLayout();
            // 
            // daxGrid
            // 
            this.daxGrid.AllowUserToAddRows = false;
            this.daxGrid.AllowUserToDeleteRows = false;
            this.daxGrid.AllowUserToOrderColumns = true;
            this.daxGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.daxGrid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.daxGrid.Location = new System.Drawing.Point(13, 13);
            this.daxGrid.Name = "daxGrid";
            this.daxGrid.Size = new System.Drawing.Size(390, 260);
            this.daxGrid.TabIndex = 0;
            // 
            // DaxResultGrid
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(430, 281);
            this.Controls.Add(this.daxGrid);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "DaxResultGrid";
            this.Text = "Query Results";

            this.ResizeEnd += new System.EventHandler(this.DaxResultGrid_ResizeEnd);
            ((System.ComponentModel.ISupportInitialize)(this.daxGrid)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView daxGrid;
    }
}