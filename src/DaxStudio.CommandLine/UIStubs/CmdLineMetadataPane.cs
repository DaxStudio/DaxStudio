using DaxStudio.Interfaces;

namespace DaxStudio.CommandLine.UIStubs
{
    internal class CmdLineMetadataPane : IMetadataPane
    {
        public bool ShowHiddenObjects { get ; set ; }
        public string SelectedModel { get; set; } = "Model";
    }
}
