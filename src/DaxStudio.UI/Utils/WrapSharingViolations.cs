using System;
using System.IO;

namespace DaxStudio.UI.Utils
{

    public static class SharingViolations
    {
        /// <summary>
        /// Wraps sharing violations that could occur on a file IO operation.
        /// </summary>
        /// <param name="action">The action to execute. May not be null.</param>
        public static void Wrap(WrapSharingViolationsCallback action)
        {
            Wrap(action, null, 10, 100);
        }

        /// <summary>
        /// Wraps sharing violations that could occur on a file IO operation.
        /// </summary>
        /// <param name="action">The action to execute. May not be null.</param>
        /// <param name="exceptionsCallback">The exceptions callback. May be null.</param>
        /// <param name="retryCount">The retry count.</param>
        /// <param name="waitTime">The wait time in milliseconds.</param>
        public static void Wrap(WrapSharingViolationsCallback action, WrapSharingViolationsExceptionsCallback exceptionsCallback, int retryCount, int waitTime)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException ioe)
                {
                    if ((IsSharingViolation(ioe)) && (i < (retryCount - 1)))
                    {
                        bool wait = true;
                        if (exceptionsCallback != null)
                        {
                            wait = exceptionsCallback(ioe, i, retryCount, waitTime);
                        }
                        if (wait)
                        {
                            System.Threading.Thread.Sleep(waitTime);
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Defines a sharing violation wrapper delegate.
        /// </summary>
        public delegate void WrapSharingViolationsCallback();

        /// <summary>
        /// Defines a sharing violation wrapper delegate for handling exception.
        /// </summary>
        public delegate bool WrapSharingViolationsExceptionsCallback(IOException ioe, int retry, int retryCount, int waitTime);

        /// <summary>
        /// Determines whether the specified exception is a sharing violation exception.
        /// </summary>
        /// <param name="exception">The exception. May not be null.</param>
        /// <returns>
        ///     <c>true</c> if the specified exception is a sharing violation exception; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsSharingViolation(IOException exception)
        {
            if (exception == null)
                throw new ArgumentNullException("exception");

            int hr = exception.HResult;
            return (hr == -2147024864); // 0x80070020 ERROR_SHARING_VIOLATION

        }
        
    }
}
