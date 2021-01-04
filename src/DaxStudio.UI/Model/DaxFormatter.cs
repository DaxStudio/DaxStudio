using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Serilog;
using DaxStudio.UI.Utils;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using System.ComponentModel.Composition;
using System.Linq.Expressions;
using DaxStudio.UI.Extensions;

using System.Security.Cryptography;
using System.Text;
using DaxStudio.UI.Events;

namespace DaxStudio.UI.Model
{

    internal static class Crypto
    {
        public static string SHA256(string input)
        {
            if (input == null) return "";

            var hasher = new SHA256Managed();
            var sb = new StringBuilder();

            byte[] hashedBytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(input), 0, Encoding.UTF8.GetByteCount(input));

            foreach (var b in hashedBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }

    public class DaxFormatterError
    {
        public int line { get; set; }
        public int column { get; set; }
        public string message { get; set; }
    }

    public class ServerDatabaseInfo
    {
        public string ServerName { get; set; } // SHA-256 hash of server name
        public string ServerEdition { get; set; } // # Values: null, "Enterprise64", "Developer64", "Standard64"
        public string ServerType { get; set; } // Values: null, "SSAS", "PBI Desktop", "SSDT Workspace", "Tabular Editor"
        public string ServerMode { get; set; } // Values: null, "SharePoint", "Tabular"
        public string ServerLocation { get; set; } // Values: null, "OnPremise", "Azure"
        public string ServerVersion { get; set; } // Example: "14.0.800.192"
        public string DatabaseName { get; set; } // SHA-256 hash of database name
        public string DatabaseCompatibilityLevel { get; set; } // Values: 1200, 1400
    }

    public class DaxFormatterRequest : ServerDatabaseInfo
    {
        public string Dax { get; set; }
        public int? MaxLineLenght { get; set; }
        public bool? SkipSpaceAfterFunctionName { get; set; }
        public char ListSeparator { get; set; }
        public char DecimalSeparator { get; set; }
        public string CallerApp { get; set; }
        public string CallerVersion { get; set; }

        public DaxFormatterRequest()
        {
            this.ListSeparator = ',';
            this.DecimalSeparator = '.';

            // Save caller app and version
            var assemblyName = System.Reflection.Assembly.GetEntryAssembly().GetName();
            this.CallerApp = assemblyName.Name;
            this.CallerVersion = assemblyName.Version.ToString();
        }
    }

    
    public class DaxFormatterResult
    {
        [JsonProperty(PropertyName = "formatted")]
        public string FormattedDax;
        public List<DaxFormatterError> errors;
    }

    [Export]
    public class DaxFormatterProxy
    {

        static DaxFormatterProxy()
        {
            // force the use of TLS 1.2
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        }

        private static string redirectUrl;  // cache the redirected URL
        private static string redirectHost;
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



        public static async Task<DaxFormatterResult> FormatDaxAsync(string query, ServerDatabaseInfo serverDbInfo, IGlobalOptions globalOptions, IEventAggregator eventAggregator, bool formatAlternateStyle )
        {
            Log.Verbose("{class} {method} {query}", "DaxFormatter", "FormatDaxAsync:Begin", query);
            string output = await CallDaxFormatterAsync(WebRequestFactory.DaxTextFormatUri, query, serverDbInfo, globalOptions, eventAggregator, formatAlternateStyle);
            var res2 = new DaxFormatterResult();
            JsonConvert.PopulateObject(output, res2);
            Log.Debug("{class} {method} {event}", "DaxFormatter", "FormatDaxAsync", "End");
            return res2;
        }
        
