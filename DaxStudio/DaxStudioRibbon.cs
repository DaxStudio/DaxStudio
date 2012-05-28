using Microsoft.Office.Tools.Ribbon;

namespace DaxStudio
{
    public partial class DaxStudioRibbon
    {
        private void Ribbon1Load(object sender, RibbonUIEventArgs e)
        {

        }

        private void BtnDaxClick(object sender, RibbonControlEventArgs e)
        {
            var appl = Globals.ThisAddIn.Application;
            //Excel.Workbook wb = (Excel.Workbook)appl.ActiveWorkbook;
            var ds = new DaxStudioForm();
            var app = appl;// (Excel.Application)this.Context;
            ds.Application = app;
            ds.Show();
        }
    }
}
