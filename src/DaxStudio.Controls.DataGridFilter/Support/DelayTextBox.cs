using System;
using System.Windows.Controls;
using System.Timers;
using System.Windows;

namespace DaxStudio.Controls.DataGridFilter.Support
{
    /// <summary>
    /// WPF port of windows forms version: http://www.codeproject.com/KB/miscctrl/CustomTextBox.aspx
    /// </summary>
    public sealed class DelayTextBox : TextBox, IDisposable
    {
        #region private globals

        private Timer DelayTimer; // used for the delay
        private bool _timerElapsed; // if true OnTextChanged is fired.
        private bool _keysPressed; // makes event fire immediately if it wasn't a keypress
        private int DELAY_TIME = 250;//for now best empiric value

        public static readonly DependencyProperty DelayTimeProperty =
            DependencyProperty.Register("DelayTime", typeof(int), typeof(DelayTextBox));

        #endregion

        #region ctor

        public DelayTextBox()
            : base()
        {
            // Initialize Timer
            DelayTimer = new Timer(DELAY_TIME);
            DelayTimer.Elapsed += new ElapsedEventHandler(DelayTimer_Elapsed);

            previousTextChangedEventArgs = null;

            AddHandler(TextBox.PreviewKeyDownEvent, new System.Windows.Input.KeyEventHandler(DelayTextBox_PreviewKeyDown));

            PreviousTextValue = String.Empty;
        }

        void DelayTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!DelayTimer.Enabled)
                DelayTimer.Enabled = true;
            else
            {
                DelayTimer.Enabled = false;
                DelayTimer.Enabled = true;
            }

            _keysPressed = true;
        }

        #endregion

        #region event handlers

        void DelayTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            DelayTimer.Enabled = false;// stop timer.

            _timerElapsed = true;// set timer elapsed to true, so the OnTextChange knows to fire

            this.Dispatcher.Invoke(new DelayOverHandler(DelayOver), null);// use invoke to get back on the UI thread.
        }

        #endregion

        #region overrides

        private TextChangedEventArgs previousTextChangedEventArgs;
        public string PreviousTextValue { get; private set; }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            // if the timer elapsed or text was changed by something besides a keystroke
            // fire base.OnTextChanged
            if (_timerElapsed || !_keysPressed)
            {
                _timerElapsed = false;
                _keysPressed = false;
                base.OnTextChanged(e);

                System.Windows.Data.BindingExpression be = this.GetBindingExpression(TextBox.TextProperty);
                if (be != null && be.Status==System.Windows.Data.BindingStatus.Active) be.UpdateSource();

                PreviousTextValue = Text;
            }

            previousTextChangedEventArgs = e;
        }

        #endregion

        #region delegates

        public delegate void DelayOverHandler();

        #endregion

        #region private helpers

        private void DelayOver()
        {
            if (previousTextChangedEventArgs != null)
                OnTextChanged(previousTextChangedEventArgs);
        }

        public void Dispose()
        {
            DelayTimer.Dispose();
        }

        #endregion
    }
}
