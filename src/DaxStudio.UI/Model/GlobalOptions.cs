using DaxStudio.Interfaces;
using DaxStudio.Interfaces.Enums;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security;

namespace DaxStudio.UI.Model
{

    public class GlobalOptions : IGlobalOptions
    {
        private const string DefaultEditorFontFamily = "Lucida Console";
        private const int DefaultEditorFontSize = 11;
        private const string DefaultResultsFontFamily = "Segoe UI";
        private const int DefaultResultsFontSize = 11;
        private const string DefaultWindowPosition = @"﻿﻿<?xml version=""1.0"" encoding=""utf-8""?><WINDOWPLACEMENT xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""><length>44</length><flags>0</flags><showCmd>1</showCmd><minPosition><X>0</X><Y>0</Y></minPosition><maxPosition><X>-1</X><Y>-1</Y></maxPosition><normalPosition><Left>5</Left><Top>5</Top><Right>1125</Right><Bottom>725</Bottom></normalPosition></WINDOWPLACEMENT>";


        [DefaultValue(true)]
        public bool EditorShowLineNumbers { get; set; }

        public double EditorFontSizePx { get; }
        [DefaultValue(DefaultEditorFontFamily)]
        public string EditorFontFamily { get; set; }

        public double ResultFontSizePx { get; }

        [DefaultValue(DefaultResultsFontFamily)]
        public string ResultFontFamily { get; set; }
        [DefaultValue(true)]
        public bool EditorEnableIntellisense { get;set; }
        [DefaultValue(200)]
        public int QueryHistoryMaxItems { get;set; }
        [DefaultValue(true)]
        public bool QueryHistoryShowTraceColumns { get; set; }
        [DefaultValue(true)]
        public bool ProxyUseSystem { get; set; }
        [DefaultValue("")]
        public string ProxyAddress { get; set; }
        [DataMember, DefaultValue("")]
        public string ProxyUser { get; set; }
        [DataMember]
        public SecureString ProxySecurePassword { get; set; }
        [DataMember, DefaultValue(30)]
        public int QueryEndEventTimeout { get; set; }
        [DataMember, DefaultValue(10)]
        public int DaxFormatterRequestTimeout { get; set; }
        [DataMember, DefaultValue(30)]
        public int TraceStartupTimeout { get; set; }
        [DataMember, DefaultValue(DelimiterType.Comma)]
        public DelimiterType DefaultSeparator { get; set; }
        [DataMember, DefaultValue(DaxFormatStyle.LongLine)]
        public DaxFormatStyle DefaultDaxFormatStyle { get; set; }
        [DataMember, DefaultValue(false)]
        public bool TraceDirectQuery { get; set; }
        public bool ShowPreReleaseNotifcations { get; set; }
        public bool ShowTooltipBasicStats { get; set; }
        public bool ShowTooltipSampleData { get; set; }
        public bool CanPublishDaxFunctions { get; set; }
        public bool ExcludeHeadersWhenCopyingResults { get; set; }
        public bool ShowExportMetrics { get; set; }
        public bool ShowExternalTools { get; set; }
        public bool ShowExportAllData { get; set; }
        public bool VpaxIncludeTom { get; set; }
        public bool ResultAutoFormat { get; set; }
        public bool ScaleResultsFontWithEditor { get; set; }
        public int CodeCompletionWindowWidthIncrease { get; set; }
        public bool KeepMetadataSearchOpen { get; set; }
        public string Theme { get; set; }
        public bool AutoRefreshMetadataLocalMachine { get; set; }
        public bool AutoRefreshMetadataLocalNetwork { get; set; }
        public bool AutoRefreshMetadataCloud { get; set; }
        public bool ShowHiddenMetadata { get; set; }
        public bool SetClearCacheAsDefaultRunStyle { get; set; }
        public bool SortFoldersFirstInMetadata { get; set; }
        public string WindowPosition { get; set; }
        public Version DismissedVersion { get; set; }
        public DateTime LastVersionCheckUTC { get; set; }
        public bool CustomCsvQuoteStringFields { get; set; }
        public string CustomCsvDelimiter { get; set; }
        public CustomCsvDelimiterType CustomCsvDelimiterType { get; set; }

        public string GetCustomCsvDelimiter()
        {
            // stub implementation
            return System.Globalization.CultureInfo.CurrentUICulture.TextInfo.ListSeparator;
        }

        //public object this[string propertyName]
        //{
        //    get
        //    {
        //        PropertyInfo property = GetType().GetProperty(propertyName);
        //        return property.GetValue(this, null);
        //    }
        //    set
        //    {
        //        PropertyInfo property = GetType().GetProperty(propertyName);
        //        Type propType = property.PropertyType;
        //        if (value == null)
        //        {
        //            if (propType.IsValueType && Nullable.GetUnderlyingType(propType) == null)
        //            {
        //                throw new InvalidCastException();
        //            }
        //            else
        //            {
        //                property.SetValue(this, null, null);
        //            }
        //        }
        //        else if (value.GetType() == propType)
        //        {
        //            property.SetValue(this, value, null);
        //        }
        //        else
        //        {
        //            TypeConverter typeConverter = TypeDescriptor.GetConverter(propType);
        //            object propValue = typeConverter.ConvertFromString(value.ToString());
        //            property.SetValue(this, propValue, null);
        //        }
        //    }
        //}

    }
}
