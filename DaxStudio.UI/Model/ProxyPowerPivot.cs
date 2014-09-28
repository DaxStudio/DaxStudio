using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using Caliburn.Micro;
using Newtonsoft.Json;
using System.Net.Http.Formatting;
using DaxStudio.UI.Events;

namespace DaxStudio.UI.Model
{
    public class ProxyPowerPivot 
        : IDaxStudioProxy
        , IHandle<ActivateDocumentEvent>
    {
        private readonly int _port;
        private readonly Uri _baseUri;
        private IEventAggregator _eventAggregator;
        private ViewModels.DocumentViewModel _activeDocument;
        public ProxyPowerPivot(IEventAggregator eventAggregator, int port)
        {
            _port = port;
            _baseUri = new Uri(string.Format("http://localhost:{0}/",port));
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
        }

        internal HttpClient GetHttpClient()
        {
            var client = new HttpClient();
            client.BaseAddress = _baseUri;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public bool IsExcel
        {
            get { return true; }
        }

        public bool SupportsQueryTable
        {
            get { throw new NotImplementedException(); }
        }

        public bool SupportsStaticTable
        {
            get { return true; }
        }

        public bool HasPowerPivotModel
        {
            get {
                var doc = _activeDocument;
                using (var client = GetHttpClient())
                {
                    try
                    {
                        HttpResponseMessage response = client.GetAsync("workbook/hasdatamodel").Result;
                        if (response.IsSuccessStatusCode)
                        {
                            return response.Content.ReadAsAsync<bool>().Result;
                        }
                    }
                    catch (Exception ex)
                    {
                        //_eventAggregator.Publish(new OutputMessage(MessageType.Error, string.Format("Error checking if active Excel workbook has a PowerPivot ({0})",ex.Message)));
                        doc.OutputError(string.Format("Error checking if active Excel workbook has a PowerPivot ({0})", ex.Message));
                    }


                    return false;
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
                        return response.Content.ReadAsAsync<string>().Result;
                    }
                    }
                    catch (Exception ex)
                    {
                        //_eventAggregator.Publish(new OutputMessage(MessageType.Error, string.Format("Error getting ActiveWorkbook from Excel",ex.Message)));
                        doc.OutputError(string.Format("Error getting ActiveWorkbook from Excel", ex.Message));
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
                            return response.Content.ReadAsAsync<string[]>().Result;
                        }
                        
                    }
                }
                catch (Exception ex)
                {
                    //_eventAggregator.Publish(new OutputMessage(MessageType.Error, string.Format("Error getting Worksheet list from Excel ({0})",ex.Message)));
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
                await client.PostAsJsonAsync<IStaticQueryResult>( "workbook/staticqueryresult", new StaticQueryResult(sheetName,results) as IStaticQueryResult);
                /*await client.PostAsync<IStaticQueryResult>("workbook/staticqueryresult", new StaticQueryResult(sheetName, results), new JsonMediaTypeFormatter
                        {
                            SerializerSettings = new JsonSerializerSettings
                            {
                                Converters = new List<JsonConverter>
                                    {
                                        //list of your converters
                                        new JsonDataTableConverter()
                                    }
                            }
                        });
                 */ 
                }
                catch (Exception ex)
                {
                    //_eventAggregator.Publish(new OutputMessage(MessageType.Error, string.Format("Error sending results to Excel ({0})",ex.Message)));
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
                    await client.PostAsJsonAsync<ILinkedQueryResult>("workbook/linkedqueryresult", new LinkedQueryResult(daxQuery,sheetName,connectionString) as ILinkedQueryResult);
                }
                catch (Exception ex)
                {
                    //_eventAggregator.Publish(new OutputMessage(MessageType.Error, string.Format("Error sending results to Excel ({0})",ex.Message)));
                    doc.OutputError(string.Format("Error sending results to Excel ({0})", ex.Message));
                }

            }
        }

        public ADOTabular.ADOTabularConnection GetPowerPivotConnection()
        {
            var connstr = string.Format("Data Source=http://localhost:{0}/xmla", _port);
            return new ADOTabular.ADOTabularConnection(connstr, ADOTabular.AdomdClientWrappers.AdomdType.AnalysisServices);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Handle(ActivateDocumentEvent message)
        {
            _activeDocument = message.Document;
        }
    }
}
