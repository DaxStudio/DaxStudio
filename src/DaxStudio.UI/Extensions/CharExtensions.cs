namespace DaxStudio.UI.Extensions
{
    public static class CharExtensions
    {
        public static bool IsDaxLetterOrDigit(this char input)
        {
            return char.IsLetterOrDigit(input) || '_' == input;
        }

        public static bool IsDaxLetter(this char input)
        {
            return char.IsLetter(input) || ('_' == input) || ('$' == input);
        }


    }
}
