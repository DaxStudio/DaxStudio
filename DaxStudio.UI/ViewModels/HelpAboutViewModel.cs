using System;
using Caliburn.Micro;

namespace DaxStudio.UI.ViewModels
{
    public class HelpAboutViewModel : Screen
    {
        public HelpAboutViewModel() {
            DisplayName = "About DaxStudio";
        }

        public string FullVersionNumber
        {
            get { return System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(); }
        }

    }
}
