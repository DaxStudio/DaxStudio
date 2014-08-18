using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using DaxStudio.Interfaces;
using System.Diagnostics;
using System.Web.Http.ModelBinding;
using DaxStudio.Xmla;

namespace DaxStudio.ExcelAddin.Xmla
{
    [RoutePrefix("workbook")]
    public class WorkbookController : ApiController
    {
        [HttpGet]
        [Route("FileName")]
        public IHttpActionResult GetWorkbookFileName()
        { 
            
            var xl = new ExcelHelper(Globals.ThisAddIn.Application);
            var addin = Globals.ThisAddIn;
            var app = addin.Application;
            var wb = app.ActiveWorkbook;
            
            return Ok(wb.FullName);
        }
        
        /*
        [HttpGet]
        //[ActionName("FileName")]
        [Route("DataModelConnectionString")]
        public async Task<IHttpActionResult> GetDataModelConnectionString()
        {
            //TODO - 
            var xl = new ExcelHelper(Globals.ThisAddIn.Application);
            xl.GetPowerPivotConnection
            var addin = Globals.ThisAddIn;
            var app = addin.Application;
            var wb = app.ActiveWorkbook;
            return Ok(wb.FullName);
        }
        */

        [HttpGet]
        [ActionName("worksheets")]
        [Route("Worksheets")]
        public IHttpActionResult GetWorksheets()
        {
            var xl = new ExcelHelper(Globals.ThisAddIn.Application);
            var addin = Globals.ThisAddIn;
            var app = addin.Application;
            var wb = app.ActiveWorkbook;
            var shts = new List<string>();

            shts.Add(WorksheetDaxResults);
            shts.Add(WorksheetNew);
            foreach (Worksheet sht in wb.Worksheets)
            {
                shts.Add(sht.Name);
            }

            return Ok(shts.ToArray()); 
        }

        private string WorksheetDaxResults
        {
            get { return "<Query Results Sheet>"; }
        }

        private string WorksheetNew
        {
            get { return "<New Sheet>"; }
        }

        [HttpGet]
        [Route("HasDataModel")]
        public IHttpActionResult GetHasDatamodel()
        {
            var xl = new ExcelHelper(Globals.ThisAddIn.Application);
            if (xl.HasPowerPivotData())
                return Ok(true);
            else
                return Ok(false);
        }

        [HttpPost]
        [Route("StaticQueryResult")]
        public void PostStaticResult(StaticQueryResult results)
        {
            var xl = new ExcelHelper(Globals.ThisAddIn.Application);
            var sht = xl.GetTargetWorksheet(results.TargetSheet);
            xl.CopyDataTableToRange(results.QueryResults, sht);
        }

        [HttpPost]
        [Route("LinkedQueryResult")]
        public void PostLinkedQueryResult(LinkedQueryResult results)
        {
            var xl = new ExcelHelper(Globals.ThisAddIn.Application);
            var sht = xl.GetTargetWorksheet(results.TargetSheet);
            xl.DaxQueryTable( sht,results.DaxQuery);
        }

    }
}
