using System;
using System.ComponentModel;

namespace DaxStudio.UI.Utils
{
    public static class EventExtension
    {
        /// <summary>
        /// Safely raises any EventHandler event asynchronously.
        /// </summary>
        /// <param name="thisEvent">The object we are extending</param>
        /// <param name="sender">The object raising the event (usually this).</param>
        /// <param name="e">The EventArgs for this event.</param>
        public static void Raise(this MulticastDelegate thisEvent, object sender,
                                 EventArgs e)
        {
            var callback = new AsyncCallback(EndAsynchronousEvent);

            foreach (Delegate d in thisEvent.GetInvocationList())
            {
                var uiMethod = d as EventHandler;
                if (uiMethod != null)
                {
                    var target = d.Target as ISynchronizeInvoke;
                    if (target != null) target.BeginInvoke(uiMethod, new[] {sender, e});
                    else uiMethod.BeginInvoke(sender, e, callback, uiMethod);
                }
            }
        }

        private static void EndAsynchronousEvent(IAsyncResult result)
        {
            ((EventHandler) result.AsyncState).EndInvoke(result);
        }
    }
}
