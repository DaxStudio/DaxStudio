extern alias ExcelAmo;

using System;
using Serilog;

namespace DaxStudio.ExcelAddin.AmoWrapper
{
    public enum AmoType
    {
        AnalysisServices,
        Excel
    }
    public class AmoServer:IDisposable
    {
        internal delegate void VoidDelegate();
        internal delegate T ReturnDelegate<T>();

        Microsoft.AnalysisServices.Server _svr;
        ExcelAmo.Microsoft.AnalysisServices.Server _xlSvr;
        AmoType _type;

        public AmoServer(AmoType amoType)
        {
            _type = amoType;
            if (amoType == AmoType.AnalysisServices)
            {
                Log.Debug("{class} {method} {message}","AmoServer","<constructor>","Using Microsoft.AnalysisServices");
                _svr = new Microsoft.AnalysisServices.Server();
            }
            else
            {
                Log.Debug("{class} {method} {message}", "AmoServer", "<constructor>", "Using Microsoft.Excel.Amo");
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


        public System.Xml.XmlReader SendXmlaRequest( System.IO.TextReader textReader)
        {
            if (_type == AmoType.AnalysisServices )
            {
                //var rt = (Microsoft.AnalysisServices.XmlaRequestType)requestType;
                return _svr.SendXmlaRequest(0, textReader);
            }
            else
            {
                AmoServer.ReturnDelegate<System.Xml.XmlReader> f = delegate
                {
                    //var req = (ExcelAmo.Microsoft.AnalysisServices.XmlaRequestType)requestType;
                    return _xlSvr.SendXmlaRequest(0, textReader);
                };
                return f();
            }

        }

        public void Dispose()
        {
            if (_svr != null)
            {
                try
                {
                    _svr.Dispose();
                }
                catch
                {
                    // swallow the exception as we should be cleaning up the object anyway
                }

            }
        }
    }
}
