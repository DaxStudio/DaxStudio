﻿namespace UnitComboLib.ViewModel
{
    using System;
    using System.ComponentModel;
    using System.Linq.Expressions;

    /// <summary>
    /// Base class for viewmodel classes being used to communicate
    /// between model and view via <seealso cref="INotifyPropertyChanged"/> interface.
    /// </summary>
    public class BaseViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Event that is fired when a property in the viewmodel changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Method to invoke when a property has changed its value. Call convention:
        /// 
        /// this.NotifyPropertyChanged(() => this.MyProperty);
        /// </summary>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="property"></param>
        internal void NotifyPropertyChanged<TProperty>(Expression<Func<TProperty>> property)
        {
            var lambda = (LambdaExpression)property;
            MemberExpression memberExpression;

            if (lambda.Body is UnaryExpression unaryExpression)
            {
                memberExpression = (MemberExpression)unaryExpression.Operand;
            }
            else
                memberExpression = (MemberExpression)lambda.Body;

            this.OnPropertyChanged(memberExpression.Member.Name);
        }

        private void OnPropertyChanged(string propertyName)
        {
            try
            {
                if (this.PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
            }
        }
    }
}
