using System.Collections.Generic;
using System.Collections.ObjectModel;
//using System.Windows.Forms;
using Microsoft.Win32;
//using ComboBox = System.Windows.Forms.ComboBox;

namespace DaxStudio.UI
{
    public static class RegistryHelper
    {
  /*      
        public static string[] LoadServerMRUListFromRegistry(ComboBox cboServers)
        {
            cboServers.Items.Clear();
            var regDaxStudio = Registry.CurrentUser.OpenSubKey("SOFTWARE\\DaxStudio");
            if (regDaxStudio != null)
            {
                var regSvrMRU = regDaxStudio.OpenSubKey("ServerMRU");
                if (regSvrMRU != null)
                    foreach (var svr in regSvrMRU.GetValueNames())
                    {
                        cboServers.Items.Add(regSvrMRU.GetValue(svr));
                    }
                
            }
            return null;
        }
        
        public static void SaveServerMRUListToRegistry(ComboBox cboServers)
        {

            var regDaxStudio = Registry.CurrentUser.OpenSubKey("SOFTWARE\\DaxStudio", RegistryKeyPermissionCheck.ReadWriteSubTree);
            if (regDaxStudio == null)
                Registry.CurrentUser.CreateSubKey("SOFTWARE\\DaxStudio");
            if (regDaxStudio != null)
            {
                // clear existing data
                regDaxStudio.DeleteSubKeyTree("SeverMRU", false);
                var regSvrMRU = regDaxStudio.CreateSubKey("ServerMRU", RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (regSvrMRU != null)
                {
                    int i = 0;
                    bool selectedItemInList = false;
                    foreach (string listItem in cboServers.Items)
                    {
                        if (listItem == cboServers.Text) selectedItemInList = true;
                        regSvrMRU.SetValue(string.Format("Server{0}", i), listItem);
                        i++;
                    }
                    if (!selectedItemInList)
                        regSvrMRU.SetValue(string.Format("Server{0}", i), cboServers.Text);
                }
            }
        }
    */    
        public static ObservableCollection<string> GetServerMRUListFromRegistry()
        {
            var servers = new ObservableCollection<string>();
            var regDaxStudio = Registry.CurrentUser.OpenSubKey("SOFTWARE\\DaxStudio");
            if (regDaxStudio != null)
            {
                var regSvrMRU = regDaxStudio.OpenSubKey("ServerMRU");
                if (regSvrMRU != null)
                    foreach (var svr in regSvrMRU.GetValueNames())
                    {
                        var svrName = regSvrMRU.GetValue(svr).ToString();
                        servers.Add(svrName);
                    }
            }

            return servers;
        }



        public static void SaveServerListToRegistry(string currentServer, ObservableCollection<string>serverList)
        {
            
            var regDaxStudio = Registry.CurrentUser.OpenSubKey("SOFTWARE\\DaxStudio",RegistryKeyPermissionCheck.ReadWriteSubTree);
            if (regDaxStudio == null)
                Registry.CurrentUser.CreateSubKey("SOFTWARE\\DaxStudio");
            if (regDaxStudio != null)
            {
                // clear existing data
                regDaxStudio.DeleteSubKeyTree("SeverMRU", false);
                var regSvrMRU = regDaxStudio.CreateSubKey("ServerMRU", RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (regSvrMRU != null)
                {
                    int i = 0;
                    bool selectedItemInList = false;
                    foreach (string listItem in serverList)
                    {
                        if (listItem == currentServer) selectedItemInList = true;
                        regSvrMRU.SetValue(string.Format("Server{0}", i), listItem );
                        i++;
                    }
                    if (!selectedItemInList)
                        regSvrMRU.SetValue(string.Format("Server{0}", i), currentServer);
                }
            }
        }
    }
}
