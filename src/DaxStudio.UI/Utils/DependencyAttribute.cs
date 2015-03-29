using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;

namespace DaxStudio.UI.Utils
{
    
    public class DependenciesAttribute : Attribute, IContextAware
    {
        public DependenciesAttribute(params string[] propertyNames)
        {
            dependentProperties = propertyNames ?? new string[] { };
            dependentProperties = dependentProperties.OrderBy(x => x);
        }

        public int Priority { get; set; }

        public void Dispose()
        {
            foreach (var o in observers) 
            { o.Dispose(); }
            context = null;
        }

        public void MakeAwareOf(ActionExecutionContext context)
        {
            this.context = context;
            var inpc = context.Target as INotifyPropertyChanged;
            if (inpc == null)
            {
                return;
            }
            foreach (var viewModelProperty in ViewModelProperties(inpc))
            {
                observers.Add(new PropertyObserver(inpc, viewModelProperty, true, UpdateAvailability));
            }
            var otherProperties = from dependentProperty in dependentProperties.Select(propertyName => propertyName.Split('.'))
                                  where dependentProperty.Length > 1
                                  select dependentProperty;
            foreach (var args in otherProperties)
            {
                var viewModelProperty = args[0];
                var observer = observers.FirstOrDefault(po => po.PropertyToWatch == viewModelProperty);
                if (observer == null)
                {
                    observers.Add(observer = new PropertyObserver(inpc, viewModelProperty, false, UpdateAvailability));
                }
                observer.AddChild(args);
            }
        }

        private void UpdateAvailability(EventPattern<PropertyChangedEventArgs> ev)
        {
            Execute.OnUIThread(() => context.Message.UpdateAvailability());
        }

        private IEnumerable<string> ViewModelProperties(object target)
        {
            var properties = from property in dependentProperties.Select(propertyName => propertyName.Split('.'))
                             where property.Length == 1
                             select property[0];
            if (properties.Contains("*"))
            {
                return from propInfo in target.GetType().GetProperties()
                       where propInfo.CanRead
                       select propInfo.Name;
            }
            return properties;
        }

        private readonly IEnumerable<string> dependentProperties;
        private readonly IList<PropertyObserver> observers = new List<PropertyObserver>();
        private ActionExecutionContext context;
    }



        public interface IContextAware : IFilter, IDisposable
        {
            #region Public Methods and Operators

            void MakeAwareOf(ActionExecutionContext context);

            #endregion
        }

        public interface IFilter
        {
            #region Public Properties

            int Priority { get; }

            #endregion
        }
}
