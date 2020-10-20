using System.Web.Http;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.IO;
using System;
using Serilog;
using System.Globalization;
using System.Xml;

namespace DaxStudio.ExcelAddin.Xmla
{
    [RoutePrefix("xmla")]
    public class XmlaController : ApiController
    {
        const int EXCEL_2013 = 15; // version number for Excel 2013

        //[HttpPost]
        //[Route("")]
        //public async Task<HttpResponseMessage> PostRawBufferManual()
        //{
        //    string connStr = "";
        //    string loc = "";
        //    try
        //    {
        //        string request = await Request.Content.ReadAsStringAsync();

        //        var addin = Globals.ThisAddIn;
        //        var app = addin.Application;
        //        var wb = app.ActiveWorkbook;
        //        loc = wb.FullName;  //@"D:\Data\Presentations\Drop Your DAX\demos\02 DAX filter similar.xlsx";

        //        // parse request looking for workbook name in Workstation ID
        //        // we are using the Workstation ID property to tunnel the location property through
        //        // from the UI to the PowerPivot engine. The Location property does not appear to get
        //        // serialized through into the XMLA request so we "hijack" the Workstation ID
        //        var wsid = ParseRequestForWorkstationID(request);
        //        if (!string.IsNullOrEmpty(wsid))
        //        {
        //            Log.Debug("{class} {method} {message}", "XmlaController", "PostRawBufferManual", "Resetting Location based on WorkstationID to: " + loc);
        //            loc = wsid;
        //        }

        //        connStr = string.Format("Provider=MSOLAP;Persist Security Info=True;Initial Catalog=Microsoft_SQLServer_AnalysisServices;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;MDX Missing Member Mode=Error;Subqueries=0;Optimize Response=7;Location=\"{0}\"", loc);
        //        //connStr = string.Format("Provider=MSOLAP;Persist Security Info=True;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;MDX Missing Member Mode=Error;Subqueries=0;Optimize Response=7;Location={0}", loc);
        //        // 2010 conn str
        //        //connStr = string.Format("Provider=MSOLAP.5;Persist Security Info=True;Initial Catalog=Microsoft_SQLServer_AnalysisServices;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue;Location={0}", loc);
        //        //connStr = string.Format("Provider=MSOLAP;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Location={0};", loc);

        //        Log.Debug("{class} {method} {message}", "XmlaController", "PostRawBufferManual", "About to Load AmoWrapper");
        //        AmoWrapper.AmoType amoType = AmoWrapper.AmoType.AnalysisServices;
        //        if (float.Parse(app.Version, CultureInfo.InvariantCulture) >= EXCEL_2013)
        //        {
        //            amoType = AmoWrapper.AmoType.Excel;
        //            Log.Debug("{class} {method} {message}", "XmlaController", "PostRawBufferManual", "Loading Microsoft.Excel.Amo");
        //        }
        //        else
        //        {
        //            Log.Debug("{class} {method} {message}", "XmlaController", "PostRawBufferManual", "defaulting to Microsoft.AnalysisServices");
        //        }

        //        var svr = new AmoWrapper.AmoServer(amoType);
        //        svr.Connect(connStr);

        //        // STEP 1: send the request to server.
        //        Log.Verbose("{class} {method} request: {request}", "XmlaController", "PostRawBufferManual", request);
        //        System.IO.TextReader streamWithXmlaRequest = new StringReader(request);
        //        System.Xml.XmlReader xmlaResponseFromServer = null; // will be used to parse the XML/A response from server
        //        string fullEnvelopeResponseFromServer = "";
        //        try
        //        {
        //            //xmlaResponseFromServer = svr.SendXmlaRequest( XmlaRequestType.Undefined, streamWithXmlaRequest);
        //            xmlaResponseFromServer = svr.SendXmlaRequest(streamWithXmlaRequest);

        //            // STEP 2: read/parse the XML/A response from server.
        //            xmlaResponseFromServer.MoveToContent();
        //            fullEnvelopeResponseFromServer = xmlaResponseFromServer.ReadOuterXml();
        //        }
        //        catch (Exception ex)
        //        {
        //            Log.Error("ERROR sending response: {class} {method} {exception}", "XmlaController", "PostRawBufferManual", ex);
        //            //result = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        //            //result.Content = new StringContent(String.Format("An unexpected error occurred (sending XMLA request): \n{0}", ex.Message));
        //        }
        //        finally
        //        {
        //            streamWithXmlaRequest.Close();
        //        }

