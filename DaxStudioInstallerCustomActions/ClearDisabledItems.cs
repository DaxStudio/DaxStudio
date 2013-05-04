using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace DaxStudioInstallerCustomActions
{
    /// <summary>
    /// This class checks whether Excel has disabled items OLAP PivotTable Extensions and clears that if it has
    /// </summary>
    internal class ClearDisabledItems
    {
        private static string[] REGISTRY_PATHS = new string[] { @"Software\Microsoft\Office\14.0\Excel\Resiliency\DisabledItems"
                                                              , @"Software\Microsoft\Office\15.0\Excel\Resiliency\DisabledItems"};

        public static void CheckDisabledItems(string TargetPath)
        {
            foreach (string sRegistryPath in REGISTRY_PATHS)
            {
                RegistryKey appKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(sRegistryPath, true);
                if (appKey != null)
                {
                    foreach (string sName in appKey.GetValueNames())
                    {
                        byte[] bDisabledItem = appKey.GetValue(sName) as byte[];
                        if (bDisabledItem != null)
                        {
                            string sDisabledItem = System.Text.Encoding.Unicode.GetString(bDisabledItem);
                            if (sDisabledItem != null)
                            {
                                if (sDisabledItem.ToLower().Contains(TargetPath.ToLower()) || sDisabledItem.ToLower().Contains("mscoree.dll"))
                                {
                                    appKey.DeleteValue(sName);
                                }
                            }
                        }
                    }
                    appKey.Close();
                }
            }

        }

    }
}

