using System;

namespace ADOTabular
{
    public static class VersionComparisonExtension
    {
        public static bool VersionGreaterOrEqualTo(this string version, string comparisonVersion)
        {
            if (version == null) throw new ArgumentNullException(nameof(version));
            if (comparisonVersion == null) throw new ArgumentNullException(nameof(comparisonVersion));

            // try to short circuit if both strings are the same
            if (version == comparisonVersion)
                return true;

            // split a string like 11.0.3000.0 into it's parts
            var verArray = version.Split('.');
            var compArray = comparisonVersion.Split('.');
            for (byte i = 0; i < verArray.Length; i++)
            {
                _ = int.TryParse(verArray[i], out int iVer);
                _ = int.TryParse(compArray[i],out int iCompVer);
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
