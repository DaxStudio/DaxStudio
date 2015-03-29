extern alias ExcelAmo;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcelAmo::Microsoft.AnalysisServices;

namespace DaxStudio.Xmla
{
    class XmlaHelpers
    {
//=====================================================================
//
//  Summary:   Sample code for running XMLA scripts with AMO.
//
//---------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF 
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO 
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
//===================================================================== 

    /// <summary>
    /// Connects to Analysis Services and sends user-specified commands with AMO.
    /// </summary>
    /*
        static int Main(string[] args)
        {
            //--------------------------------------------------------------------------------
            // In this sample code, the XML/A protocol is mentioned; to read 
            // about it: http://www.xmla.org/.
            //--------------------------------------------------------------------------------

            try
            {
                Server server = new Server();

                server.Connect("Data Source=localhost");

                try
                {
                    // Run an empty Batch command (there will be no results displayed).
                    ServerExecute(server, "<Batch xmlns='http://schemas.microsoft.com/analysisservices/2003/engine'/>");

                    // Run an invalid command, to get errors.
                    ServerExecute(server, "<RandomXmlElementHereNotRecognizedByAnalysisServices/>");

                    // Run another invalid command.
                    ServerExecute(server, "<InvalidXmlFragment");

                    // Run a custom SOAP Envelope request. This allows user the full control over
                    // what is sent to the Analysis Services server and also full control over parsing
                    // the result.
                    StartAndEndXmlaRequest(server);

                    // Run a custom SOAP Envelope request read from a stream. 
                    // Useful to run a full SOAP Envelope from a file.
                    SendXmlaRequestFromStream(server);

                    return 0;
                }
                finally
                {
                    server.Disconnect();
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                return 1;
            }
        }
        */

        /// <summary>
        /// Sends the specified command to Analysis Services server and displays the results in Console.
        /// </summary>
        private static void ServerExecute(Server server, string command)
        {
            //--------------------------------------------------------------------------------
            // The Server.Execute method returns a collection of XmlaResult objects (and not 
            // just a single result) because the command being executed can be a Batch 
            // containing multiple commands, each with its own result.
            // Each XmlaResult objects has 2 parts:
            //  - the Value (as a string)
            //  - the Messages: errors and/or warnings
            // A command can be successful and have one or more warnings. But if at least one 
            // error is returned in the Messages section (combined eventually with warnings), 
            // the command failed.
            //--------------------------------------------------------------------------------

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("========== EXECUTE ==========");
            Console.WriteLine(command);

            XmlaResultCollection results = server.Execute(command);

            foreach (XmlaResult result in results)
            {
                Console.WriteLine("-----------------------------");
                Console.WriteLine("VALUE: {0}", result.Value);

                foreach (XmlaMessage message in result.Messages)
                {
                    if (message is XmlaError)
                    {
                        Console.WriteLine("ERROR: {0}", message.Description);
                    }
                    else
                    {
                        System.Diagnostics.Debug.Assert(message is XmlaWarning);
                        Console.WriteLine("WARNING: {0}", message.Description);
                    }
                }
            }
        }

        /// <summary>
        /// Sends a custom SOAP Envelope request to Analysis Services and displays the result in Console.
        /// </summary>
        private static void StartAndEndXmlaRequest( Server server )
        {
            //--------------------------------------------------------------------------------
            // To run a custom full SOAP Envelope request on Analysis Services server, we
            // need to follow 5 steps:
            // 
            // 1. Start the XML/A request and specify its type (Discover or Execute).
            //    For native connections (direct over TCP/IP with DIME protocol), local 
            //    cube connections and stored procedures connections we don't need to
            //    specify the XML/A request type in advance (an Undefined value is
            //    available). But for HTTP and HTTPS connections we need to.
            //
            // 2. Write the xml request (as an xml SOAP Envelope containing the command
            //    for Analysis Services).
            //
            // 3. End the xml request; this will send the previously written request to the 
            //    server for execution.
            //
            // 4. Read/Parse the xml response from the server (with System.Xml.XmlReader).
            //
            // 5. Close the System.Xml.XmlReader from the previous step, to release the 
            //    connection to be used after.
            //--------------------------------------------------------------------------------
            
            // Step 1: start the XML/A request.
            System.Xml.XmlWriter xmlWriter = server.StartXmlaRequest(XmlaRequestType.Undefined);

            // Step 2: write the XML/A request.
            WriteSoapEnvelopeWithEmptyBatch(xmlWriter, server.SessionID);

            // Step 3: end the XML/A request and get the System.Xml.XmlReader for parsing the result from server.
            System.Xml.XmlReader xmlReader = server.EndXmlaRequest();

            // Step 4: read/parse the XML/A response from server.
            xmlReader.MoveToContent();
            string fullEnvelopeResponseFromServer = xmlReader.ReadOuterXml();

            // Step 5: close the System.Xml.XmlReader, to release the connection for future use.
            xmlReader.Close();


            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("========== XML/A RESPONSE ==========");
            Console.WriteLine(fullEnvelopeResponseFromServer);
        }


