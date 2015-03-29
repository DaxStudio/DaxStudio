using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;
using System.Windows.Threading;

namespace DaxStudio.UI.Model
{
    public class NotifyIcon : IDisposable
    {
        private TaskbarIcon icon;
        private string BalloonMessage;

        public NotifyIcon() 
        {
            BalloonTitle = "DaxStudio";
            Uri iconUri = new Uri("pack://application:,,,/DaxStudio.UI;component/Images/DaxStudio.Ico");
            System.Drawing.Icon ico;
            using (var strm = Application.GetResourceStream(iconUri).Stream)
            {
                ico = new System.Drawing.Icon(strm);
            }
            Dispatcher.CurrentDispatcher.Invoke(new System.Action(() =>
            {
                icon = new TaskbarIcon
                {
                    Name = "NotifyIcon",
                    Icon = ico,
                    Visibility = Visibility.Collapsed
                };
                icon.TrayBalloonTipClicked += icon_TrayBalloonTipClicked;
                icon.TrayLeftMouseDown += icon_TrayMouseDown;
                icon.TrayRightMouseDown += icon_TrayMouseDown;
            }));
        }

        private void icon_TrayMouseDown(object sender, RoutedEventArgs e)
        {
            icon.ShowBalloonTip(BalloonTitle, BalloonMessage, BalloonIcon.Info);
        }

        private void icon_TrayBalloonTipClicked(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(DownloadUrl);
        }

        public string DownloadUrl { get; set; }

        public void Notify(string message, string downloadUrl)
        {
            DownloadUrl = downloadUrl;
            BalloonMessage = message;
            icon.Visibility = Visibility.Visible;
            icon.ShowBalloonTip(BalloonTitle, BalloonMessage, BalloonIcon.Info); //TODO - get current version for title
        }
        public string BalloonTitle { get; set; }

        public void Dispose()
        {
            icon.TrayBalloonTipClicked -= icon_TrayBalloonTipClicked;
            icon.TrayLeftMouseDown -= icon_TrayMouseDown;
            icon.TrayRightMouseDown -= icon_TrayMouseDown;
            icon.Dispose();
        }
    }
}
