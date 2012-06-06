using Microsoft.Office.Tools.Ribbon;

namespace DaxStudio
{
    public partial class DaxStudioRibbon
    {
        private void Ribbon1Load(object sender, RibbonUIEventArgs e)
        {

        }

        private DaxStudioForm _ds;
        private void BtnDaxClick(object sender, RibbonControlEventArgs e)
        {
            if (_ds == null || _ds.IsDisposed)
            {
                _ds = new DaxStudioForm();
                _ds.Application = Globals.ThisAddIn.Application;
            }
            if (!_ds.Visible)
                _ds.Show();
            else
                _ds.Activate();
        }
    }
}
