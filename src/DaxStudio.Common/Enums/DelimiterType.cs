﻿using System.ComponentModel;

namespace DaxStudio.Common.Enums
{
    public enum DelimiterType
    {
        [Description("US/UK (a,b,c | 1.23)")]
        Comma,
        [Description("OTHER (a;b;c | 1,23")]
        SemiColon,
        Unknown
    }
}
