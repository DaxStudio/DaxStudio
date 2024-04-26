using System;
using CrashReporterDotNET;
using Serilog;

namespace DaxStudio.Common
{
    public static class CrashReporter
    {
        private static readonly Guid ApplicationId = new Guid("ca045521-7046-4979-9db9-3418a1352e94");
        
        public static void ReportCrash(Exception exception, string developerMessage)
        {
            Log.Error(exception, "About to call CrashReporter");
            Log.CloseAndFlush();

            Telemetry.TrackException(exception,developerMessage);

            var reportCrash = new ReportCrash("daxstudiocrash@gmail.com")
            {
                AnalyzeWithDoctorDump = true,
                DeveloperMessage = developerMessage,
                DoctorDumpSettings = new DoctorDumpSettings()
                {
                    ApplicationID = ApplicationId,
                    OpenReportInBrowser = true,  // open DrDump report page
                    SendAnonymousReportSilently = true
                }
            };

            reportCrash.Send(exception);

        }
        
    }
}
