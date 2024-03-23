using System;

namespace DaxStudio.UI.Extensions
{
    public static class StoreageModeExtensions
    {
        public static string ParseStorageMode(this string storageMode)
        {
            string _storageMode;
            // if the value can be parsed as an integer
            if (int.TryParse(storageMode, out var _))
            {
                // try to parse it as a ModeType enum
                if (Enum.TryParse<Microsoft.AnalysisServices.Tabular.ModeType>(storageMode, true, out var modeEnum))
                {
                    _storageMode = modeEnum.ToString();
                }
                else
                {
                    _storageMode = "UNKNOWN";
                }
            }
            else
            {
                _storageMode = storageMode;
            }
            return _storageMode;
        }
    }
}