        private static async Task<string> CallDaxFormatterAsync(string uri, string query, ServerDatabaseInfo serverDbInfo, IGlobalOptions globalOptions, IEventAggregator eventAggregator, bool formatAlternateStyle )
        {
            Log.Verbose("{class} {method} {uri} {query}","DaxFormatter","CallDaxFormatterAsync:Begin",uri,query );
            try
            {

                DaxFormatterRequest req = new DaxFormatterRequest();
                req.Dax = query;
                if (globalOptions.DefaultSeparator == DaxStudio.Interfaces.Enums.DelimiterType.SemiColon)
                {
                    req.DecimalSeparator = ',';
                    req.ListSeparator = ';';
                }
                req.ServerName = Crypto.SHA256( serverDbInfo.ServerName );
                req.ServerEdition = serverDbInfo.ServerEdition;
                req.ServerType = serverDbInfo.ServerType; 
                req.ServerMode = serverDbInfo.ServerMode;
                req.ServerLocation = serverDbInfo.ServerLocation;
                req.ServerVersion = serverDbInfo.ServerVersion;
                req.DatabaseName = Crypto.SHA256( serverDbInfo.DatabaseName );
                req.DatabaseCompatibilityLevel = serverDbInfo.DatabaseCompatibilityLevel;
                if ( (globalOptions.DefaultDaxFormatStyle == DaxStudio.Interfaces.Enums.DaxFormatStyle.ShortLine && !formatAlternateStyle)
                    ||
                     (globalOptions.DefaultDaxFormatStyle == DaxStudio.Interfaces.Enums.DaxFormatStyle.LongLine && formatAlternateStyle)
                    )
                {
                    req.MaxLineLenght = 1;
                }
                req.SkipSpaceAfterFunctionName = globalOptions.SkipSpaceAfterFunctionName;

                var data = JsonConvert.SerializeObject(req);

                var enc = System.Text.Encoding.UTF8;
                var data1 = enc.GetBytes(data);

                // this should allow DaxFormatter to work through http 1.0 proxies
                // see: https://stackoverflow.com/questions/566437/http-post-returns-the-error-417-expectation-failed-c
                //System.Net.ServicePointManager.Expect100Continue = false;

                

                await PrimeConnectionAsync(uri, globalOptions,eventAggregator);

                Uri originalUri = new Uri(uri);
                string actualUrl = new UriBuilder(originalUri.Scheme, redirectHost, originalUri.Port, originalUri.PathAndQuery).ToString();

                //var webRequestFactory = IoC.Get<WebRequestFactory>();
                var webRequestFactory = await WebRequestFactory.CreateAsync( globalOptions, eventAggregator);
                var wr = webRequestFactory.Create(new Uri(actualUrl));

                wr.Timeout = globalOptions.DaxFormatterRequestTimeout.SecondsToMilliseconds();
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

        public static async Task PrimeConnectionAsync(string uri, IGlobalOptions globalOptions, IEventAggregator eventAggregator)
        {
            
            Log.Debug("{class} {method} {event}", "DaxFormatter", "PrimeConnectionAsync", "Start");
            try
            {
                if (globalOptions.BlockExternalServices)
                {
                    Log.Debug(Common.Constants.LogMessageTemplate, nameof(DaxFormatterProxy), nameof(PrimeConnectionAsync), "Skipping Priming Connection to DaxFormatter.com as External Services are blocked in options");
                    return;
                }
                
                if (redirectHost == null)
                {
                    // www.daxformatter.com redirects request to another site.  HttpWebRequest does redirect with GET.  It fails, since the web service works only with POST
                    // The following 2 requests are doing manual POST re-direct
                    //var webRequestFactory = IoC.Get<WebRequestFactory>();
                    WebRequestFactory webRequestFactory =
                        await WebRequestFactory.CreateAsync(globalOptions, eventAggregator);
                    var redirectRequest = webRequestFactory.Create(uri) as HttpWebRequest;

                    redirectRequest.AllowAutoRedirect = false;
                    redirectRequest.Timeout = globalOptions.DaxFormatterRequestTimeout.SecondsToMilliseconds();
                    try
                    {
                        using (var netResponse = await redirectRequest.GetResponseAsync())
                        {
                            var redirectResponse = (HttpWebResponse) netResponse;
                            redirectUrl = redirectResponse.Headers["Location"];
                            var redirectUri = new Uri(redirectUrl);

                            // set the shared redirectHost variable
                            redirectHost = redirectUri.Host;
                            Log.Debug("{class} {method} Redirected to: {redirectUrl}", "DaxFormatter",
                                "CallDaxFormatterAsync", uri.ToString());
                            System.Diagnostics.Debug.WriteLine("Host: " + redirectUri.Host);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("{class} {method} {error}", "DaxFormatter", "PrimeConnectionAsync",
                            $"Error getting redirect response: {ex.Message}");
                    }
                }
            }
            catch (Exception ex1)
            {
                Log.Error("{class} {method} {error}", "DaxFormatter", "PrimeConnectionAsync",
                    $"Error getting redirect location: {ex1.Message}");
                await eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning,
                    $"An error occurred while checking the connection to daxformatter.com\n\t{ex1.Message}"));
            }

            Log.Debug("{class} {method} {event}", "DaxFormatter", "PrimeConnectionAsync", "End");

        }
        public static async Task PrimeConnectionAsync(IGlobalOptions globalOptions, IEventAggregator eventAggregator)
        {
            await PrimeConnectionAsync(WebRequestFactory.DaxTextFormatUri, globalOptions, eventAggregator);
        }
        
    }
}
