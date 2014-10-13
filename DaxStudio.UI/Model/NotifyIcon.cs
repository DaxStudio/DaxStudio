using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;

namespace DaxStudio.UI.Model
{
    public class NotifyIcon
    {
        private readonly TaskbarIcon icon;

        public NotifyIcon()
        {
            Uri iconUri = new Uri("pack://application:,,,/DaxStudio.UI;component/Images/DaxStudio.Ico");
            using (var strm = Application.GetResourceStream(iconUri).Stream)
            {
                var ico = new System.Drawing.Icon(strm);
                icon = new TaskbarIcon
                {
                    Name = "NotifyIcon",
                    Icon = ico,
                    Visibility = Visibility.Collapsed
                };
                icon.TrayBalloonTipClicked += icon_TrayBalloonTipClicked;
            }
           
        }

        private void icon_TrayBalloonTipClicked(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(DownloadUrl);
        }

        public string DownloadUrl { get; set; }

        public void Notify(string message, string downloadUrl)
        {
            DownloadUrl = downloadUrl;
            icon.Visibility = Visibility.Visible;
            icon.ShowBalloonTip("DaxStudio", message, BalloonIcon.None); //TODO - get current version for title
        }
    }
}
