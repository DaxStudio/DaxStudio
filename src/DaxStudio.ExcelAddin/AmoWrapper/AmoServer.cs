extern alias ExcelAmo;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AnalysisServices;
using Serilog;

namespace DaxStudio.ExcelAddin.AmoWrapper
{
    public enum AmoType
    {
        AnalysisServices,
        Excel
    }
    public class AmoServer
    {
        internal delegate void VoidDelegate();
        internal delegate T ReturnDelegate<T>();

        Server _svr;
        ExcelAmo.Microsoft.AnalysisServices.Server _xlSvr;
        AmoType _type;

        public AmoServer(AmoType amoType)
        {
            _type = amoType;
            if (amoType == AmoType.AnalysisServices)
            {
                Log.Verbose("{class} {method} {message}","AmoServer","<constructor>","Using Microsoft.AnalysisServices");
                _svr = new Server();
            }
            else
            {
                Log.Verbose("{class} {method} {message}", "AmoServer", "<constructor>", "Using Microsoft.Excel.Amo");
                AmoServer.VoidDelegate f = delegate
                {
                    _xlSvr = new ExcelAmo.Microsoft.AnalysisServices.Server();
                };
                f();

            }
        }

        public void Connect(string connectionString)
        {

            if (_type == AmoType.AnalysisServices)
            {
                _svr.Connect(connectionString);
            }
            else
            {
                AmoServer.VoidDelegate f = delegate
                {
                    _xlSvr.Connect(connectionString);
                };
                f();
            }

        }

        public System.Xml.XmlReader SendXmlaRequest(Microsoft.AnalysisServices.XmlaRequestType requestType, System.IO.TextReader textReader)
        {
            if (_type == AmoType.AnalysisServices )
            {
                return _svr.SendXmlaRequest(requestType, textReader);
            }
            else
            {
                AmoServer.ReturnDelegate<System.Xml.XmlReader> f = delegate
                {
                    var req = (ExcelAmo.Microsoft.AnalysisServices.XmlaRequestType)requestType;
                    return _xlSvr.SendXmlaRequest(req, textReader);
                };
                return f();
            }

        }
    }
}
