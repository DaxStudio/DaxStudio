using DaxStudio.Common.Interfaces;

namespace DaxStudio.CommandLine.UIStubs
{
    internal class HaveLastUsedUPNStub : IHaveLastUsedUPN
    {
        public string LastUsedUPN { get; set; } = "<always prompt for a user>";
    }
}
