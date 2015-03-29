using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive;

namespace DaxStudio.UI.Utils
{
    public class PropertyObserver : IDisposable
    {
        public PropertyObserver(INotifyPropertyChanged parentINPC, string propertyToWatch, bool executeAction, Action<EventPattern<PropertyChangedEventArgs>> action)
        {
            this.parentINPC = parentINPC;
            this.propertyToWatch = propertyToWatch;
            this.action = action;
            observer = Observable.FromEventPattern<PropertyChangedEventArgs>(parentINPC, "PropertyChanged")
            .Where(ev => string.IsNullOrEmpty(ev.EventArgs.PropertyName)
            || propertyToWatch == ev.EventArgs.PropertyName
            || propertyToWatch == "*")
            .Subscribe(ev =>
            {
                if (executeAction)
                {
                    action(ev);
                }
                RefreshChildren(ev.EventArgs.PropertyName);
            });
        }

        public PropertyObserver(INotifyPropertyChanged parentINPC, string propertyToWatch, bool executeAction, Action<EventPattern<PropertyChangedEventArgs>> action, IEnumerable<string> childArguments)
            : this(parentINPC, propertyToWatch, executeAction, action)
        {
            this.childArguments = childArguments;
        }

        public string PropertyToWatch
        {
            get { return propertyToWatch; }
        }

        private IEnumerable<IEnumerable<string>> ChildrenArguments
        {
            get { return Enumerable.Repeat(childArguments, 1).Concat(children.SelectMany(x => x.ChildrenArguments)).Where(x => x != null); }
        }

        public void AddChild(IEnumerable<string> arguments)
        {
            var args = arguments.ToArray();
            if (args[0] != propertyToWatch)
            {
                throw new ArgumentException();
            }
            if (args.Length - 2 == 0)
            {
                var childObserver = CreateChildObserver(args, true);
                if (childObserver == null)
                {
                    return;
                }
                children.Add(childObserver);
            }
            else
            {
                var childProperty = args[1];
                var child = children.FirstOrDefault(po => po.PropertyToWatch == childProperty);
                if (child == null)
                {
                    child = CreateChildObserver(args, false);
                    if (child == null)
                    {
                        return;
                    }
                    children.Add(child);
                }
                child.AddChild(arguments.Skip(1));
            }
        }

        public void Dispose()
        {
            observer.Dispose();
            foreach (var child in children)
            {
                child.Dispose();
            }
        }

        private PropertyObserver CreateChildObserver(string[] args, bool executeAction)
        {
            var propertyName = args[0];
            var property = GetProperty(parentINPC, propertyName);
            var inpc = property as INotifyPropertyChanged;
            return inpc == null
            ? null
            : new PropertyObserver(inpc, args[1], executeAction, action, args);
        }

        private object GetProperty(object target, string propertyName)
        {
            var propInfo = target.GetType().GetProperty(propertyName);
            if (propInfo == null)
            {
                return null;
            }
            return propInfo.GetValue(target, null);
        }

        private void RefreshChildren(string propertyName)
        {
            var childrenArgs = ChildrenArguments.Where(x => x.First() == propertyName).ToArray();
            foreach (var child in children)
            {
                child.Dispose();
            }
            children.Clear();
            foreach (var childArgs in childrenArgs)
            {
                AddChild(childArgs);
            }
        }

        private readonly Action<EventPattern<PropertyChangedEventArgs>> action;
        private readonly IEnumerable<string> childArguments;
        private readonly IList<PropertyObserver> children = new List<PropertyObserver>();
        private readonly IDisposable observer;
        private readonly INotifyPropertyChanged parentINPC;
        private readonly string propertyToWatch;
    }
}