        /// <summary>
        /// Sends a custom SOAP Envelope request to Analysis Services and displays the result in Console.
        /// </summary>
        private static void SendXmlaRequestFromStream(Server server)
        {
            //--------------------------------------------------------------------------------
            // To run a custom full SOAP Envelope request from a stream, we
            // need to follow 3 steps:
            //
            // 1. Send the request from the input stream to the server. As for the StartXmlaRequest 
            //    method, we need to specify the request type (Discover or Execute).
            //    For native connections (direct over TCP/IP with DIME protocol), local 
            //    cube connections and stored procedures connections we don't need to
            //    specify the XML/A request type in advance (an Undefined value is
            //    available). But for HTTP and HTTPS connections we need to.
            //
            // 2. Read/Parse the xml response from the server (with System.Xml.XmlReader).
            //
            // 3. Close the System.Xml.XmlReader from the previous step, to release the 
            //    connection to be used after.
            //--------------------------------------------------------------------------------

            
            // In order to demostrate the use of the SendXmlaRequest method, we'll prepare
            // a file with an XML/A request (a SOAP Envelope with an empty Batch command).
            string tempFile = System.IO.Path.GetTempFileName();

            try
            {
                System.Xml.XmlTextWriter xmlWriter = new System.Xml.XmlTextWriter(tempFile, System.Text.Encoding.UTF8);
                try
                {
                    WriteSoapEnvelopeWithEmptyBatch(xmlWriter, server.SessionID);
                }
                finally
                {
                    xmlWriter.Close();
                }
    
                
                // STEP 1: send the request to server.
                System.IO.TextReader streamWithXmlaRequest = new System.IO.StreamReader(tempFile);
                System.Xml.XmlReader xmlaResponseFromServer; // will be used to parse the XML/A response from server

                try
                {
                    xmlaResponseFromServer = server.SendXmlaRequest(XmlaRequestType.Undefined, streamWithXmlaRequest);
                }
                finally
                {
                    streamWithXmlaRequest.Close();
                }

                // STEP 2: read/parse the XML/A response from server.
                xmlaResponseFromServer.MoveToContent();
                string fullEnvelopeResponseFromServer = xmlaResponseFromServer.ReadOuterXml();


                // STEP 3: close the System.Xml.XmlReader, to release the connection for future use.
                xmlaResponseFromServer.Close();


                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("========== XML/A RESPONSE ==========");
                Console.WriteLine(fullEnvelopeResponseFromServer);
            }
            finally
            {
                System.IO.File.Delete(tempFile);
            }
        }


        /// <summary>
        /// Writes a SOAP Envelope with an empty Batch command.
        /// </summary>
        /// <param name="xmlWriter">The System.Xml.XmlWriter to write to.</param>
        /// <param name="sessionId">The SessionId to be used for the request. Use null to create a sessionless request.</param>
        private static void WriteSoapEnvelopeWithEmptyBatch(System.Xml.XmlWriter xmlWriter, string sessionId)
        {
            //--------------------------------------------------------------------------------
            // To read about XML/A requests and their SOAP Envelope wrapper: http://www.xmla.org/.
            //--------------------------------------------------------------------------------


            //--------------------------------------------------------------------------------
            // This is the XML/A request we'll write:
            //
            // <Envelope xmlns="http://schemas.xmlsoap.org/soap/envelope/">
            //   <Header>
            //     <Session soap:mustUnderstand="1" SessionId="THE SESSION ID HERE" xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns="urn:schemas-microsoft-com:xml-analysis" />
            //   </Header>
            //   <Body>
            //     <Execute xmlns="urn:schemas-microsoft-com:xml-analysis">
            //       <Command>
            //         <Batch xmlns="http://schemas.microsoft.com/analysisservices/2003/engine" />
            //       </Command>
            //       <Properties>
            //         <PropertyList>
            //           <LocaleIdentifier>1033</LocaleIdentifier>
            //         </PropertyList>
            //       </Properties>
            //     </Execute>
            //   </Body>
            // </Envelope>
            //--------------------------------------------------------------------------------
            xmlWriter.WriteStartElement("Envelope", "http://schemas.xmlsoap.org/soap/envelope/");
            xmlWriter.WriteStartElement("Header");
            if (sessionId != null)
            {
                xmlWriter.WriteStartElement("Session", "urn:schemas-microsoft-com:xml-analysis");
                xmlWriter.WriteAttributeString("soap", "mustUnderstand", "http://schemas.xmlsoap.org/soap/envelope/", "1");
                xmlWriter.WriteAttributeString("SessionId", sessionId);
                xmlWriter.WriteEndElement(); // </Session>
            }
            xmlWriter.WriteEndElement(); // </Header>
            xmlWriter.WriteStartElement("Body");
            xmlWriter.WriteStartElement("Execute", "urn:schemas-microsoft-com:xml-analysis");
            xmlWriter.WriteStartElement("Command");
            xmlWriter.WriteStartElement("Batch", "http://schemas.microsoft.com/analysisservices/2003/engine");
            xmlWriter.WriteEndElement(); // </Batch>
            xmlWriter.WriteEndElement(); // </Command>
            xmlWriter.WriteStartElement("Properties");
            xmlWriter.WriteStartElement("PropertyList");
            xmlWriter.WriteElementString("LocaleIdentifier", System.Globalization.CultureInfo.CurrentCulture.LCID.ToString(System.Globalization.CultureInfo.InvariantCulture));
            xmlWriter.WriteEndElement(); // </PropertyList>
            xmlWriter.WriteEndElement(); // </Properties>
            xmlWriter.WriteEndElement(); // </Execute>
            xmlWriter.WriteEndElement(); // </Body>
            xmlWriter.WriteEndElement(); // </Envelope>
        }
    }
}


