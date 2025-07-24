using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DaxStudio.UI.Controls
{
    public abstract class DaxStudioDialog : ContentControl
    {
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

        }
        #region Label DP

        /// <summary>
        /// Gets or sets the Label which is displayed next to the field
        /// </summary>
        public string Caption
        {
            get { return (String)GetValue(CaptionProperty); }
            set { SetValue(CaptionProperty, value); }
        }

        /// <summary>
        /// Identified the Label dependency property
        /// </summary>
        public static readonly DependencyProperty CaptionProperty =
            DependencyProperty.Register("Caption", typeof(string),
              typeof(DaxStudioDialog), new PropertyMetadata(""));

        public ImageSource Icon
        {
            get { return (ImageSource)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        /// <summary>
        /// Identified the Label dependency property
        /// </summary>
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(ImageSource),
              typeof(DaxStudioDialog), new PropertyMetadata(null));


        /// <summary>
        /// Gets or sets the Label which is displayed next to the field
        /// </summary>
        public bool CloseIsDefaultCancel
        {
            get { return (bool)GetValue(CloseIsDefaultCancelProperty); }
            set { SetValue(CloseIsDefaultCancelProperty, value); }
        }

        /// <summary>
        /// Identified the Label dependency property
        /// </summary>
        public static readonly DependencyProperty CloseIsDefaultCancelProperty =
            DependencyProperty.Register("CloseIsDefaultCancel", typeof(bool),
              typeof(DaxStudioDialog), new PropertyMetadata(false));


        public bool ShowDefaultClose
        {
            get { return (bool)GetValue(ShowDefaultCloseProperty); }
            set { SetValue(ShowDefaultCloseProperty, value); }
        }

        /// <summary>
        /// Identified the Label dependency property
        /// </summary>
        public static readonly DependencyProperty ShowDefaultCloseProperty =
            DependencyProperty.Register("ShowDefaultClose", typeof(bool),
              typeof(DaxStudioDialog), new PropertyMetadata(true));
        #endregion


    }
}
