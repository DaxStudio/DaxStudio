using System.Text;

namespace DaxStudio.UI.Utils.RegionalTranslator
{

    public enum RegionalState
    {
        European,
        English,
        Unknown
    };

    public enum CharState
    {
        String,
        Column,
        Table,
        Other
    }

    public static class RegionalTranslator
    {


        public static string Translate(string input)
        {
            // from European
            // ; -> ,
            // , -> .
            // from English
            // , -> ;
            // . -> ,
            // TopPercent( 10.0, 

            RegionalState targetRegion = RegionalState.Unknown;
            StringBuilder output = new StringBuilder( input.Length);

            IRegionalStateMachine currentState = new CharStateOther();

            for (int i = 0; i < input.Length;i++)
            {
                currentState = currentState.Process(input, i, targetRegion, output);
            }
            return output.ToString();
        }
    }
}
