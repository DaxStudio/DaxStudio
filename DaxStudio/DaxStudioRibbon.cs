using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Office.Tools.Ribbon;
using Excel = Microsoft.Office.Interop.Excel;

namespace DaxStudio
{
    public partial class DaxStudioRibbon
    {
        private void Ribbon1_Load(object sender, RibbonUIEventArgs e)
        {

        }

        private void btnDax_Click(object sender, RibbonControlEventArgs e)
        {
            Excel.Application appl = (Excel.Application)Globals.ThisAddIn.Application;
            //Excel.Workbook wb = (Excel.Workbook)appl.ActiveWorkbook;
            DaxStudioForm ds = new DaxStudioForm();
            Excel.Application app = appl;// (Excel.Application)this.Context;
            ds.Application = app;
            ds.Show();
        }
    }
}
