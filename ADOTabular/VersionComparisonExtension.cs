using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADOTabular
{
    public static class VersionComparisonExtension
    {
        public static bool VersionGreaterOrEqualTo(this string version, string comparisonVersion)
        {
            // try to short circuit if both strings are the same
            if (version == comparisonVersion)
                return true;

            // split a string like 11.0.3000.0 into it's parts
            var verArray = version.Split('.');
            var compArray = comparisonVersion.Split('.');
            for (byte i = 0; i < verArray.Length; i++)
            {
                var iVer = 0;
                int.TryParse(verArray[i], out iVer);
                var iCompVer = 0;
                int.TryParse(compArray[i],out iCompVer);
                if (iVer > iCompVer)
                {
                    return true;
                }
                if (iVer < iCompVer)
                {
                    return false;
                }
                // if equal loop around to the next number in the sequence    
            }
            // if we get to the end both strings must be the same
            return true;
        }
    }
}
