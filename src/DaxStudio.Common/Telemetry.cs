using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Serilog;

namespace DaxStudio.Common
{
    /*
    usage:  Telemetry.TrackEvent("CreateComparisonInitialized", new Dictionary<string, string> { { "App", comparisonInfo.AppName.Replace(" ", "") } });
    */

    public static class Telemetry
    {
        // instrumentation key from Azure Portal
        // this should be unique to the calling app, do not use this same guid in other applications
        private const string TelemetryKey = "06a7c6f2-d406-4f90-8a9d-6b367503c22d"; 

        private static readonly TelemetryClient _telemetryClient = GetAppInsightsClient();

        public static bool Enabled { get; set; } = true;

        private static TelemetryClient GetAppInsightsClient()
        {
            var config = new TelemetryConfiguration();
            config.InstrumentationKey = TelemetryKey;
            config.TelemetryChannel = new Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel.ServerTelemetryChannel();
            config.TelemetryChannel.DeveloperMode = Debugger.IsAttached;
    #if DEBUG
            config.TelemetryChannel.DeveloperMode = true;
    #endif
            TelemetryClient client = new TelemetryClient(config);
            client.Context.Component.Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            client.Context.Session.Id = Guid.NewGuid().ToString();
            client.Context.User.Id = (Environment.UserName + Environment.MachineName).GetHashCode().ToString();
            return client;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any errors while tracking events should be ignored")]
        public static void TrackEvent(string key, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            if (Enabled)
            {
                try
                {
                    _telemetryClient.TrackEvent(key, properties, metrics);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{class} {method} {message}", nameof(Telemetry), nameof(TrackEvent), $"Error tracking event: {key} Message: {ex.Message}");
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any errors while tracking exceptions should be ignored")]
        public static void TrackException(Exception ex,string source)
        {

            if (ex != null && Enabled)
            {
                try
                {
                    var telex = new Microsoft.ApplicationInsights.DataContracts.ExceptionTelemetry(ex);
                    telex.Properties.Add("Source", source);
                    _telemetryClient.TrackException(telex);
                    Flush();
                }
                catch(Exception trackEx)
                {
                    Log.Fatal(ex, "{class} {method} {message}", nameof(Telemetry), nameof(TrackException), trackEx.Message);
                }
            }
        }

        public static void Flush()
        {
            _telemetryClient.Flush();

        }
    }
}
