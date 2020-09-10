using ControlzEx.Theming;
using Fluent;
using MLib.Interfaces;
using System;
using System.Collections.Generic;
using System.Windows;

namespace DaxStudio.UI.Extensions
{
    public static class ApplicationExtensions
    {
        //private const string _prefix = "/DaxStudio.UI;component/Theme/";
        private const string _prefix = "pack://application:,,,/DaxStudio.UI;Component/Theme/";
        public static void LoadDarkTheme(this Application app)
        {

            RemoveMergedThemeDictionaries(app, new[] {"Light.DaxStudio.xaml" });

            app.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(String.Concat(_prefix + "Dark.DaxStudio.xaml"), UriKind.Absolute) });

            //Monotone Theme files
            app.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(String.Concat(_prefix + "Monotone.Colors.xaml"), UriKind.Absolute) });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(String.Concat(_prefix + "Monotone.Brushes.xaml"), UriKind.Absolute) });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(String.Concat(_prefix + "Monotone.xaml"), UriKind.Absolute) });
            //app.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(String.Concat(_prefix + "Monotone.ExtendedWPFToolkit.xaml"), UriKind.Absolute) });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(String.Concat(_prefix + "Monotone.DaxEditor.xaml"), UriKind.Absolute) });
            //app.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(String.Concat(_prefix + "DarkTheme.xaml"), UriKind.Absolute) });
            app.ChangeRibbonTheme("Dark");
        }

        public static void LoadLightTheme(this Application app)
        {
            //List<int> indexesToRemove = new List<int>();
            //for (int i =0; i< app.Resources.MergedDictionaries.Count;i++)
            //{

            //    if (app.Resources.MergedDictionaries[i].Source.LocalPath.EndsWith("/Monotone.Colors.xaml")) indexesToRemove.Add(i);
            //    if (app.Resources.MergedDictionaries[i].Source.LocalPath.EndsWith("/Monotone.Brushes.xaml")) indexesToRemove.Add(i);
            //    if (app.Resources.MergedDictionaries[i].Source.LocalPath.EndsWith("/Monotone.xaml")) indexesToRemove.Add(i);
            //    if (app.Resources.MergedDictionaries[i].Source.LocalPath.EndsWith("/Monotone.ExtendedWPFToolkit.xaml")) indexesToRemove.Add(i);
            //    if (app.Resources.MergedDictionaries[i].Source.LocalPath.EndsWith("/Monotone.DaxEditor.xaml")) indexesToRemove.Add(i);
            //    //if (app.Resources.MergedDictionaries[i].Source.LocalPath.EndsWith("/DarkTheme.xaml")) indexesToRemove.Add(i);
            //}

            //indexesToRemove.Reverse();
            //foreach (var i in indexesToRemove)
            //{
            //    app.Resources.MergedDictionaries.RemoveAt(i);
            //}

            RemoveMergedThemeDictionaries(app, new[] {
                "Monotone.Colors.xaml"
                , "Monotone.Brushes.xaml"
                , "Monotone.xaml"
                , "Monotone.ExtendedWPFToolkit.xaml"
                , "Monotone.DaxEditor.xaml" });

            app.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(String.Concat(_prefix + "Light.DaxStudio.xaml"), UriKind.Absolute) });

            app.ChangeRibbonTheme("Light");
        }



        private static void RemoveMergedThemeDictionaries(Application app, string[] dictionariesToRemove)
        {
            // walk through dictionaries backwards while removing items
            for (int i = app.Resources.MergedDictionaries.Count -1; i >= 0; i--)
            {
                foreach (var dict in dictionariesToRemove)
                {
                    if (app.Resources.MergedDictionaries[i].Source.LocalPath.EndsWith($"/{dict}"))
                    {
                        System.Diagnostics.Debug.WriteLine($"Removing Merged Dictionary - [{i}] {dict}");
                        app.Resources.MergedDictionaries.RemoveAt(i);
                        System.Diagnostics.Debug.WriteLine($">> Removed Merged Dictionary - [{i}] {dict}");
                        break; // jump to next item in outer loop
                    }
                }
            }

        }

        public static void AddDaxStudioAccentColor(this Application app)
        {
            // Fluent Dark Theme
            var dictGeneric = new ResourceDictionary()
            {
                Source = new Uri("pack://application:,,,/Fluent;component/Themes/Generic.xaml", UriKind.Absolute)
            };
            app.Resources.MergedDictionaries.Add(dictGeneric);

            //var dictDark = new ResourceDictionary()
            //{
            //    Source = new Uri("pack://application:,,,/Fluent;component/Themes/Colors/BaseDark.xaml", UriKind.Absolute)
            //};
            //app.Resources.MergedDictionaries.Add(dictDark);
            //var dictAccent = new ResourceDictionary()
            //{
            //    Source = new Uri("pack://application:,,,/Fluent;component/Themes/Accents/Green.xaml", UriKind.Absolute)
            //};
            //app.Resources.MergedDictionaries.Add(dictAccent);

            // add custom accent and theme resource dictionaries to the ThemeManager

            var source = new Uri("pack://application:,,,/DaxStudio.UI;component/Theme/Light.DaxStudio.xaml");
            var lightTheme = new ControlzEx.Theming.Theme(new LibraryTheme(source, null));

            ThemeManager.Current.AddTheme( lightTheme);

            source = new Uri("pack://application:,,,/DaxStudio.UI;component/Theme/Dark.DaxStudio.xaml");
            var darkTheme = new ControlzEx.Theming.Theme(new LibraryTheme(source, null));
            ThemeManager.Current.AddTheme( darkTheme);
            
            // get the current theme from the application
            var theme = ThemeManager.Current.DetectTheme(Application.Current);

        }

        //public static void LoadRibbonTheme(this Application app)
        //{
        //    var theme = SettingProvider.GetValue<string>("Theme", "Light");
        //    ChangeRibbonTheme(app, theme);
        //}

        public static void ChangeRibbonTheme(this Application app, string theme)
        {
            
            var appTheme = ThemeManager.Current.GetTheme("Light.DaxStudio");
            if (theme == "Dark") appTheme = ThemeManager.Current.GetTheme("Dark.DaxStudio");


            // now change app style to the custom accent and current theme
            ThemeManager.Current.ChangeTheme(app,
                                     appTheme);

            //if (app.MainWindow != null)
            //    ThemeManager.ChangeTheme(app.MainWindow,
            //                            appTheme);

        }
    }
}
