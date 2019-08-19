using ADOTabular;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.AnalysisServices;
using DaxStudio.UI.Extensions;
using System.IO.Packaging;
using System.IO;
using Newtonsoft.Json;
using System.Data.OleDb;

namespace DaxStudio.UI.Utils
{

    public static class ModelAnalyzer
    {
        /// <summary>
        /// Export to VertiPaq Analyzer (VPAX) file
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="pathOutput"></param>
        /// <param name="export"></param>
        public static void ExportVPAX(string path, Dax.Model.Model model, Dax.ViewVpaExport.Model export)
        {
            Uri uriModel = PackUriHelper.CreatePartUri(new Uri("DaxModel.json", UriKind.Relative));
            Uri uriModelVpa = PackUriHelper.CreatePartUri(new Uri("DaxModelVpa.json", UriKind.Relative));
            using (Package package = Package.Open(path, FileMode.Create))
            {
                using (TextWriter tw = new StreamWriter(package.CreatePart(uriModel, "application/json", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
                {
                    tw.Write(
                        JsonConvert.SerializeObject(
                            model,
                            Formatting.Indented,
                            new JsonSerializerSettings
                            {
                                PreserveReferencesHandling = PreserveReferencesHandling.All,
                                ReferenceLoopHandling = ReferenceLoopHandling.Serialize
                            }
                        )
                    );
                    tw.Close();
                }
                using (TextWriter tw = new StreamWriter(package.CreatePart(uriModelVpa, "application/json", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
                {
                    tw.Write(JsonConvert.SerializeObject(export, Formatting.Indented));
                    tw.Close();
                }
                package.Close();
            }
        }

        public static void ExportVPAX(string serverName, string databaseName, string path, string applicationName, string applicationVersion)
        {
            //
            // Get Dax.Model object from the SSAS engine
            //
            Dax.Model.Model model = Dax.Model.Extractor.TomExtractor.GetDaxModel(serverName, databaseName, applicationName, applicationVersion);

            // 
            // Create VertiPaq Analyzer views
            //
            Dax.ViewVpaExport.Model export = new Dax.ViewVpaExport.Model(model);

            // Save VPAX file
            ModelAnalyzer.ExportVPAX(path, model, export);
        }
        
    }
}
