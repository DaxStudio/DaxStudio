using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    // This class is used to convert OneDrive https:// urls to the equivalent local path when opening PowerPivot files 
    // that are stored on OneDrive (since we need to pass in the local path on the connection string)
    public static class OneDriveHelper
    {
        private static string ConsumerOneDrivePrefix = "https://d.docs.live.net";
        private static readonly Regex ConsumerOneDriveRegex = new Regex("https://d.docs.live.net/(?:[^/]+/)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex CommercialOneDriveRegex = new Regex("https://(?:.+)-my.sharepoint.com/",RegexOptions.IgnoreCase | RegexOptions.Compiled );
        private static readonly string OneDriveConsumerLocalRoot = System.Environment.GetEnvironmentVariable("OneDriveConsumer");
        private static readonly string OneDriveCommercialLocalRoot = System.Environment.GetEnvironmentVariable("OneDriveCommercial");

        #region Public Methods
        public static bool IsOneDrivePath(string path)
        {
            if (ConsumerOneDriveRegex.IsMatch(path)) return true;
            if (CommercialOneDriveRegex.IsMatch(path)) return true;
            return false;
        }

        public static string ConvertToLocalPath(string oneDriveUrl)
        {
            if (IsConsumerOneDrive(oneDriveUrl))
            {
                return ConvertConsumerOneDriveToLocalPath(oneDriveUrl);
            }
            else
            {
                return ConvertCommercialOneDriveToLocalPath(oneDriveUrl);
            }
        }
        #endregion

        #region Private Methods
        private static string ConvertCommercialOneDriveToLocalPath(string oneDriveUrl)
        {
            var result = CommercialOneDriveRegex.Replace(oneDriveUrl, "").Replace('/', '\\');
            return Path.Combine(OneDriveCommercialLocalRoot, result);
        }

        private static string ConvertConsumerOneDriveToLocalPath(string oneDriveUrl)
        {
            var parts = oneDriveUrl.Split('/');
            return parts[parts.Length - 1]; // return just the filename

            //var result = ConsumerOneDriveRegex.Replace(oneDriveUrl, "").Replace('/','\\');
            //return Path.Combine( OneDriveConsumerLocalRoot, result);

        }

        private static bool IsConsumerOneDrive(string oneDriveUrl)
        {
            return oneDriveUrl.StartsWith(ConsumerOneDrivePrefix, StringComparison.OrdinalIgnoreCase);
        }
        #endregion
    }
}
