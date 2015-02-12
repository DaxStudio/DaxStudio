using DaxStudio.UI.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace DaxStudio.UI.Model
{
    public class DaxFormatterError
    {
        public int line;
        public int column;
        public string message;
    }

    public class DaxFormatterRequest
    {
        public string Dax { get; set; }
        public char ListSeparator { get; set; }
        public char DecimalSeparator { get; set; }

        public DaxFormatterRequest()
        {
            this.ListSeparator = ',';
            this.DecimalSeparator = '.';
        }
    }

    
    public class DaxFormatterResult
    {
        public string FormattedDax;
        public List<DaxFormatterError> errors;
    }

    public class DaxFormatterProxy
    {
        const string DaxFormatUri =  "http://www.daxformatter.com/api/daxformatter/DaxFormat";
        const string DaxFormatVerboseUri = "http://www.daxformatter.com/api/daxformatter/DaxrichFormatverbose";
        private static string redirectUrl = null;  // cache the redirected URL
        private static string redirectHost = null;
        public static async Task FormatQuery(DocumentViewModel doc, DAXEditor.DAXEditor editor)
        {
            try
            {
                Log.Verbose("{class} {method}", "DaxFormatter", "FormatQuery:Begin");
                // todo - do I want to disable the editor control while formatting is in progress???

                // if there is a selection send that to daxformatter.com otherwise send all the text
                var qry = editor.SelectionLength == 0 ? editor.Text : editor.SelectedText;

                var res = await FormatDaxAsync(qry);
                if (res.errors == null)
                {
                    if (editor.SelectionLength == 0)
                    {
                        //editor.Document.UndoStack.StartContinuedUndoGroup("DaxFormatter");
                        editor.IsEnabled = false;
                        editor.Document.BeginUpdate();
                        editor.Document.Text = res.FormattedDax.TrimEnd();
                        editor.Document.EndUpdate();
                        editor.IsEnabled = true;
                    }
                    else
                    {
                        //editor.Document.UndoStack.StartUndoGroup();
                        editor.SelectedText = res.FormattedDax.TrimEnd();
                        //editor.Document.UndoStack.EndUndoGroup();
                    }
                    doc.OutputMessage("Query Formatted via daxformatter.com");
                }
                else
                {

                    foreach (var err in res.errors)
                    {
                        // write error 
                        // note: daxformatter.com returns 0 based coordinates so we add 1 to them
                        doc.OutputError(string.Format("(Ln {0}, Col {1}) {2} ", err.line+1, err.column+1, err.message),err.line+1, err.column+1);

                        // show waveline under error
                        // editor
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Error("{Class} {Event} {Exception}", "DaxFormatter", "FormatQuery", ex.Message);
                doc.OutputError(string.Format("DaxFormatter.com Error: {0}", ex.Message ));
            }
            finally
            {
                Log.Verbose("{class} {method}", "DaxFormatter", "FormatDax:End");
            }
        }

        public static async Task<DaxFormatterResult> FormatDaxAsync(string query)
        {
            Log.Verbose("{class} {method} {query}", "DaxFormatter", "FormatDaxAsync:Begin", query);
            var errorFound = false;
            string output = await CallDaxFormatterAsync(DaxFormatUri, query);
            if (output == "\"\"")
            {
                errorFound = true;
                output = await CallDaxFormatterAsync(DaxFormatVerboseUri, query);
            }
            
            // trim off leading and trailing quotes
            var o2 = output.Substring(1, output.Length - 2);
            o2 = o2.Replace("\\r\\n", "\r\n");
            o2 = o2.Replace("\\\"", "\"");

            //todo if result is empty string then call out to rich format API to get error message
            var res2 = new DaxFormatterResult();
            if (errorFound)
            {
                JsonConvert.PopulateObject(o2, res2);
                res2.FormattedDax = "";
            }
            else
            {
                res2.FormattedDax = o2;
            }
            Log.Verbose("{class} {method}", "DaxFormatter", "FormatDaxAsync:End");
            return res2;
        }

        private static async Task<string> CallDaxFormatterAsync(string uri, string query)
        {
            Log.Verbose("{class} {method} {uri} {query}","DaxFormatter","CallDaxFormatterAsync:Begin",uri,query );
            DaxFormatterRequest req = new DaxFormatterRequest();
            req.Dax = query;

            var data = JsonConvert.SerializeObject(req);

            var enc = System.Text.Encoding.UTF8;
            var data1 = enc.GetBytes(data);
            Uri redirectUri;

            //TODO - figure out when to use proxy
            var proxy = System.Net.WebRequest.GetSystemWebProxy();
            proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;

            if (redirectHost == null)
            {
                // www.daxformatter.com redirects request to another site.  HttpWebRequest does redirect with GET.  It fails, since the web service works only with POST
                // The following 2 requests are doing manual POST re-direct
                var redirectRequest = System.Net.HttpWebRequest.Create(uri) as HttpWebRequest;
                redirectRequest.AllowAutoRedirect = false;

                redirectRequest.Proxy = proxy;

                using (var netResponse = await redirectRequest.GetResponseAsync())
                {
                    var redirectResponse = (HttpWebResponse)netResponse;
                    redirectUrl = redirectResponse.Headers["Location"];
                    redirectUri = new Uri(redirectUrl);
                    redirectHost = redirectUri.Host;
                    Log.Debug("{class} {method} Redirected to: {redirectUrl}", "DaxFormatter", "CallDaxFormatterAsync", uri.ToString());
                    System.Diagnostics.Debug.WriteLine("Host: " + redirectUri.Host);
                }
            }
            Uri originalUri = new Uri(uri);
            var actualUrl = new UriBuilder(originalUri.Scheme, redirectHost, originalUri.Port, originalUri.PathAndQuery).ToString();
            
            
            var wr = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(actualUrl);

            wr.ContentType = "application/json";
            wr.Method = "POST";
            wr.Accept = "application/json, text/javascript, */*; q=0.01";
            wr.Headers.Add("Accept-Encoding", "gzip,deflate");
            wr.Headers.Add("Accept-Language", "en-US,en;q=0.8");
            wr.ContentType = "application/json; charset=UTF-8";
            wr.AutomaticDecompression = DecompressionMethods.GZip;

            //todo 
            wr.Proxy = proxy;

            string output = "";
            using (var strm = await wr.GetRequestStreamAsync())
            {
                strm.Write(data1, 0, data1.Length);

                using (var resp = wr.GetResponse())
                {
                    //var outStrm = new System.IO.Compression.GZipStream(resp.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                    var outStrm = resp.GetResponseStream();
                    using (var reader = new System.IO.StreamReader(outStrm))
                    {
                        output = reader.ReadToEnd();
                    }
                }
            }
            Log.Verbose("{class} {method}", "DaxFormatter", "CallDaxFormatterAsync:End");
            return output;
        }
    }
}
