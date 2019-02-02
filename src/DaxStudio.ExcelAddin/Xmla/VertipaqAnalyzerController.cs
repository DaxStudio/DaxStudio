using System.Data;
using System.Web.Http;
using System.Web.Http.Controllers;
using Microsoft.AspNet;

namespace DaxStudio.Xmla
{
    [RoutePrefix("vertipaqanalyzer")]
    public class VertipaqAnalyzerController: ApiController, IControllerConfiguration
    {
        [HttpGet]
        [Route("TMSCHEMA_TABLES")]
        public DataTable GetTmschema_Tables()
        {
            var conn = new ADOTabular.ADOTabularConnection("data source=localhost\\tab17;initial catalog=Adventure Works",   ADOTabular.AdomdClientWrappers.AdomdType.AnalysisServices);
            var ds = conn.GetSchemaDataSet("TMSCHEMA_TABLES");
            return ds.Tables[0];
        }

        public void Initialize(HttpControllerSettings controllerSettings,
                                   HttpControllerDescriptor controllerDescriptor)
        {
            // Register an additional plain text media type formatter
            controllerSettings.Formatters.Add(new System.ServiceModel.Syndication.Rss20FeedFormatter());
            //controllerSettings.Formatters.Add(new System.Net.Http.Formatting.XmlMediaTypeFormatter() ); // SyndicationMediaTypeFormatter());
        }
    }
}
