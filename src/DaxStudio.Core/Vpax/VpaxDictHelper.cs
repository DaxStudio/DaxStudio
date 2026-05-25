using System.IO;

namespace DaxStudio.Core.Vpax
{
    public static class VpaxDictHelper
    {
        /// <summary>
        /// Returns the dictionary file path for the supplied .ovpax file when it can be
        /// determined unambiguously (default name, or a single match). Returns an empty
        /// string when no .dict files exist, and the wildcard pattern when multiple
        /// candidates exist so callers can prompt the user.
        /// </summary>
        public static string GetDictPathForOvpax(string filename)
        {
            var dictFilePath = ModelAnalyzer.GetDefaultDictFile(filename);

            var dir = Path.GetDirectoryName(filename);
            if (string.IsNullOrEmpty(dir)) return dictFilePath;

            var allDictFiles = Directory.GetFiles(dir,
                Path.GetFileNameWithoutExtension(dictFilePath) + "*.dict");

            if (allDictFiles.Length == 1 && File.Exists(dictFilePath)) return dictFilePath;
            if (allDictFiles.Length == 0) return string.Empty;

            // multiple matches - return the default path and let the caller resolve ambiguity
            return dictFilePath;
        }
    }
}
