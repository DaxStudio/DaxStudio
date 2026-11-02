namespace DaxStudio.Core.Utils
{
    /// <summary>
    /// Creates the default <see cref="IPowerBIInstanceScanner"/> for the current platform.
    /// This Windows variant is compiled into the Windows target frameworks and returns the real
    /// WMI/Win32 based scanner. The plain net8.0 (cross-platform) build compiles
    /// <c>PowerBIScannerFactory.Stub.cs</c> instead, which returns a no-op scanner.
    /// </summary>
    internal static class PowerBIScannerFactory
    {
        public static IPowerBIInstanceScanner Create() => new WindowsPowerBIInstanceScanner();
    }
}
