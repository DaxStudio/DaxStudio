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

            using (var xl = new ExcelHelper(Globals.ThisAddIn.Application))
            {
                var addin = Globals.ThisAddIn;
                var app = addin.Application;
                var wb = app.ActiveWorkbook;
                var wbName = "<No Workbook>";
                if (wb != null) { wbName = wb.FullName; }
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
        public static void PostStaticResult(StaticQueryResult results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            Log.Debug("{class} {method} {event}", "WorkbookController", "PostStaticResult", "Start");
            using (var xl = new ExcelHelper(Globals.ThisAddIn.Application))
            {
                var sht = xl.GetTargetWorksheet(results.TargetSheet);
                ExcelHelper.CopyDataTableToRange(results.QueryResults, sht);
            }
            Log.Debug("{class} {method} {event}", "WorkbookController", "PostStaticResult", "End");
        }

        [HttpPost]
        [Route("LinkedQueryResult")]
        public IHttpActionResult PostLinkedQueryResult(LinkedQueryResult results)
        {
            try
            {
                Log.Debug("{class} {method} {event}", "WorkbookController", "PostLinkedQueryResult", "Start");
                using (var xl = new ExcelHelper(Globals.ThisAddIn.Application))
                {
#pragma warning disable CA1062 // Validate arguments of public methods
                    var sht = xl.GetTargetWorksheet(results.TargetSheet);
#pragma warning restore CA1062 // Validate arguments of public methods
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
