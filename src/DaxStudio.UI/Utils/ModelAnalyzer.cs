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
using Dax.ViewModel;
using Dax.Vpax.Tools;
using static Dax.Vpax.Tools.VpaxTools;

namespace DaxStudio.UI.Utils
{

    public static class ModelAnalyzer
    {
        public static void ExportVPAX(Microsoft.AnalysisServices.Tabular.Model model, string path, bool includeTomModel, string applicationName, string applicationVersion, bool readStatisticsFromData, string modelName, bool readStatisticsFromDirectQuery)
        {
            //
            // Get Dax.Model object from the SSAS engine
            //
            Dax.Metadata.Model daxModel = Dax.Metadata.Extractor.TomExtractor.GetDaxModel(model, applicationName, applicationVersion);

            //
            // Get TOM model from the SSAS engine
            //
            Microsoft.AnalysisServices.Tabular.Database database = includeTomModel ? (Microsoft.AnalysisServices.Tabular.Database)model.Database : null;

            // 
            // Create VertiPaq Analyzer views
            //
            Dax.ViewVpaExport.Model viewVpa = new Dax.ViewVpaExport.Model(daxModel);

            daxModel.ModelName = new Dax.Metadata.DaxName(modelName);

            //
            // Save VPAX file
            // 
            // TODO: export of database should be optional
            Dax.Vpax.Tools.VpaxTools.ExportVpax(path, daxModel, viewVpa, database);
        }
        public static void ExportVPAX(string serverName, string databaseName, string path, bool includeTomModel, string applicationName, string applicationVersion, bool readStatisticsFromData, string modelName, bool readStatisticsFromDirectQuery)
        {
            //
            // Get Dax.Model object from the SSAS engine
            //
            Dax.Metadata.Model model = Dax.Metadata.Extractor.TomExtractor.GetDaxModel( serverName, databaseName, applicationName, applicationVersion, 
                                                                                       readStatisticsFromData: readStatisticsFromData, 
                                                                                       sampleRows: 0,
                                                                                       analyzeDirectQuery: readStatisticsFromDirectQuery);
            
            //
            // Get TOM model from the SSAS engine
            //
            Microsoft.AnalysisServices.Tabular.Database database = includeTomModel ? Dax.Metadata.Extractor.TomExtractor.GetDatabase(serverName, databaseName): null;

            // 
            // Create VertiPaq Analyzer views
            //
            Dax.ViewVpaExport.Model viewVpa = new Dax.ViewVpaExport.Model(model);

            model.ModelName = new Dax.Metadata.DaxName(modelName);

            //
            // Save VPAX file
            // 
            // TODO: export of database should be optional
            Dax.Vpax.Tools.VpaxTools.ExportVpax(path, model, viewVpa, database);
        }

        public static void ExportVPAX(string connectionString, string path, bool includeTomModel, string applicationName, string applicationVersion, bool readStatisticsFromData, string modelName, bool readStatisticsFromDirectQuery)
        {
            //
            // Get Dax.Model object from the SSAS engine
            //
            Dax.Metadata.Model model = Dax.Metadata.Extractor.TomExtractor.GetDaxModel( connectionString, applicationName, applicationVersion,
                                                                                       readStatisticsFromData: readStatisticsFromData,
                                                                                       sampleRows: 0,
                                                                                       analyzeDirectQuery: readStatisticsFromDirectQuery);

            //
            // Get TOM model from the SSAS engine
            //
            Microsoft.AnalysisServices.Tabular.Database database = includeTomModel ? Dax.Metadata.Extractor.TomExtractor.GetDatabase(connectionString) : null;

            // 
            // Create VertiPaq Analyzer views
            //
            Dax.ViewVpaExport.Model viewVpa = new Dax.ViewVpaExport.Model(model);

            model.ModelName = new Dax.Metadata.DaxName(modelName);

            //
            // Save VPAX file
            // 
            // TODO: export of database should be optional
            Dax.Vpax.Tools.VpaxTools.ExportVpax(path, model, viewVpa, database);
        }

        public static void ExportExistingModelToVPAX(string filename, Dax.Metadata.Model model, Dax.ViewVpaExport.Model viewVpa, Microsoft.AnalysisServices.Tabular.Database database)
        {
            VpaxTools.ExportVpax(filename, model, viewVpa, database);
        }

        public static VpaxContent ImportVPAX(string filename)
        {
            var content = VpaxTools.ImportVpax(filename);
            return content;
        }
        
    }
}
