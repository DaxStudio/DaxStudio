using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System;
using DaxStudio.UI.Model;
using System.Linq;
using DaxStudio.UI.Interfaces;
using System.ComponentModel.Composition;
using DaxStudio.Common;
using DaxStudio.Interfaces;
using System.ComponentModel;
using System.Security;
using System.Runtime.Serialization;
using System.Globalization;
using System.Reflection;

namespace DaxStudio.UI.Utils
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    //[Export(typeof(ISettingProvider))]
    public class RegistrySettingProvider : ISettingProvider
    {

        private const string RegistryRootKey = "SOFTWARE\\DaxStudio";


        public string LogPath => ApplicationPaths.LogPath;
        public bool IsRunningPortable => false;
        public string SettingsFile => string.Empty;

        public ObservableCollection<string> GetServerMRUList()
        {
            return GetMRUListFromRegistry("Server");
        }

        public void SaveServerMRUList(string currentServer, ObservableCollection<string> servers)
        {
            var existingIdx = servers.IndexOf(currentServer);

            // server is already first in the list
            if (existingIdx == 0) return; // do nothing

            if (existingIdx > 0)
            {
                // server exists, make it first in the list
                servers.Move(existingIdx, 0);
            }
            else
            {
                // server does not exist in list, so insert it as the first item
                servers.Insert(0, currentServer);
                while (servers.Count > Constants.MaxMruSize)
                {
                    servers.RemoveAt(servers.Count - 1);
                }
            }


            SaveListToRegistry("Server", currentServer, servers);
        }

        public ObservableCollection<IDaxFile> GetFileMRUList()
        {
            var lst = new ObservableCollection<IDaxFile>();
            foreach (var itm in GetMRUListFromRegistry("File"))
            {
                lst.Add(new DaxFile(itm));
            }
            return lst;
        }

        public void SaveFileMRUList(IDaxFile file, ObservableCollection<IDaxFile> files)
        {
            var existingItem = files.FirstOrDefault(f => f.FullPath.Equals(file.FullPath, StringComparison.CurrentCultureIgnoreCase));
            // file does not exist in list so add it as the first item
            if (existingItem == null)
            {
                files.Insert(0, file);
                while (files.Count > Constants.MaxRecentFiles)
                {
                    files.RemoveAt(files.Count - 1);
                }
                SaveListToRegistry("File", file, files);
                return;
            }

            var existingIndex = files.IndexOf(existingItem);
            // file is already first in the list so do nothing
            if (existingIndex == 0) return;

            // otherwise move the file to first in the list
            files.Move(existingIndex, 0);



            SaveListToRegistry("File", file, files);
        }



        public T GetValue<T>(string subKey, T defaultValue)
        {
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(RegistryRootKey, RegistryKeyPermissionCheck.ReadSubTree, System.Security.AccessControl.RegistryRights.QueryValues);
            if (regDaxStudio == null) return defaultValue;
            return (T)Convert.ChangeType(regDaxStudio.GetValue(subKey, defaultValue), typeof(T), CultureInfo.InvariantCulture);
        }


        private static T GetValue<T>(string subKey)
        {
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(RegistryRootKey);
            if (regDaxStudio != null) return (T)regDaxStudio.GetValue(subKey);
            return default;
        }

        // Datetime overload of SetValue
        public void SetValue(string subKey, DateTime value, bool isInitializing, object options, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (isInitializing) return;
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(RegistryRootKey, true)
                                ?? Registry.CurrentUser.CreateSubKey(RegistryRootKey);

            PropertyDescriptor prop = TypeDescriptor.GetProperties(options).Find(propertyName, true);
            var defaultValueAttribute = (DefaultValueAttribute)prop.Attributes[typeof(DefaultValueAttribute)];

            var defaultValue = DateTime.Parse(defaultValueAttribute.Value.ToString());
            if (value == defaultValue)
            {
                regDaxStudio.DeleteValue(subKey);
                return;
            }

            regDaxStudio?.SetValue(subKey,
                value.ToString(Constants.IsoDateFormat, CultureInfo.InvariantCulture));
        }

        // Version overload of SetValue
        public void SetValue(string subKey, Version value, bool isInitializing, object options, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (isInitializing) return;
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(RegistryRootKey, true)
                                ?? Registry.CurrentUser.CreateSubKey(RegistryRootKey);

            PropertyDescriptor prop = TypeDescriptor.GetProperties(options).Find(propertyName, true);
            var defaultValueAttribute = (DefaultValueAttribute)prop.Attributes[typeof(DefaultValueAttribute)];

            var defaultValue = Version.Parse(defaultValueAttribute.Value.ToString());
            if (value == defaultValue)
            {
                regDaxStudio.DeleteValue(subKey);
                return;
            }

            regDaxStudio?.SetValue(subKey, value.ToString());
        }

        // Generic SetValue method
        public void SetValue<T>(string subKey, T value, bool isInitializing, object options, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (isInitializing) return;
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(RegistryRootKey, true);
            if (regDaxStudio == null) { regDaxStudio = Registry.CurrentUser.CreateSubKey(RegistryRootKey); }

            PropertyDescriptor prop = TypeDescriptor.GetProperties(options).Find(propertyName, true);

            var defaultValueAttribute = (DefaultValueAttribute)prop.Attributes[typeof(DefaultValueAttribute)];
            bool valueIsSetToDefault = false;
            if (typeof(T) == typeof(Version))
            {
                try
                {
                    var defaultValueVer = Version.Parse(defaultValueAttribute.Value.ToString());
                    valueIsSetToDefault = defaultValueVer.Equals(value);
                }
                catch
                {
                    // ignore any errors parsing the version and assume the versions do not match
                }
            }
            else
            {
                var defaultValue = (T)(object)defaultValueAttribute.Value;
                valueIsSetToDefault = (defaultValue == null && value == null) || (defaultValue?.Equals(value)??false);
            }

            if (valueIsSetToDefault)
            {
                if (regDaxStudio.GetValue(subKey) != null) regDaxStudio.DeleteValue(subKey);
                return;
            }

            regDaxStudio.SetValue(subKey, value);
        }

        public bool IsFileLoggingEnabled()
        {
            if (!KeyExists("IsFileLoggingEnabled")) return false;
            return GetValue<bool>("IsFileLoggingEnabled");
        }

        private bool KeyExists(string subKey)
        {
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(RegistryRootKey);
            return (regDaxStudio.GetSubKeyNames().ToList().Contains(subKey));
        }


        internal ObservableCollection<string> GetMRUListFromRegistry(string listName)
        {
            var list = new ObservableCollection<string>();
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(RegistryRootKey);
            if (regDaxStudio != null)
            {
                var regListMRU = regDaxStudio.OpenSubKey($"{listName}MRU");
                if (regListMRU != null)
                    foreach (var svr in regListMRU.GetValueNames())
                    {
                        var itmName = regListMRU.GetValue(svr).ToString();
                        list.Add(itmName);
                    }
            }

            return list;
        }



        internal void SaveListToRegistry(string listName, object currentItem, IEnumerable<object> itemList)
        {
            var listKey = $"{listName}MRU";
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(RegistryRootKey, RegistryKeyPermissionCheck.ReadWriteSubTree);
            if (regDaxStudio == null)
                Registry.CurrentUser.CreateSubKey(RegistryRootKey);
            if (regDaxStudio != null)
            {
                // clear existing data
                regDaxStudio.DeleteSubKeyTree(listKey, false);
                var regListMRU = regDaxStudio.CreateSubKey(listKey, RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (regListMRU != null)
                {
                    int i = 1;

                    foreach (object listItem in itemList)
                    {
                        if (i > Constants.MaxMruSize) break; // don't save more than the max mru size
                        if (!(listItem is string str)) str = listItem.ToString();
                        regListMRU.SetValue($"{listName}{i}", str);
                        i++;
                    }
                }
            }
        }

        /// <summary>
        /// This method loops through each of the properties using reflection and attempts
        /// to load the values from the registry
        /// </summary>
        /// <param name="options"></param>
        public void Initialize(IGlobalOptions options)
        {

            var invariantCulture = CultureInfo.InvariantCulture;
            foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(options))
            {

                // Set default value if DefaultValueAttribute is present
                DefaultValueAttribute attrDefaultVal = prop.Attributes[typeof(DefaultValueAttribute)]
                                                                 as DefaultValueAttribute;

                DataMemberAttribute attrDataMember = prop.Attributes[typeof(DataMemberAttribute)]
                                                 as DataMemberAttribute;

                // if we don't have either a default value or a data member attribute 
                // then skip this property
                if (attrDefaultVal == null && attrDataMember == null) continue;
                // read the value from the registry or use the default value
                Object val = null;
                if (attrDefaultVal != null) val = this.GetValue(prop.Name, attrDefaultVal.Value);
                // set the property value
                if (prop.PropertyType == typeof(SecureString))
                {
                    using (SecureString secStr = new SecureString())
                    {
                        if (val == null) continue;
                        foreach (char c in (string)val)
                        {
                            secStr.AppendChar(c);
                        }
                        prop.SetValue(options, secStr);
                    }
                }
                else if (prop.PropertyType == typeof(Version))
                {
                    var versionVal = new Version("0.0.0.0"); 
                    if ( val != null) Version.TryParse(val.ToString(), out versionVal);
                    prop.SetValue(options, versionVal);
                }
                else if (prop.PropertyType == typeof(DateTime))
                {
                    DateTime dateVal = DateTime.Parse(attrDefaultVal.Value.ToString(), CultureInfo.InvariantCulture);
                    if (val != null) _ = DateTime.TryParse(val.ToString(), out dateVal);
                    prop.SetValue(options, dateVal);
                }
                else if (prop.PropertyType == typeof(double))
                {
                    var doubleVal = val == null ? Double.Parse(attrDefaultVal.Value.ToString(), invariantCulture) : double.Parse(val.ToString(), invariantCulture);
                    prop.SetValue(options, doubleVal);
                }
                else if (prop.PropertyType == typeof(bool))
                {
                    var boolVal = bool.Parse(attrDefaultVal.Value.ToString());
                    if (val != null) bool.TryParse(val.ToString(), out boolVal);
                    prop.SetValue(options, boolVal);
                }
                else if (prop.PropertyType == typeof(ObservableCollection<IDaxFile>))
                {
                    var files = GetFileMRUList();
                    prop.SetValue(options, files);
                }
                else if (prop.PropertyType == typeof(ObservableCollection<string>))
                {
                    var servers = GetServerMRUList();
                    prop.SetValue(options, servers);
                }
                else if (prop.PropertyType.IsEnum)
                {
                    var enumVal = Enum.Parse(prop.PropertyType, attrDefaultVal.Value.ToString(),true);
                    if( val != null) enumVal = Enum.Parse(prop.PropertyType, val.ToString(),true);
                    prop.SetValue(options, enumVal);
                }
                else
                {
                    try
                    {
                        prop.SetValue(options, val);
                    }
                    catch //(Exception ex)
                    {
                        // if the set fails try using the default value
                        prop.SetValue(options, attrDefaultVal);
                    }

                }
            }

            // Check for machine level settings which are configured by the installer
            InitializeMachineSettings(options);

            // Clean up Preview keys
            CleanupPreviewKeys();
        }

        private void CleanupPreviewKeys()
        {
            var previewKeys = new string[] { "ShowPreviewQueryBuilder", "ShowPreviewBenchmark" };

            var regDaxStudio = Registry.CurrentUser.OpenSubKey(RegistryRootKey, true);
            if (regDaxStudio == null) return;
            foreach (var subKey in previewKeys)
            {
                regDaxStudio.DeleteValue(subKey, false);
            }
        }

        private static void InitializeMachineSettings(IGlobalOptions options)
        {
            var regDaxStudio = Registry.LocalMachine.OpenSubKey(RegistryRootKey, RegistryKeyPermissionCheck.ReadSubTree, System.Security.AccessControl.RegistryRights.QueryValues);
            if (regDaxStudio == null) return;
            const string subKey = "BlockAllInternetAccess";
            options.BlockAllInternetAccess = (bool)Convert.ChangeType(regDaxStudio.GetValue(subKey, false), typeof(bool), CultureInfo.InvariantCulture);
        }
    }

    public static class PropertyDescriptorExtensions
    {
        public static void TrySetValue(this PropertyDescriptor prop, object component,object val, object defaultVal)
        {


        }
    }
}
