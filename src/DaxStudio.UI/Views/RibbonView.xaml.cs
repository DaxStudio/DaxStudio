using DaxStudio.UI.ViewModels;
using Serilog;
using System.Windows.Controls;

namespace DaxStudio.UI.Views
{
    /// <summary>
    /// Interaction logic for RibbonView.xaml
    /// </summary>
    public partial class RibbonView : UserControl
    {
        public RibbonView()
        {
            InitializeComponent();
        }

        #region "QuickAccessToolbar Event Handlers"
        // The following 3 event handlers are only used for the Quick Access Buttons in the title bar
        // Adding the handlers here does violate MVVM, but I have been unable to get the bindings to
        // work consistently any other way and given that we are highly unlikely to create alternate
        // views for the RibbonViewModel this is probably not a big issue
        private void NewQuery_Click(object sender, System.Windows.RoutedEventArgs e)
        {

            var vm = this.DataContext as RibbonViewModel;
            if (vm != null)
            {
                vm.NewQuery();
            }
            else
            {
                Log.Error("{class} {method} Pmessage}", nameof(RibbonView), nameof(NewQuery_Click), "Unable to get an instance of RibbonViewModel");
            }
        }

        private void NewConnectedQuery_Click(object sender, System.Windows.RoutedEventArgs e)
        {

            var vm = this.DataContext as RibbonViewModel;
            if (vm != null)
            {
                vm.NewQueryWithCurrentConnection();
            }
            else
            {
                Log.Error("{class} {method} Pmessage}", nameof(RibbonView), nameof(NewConnectedQuery_Click), "Unable to get an instance of RibbonViewModel");
            }
        }

        private void Save_Click(object sender, System.Windows.RoutedEventArgs e)
        {

            var vm = this.DataContext as RibbonViewModel;
            if (vm != null)
            {
                vm.Save();
            }
            else
            {
                Log.Error("{class} {method} Pmessage}", nameof(RibbonView), nameof(Save_Click), "Unable to get an instance of RibbonViewModel");
            }
        }
        #endregion
    }
}
