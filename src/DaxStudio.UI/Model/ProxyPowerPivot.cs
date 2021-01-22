using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using Caliburn.Micro;
using Newtonsoft.Json;
using DaxStudio.UI.Events;
using Serilog;
using System.IO;
using System.Diagnostics.Contracts;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Utils;
using ADOTabular.Enums;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Model
{
    public class ProxyPowerPivot 
        : IDaxStudioProxy
        , IHandle<ActivateDocumentEvent>
    {
        private readonly int _port;
        private readonly Uri _baseUri;
        private readonly IEventAggregator _eventAggregator;
        private ViewModels.DocumentViewModel _activeDocument;
        public ProxyPowerPivot(IEventAggregator eventAggregator, int port)
        {
            Contract.Requires(eventAggregator != null, "The eventAggregator argument must not be null");
            _port = port;
            _baseUri = new Uri($"http://localhost:{port}/");
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
        }

        internal HttpClient GetHttpClient()
        {
            var client = new HttpClient();
            client.BaseAddress = _baseUri;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public bool IsExcel
        {
            get { return true; }
        }

        public bool SupportsQueryTable
        {
            get { return true; }
        }

        public bool SupportsStaticTable
        {
            get { return true; }
        }

        public bool HasPowerPivotModel
        {
            get {
                Log.Verbose("{class} {method} {event}", "Model.ProxyPowerPivot", "HasPowerPivotModel:Get", "Start");
                var hasModel = false;
                var doc = _activeDocument;
                using (var client = GetHttpClient())
                {
                    try
                    {
                        //HACK: see if this helps with the PowerPivot client spinning issue
                        client.Timeout = new TimeSpan(0, 0, 30); // set 30 second timeout

                        HttpResponseMessage response = client.GetAsync("workbook/hasdatamodel").Result;
                        Log.Verbose("{class} {method} {event}", "Model.ProxyPowerPivot", "HasPowerPivotModel:Get", "Got Response");
                        if (response.IsSuccessStatusCode)
                        {
                            Log.Verbose("{class} {method} {event}", "Model.ProxyPowerPivot", "HasPowerPivotModel:Get", "Reading Result");
                            hasModel = JsonConvert.DeserializeObject<bool>(response.Content.ReadAsStringAsync().Result);
                        }
                        else
                        {
                            var msg = response.Content.ReadAsStringAsync().Result;
                            Log.Error("{class} {method} {message}", "ProxyPowerPivot", "WorkbookName", $"Error checking if Workbook has a PowerPivot model\n {msg}");
                            doc.OutputError("Error checking if the active Workbook in Excel has a PowerPivot model");
                        }
                        
                    }
                    catch (Exception ex)
                    {
                        var innerEx = ex.GetLeafException();
                        //_eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, string.Format("Error checking if active Excel workbook has a PowerPivot ({0})",ex.Message)));
                        Log.Error("{class} {method} {exception} {stacktrace}", "Model.ProxyPowerPivot", "HasPowerPivotModel:Get", ex.Message, ex.StackTrace );
                        doc.OutputError($"Error checking if active Excel workbook has a PowerPivot model ({innerEx.Message})");
                    }


                    return hasModel;
                }
            }
        }

        public void EnsurePowerPivotDataIsLoaded()
        {
            throw new NotImplementedException();
        }

        public string WorkbookName
        {
            get
            {
                var doc = _activeDocument;
                using (var client = GetHttpClient())
                {
                    try { 
                        HttpResponseMessage response = client.GetAsync("workbook/filename").Result;
                        if (response.IsSuccessStatusCode)
                        {
                            var workbookName = JsonConvert.DeserializeObject<string>( response.Content.ReadAsStringAsync().Result);
                            return workbookName;
                        } 
                        else
                        {
                            var msg = $"{response.Content.ReadAsStringAsync().Result} ({response.StatusCode})";
                            Log.Error("{class} {method} {message}", "ProxyPowerPivot", "WorkbookName", $"Error getting WorkbookName:\n{msg}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "{class} {method} {message}", nameof(ProxyPowerPivot), nameof(WorkbookName), ex.Message);
                        doc.OutputError(string.Format("Error getting ActiveWorkbook from Excel: {0} ", ex.Message));
                    }

                    return "<Workbook not found>";
                }

            }
        }

        public IEnumerable<string> Worksheets
        {
            get
            {
                var doc = _activeDocument;
                try
                {
                    using (var client = GetHttpClient())
                    {
                        HttpResponseMessage response = client.GetAsync("workbook/worksheets").Result;
                        if (response.IsSuccessStatusCode)
                        {
                            return JsonConvert.DeserializeObject<string[]>(response.Content.ReadAsStringAsync().Result);
                            //return response.Content.ReadAsAsync<string[]>().Result;
                        }
                        
                    }
                }
                catch (Exception ex)
                {
                    //_eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, string.Format("Error getting Worksheet list from Excel ({0})",ex.Message)));
                    doc.OutputError(string.Format("Error getting Worksheet list from Excel ({0})", ex.Message));
                }
                
                return new string[] { };
                
            }
        }

        public async Task OutputStaticResultAsync(System.Data.DataTable results, string sheetName)
        {
            var doc = _activeDocument;
            using (var client = GetHttpClient())
            {
                
                try { 
                    //await client.PostAsJsonAsync<IStaticQueryResult>( "workbook/staticqueryresult", new StaticQueryResult(sheetName,results) as IStaticQueryResult).ConfigureAwait(false);

                    var response = await client.PostStreamAsync("workbook/staticqueryresult", new StaticQueryResult(sheetName, results) as IStaticQueryResult).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        var msg = await response.Content.ReadAsStringAsync();
                        doc.OutputError(string.Format("Error sending results to Excel: ({0})", msg));

                    }


                    //await client.PostAsync("workbook/staticqueryresult", new StaticQueryResult(sheetName, results), new JsonMediaTypeFormatter
                    //        {
                    //            SerializerSettings = new JsonSerializerSettings
                    //            {s
                    //                Converters = new List<JsonConverter>
                    //                    {
                    //                        //list of your converters
                    //                        new JsonDataTableConverter()
                    //                    }
                    //            }
                    //        });

                }
                catch (Exception ex)
                {
                    //_eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, string.Format("Error sending results to Excel ({0})",ex.Message)));
                    doc.OutputError(string.Format("Error sending results to Excel ({0})", ex.Message));
                }

            }
        }

        public async Task OutputLinkedResultAsync(string daxQuery, string sheetName, string connectionString)
        {
            var doc = _activeDocument;
            using (var client = GetHttpClient())
            {
                try
                {
                    //var resp = await client.PostAsJsonAsync<ILinkedQueryResult>("workbook/linkedqueryresult", new LinkedQueryResult(daxQuery,sheetName,connectionString) as ILinkedQueryResult).ConfigureAwait(false);
                    var resp = await client.PostStreamAsync("workbook/linkedqueryresult", new LinkedQueryResult(daxQuery, sheetName, connectionString) as ILinkedQueryResult).ConfigureAwait(false);
                    if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        //var str = resp.Content.ReadAsAsync<string>().Result;
                        var str = JsonConvert.DeserializeObject<string>(resp.Content.ReadAsStringAsync().Result);
                        var msg = (string)Newtonsoft.Json.Linq.JObject.Parse(str)["Message"];
                        
                        doc.OutputError(string.Format("Error sending results to Excel ({0})", msg));
                    }
                }
                catch (Exception ex)
                {
                    //_eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, string.Format("Error sending results to Excel ({0})",ex.Message)));
                    doc.OutputError(string.Format("Error sending results to Excel ({0})", ex.Message));
                }

            }
        }

        public ADOTabular.ADOTabularConnection GetPowerPivotConnection(string applicationName, string additionalproperties)
        {
            var connstr = string.Format("Data Source=http://localhost:{0}/xmla;{1};{2}", _port,applicationName,additionalproperties);
            return new ADOTabular.ADOTabularConnection(connstr, AdomdType.AnalysisServices);
        }

        public int Port { get { return _port; } }
        public void Dispose()
        {
            // Do Nothing
        }

        public void Handle(ActivateDocumentEvent message)
        {
            _activeDocument = message.Document;
        }
    }
}
