using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;


namespace DaxStudio.CheckerApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Checker checker;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                checker = new Checker(Output);
                checker.SetFusionLoggingState(menuToggleFusionLogging);
                checker.SetVSTOLoggingState(menuToggleVSTOLogging);
                checker.ShowVersionInfo();
                checker.CheckOSInfo();
                checker.CheckScreenInfo();
                checker.CheckNetFramework();
                //checker.CheckLocalLibrary("AMO", "Microsoft.AnalysisServices.dll");
                //checker.CheckLibraryExact("Microsoft.AnalysisServices",true);
                //checker.CheckLocalLibrary("ADOMD.NET", "Microsoft.AnalysisServices.AdomdClient.dll");
                //checker.CheckLibraryExact("Microsoft.AnalysisServices.AdomdClient", true);
                //checker.CheckLibrary("AMO",       "Microsoft.AnalysisServices, Version={0}.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91");
                //checker.CheckLibrary("ADOMD.NET", "Microsoft.AnalysisServices.AdomdClient, Version={0}.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91");
                checker.CheckDaxStudioBindings();
                checker.CheckExcelAddin();
                checker.CheckSettings();
            }
            catch (Exception exception)
            {
                Output.AppendLine($"\n\nFATAL ERROR:\n{exception.Message}\n{exception.StackTrace}");
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CopyToClipboardClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Output.SelectAll();
                Output.Copy();
                MessageBox.Show("Results succesfully copied to the clipboard", "DAX Studio Checker", MessageBoxButton.OK, MessageBoxImage.Information );
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred while copying the results to the clipboard\n\n" + ex.Message, "DAX Studio Checker", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnQuitMenuClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnToggleFusionLoggingMenuClick(object sender, RoutedEventArgs e)
        {
            checker.ToggleFusionLogging(menuToggleFusionLogging.IsChecked);
        }

        private void OnToggleVSTOLoggingMenuClick(object sender, RoutedEventArgs e)
        {
            checker.ToggleVSTOLogging(this.menuToggleVSTOLogging.IsChecked);
        }

        private void OnOpenFusionLogFolderMenuClick(object sender, RoutedEventArgs e)
        {
            Checker.OpenFusionLogFolder();
        }
    }
}
