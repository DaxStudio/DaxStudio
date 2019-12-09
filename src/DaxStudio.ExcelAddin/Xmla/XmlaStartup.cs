using DaxStudio.Interfaces;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Configuration;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using Owin;
using System.Collections.Generic;
using System.Net.Http.Formatting;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using System;
using Serilog;
using DaxStudio.Xmla;

namespace DaxStudio.ExcelAddin.Xmla
{
    public class Startup
    {
        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
        public void Configuration( IAppBuilder appBuilder)
        {
            Serilog.Log.Debug("{class} {method} {message}", "Startup", "Configuration", "Starting OWIN configuration");
            // Configure Web API for self-host. 
            try {
                using (HttpConfiguration config = new HttpConfiguration())
                {

                    appBuilder.Use<UnhandledExceptionMiddleware>();
                    appBuilder.Map("/signalr", map =>
                    {
                        var hubConfiguration = new HubConfiguration
                        {
                            EnableDetailedErrors = true
                        };
                        map.RunSignalR(hubConfiguration);
                    });

                    config.MapHttpAttributeRoutes();
                    config.Services.Add(typeof(IExceptionLogger), new TraceExceptionLogger());
                    config.Formatters.Clear();
                    config.Formatters.Add(new JsonMediaTypeFormatter());
                    config.Formatters.Add(new XmlMediaTypeFormatter());
                    config.Formatters.JsonFormatter.SerializerSettings.TypeNameHandling = TypeNameHandling.All;

                    appBuilder.UseWebApi(config);
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message}","Startup", "Configuration", ex.Message);
            }
            Serilog.Log.Debug("{class} {method} {message}", "Startup", "Configuration", "Finished OWIN configuration");
        }
    }
}