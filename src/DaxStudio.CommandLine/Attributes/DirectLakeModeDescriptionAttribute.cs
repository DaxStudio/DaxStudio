using Dax.Metadata;
using System;
using System.ComponentModel;

namespace DaxStudio.CommandLine.Attributes
{
    [AttributeUsage(AttributeTargets.All)]
    public class DirectLakeModeDescriptionAttribute : DescriptionAttribute
    {
        public override string Description => "Sets the Direct Lake mode. Valid avalues are: " + string.Join(", ", Enum.GetNames(typeof(DirectLakeExtractionMode)));
    }
}
