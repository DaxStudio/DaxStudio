using DaxStudio.Interfaces;

namespace DaxStudio.CommandLine
{
    internal class CmdLineMetadataPane : IMetadataPane
    {
        public bool ShowHiddenObjects { get ; set ; }
        public string SelectedModel { get; set; } = "Model";
    }
}
