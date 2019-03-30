using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System;
using DaxStudio.UI.Model;
using System.Threading.Tasks;
using System.Linq;
using DaxStudio.UI.Interfaces;
using System.ComponentModel.Composition;

namespace DaxStudio.UI
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    //[Export(typeof(ISettingProvider))]
    public class RegistrySettingProvider:ISettingProvider
    {
    
        private const string registryRootKey = "SOFTWARE\\DaxStudio";
        private const string REGISTRY_LAST_VERSION_CHECK_SETTING_NAME = "LastVersionCheckUTC";
        private const string REGISTRY_DISMISSED_VERSION_SETTING_NAME = "DismissedVersion";
        private const string REGISTRY_LAST_WINDOW_POSITION_SETTING_NAME = "WindowPosition";
        private const string DefaultPosition = @"﻿﻿<?xml version=""1.0"" encoding=""utf-8""?><WINDOWPLACEMENT xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""><length>44</length><flags>0</flags><showCmd>1</showCmd><minPosition><X>0</X><Y>0</Y></minPosition><maxPosition><X>-1</X><Y>-1</Y></maxPosition><normalPosition><Left>5</Left><Top>5</Top><Right>1125</Right><Bottom>725</Bottom></normalPosition></WINDOWPLACEMENT>";
        private const int MAX_MRU_SIZE = 25;

        public ObservableCollection<string> GetServerMRUList()
        {
            return GetMRUListFromRegistry("Server");
        }

        public void SaveServerMRUList(string currentServer, ObservableCollection<string> servers)
        {
            SaveListToRegistry("Server", currentServer, servers);
        }

        public ObservableCollection<DaxFile> GetFileMRUList()
        {
            var lst = new ObservableCollection<DaxFile>();
            foreach (var itm in  GetMRUListFromRegistry("File"))
            {
                lst.Add(new DaxFile(itm));
            }
            return lst;
        }

        public void SaveFileMRUList( IEnumerable<object> files)
        {
            SaveListToRegistry("File","", files);
        }

        public T GetValue<T>(string subKey, T defaultValue )
        {
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(registryRootKey, RegistryKeyPermissionCheck.ReadSubTree, System.Security.AccessControl.RegistryRights.QueryValues);
            if (regDaxStudio == null) return defaultValue;
            return (T)Convert.ChangeType(regDaxStudio.GetValue(subKey, defaultValue), typeof(T) );
        }

        public bool IsFileLoggingEnabled()
        {
            if (!KeyExists("IsFileLoggingEnabled")) return false;
            return GetValue<bool>("IsFileLoggingEnabled");
        }

        private bool KeyExists(string subKey)
        {
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(registryRootKey);
            return (regDaxStudio.GetSubKeyNames().ToList().Contains(subKey));
        }


        private T GetValue<T>(string subKey)
        {
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(registryRootKey);
            return (T)regDaxStudio.GetValue(subKey);
        }

        public Task SetValueAsync<T>(string subKey, T value)
        {
            return Task.Run(()=>{
                var regDaxStudio = Registry.CurrentUser.OpenSubKey(registryRootKey, true);
                if (regDaxStudio == null) { regDaxStudio = Registry.CurrentUser.CreateSubKey(registryRootKey); }
                regDaxStudio.SetValue(subKey, value);
            });
        }

        internal ObservableCollection<string> GetMRUListFromRegistry(string listName)
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



        internal void SaveListToRegistry(string listName, object currentItem, IEnumerable<object>itemList)
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
                    if (currentItem?.ToString().Length > 0)
                    {
                        regListMRU.SetValue(string.Format("{0}{1}", listName, i), currentItem);
                        i++;
                    }
                    foreach (object listItem in itemList)
                    {
                        if (i > MAX_MRU_SIZE) break; // don't save more than the max mru size
                        var str = listItem as string;
                        if (str == null) str = listItem.ToString();
                        if (string.Equals(str, currentItem.ToString(),StringComparison.CurrentCultureIgnoreCase)) continue;
                        regListMRU.SetValue(string.Format("{0}{1}",listName, i), str );
                        i++;
                    }
                }
            }
        }


        public void SetLastVersionCheck(DateTime value)
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

        public DateTime GetLastVersionCheck()
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


        public void SetWindowPosition(string value)
        {
            string path = registryRootKey;
            RegistryKey settingKey = Registry.CurrentUser.OpenSubKey(path, true);
            if (settingKey == null) settingKey = Registry.CurrentUser.CreateSubKey(path);
            using (settingKey)
            {
                settingKey.SetValue(REGISTRY_LAST_WINDOW_POSITION_SETTING_NAME, value, RegistryValueKind.String);
            }
        }

        public string GetWindowPosition()
        {
            DateTime dtReturnVal = DateTime.MinValue;
            RegistryKey rk = Registry.CurrentUser.OpenSubKey(registryRootKey);
            string pos = String.Empty;
            if (rk != null)
            {
                using (rk)
                {
                    pos = (string)rk.GetValue(REGISTRY_LAST_WINDOW_POSITION_SETTING_NAME, DefaultPosition);
                }
            }
            return pos;
        }


        public string GetDismissedVersion()
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

        public void SetDismissedVersion(string value)
        {
            string path = registryRootKey;
            RegistryKey settingKey = Registry.CurrentUser.OpenSubKey(path, true);
            if (settingKey == null) settingKey = Registry.CurrentUser.CreateSubKey(path);
            settingKey.SetValue(REGISTRY_DISMISSED_VERSION_SETTING_NAME, value, RegistryValueKind.String);
            settingKey.Close();
        }

    }
}
