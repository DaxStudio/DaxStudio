using Caliburn.Micro;
using System;
using System.Collections.Generic;

namespace DaxStudio.UI.ViewModels.ExportDataWizard
{
#pragma warning disable CA2237 // Mark ISerializable types with serializable
    public class TransitionMap : Dictionary<Type, Dictionary<ExportDataWizardPage, Type>>
#pragma warning restore CA2237 // Mark ISerializable types with serializable
    {

        public TransitionMap() { }

        public  void Add<TIdentity, TResponse>(ExportDataWizardPage transition)
            where TIdentity : IScreen
            where TResponse : IScreen
        {

            if (!this.ContainsKey(typeof(TIdentity)))
            {
                Add(typeof(TIdentity), new Dictionary<ExportDataWizardPage, Type>() { { transition, typeof(TResponse) } });
            }
            else
            {
                this[typeof(TIdentity)].Add(transition, typeof(TResponse));
            }
        }

        public Type GetNextScreenType(ExportDataWizardBasePageViewModel screenThatClosed)
        {

            var identity = screenThatClosed.GetType();
            var transition = screenThatClosed.NextPage;

            if (!this.ContainsKey(identity))
            {
                throw new InvalidOperationException(string.Format("There are no states transitions defined for state {0}", identity.ToString()));
            }

            if (!this[identity].ContainsKey(transition))
            {
                throw new InvalidOperationException(string.Format("There is no response setup for transition {0} from screen {1}", transition.ToString(), identity.ToString()));
            }

            return this[identity][transition];
        }


    }
}
