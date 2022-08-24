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

        }


        #region TitleBar

        /// <summary>
        /// Gets ribbon titlebar
        /// </summary>
        public new RibbonTitleBar TitleBar
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