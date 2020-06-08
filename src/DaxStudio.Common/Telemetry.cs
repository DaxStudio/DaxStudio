using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Serilog;

namespace DaxStudio.UI.Utils
{
    /*
    useage:  Telemetry.TrackEvent("CreateComparisonInitialized", new Dictionary<string, string> { { "App", comparisonInfo.AppName.Replace(" ", "") } });
    */

    public static class Telemetry
{
    private const string TelemetryKey = "06a7c6f2-d406-4f90-8a9d-6b367503c22d"; // instrumentation key from Azure Portal

    private static TelemetryClient _telemetry = GetAppInsightsClient();

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

    public static void TrackEvent(string key, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
    {
        if (Enabled)
        {
                try
                {
                    _telemetry.TrackEvent(key, properties, metrics);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{class} {method} {message}", nameof(Telemetry), nameof(TrackEvent), $"Error tracking event: {key} Message: {ex.Message}");
                }
        }
    }

    public static void TrackException(Exception ex,string source)
    {

        if (ex != null && Enabled)
        {
                try
                {
                    var telex = new Microsoft.ApplicationInsights.DataContracts.ExceptionTelemetry(ex);
                    telex.Properties.Add("Source", source);
                    _telemetry.TrackException(telex);
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
        _telemetry.Flush();

    }
}
}
