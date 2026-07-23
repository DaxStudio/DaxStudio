namespace DaxStudio.Core.Utils
{
    /// <summary>
    /// Creates the default <see cref="IPowerBIInstanceScanner"/> for the current platform.
    /// This stub variant is compiled into the cross-platform (plain net8.0) build and returns a
    /// no-op scanner, since there is no Power BI Desktop to discover on non-Windows platforms.
    /// The Windows target frameworks compile <c>PowerBIScannerFactory.Windows.cs</c> instead.
    /// </summary>
    internal static class PowerBIScannerFactory
    {
        public static IPowerBIInstanceScanner Create() => new NullPowerBIInstanceScanner();
    }
}
