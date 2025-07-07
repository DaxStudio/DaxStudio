using Caliburn.Micro;

namespace DaxStudio.UI.ViewModels
{
    public class BaseDialogViewModel : Screen
    {

        public virtual void Close()
        {
            System.Diagnostics.Debug.WriteLine("Dialog Close");
            this.TryCloseAsync();
        }

    }
}
