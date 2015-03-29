using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Data;

namespace DaxStudio.UI.Model
{
    public class ExcelProxy
    {

        private readonly int _port;
        public const string baseAddress = "http://localhost";
        public ExcelProxy(int port)
        {
            _port = port;
        }

        private string BaseAddress
        {
            get {return string.Format("{0}:{1}", baseAddress,_port);}
        }

        public async Task<string> GetWorkbookFileNameAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage res = await client.GetAsync(BaseAddress + "workbook/filename");
                var r = await res.Content.ReadAsAsync<string>();
                return r;
            }
            
        }

        public async Task<string[]> GetWorkSheetsAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage res = await client.GetAsync(BaseAddress + "workbook/worksheets");
                var r = await res.Content.ReadAsAsync<string[]>();
                return r;
            }
        }

        public async Task<bool> HasPowerPivotDataAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage res = await client.GetAsync(BaseAddress + "workbook/haspowerpivotdata");
                var r = await res.Content.ReadAsAsync<bool>();
                return r;
            }
        }

        public async Task<HttpResponseMessage> PostDataSet(DataSet queryResults, string targetSheet)
        {
            using (HttpClient client = new HttpClient())
            {
                var data = new QueryResult() {QueryResults = queryResults, TargetSheet = targetSheet};
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var res = client.PostAsJsonAsync( BaseAddress + "workbook/senddata",data);
                
                return await res;
            }
        }

        private class QueryResult
        {
            public DataSet QueryResults { get; set; }
            public string TargetSheet { get; set; }
        }
    }
}
