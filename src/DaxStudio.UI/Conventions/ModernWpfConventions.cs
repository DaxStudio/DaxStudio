using Caliburn.Micro;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Conventions
{
    public class ModernWpfConventions: ElementConvention
    {
        public static void Install()
        {

            ConventionManager.AddElementConvention<ToggleSwitch>(ToggleSwitch.IsOnProperty, "IsOn", "Toggled");

            var baseBindProperties = ViewModelBinder.BindProperties;
            ViewModelBinder.BindProperties =
                (frameWorkElements, viewModel) =>
                {
                    var switches = frameWorkElements.OfType<ToggleSwitch>();
                    BindIsOnProperties(switches, viewModel);
                    return baseBindProperties(frameWorkElements, viewModel);
                };

            // Need to override BindActions as well, as it's called first and filters out anything it binds to before
            // BindProperties is called.
            var baseBindActions = ViewModelBinder.BindActions;
            ViewModelBinder.BindActions =
                (frameWorkElements, viewModel) =>
                {
                    var switches = frameWorkElements.OfType<ToggleSwitch>();
                    BindIsOnProperties(switches, viewModel);
                    return baseBindActions(frameWorkElements, viewModel);
                };

        }

        static void BindIsOnProperties(IEnumerable<ToggleSwitch> frameWorkElements, Type viewModel)
        {
            foreach (var frameworkElement in frameWorkElements)
            {
                var propertyName = frameworkElement.Name + "IsOn";
                var property = viewModel.GetPropertyCaseInsensitive(propertyName);
                if (property != null)
                {
                    var convention = ConventionManager
                        .GetElementConvention(typeof(ToggleSwitch));
                    ConventionManager.SetBindingWithoutBindingOverwrite(
                        viewModel,
                        propertyName,
                        property,
                        frameworkElement,
                        convention,
                        convention.GetBindableProperty(frameworkElement));
                }
            }
        }
    }
}