        //        HttpResponseMessage result;
        //        try
        //        {
        //            result = new HttpResponseMessage(HttpStatusCode.OK);
        //            result.Headers.TransferEncodingChunked = true;
        //            result.Content = new StringContent(fullEnvelopeResponseFromServer);
        //            result.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        //        }
        //        catch (Exception ex)
        //        {
        //            Log.Error("ERROR sending response: {class} {method} {exception}", "XmlaController", "PostRawBufferManual", ex);
        //            result = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        //            result.Content = new StringContent(String.Format("An unexpected error occurred (reading XMLA response): \n{0}", ex.Message));
        //        }
        //        finally
        //        {
        //            // STEP 3: close the System.Xml.XmlReader, to release the connection for future use.
        //            if (xmlaResponseFromServer != null)
        //            {
        //                xmlaResponseFromServer.Close();
        //            }
        //        }
        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error("ERROR Connecting: {class} {method} loc: '{loc}' conn:'{connStr}' ex: {exception}", "XmlaController", "PostRawBufferManual", loc, connStr, ex);
        //        var expResult = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        //        expResult.Content = new StringContent(String.Format("An unexpected error occurred: \n{0}", ex.Message));
        //        return expResult;
        //    }
        //}


        [HttpPost]
        [Route("")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Returning a HTTP error response with the error details")]
        public async Task<HttpResponseMessage> PostRawBufferManual()
        {
            string connStr = "";
            string loc = "";
            try
            {
                string request = await Request.Content.ReadAsStringAsync().ConfigureAwait(false);

                var addin = Globals.ThisAddIn;
                var app = addin.Application;
                var wb = app.ActiveWorkbook;
                loc = wb.FullName;  //@"D:\Data\Presentations\Drop Your DAX\demos\02 DAX filter similar.xlsx";

                // parse request looking for workbook name in Workstation ID
                // we are using the Workstation ID property to tunnel the location property through
                // from the UI to the PowerPivot engine. The Location property does not appear to get
                // serialized through into the XMLA request so we "hijack" the Workstation ID
                var wsid = ParseRequestForWorkstationID(request);
                if (!string.IsNullOrEmpty(wsid))
                {
                    Log.Debug("{class} {method} {message}", "XmlaController", "PostRawBufferManual", "Resetting Location based on WorkstationID to: " + loc);
                    loc = wsid;
                }

                //connStr = $"Provider=MSOLAP;Persist Security Info=True;Initial Catalog=Microsoft_SQLServer_AnalysisServices;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;MDX Missing Member Mode=Error;Subqueries=0;Optimize Response=7;Location=\"{loc}\"";
                connStr = $"Provider=MSOLAP;Persist Security Info=True;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;MDX Missing Member Mode=Error;Subqueries=0;Optimize Response=7;Location=\"{loc}\"";
                // 2010 conn str
                //connStr = string.Format("Provider=MSOLAP.5;Persist Security Info=True;Initial Catalog=Microsoft_SQLServer_AnalysisServices;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue;Location={0}", loc);
                //connStr = string.Format("Provider=MSOLAP;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Location={0};", loc);

                Log.Debug("{class} {method} {message}", "XmlaController", "PostRawBufferManual", "About to Load AmoWrapper");
                AmoWrapper.AmoType amoType = AmoWrapper.AmoType.AnalysisServices;
                if (float.Parse(app.Version, CultureInfo.InvariantCulture) >= EXCEL_2013)
                {
                    amoType = AmoWrapper.AmoType.Excel;
                    Log.Debug("{class} {method} {message}", "XmlaController", "PostRawBufferManual", "Loading Microsoft.Excel.Amo");
                }
                else
                {
                    Log.Debug("{class} {method} {message}", "XmlaController", "PostRawBufferManual", "defaulting to Microsoft.AnalysisServices");
                }

                using (var svr = new AmoWrapper.AmoServer(amoType))
                {
                    svr.Connect(connStr);

                    // STEP 1: send the request to server.
                    Log.Verbose("{class} {method} request: {request}", "XmlaController", "PostRawBufferManual", request);
                    using (System.IO.TextReader streamWithXmlaRequest = new StringReader(request))
                    {
                        System.Xml.XmlReader xmlaResponseFromServer = null; // will be used to parse the XML/A response from server

                        HttpResponseMessage result;
                        try
                        {
                            //xmlaResponseFromServer = svr.SendXmlaRequest( XmlaRequestType.Undefined, streamWithXmlaRequest);
                            xmlaResponseFromServer = svr.SendXmlaRequest(streamWithXmlaRequest);

                            // STEP 2: read/parse the XML/A response from server.
                            //xmlaResponseFromServer.MoveToContent();
                            //fullEnvelopeResponseFromServer = xmlaResponseFromServer.ReadOuterXml();

                            result = Request.CreateResponse(HttpStatusCode.OK); //new HttpResponseMessage(HttpStatusCode.OK);
                            result.Headers.TransferEncodingChunked = true;


                            // we stream the response instead of buffering the whole thing in a string variable
                            result.Content = new PushStreamContent((stream, content, context) =>
                            {
                                try
                                {
                                    StreamResponse(xmlaResponseFromServer, stream);
                                    stream.Flush();
                                    stream.Close();
                                }
                                catch (Exception ex2)
                                {
                                    Log.Fatal(ex2, "Error Streaming XMLA response");
                                    throw;
                                }
                                finally
                                {
                                // STEP 3: close the System.Xml.XmlReader, to release the connection for future use.
                                if (xmlaResponseFromServer != null)
                                    {
                                        xmlaResponseFromServer.Close();
                                    }
                                }
                            });
                            result.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");

                            //StreamResponse(xmlaResponseFromServer, ms);
                            //ms.Flush();
                            //}

                        }
                        catch (Exception ex3)
                        {
                            Log.Error("ERROR sending response: {class} {method} {exception}", "XmlaController", "PostRawBufferManual", ex3);
                            result = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                            {
                                Content = new StringContent($"An unexpected error occurred (reading XMLA response): \n{ex3.Message}")
                            };
                        }
                        //finally
                        //{
                        //    // STEP 3: close the System.Xml.XmlReader, to release the connection for future use.
                        //    if (xmlaResponseFromServer != null)
                        //    {
                        //        xmlaResponseFromServer.Close();
                        //    }
                        //}
                        return result;
                    }
                }
            }
            catch (Exception ex4)
            {
                Log.Error("ERROR Connecting: {class} {method} loc: '{loc}' conn:'{connStr}' ex: {exception}", "XmlaController", "PostRawBufferManual", loc, connStr, ex4);
                var expResult = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent($"An unexpected error occurred: \n{ex4.Message}")
                };
                return expResult;
            }
        
        }


