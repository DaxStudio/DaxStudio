using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.Checker
{
    public static class RegistryHelpers
    {
        public static IEnumerable<string> GetRegValueNames(RegistryView view, string regPath,
                                  RegistryHive hive = RegistryHive.LocalMachine)
        {
            return RegistryKey.OpenBaseKey(hive, view)
                             ?.OpenSubKey(regPath)?.G‌​etValueNames();
        }

        public static IEnumerable<string> GetAllRegValueNames(string RegPath,
                                          RegistryHive hive = RegistryHive.LocalMachine)
        {
            var reg64 = GetRegValueNames(RegistryView.Registry64, RegPath, hive);
            var reg32 = GetRegValueNames(RegistryView.Re‌​gistry32, RegPath, hive);
            var result = (reg64 != null && reg32 != null) ? reg64.Union(reg32) : (reg64 ?? reg32);
            return (result ?? new List<string>().AsEnumerable()).OrderBy(x => x);
        }

        public static object GetRegValue(RegistryView view, string regPath, string ValueName,
                                         RegistryHive hive = RegistryHive.LocalMachine)
        {
            return RegistryKey.OpenBaseKey(hive, view)
                               ?.OpenSubKey(regPath)?.G‌​etValue(ValueName);
        }

        public static object GetRegValue(string RegPath, string ValueName,
                                         RegistryHive hive = RegistryHive.LocalMachine)
        {
            return GetRegValue(RegistryView.Registry64, RegPath, ValueName, hive)
                             ?? GetRegValue(RegistryView.Re‌​gistry32, RegPath, ValueName, hive);
        }
    }
}
