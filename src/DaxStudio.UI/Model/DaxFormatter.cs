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
using System.Windows.Threading;
using DaxStudio.UI.Utils;
using Caliburn.Micro;

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
        const int REQUEST_TIMEOUT = 10000;

        private static string redirectUrl = null;  // cache the redirected URL
        private static string redirectHost = null;
        //public static async Task FormatQuery(DocumentViewModel doc, DAXEditor.DAXEditor editor)
        //{
        //    Log.Debug("{class} {method} {event}", "DaxFormatter", "FormatQuery", "Start");
        //    int colOffset = 1;
        //    int rowOffset = 1;
        //    Log.Verbose("{class} {method} {event}", "DaxFormatter", "FormatQuery", "Getting Query Text");
        //    // todo - do I want to disable the editor control while formatting is in progress???
        //    string qry;
        //    // if there is a selection send that to daxformatter.com otherwise send all the text
        //    qry = editor.SelectionLength == 0 ? editor.Text : editor.SelectedText;

        //    Log.Debug("{class} {method} {event}", "DaxFormatter", "FormatQuery", "About to Call daxformatter.com");

        //    var res = await FormatDaxAsync(qry);

        //    Log.Debug("{class} {method} {event}", "DaxFormatter", "FormatQuery", "daxformatter.com call complete");
    
        //    try
        //    {  
        //        if (res.errors == null)
        //        {
        //            if (editor.SelectionLength == 0)
        //            {
        //                editor.IsEnabled = false;
        //                editor.Document.BeginUpdate();
        //                editor.Document.Text = res.FormattedDax;
        //                editor.Document.EndUpdate();
        //                editor.IsEnabled = true;
        //            }
        //            else
        //            {
        //                var loc = editor.Document.GetLocation(editor.SelectionStart);
        //                colOffset = loc.Column;
        //                rowOffset = loc.Line;
        //                editor.SelectedText = res.FormattedDax;
        //            }
        //            Log.Debug("{class} {method} {event}", "DaxFormatter", "FormatQuery", "Query Text updated");
        //            doc.OutputMessage("Query Formatted via daxformatter.com");
        //        }
        //        else
        //        {

        //            foreach (var err in res.errors)
        //            {
        //                // write error 
        //                // note: daxformatter.com returns 0 based coordinates so we add 1 to them
        //                int errLine = err.line + rowOffset;
        //                int errCol = err.column + colOffset;
                        
        //                // if the error is at the end of text then we need to move in 1 character
        //                var errOffset = editor.Document.GetOffset(errLine, errCol);
        //                if (errOffset == editor.Document.TextLength && !editor.Text.EndsWith(" "))
        //                {
        //                    editor.Document.Insert(errOffset, " ");
        //                }

        //                // TODO - need to figure out if more than 1 character should be highlighted
        //                doc.OutputError(string.Format("(Ln {0}, Col {1}) {2} ", errLine, errCol, err.message), err.line + rowOffset, err.column + colOffset);
        //                doc.ActivateOutput();

        //                Log.Debug("{class} {method} {event}", "DaxFormatter", "FormatQuery", "Error markings set");
        //            }

        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error("{Class} {Event} {Exception}", "DaxFormatter", "FormatQuery", ex.Message);
        //        doc.OutputError(string.Format("DaxFormatter.com Error: {0}", ex.Message));
        //    }
        //    finally
        //    {
        //        Log.Debug("{class} {method} {end}", "DaxFormatter", "FormatDax:End");
        //    }
        //}

        public static async Task<DaxFormatterResult> FormatDaxAsync(string query)
        {
            Log.Verbose("{class} {method} {query}", "DaxFormatter", "FormatDaxAsync:Begin", query);
            var errorFound = false;
            string output = await CallDaxFormatterAsync(WebRequestFactory.DaxFormatUri, query);
            if (output == "\"\"")
            {
                errorFound = true;
                output = await CallDaxFormatterAsync(WebRequestFactory.DaxFormatVerboseUri, query);
            }
            
            // trim off leading and trailing quotes
            var o2 = output.Substring(1, output.Length - 2);
            o2 = o2.Replace("\\r\\n", "\r\n");
            o2 = o2.TrimEnd( Environment.NewLine.ToCharArray() );
            o2 = o2.Replace("\\\"", "\"");
            o2 = o2.Replace("\\\\", "\\");
            
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
            Log.Debug("{class} {method} {event}", "DaxFormatter", "FormatDaxAsync", "End");
            return res2;
        }

        private static async Task<string> CallDaxFormatterAsync(string uri, string query)
        {
            Log.Verbose("{class} {method} {uri} {query}","DaxFormatter","CallDaxFormatterAsync:Begin",uri,query );
            try
            {

                DaxFormatterRequest req = new DaxFormatterRequest();
                req.Dax = query;

                var data = JsonConvert.SerializeObject(req);

                var enc = System.Text.Encoding.UTF8;
                var data1 = enc.GetBytes(data);

                // this should allow DaxFormatter to work through http 1.0 proxies
                // see: http://stackoverflow.com/questions/566437/http-post-returns-the-error-417-expectation-failed-c
                //System.Net.ServicePointManager.Expect100Continue = false;

                

                await PrimeConnectionAsync(uri);

                Uri originalUri = new Uri(uri);
                string actualUrl = new UriBuilder(originalUri.Scheme, redirectHost, originalUri.Port, originalUri.PathAndQuery).ToString();

                var webRequestFactory = IoC.Get<WebRequestFactory>();
                var wr = webRequestFactory.Create(new Uri(actualUrl));

                wr.Timeout = REQUEST_TIMEOUT;
                wr.ContentType = "application/json";
                wr.Method = "POST";
                wr.Accept = "application/json, text/javascript, */*; q=0.01";
                wr.Headers.Add("Accept-Encoding", "gzip,deflate");
                wr.Headers.Add("Accept-Language", "en-US,en;q=0.8");
                wr.ContentType = "application/json; charset=UTF-8";
                wr.AutomaticDecompression = DecompressionMethods.GZip;

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
                            output = await reader.ReadToEndAsync();
                        }
                    }
                }

                return output;
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message}", "DaxFormatter", "CallDaxFormatterAsync", ex.Message);
                throw;
            }
            finally
            {
                Log.Debug("{class} {method}", "DaxFormatter", "CallDaxFormatterAsync:End");
            }
        }

        public static async Task PrimeConnectionAsync(string uri)
        {
            await Task.Run(() =>
            {
                Log.Debug("{class} {method} {event}", "DaxFormatter", "PrimeConnectionAsync", "Start");
                if (redirectHost == null)
                {
                    

                    // www.daxformatter.com redirects request to another site.  HttpWebRequest does redirect with GET.  It fails, since the web service works only with POST
                    // The following 2 requests are doing manual POST re-direct
                    var webRequestFactory = IoC.Get<WebRequestFactory>();
                    var redirectRequest =  webRequestFactory.Create(uri) as HttpWebRequest;

                    redirectRequest.AllowAutoRedirect = false;
                    redirectRequest.Timeout = REQUEST_TIMEOUT;
                    try
                    {
                        using (var netResponse = redirectRequest.GetResponse())
                        {
                            var redirectResponse = (HttpWebResponse)netResponse;
                            redirectUrl = redirectResponse.Headers["Location"];
                            var redirectUri = new Uri(redirectUrl);

                            // set the shared redirectHost variable
                            redirectHost = redirectUri.Host;
                            Log.Debug("{class} {method} Redirected to: {redirectUrl}", "DaxFormatter", "CallDaxFormatterAsync", uri.ToString());
                            System.Diagnostics.Debug.WriteLine("Host: " + redirectUri.Host);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("{class} {method} {error}", "DaxFormatter", "PrimeConnectionAsync", ex.Message);
                    }
                }
                Log.Debug("{class} {method} {event}", "DaxFormatter", "PrimeConnectionAsync", "End");
            });

        }
        public static async Task PrimeConnectionAsync()
        {
            await PrimeConnectionAsync(WebRequestFactory.DaxFormatUri);
        }
        
    }
}
