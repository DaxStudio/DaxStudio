namespace DaxStudio.CommandLine.Extensions
{
    internal static class StringExtensions
    {
        public static string ToDaxName(this string name)
        {
            return $"'{name}'";
        }
    }
}
