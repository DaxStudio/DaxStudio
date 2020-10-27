using System;
using System.Windows;
using ControlzEx.Theming;
using Fluent;
using MahApps.Metro.Controls;

namespace DaxStudio.UI.Views
{
    public partial class ShellView : IRibbonWindow
    {
        public ShellView()
        {
            this.InitializeComponent();

            //this.TestContent.Backstage.UseHighestAvailableAdornerLayer = false;

            this.Loaded += this.ShellView_Loaded;
            this.Closed += this.ShellView_Closed;
        }

        private void ShellView_Loaded(object sender, RoutedEventArgs e)
        {
            this.TitleBar = this.FindChild<RibbonTitleBar>("RibbonTitleBar");
            this.TitleBar.InvalidateArrange();
            this.TitleBar.UpdateLayout();

            // We need this inside this window because MahApps.Metro is not loaded globally inside the Fluent.Ribbon Showcase application.
            // This code is not required in an application that loads the MahApps.Metro styles globally.
        //    ThemeManager.Current.ChangeTheme(this, ThemeManager.Current.DetectTheme(Application.Current));
        //    ThemeManager.Current.ThemeChanged += this.SyncThemes;
        }

        private void SyncThemes(object sender, ThemeChangedEventArgs e)
        {
            if (e.Target == this)
            {
                return;
            }

            ThemeManager.Current.ChangeTheme(this, e.NewTheme);
        }

        private void ShellView_Closed(object sender, EventArgs e)
        {
            ThemeManager.Current.ThemeChanged -= this.SyncThemes;
        }


        #region TitleBar

        /// <summary>
        /// Gets ribbon titlebar
        /// </summary>
        public RibbonTitleBar TitleBar
        {
            get => (RibbonTitleBar)this.GetValue(TitleBarProperty);
            private set => SetValue(TitleBarPropertyKey, value);
        }

        // ReSharper disable once InconsistentNaming
        private static readonly DependencyPropertyKey TitleBarPropertyKey = DependencyProperty.RegisterReadOnly(nameof(TitleBar), typeof(RibbonTitleBar), typeof(ShellView), new PropertyMetadata());

#pragma warning disable WPF0060
        /// <summary>Identifies the <see cref="TitleBar"/> dependency property.</summary>
        public static readonly DependencyProperty TitleBarProperty = TitleBarPropertyKey.DependencyProperty;
#pragma warning restore WPF0060

        #endregion
    }
}