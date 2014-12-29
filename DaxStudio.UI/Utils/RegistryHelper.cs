using System.Collections.Generic;
using System.Collections.ObjectModel;
//using System.Windows.Forms;
using Microsoft.Win32;
//using ComboBox = System.Windows.Forms.ComboBox;
using System;

namespace DaxStudio.UI
{
    public static class RegistryHelper
    {
    
        private const string registryRootKey = "SOFTWARE\\DaxStudio";
        private const string REGISTRY_LAST_VERSION_CHECK_SETTING_NAME = "LastVersionCheckUTC";
        private const string REGISTRY_DISMISSED_VERSION_SETTING_NAME = "DismissedVersion";

        public static ObservableCollection<string> GetServerMRUListFromRegistry()
        {
            return GetMRUListFromRegistry("Server");
        }

        public static void SaveServerMRUListToRegistry(string currentServer, ObservableCollection<string> servers)
        {
            SaveListToRegistry("Server", currentServer, servers);
        }

        public static ObservableCollection<string> GetFileMRUListFromRegistry()
        {
            return GetMRUListFromRegistry("File");
        }

        public static void SaveFileMRUListToRegistry(string currentServer, ObservableCollection<string> servers)
        {
            SaveListToRegistry("File", currentServer, servers);
        }

        internal static ObservableCollection<string> GetMRUListFromRegistry(string listName)
        {
            var list = new ObservableCollection<string>();
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(registryRootKey);
            if (regDaxStudio != null)
            {
                var regListMRU = regDaxStudio.OpenSubKey(string.Format("{0}MRU",listName));
                if (regListMRU != null)
                    foreach (var svr in regListMRU.GetValueNames())
                    {
                        var itmName = regListMRU.GetValue(svr).ToString();
                        list.Add(itmName);
                    }
            }

            return list;
        }



        internal static void SaveListToRegistry(string listName, string currentItem, ObservableCollection<string>itemList)
        {
            var listKey = string.Format("{0}MRU", listName);
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(registryRootKey, RegistryKeyPermissionCheck.ReadWriteSubTree);
            if (regDaxStudio == null)
                Registry.CurrentUser.CreateSubKey(registryRootKey);
            if (regDaxStudio != null)
            {
                // clear existing data
                regDaxStudio.DeleteSubKeyTree(listKey, false);
                var regListMRU = regDaxStudio.CreateSubKey(listKey, RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (regListMRU != null)
                {
                    int i = 1;
                    // set current item as item 1
                    regListMRU.SetValue(string.Format("{0}{1}",listName, i), currentItem);
                    foreach (string listItem in itemList)
                    {
                        i++;
                        if (listItem == currentItem) continue;
                        regListMRU.SetValue(string.Format("{0}{1}",listName, i), listItem );
                    }
                }
            }
        }


        public static void SetLastVersionCheck(DateTime value)
        {
            string path = registryRootKey;
            RegistryKey settingKey = Registry.CurrentUser.OpenSubKey(path, true);
            if (settingKey == null) settingKey = Registry.CurrentUser.CreateSubKey(path);
            using (settingKey)
            {
                var strDate = value.ToUniversalTime().ToString("s", System.Globalization.CultureInfo.InvariantCulture);
                settingKey.SetValue(REGISTRY_LAST_VERSION_CHECK_SETTING_NAME, strDate, RegistryValueKind.String);
            }
        }

        public static DateTime GetLastVersionCheck()
        {
            DateTime dtReturnVal = DateTime.MinValue;
            RegistryKey rk = Registry.CurrentUser.OpenSubKey(registryRootKey);
            if (rk != null)
            {
                using (rk)
                {
                    DateTime.TryParse((string)rk.GetValue(REGISTRY_LAST_VERSION_CHECK_SETTING_NAME, DateTime.MinValue.ToShortDateString()), out dtReturnVal);
                }
            }

            return dtReturnVal.ToLocalTime();
        }


        public static string GetDismissedVersion()
        {
            string sReturnVal = string.Empty;
            RegistryKey rk = Registry.CurrentUser.OpenSubKey(registryRootKey);
            if (rk != null)
            {
                sReturnVal = (string)rk.GetValue(REGISTRY_DISMISSED_VERSION_SETTING_NAME, string.Empty);
                rk.Close();
            }

            return string.IsNullOrEmpty(sReturnVal)?"0.0.0.0":sReturnVal;
        }

        public static void SetDismissedVersion(string value)
        {
            string path = registryRootKey;
            RegistryKey settingKey = Registry.CurrentUser.OpenSubKey(path, true);
            if (settingKey == null) settingKey = Registry.CurrentUser.CreateSubKey(path);
            settingKey.SetValue(REGISTRY_DISMISSED_VERSION_SETTING_NAME, value, RegistryValueKind.String);
            settingKey.Close();
        }

    }
}
