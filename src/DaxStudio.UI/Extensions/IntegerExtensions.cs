namespace DaxStudio.UI.Extensions
{
    public static class IntegerExtensions
    {
        private const int MILLISECONDS_PER_SECOND = 1000;
        public static int SecondsToMilliseconds(this int seconds)
        {
        return seconds * MILLISECONDS_PER_SECOND;
        }
    }
}
