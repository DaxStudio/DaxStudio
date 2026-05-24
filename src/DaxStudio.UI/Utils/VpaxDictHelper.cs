using System.IO;
using System.Windows.Forms;
using DaxStudio.Core.Vpax;

namespace DaxStudio.UI.Utils
{
    public static class VpaxDictHelper
    {
        public static string GetDictPathForOvpax(string filename)
        {
            var dictFilePath = ModelAnalyzer.GetDefaultDictFile(filename);

            // check if multiple dict files exist
            var allDictFiles = Directory.GetFiles(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(dictFilePath) + "*.dict");

            // only 1 file matches the pattern so return that
            if (allDictFiles.Length == 1 && File.Exists(dictFilePath)) return dictFilePath;

            // if the default dict file does not exist ask the user
            var dlg = new OpenFileDialog()
            {
                InitialDirectory = Path.GetDirectoryName(filename),
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
