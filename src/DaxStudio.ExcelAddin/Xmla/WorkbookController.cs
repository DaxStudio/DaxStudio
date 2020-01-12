using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
using System.Web.Http;
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

            using (var xl = new ExcelHelper(Globals.ThisAddIn.Application))
            {
                var addin = Globals.ThisAddIn;
                var app = addin.Application;
                var wb = app.ActiveWorkbook;
                var wbName = "<No Workbook>";
                if (wb != null) { wbName = wb.FullName; }
                Log.Debug("{class} {method} {message}", nameof(WorkbookController), nameof(GetWorkbookFileName), $"Workbook Fullname: '{wb.FullName}'");
                Log.Debug("{class} {method} {message}", nameof(WorkbookController), nameof(GetWorkbookFileName), $"Workbook FullnameUrlEncoded: '{wb.FullNameURLEncoded}'");
                System.Diagnostics.Debug.WriteLine($"Workbook: {wbName}");
                return Ok(wbName);
            }
        }
        
        
        [HttpGet]
        [ActionName("worksheets")]
        [Route("Worksheets")]
        public IHttpActionResult GetWorksheets()
        {
            try
            {
                Log.Debug("{class} {method} {event}", "WorkbookController", "GetWorksheets", "Start");
                using (var xl = new ExcelHelper(Globals.ThisAddIn.Application))
                {
                    var addin = Globals.ThisAddIn;
                    var app = addin.Application;
                    var wb = app.ActiveWorkbook;
                    var shts = new List<string>
                    {
                        WorksheetDaxResults,
                        WorksheetNew
                    };
                    foreach (Worksheet sht in wb.Worksheets)
                    {
                        shts.Add(sht.Name);
                    }
                
                    Log.Debug("{class} {method} {event}", "WorkbookController", "GetWorksheets", "End");
                    return Ok(shts.ToArray());
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log.Error("{class} {method} {message}", "WorkbookController", "GetWorksheets", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        private static string WorksheetDaxResults
        {
            get { return "<Query Results Sheet>"; }
        }

        private static string WorksheetNew => "<New Sheet>";

        [HttpGet]
        [Route("HasDataModel")]
        public IHttpActionResult GetHasDatamodel()
        {
            Log.Debug("{class} {method} {event}", "WorkbookController", "GetHasDataModel", "Start");
            using (var xl = new ExcelHelper(Globals.ThisAddIn.Application))
            {
                if (xl.HasPowerPivotData())
                    return Ok(true);
                else
                    return Ok(false);
            }
        }

        [HttpPost]
        [Route("StaticQueryResult")]
        public IHttpActionResult PostStaticResult(StaticQueryResult results)
        {
            Log.Debug("{class} {method} {event}", "WorkbookController", "PostStaticResult", "Start");

            if (results == null) throw new ArgumentNullException(nameof(results));
            if (results.QueryResults == null) return this.BadRequest("Resultset is null");
            if (results.QueryResults.Columns.Count == 0) return this.NotFound();// BadRequest("Resultset has no columns");
            
            using (var xl = new ExcelHelper(Globals.ThisAddIn.Application))
            {
                var sht = xl.GetTargetWorksheet(results.TargetSheet);
                ExcelHelper.CopyDataTableToRange(results.QueryResults, sht);
            }
            Log.Debug("{class} {method} {event}", "WorkbookController", "PostStaticResult", "End");
            return this.Ok();
        }

        [HttpPost]
        [Route("LinkedQueryResult")]
        public IHttpActionResult PostLinkedQueryResult(LinkedQueryResult results)
        {
            if (results == null)
            {
                return this.BadRequest("The results parameter cannot be null");
            }

            try
            {
                Log.Debug("{class} {method} {event}", "WorkbookController", "PostLinkedQueryResult", "Start");
                using (var xl = new ExcelHelper(Globals.ThisAddIn.Application))
                {
                    var sht = xl.GetTargetWorksheet(results.TargetSheet);
                    xl.DaxQueryTable(sht, results.DaxQuery, results.ConnectionString);
                }
                Log.Debug("{class} {method} {event}", "WorkbookController", "PostLinkedQueryResult", "End");
                return Ok();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log.Error("{class} {method} {message} {stacktrace}", "WorkbookController", "PostLinkedQueryResult", ex.Message, ex.StackTrace);
                return BadRequest(ex.Message);
            }
        }

    }
}
