using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DaxStudio.UI.Utils
{
    public class HighContrastHelper
        : DependencyObject
    {
        #region Singleton pattern

        private HighContrastHelper()
        {
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
            IsHighContrast = SystemParameters.HighContrast;
        }

        private static HighContrastHelper _instance;

        public static HighContrastHelper Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new HighContrastHelper();

                return _instance;
            }
        }

        #endregion

        void SystemParameters_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Console.WriteLine(e.PropertyName);
            if (e.PropertyName == "HighContrast")
            {
                HighContrastHelper.Instance.IsHighContrast = SystemParameters.HighContrast;
            }
        }

        #region DP IsHighContrast

        public static readonly DependencyProperty IsHighContrastProperty = DependencyProperty.Register(
            "IsHighContrast",
            typeof(bool),
            typeof(HighContrastHelper),
            new PropertyMetadata(
                false
                ));

        public bool IsHighContrast
        {
            get { return (bool)GetValue(IsHighContrastProperty); }
            private set { SetValue(IsHighContrastProperty, value); }
        }

        #endregion
    }
}
