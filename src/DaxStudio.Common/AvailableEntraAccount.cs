namespace DaxStudio.Common
{
    public enum EntraAccountSource
    {
        DaxStudioCache,
        Windows
    }

    public sealed class AvailableEntraAccount
    {
        public AvailableEntraAccount(string username, string tenantId, string homeAccountId, EntraAccountSource source)
        {
            Username = username ?? string.Empty;
            TenantId = tenantId ?? string.Empty;
            HomeAccountId = homeAccountId ?? string.Empty;
            Source = source;
        }

        public string Username { get; }
        public string TenantId { get; }
        public string HomeAccountId { get; }
        public EntraAccountSource Source { get; }
    }
}