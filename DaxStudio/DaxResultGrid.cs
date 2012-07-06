using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DaxStudio
{
    public partial class DaxResultGrid : Form
    {

        private int WidthBuffer = 40;
        private int HeightBuffer = 60;

        public DaxResultGrid()
        {
            InitializeComponent();
        }


        public void BindResults(System.Data.DataTable ResultTable)
        {
            daxGrid.DataSource = ResultTable;
            daxGrid.Refresh();

            if (daxGrid.Width < this.Width - WidthBuffer )
                this.Width = daxGrid.Width + WidthBuffer;

        }

        //private void DaxResultGrid_ResizeEnd(object sender, EventArgs e)
        //{
        //    daxGrid.Width = this.Width - WidthBuffer;
        //    daxGrid.Height = this.Height - 60;


        //}





    }
}
