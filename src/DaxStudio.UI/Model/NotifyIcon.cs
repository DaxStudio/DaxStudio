using System;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;
using Caliburn.Micro;
using Serilog;
using DaxStudio.UI.Events;

namespace DaxStudio.UI.Model
{
    public class NotifyIcon : IDisposable
    {
        private TaskbarIcon icon;
        private string BalloonMessage;
        private System.Drawing.Icon _icon;

        public NotifyIcon(Window window, IEventAggregator eventAggreator) 
        {
            BalloonTitle = "DAX Studio"; //TODO - get current version for title
            System.Drawing.Icon ico = Icon;
            EventAggregator = eventAggreator;
            window.Dispatcher.Invoke(new System.Action(() =>
            {
                icon = new TaskbarIcon
                {
                    Name = "NotifyIcon",
                    Icon = ico,
                    Visibility = Visibility.Collapsed,
                    ToolTipText = "DAX Studio"
                };
                
                icon.TrayBalloonTipClicked += icon_TrayBalloonTipClicked;
                icon.TrayLeftMouseDown += icon_TrayMouseDown;
                icon.TrayRightMouseDown += icon_TrayMouseDown;
            }));
        }

        private System.Drawing.Icon Icon {
            get {
                if (_icon == null)
                {
                    Uri iconUri = new Uri("pack://application:,,,/DaxStudio.UI;component/Images/DaxStudio.Ico");
                    using (var strm = Application.GetResourceStream(iconUri).Stream)
                    {
                        _icon = new System.Drawing.Icon(strm);
                    }
                }
                return _icon;
            }
        }

        private void icon_TrayMouseDown(object sender, RoutedEventArgs e)
        {
            icon.ShowBalloonTip(BalloonTitle, BalloonMessage, BalloonIcon.Info);
        }

        private void icon_TrayBalloonTipClicked(object sender, RoutedEventArgs e)
        {
            if (DownloadUrl != null)
            {
                try
                {
                    System.Diagnostics.Process.Start(DownloadUrl);

                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{class} {method} {message}", nameof(NotifyIcon), nameof(icon_TrayBalloonTipClicked), "Unable to open log folder");
                    EventAggregator.PublishOnUIThread(new OutputMessage( MessageType.Error, $"Unable to open log folder: '{DownloadUrl}'\n{ex.Message}"));
                }
            }
        
        }

        public string DownloadUrl { get; set; }

        public void Notify(string message, string downloadUrl)
        {
            BalloonMessage = message;
            DownloadUrl = downloadUrl;
            icon.Visibility = Visibility.Visible;
            icon.ShowBalloonTip(BalloonTitle, BalloonMessage, BalloonIcon.Info); 
        }
        public string BalloonTitle { get; set; }
        public IEventAggregator EventAggregator { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (icon != null)
                {
                    icon.TrayBalloonTipClicked -= icon_TrayBalloonTipClicked;
                    icon.TrayLeftMouseDown -= icon_TrayMouseDown;
                    icon.TrayRightMouseDown -= icon_TrayMouseDown;
                    icon.Dispose();
                    icon = null;
                }
                if (_icon != null)
                {
                    _icon.Dispose();
                    _icon = null;
                }
            }
        }
    }
}
