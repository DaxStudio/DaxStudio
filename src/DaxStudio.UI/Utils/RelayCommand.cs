using System;
using System.Diagnostics;
using System.Windows.Input;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// Class representing a command sent by a button in the UI, defines what to launch when the command is called
    /// </summary>
    public class RelayCommand : ICommand
    {
        readonly Action<object> _execute;
        readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute)
            : this(execute, null)
        {
        }

        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");

            _execute = execute;
            _canExecute = canExecute;
        }

        //[DebuggerStepThrough]
        /// <summary>
        /// Defines if the current command can be executed or not
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }
    }





      /// <summary>
      /// A command whose sole purpose is to 
      /// relay its functionality to other
      /// objects by invoking delegates. The
      /// default return value for the CanExecute
      /// method is 'true'.
      /// 
      /// Source: http://www.codeproject.com/Articles/31837/Creating-an-Internationalized-Wizard-in-WPF
      /// </summary>
    internal class RelayCommand<T> : ICommand
    {
        #region Fields
        private readonly Action<T> mExecute;
        private readonly Predicate<T> mCanExecute;
        #endregion // Fields

        #region Constructors
        /// <summary>
        /// Class constructor
        /// </summary>
        /// <param name="execute"></param>
        public RelayCommand(Action<T> execute)
          : this(execute, null)
        {
        }

        /// <summary>
        /// Creates a new command.
        /// </summary>
        /// <param name="execute">The execution logic.</param>
        /// <param name="canExecute">The execution status logic.</param>
        public RelayCommand(Action<T> execute, Predicate<T> canExecute)
        {
            this.mExecute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.mCanExecute = canExecute;
        }

        #endregion // Constructors

        #region events
        /// <summary>
        /// Eventhandler to re-evaluate whether this command can execute or not
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add
            {
                if (this.mCanExecute != null)
                    CommandManager.RequerySuggested += value;
            }

            remove
            {
                if (this.mCanExecute != null)
                    CommandManager.RequerySuggested -= value;
            }
        }
        #endregion

        #region methods
        /// <summary>
        /// Determine whether this pre-requisites to execute this command are given or not.
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        [DebuggerStepThrough]
        public bool CanExecute(object parameter)
        {
            return this.mCanExecute == null || this.mCanExecute((T)parameter);
        }

        /// <summary>
        /// Execute the command method managed in this class.
        /// </summary>
        /// <param name="parameter"></param>
        public void Execute(object parameter)
        {
            this.mExecute((T)parameter);
        }
        #endregion methods
    }

}
