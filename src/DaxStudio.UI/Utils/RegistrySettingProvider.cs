using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System;
using DaxStudio.UI.Model;
using System.Threading.Tasks;
using System.Linq;
using DaxStudio.UI.Interfaces;
using System.ComponentModel.Composition;
using DaxStudio.Common;
using DaxStudio.Interfaces;
using System.ComponentModel;
using Newtonsoft.Json;
using System.Security;

namespace DaxStudio.UI
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    //[Export(typeof(ISettingProvider))]
    public class RegistrySettingProvider:ISettingProvider
    {
    
        private const string registryRootKey = "SOFTWARE\\DaxStudio";

        private const int MAX_MRU_SIZE = 25;

        public string LogPath => Environment.ExpandEnvironmentVariables(Constants.LogFolder);

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


        private T GetValue<T>(string subKey)
        {
            var regDaxStudio = Registry.CurrentUser.OpenSubKey(registryRootKey);
            return (T)regDaxStudio.GetValue(subKey);
        }

        public Task SetValueAsync<T>(string subKey, T value, bool isInitializing)
        {
            return Task.Run(() => {
                if (isInitializing) return;
                var regDaxStudio = Registry.CurrentUser.OpenSubKey(registryRootKey, true);
                if (regDaxStudio == null) { regDaxStudio = Registry.CurrentUser.CreateSubKey(registryRootKey); }
                regDaxStudio.SetValue(subKey, value);
            });
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

        public void Initialize(IGlobalOptions options)
        {
            
            foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(options))
            {
                //if (interfaceProps.Find(prop.Name, true)==null) continue;

                //JsonIgnoreAttribute ignoreAttr = prop.Attributes[typeof(JsonIgnoreAttribute)] 
                //                                                 as JsonIgnoreAttribute;
                //if (ignoreAttr != null) continue; // go to next attribute if this prop is tagged as [JsonIgnore]

                // Set default value if DefaultValueAttribute is present
                DefaultValueAttribute attr = prop.Attributes[typeof(DefaultValueAttribute)]
                                                                 as DefaultValueAttribute;

                if (attr == null) continue;
                // read the value from the registry or use the default value
                Object val = this.GetValue(prop.Name, attr.Value);
                // set the property value
                if (prop.PropertyType == typeof(SecureString))
                {
                    SecureString secStr = new SecureString();
                    foreach (char c in (string)val)
                    {
                        secStr.AppendChar(c);
                    }
                    prop.SetValue(options, secStr);
                }
                else if (prop.PropertyType == typeof(Version))
                {
                    var versionVal = val==null? new Version("0.0.0.0"):Version.Parse(val.ToString());
                    prop.SetValue(options, versionVal);
                }
                else if (prop.PropertyType == typeof(DateTime))
                {
                    var dateVal = val==null ? DateTime.Parse(attr.Value.ToString()):DateTime.Parse(val.ToString());
                    prop.SetValue(options, dateVal);
                }
                else if (prop.PropertyType == typeof(double))
                {
                    var doubleVal = val == null ? Double.Parse(attr.Value.ToString()) : double.Parse(val.ToString());
                    prop.SetValue(options, doubleVal);
                }
                else if (prop.PropertyType == typeof(bool))
                {
                    var boolVal = val == null ? bool.Parse(attr.Value.ToString()) : bool.Parse(val.ToString());
                    prop.SetValue(options, boolVal);
                }
                else if (prop.PropertyType.IsEnum)
                {
                    var enumVal =  Enum.Parse(prop.PropertyType, val.ToString());
                    prop.SetValue(options, enumVal);
                }
                else
                {
                    prop.SetValue(options, val);
                }
            }
            
        }
    }
}
