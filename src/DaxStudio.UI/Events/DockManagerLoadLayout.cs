namespace DaxStudio.UI.Events
{
    public class DockManagerLoadLayout
    {
        public DockManagerLoadLayout(bool restoreDefault )
        {
            RestoreDefault = restoreDefault;
        }

        public bool RestoreDefault { get; private set; }
    }
}
