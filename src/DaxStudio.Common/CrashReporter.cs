using CrashReporterDotNET;
using System;
using System.Globalization;

namespace DaxStudio.UI.Utils
{
    public static class CrashReporter
    {
        private static Guid applicationID = new Guid("ca045521-7046-4979-9db9-3418a1352e94");
        
        public static void ReportCrash(Exception exception, string developerMessage)
        {
            var reportCrash = new ReportCrash
            {
                CurrentCulture = new CultureInfo("en-US"),
                AnalyzeWithDoctorDump = true,
                DeveloperMessage = developerMessage,
                ToEmail = "daxstudiocrash@gmail.com",
                DoctorDumpSettings = new DoctorDumpSettings()
                {
                    ApplicationID = applicationID,
                    OpenReportInBrowser = false,
                    SendAnonymousReportSilently = true
                }
            };

            reportCrash.Send(exception);

        }
        
    }
}
