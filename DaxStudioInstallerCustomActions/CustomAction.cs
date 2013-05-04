using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Deployment.WindowsInstaller;

namespace DaxStudioInstallerCustomActions
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult CustomActionClearExcelDisabledItems(Session session)
        {
            session.Log("Begin CustomActionClearExcelDisabledItems");
            try
            {
                ClearDisabledItems.CheckDisabledItems("daxstudio.vsto");
                session.Log("Completed CustomActionClearExcelDisabledItems");
            }
            catch (Exception ex)
            {
                session.Log("Failed CustomActionClearExcelDisabledItems:" + ex.Message);
                return ActionResult.Failure;
            }
            
            return ActionResult.Success;
        }
    }
}
