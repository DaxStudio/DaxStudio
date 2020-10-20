using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.CheckerApp
{
    public static class RegistryHelpers
    {
        public static IEnumerable<string> GetRegValueNames(RegistryView view, string regPath,
                                  RegistryHive hive = RegistryHive.LocalMachine)
        {
            return RegistryKey.OpenBaseKey(hive, view)
                             ?.OpenSubKey(regPath)?.G‌​etValueNames();
        }

        public static IEnumerable<string> GetAllRegValueNames(string regPath,
                                          RegistryHive hive = RegistryHive.LocalMachine)
        {
            var reg64 = GetRegValueNames(RegistryView.Registry64, regPath, hive);
            var reg32 = GetRegValueNames(RegistryView.Registry32, regPath, hive);
            var result = (reg64 != null && reg32 != null) ? reg64.Union(reg32) : (reg64 ?? reg32);
            return (result ?? new List<string>().AsEnumerable()).OrderBy(x => x);
        }

        public static object GetRegValue(RegistryView view, string regPath, string valueName,
                                         RegistryHive hive = RegistryHive.LocalMachine)
        {
            return RegistryKey.OpenBaseKey(hive, view)
                               ?.OpenSubKey(regPath)?.G‌​etValue(valueName);
        }

        public static object GetRegValue(string regPath, string valueName,
                                         RegistryHive hive = RegistryHive.LocalMachine)
        {
            return GetRegValue(RegistryView.Registry64, regPath, valueName, hive)
                             ?? GetRegValue(RegistryView.Re‌​gistry32, regPath, valueName, hive);
        }
    }
}
