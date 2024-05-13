using System.IO;
using Dax.Vpax.Tools;
using static Dax.Vpax.Tools.VpaxTools;
using Dax.Vpax.Obfuscator.Common;
using Dax.Vpax.Obfuscator;
using System.Windows;
using ModernWpf.Controls;
using System.Windows.Forms;
using Dax.Metadata;
#nullable enable
namespace DaxStudio.UI.Utils
{

    public static class ModelAnalyzer
    {
        const string dictExtension = ".dict";
        const string ovpaxExtension = ".ovpax";

        public static void ExportVPAX(string connectionString, string path, string dictionaryPath,string inputDictionaryPath, bool includeTomModel, string applicationName, string applicationVersion, bool readStatisticsFromData, string modelName, bool readStatisticsFromDirectQuery, DirectLakeExtractionMode directLakeMode)
        {

            //
            // Get Dax.Model object from the SSAS engine
            //
            Dax.Metadata.Model daxModel = Dax.Model.Extractor.TomExtractor.GetDaxModel(connectionString, applicationName, applicationVersion,
                                                                                       readStatisticsFromData: readStatisticsFromData,
                                                                                       sampleRows: 0,
                                                                                       analyzeDirectQuery: readStatisticsFromDirectQuery, analyzeDirectLake: directLakeMode);

            //
            // Get TOM model from the SSAS engine
            //
            Microsoft.AnalysisServices.Tabular.Database? tomDatabase = includeTomModel ? Dax.Model.Extractor.TomExtractor.GetDatabase(connectionString) : null;

            // 
            // Create VertiPaq Analyzer views
            //
            Dax.ViewVpaExport.Model vpaModel = new Dax.ViewVpaExport.Model(daxModel);

            daxModel.ModelName = new Dax.Metadata.DaxName(modelName);

            //
            // Save VPAX file
            // 
            // TODO: export of database should be optional
            //Dax.Vpax.Tools.VpaxTools.ExportVpax(path, daxModel, vpaModel, tomDatabase);


            WriteVpaxFile(path, dictionaryPath, inputDictionaryPath, daxModel, tomDatabase, vpaModel);

        }

        private static void WriteVpaxFile(string path, string dictionaryPath, string inputDictionaryPath, Dax.Metadata.Model daxModel, Microsoft.AnalysisServices.Tabular.Database? tomDatabase, Dax.ViewVpaExport.Model vpaModel)
        {
            if (string.IsNullOrEmpty(dictionaryPath)) // If null, no obfuscation is required
            {
                VpaxTools.ExportVpax(path, daxModel, vpaModel, tomDatabase);
            }
            else
            {
                using var stream = new MemoryStream();
                VpaxTools.ExportVpax(stream, daxModel);

                var inputDictionary = string.IsNullOrEmpty(inputDictionaryPath) ? null : ObfuscationDictionary.ReadFrom(inputDictionaryPath);
                var obfuscator = new VpaxObfuscator();
                var dictionary = obfuscator.Obfuscate(stream, inputDictionary);
                dictionary.WriteTo(dictionaryPath, overwrite: false, indented: true); // To prevent loss of the dictionary, always deny overwrite

                using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
                stream.Position = 0;
                stream.CopyTo(fileStream);

            }
        }

        public static void ExportExistingModelToVPAX(string filename,string dictionaryPath,string inputDictionaryPath, Dax.Metadata.Model model, Dax.ViewVpaExport.Model viewVpa, Microsoft.AnalysisServices.Tabular.Database database)
        {
            WriteVpaxFile(filename, dictionaryPath, inputDictionaryPath, model, database, viewVpa);
        }

        public static VpaxContent ImportVPAX(string filename)
        {
            // TODO add logic to de-obfuscate
            var content = VpaxTools.ImportVpax(filename);
            return content;
        }

        public static string GetDefaultDictFile(string filename)
        {
            if (!filename.EndsWith(ovpaxExtension, System.StringComparison.OrdinalIgnoreCase)) { return string.Empty; }
            return Path.Combine(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(filename) + dictExtension);
        }

        public static string GetDictPathForOvpax(string filename)
        {
            var dictFilePath = GetDefaultDictFile(filename);

            // check if multiple dict files exist
            var allDictFiles = Directory.GetFiles(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(dictFilePath) + "*.dict");

            // only 1 file matches the pattern so return that
            if (allDictFiles.Length == 1 && File.Exists(dictFilePath)) return dictFilePath;

            // if the default dict file does not exist ask the user
            var dlg = new OpenFileDialog()
            {
                InitialDirectory= Path.GetDirectoryName(filename),
                FileName = Path.GetFileNameWithoutExtension(dictFilePath) + "*.dict",
                Title = "Select the .dict file to use",
                Filter = "Obfuscation Dictionary|*.dict",
                DefaultExt = ".dict",
                Multiselect = false
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            { return dlg.FileName; }

            // if the dialog was cancelled then return an empty string
            return string.Empty;
        }
    }
}