        private static string ParseRequestForWorkstationID(string request)
        {
            string wsid = string.Empty;
            using (var sr = new StringReader(request))
            {
                using (XmlTextReader xmlRdr = new XmlTextReader(sr) { DtdProcessing = DtdProcessing.Prohibit })
                {
                    while (xmlRdr.Read())
                    {
                        if (xmlRdr.NodeType == XmlNodeType.Element && xmlRdr.LocalName == "SspropInitWsid")
                        {
                            wsid = xmlRdr.ReadElementContentAsString();
                            return wsid;
                        }
                    }
                }
            }
            return wsid;
        }


        static void StreamResponse(XmlReader reader, Stream stream)
        {
            long elementCnt = 0;
            using (XmlWriter writer = new XmlTextWriter(stream, null))
            {

                while (reader.Read())
                {
                    WriteShallowNode(reader, writer);
                    if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        elementCnt++;
                        if (elementCnt % 2000 == 0)
                        {
                            writer.Flush();
                        }
                    }
                }
                writer.Flush();
            }
        }

        static void WriteShallowNode(XmlReader reader, XmlWriter writer)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                    writer.WriteAttributes(reader, true);
                    if (reader.IsEmptyElement)
                    {
                        writer.WriteEndElement();
                    }
                    break;
                case XmlNodeType.Text:
                    writer.WriteString(reader.Value);
                    break;
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    writer.WriteWhitespace(reader.Value);
                    break;
                case XmlNodeType.CDATA:
                    writer.WriteCData(reader.Value);
                    break;
                case XmlNodeType.EntityReference:
                    writer.WriteEntityRef(reader.Name);
                    break;
                case XmlNodeType.XmlDeclaration:
                case XmlNodeType.ProcessingInstruction:
                    writer.WriteProcessingInstruction(reader.Name, reader.Value);
                    break;
                case XmlNodeType.DocumentType:
                    writer.WriteDocType(reader.Name, reader.GetAttribute("PUBLIC"), reader.GetAttribute("SYSTEM"), reader.Value);
                    break;
                case XmlNodeType.Comment:
                    writer.WriteComment(reader.Value);
                    break;
                case XmlNodeType.EndElement:
                    writer.WriteFullEndElement();
                    break;

            }
        }
    }
    
}