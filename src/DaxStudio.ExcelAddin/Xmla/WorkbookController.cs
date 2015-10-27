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
using DaxStudio.ExcelAddin.Xmla;
using Serilog;

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

            System.Diagnostics.Debug.WriteLine(string.Format("Workbook: {0}", wb.FullName));
            return Ok(wb.FullName);
        }
        
        
        [HttpGet]
        [ActionName("worksheets")]
        [Route("Worksheets")]
        public IHttpActionResult GetWorksheets()
        {
            try
            {
                Log.Debug("{class} {method} {event}", "WorkbookController", "GetWorksheets", "Start");
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
                Log.Debug("{class} {method} {event}", "WorkbookController", "GetWorksheets", "End");
                return Ok(shts.ToArray());
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message}", "WorkbookController", "GetWorksheets", ex.Message);
                return BadRequest(ex.Message);
            }
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
            Log.Debug("{class} {method} {event}", "WorkbookController", "GetHasDataModel", "Start");
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
            Log.Debug("{class} {method} {event}", "WorkbookController", "PostStaticResult", "Start");
            var xl = new ExcelHelper(Globals.ThisAddIn.Application);
            var sht = xl.GetTargetWorksheet(results.TargetSheet);
            xl.CopyDataTableToRange(results.QueryResults, sht);
            Log.Debug("{class} {method} {event}", "WorkbookController", "PostStaticResult", "End");
        }

        [HttpPost]
        [Route("LinkedQueryResult")]
        public IHttpActionResult PostLinkedQueryResult(LinkedQueryResult results)
        {
            try
            {
                Log.Debug("{class} {method} {event}", "WorkbookController", "PostLinkedQueryResult", "Start");
                var xl = new ExcelHelper(Globals.ThisAddIn.Application);
                var sht = xl.GetTargetWorksheet(results.TargetSheet);
                xl.DaxQueryTable(sht, results.DaxQuery, results.ConnectionString);
                Log.Debug("{class} {method} {event}", "WorkbookController", "PostLinkedQueryResult", "End");
                return Ok();
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "WorkbookController", "PostLinkedQueryResult", ex.Message, ex.StackTrace);
                return BadRequest(ex.Message);
            }
        }

    }
}
