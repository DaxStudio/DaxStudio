using Newtonsoft.Json;
using Owin;
using System.Collections.Generic;
using System.Net.Http.Formatting;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using DaxStudio.Interfaces;

namespace DaxStudio.ExcelAddin.Xmla
{
    public class Startup
    {
        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public void Configuration( IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            HttpConfiguration config = new HttpConfiguration();
            /*
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
            config.Routes.MapHttpRoute(
                name: "WorkbookApi",
                routeTemplate: "api/{controller}/{action}", 
                defaults: new { id = RouteParameter.Optional}
                );
             */
            config.MapHttpAttributeRoutes();
            /*config.Formatters.Add(new JsonMediaTypeFormatter
            {
                SerializerSettings = new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter>
                    {
                        //list of your converters
                        new JsonDataTableConverter()
                    }
                }
            });*/
            config.Services.Add( typeof(IExceptionLogger), new TraceExceptionLogger());
            config.Formatters.Clear();
            config.Formatters.Add(new JsonMediaTypeFormatter());

            appBuilder.UseWebApi(config);
        }
    }
}