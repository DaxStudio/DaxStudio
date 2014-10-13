using System.Diagnostics;
using System.Reflection;
using System.Windows;
using DaxStudio.UI.ViewModels;


namespace DaxStudio.UI
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel.Composition;
	using System.ComponentModel.Composition.Hosting;
	using System.ComponentModel.Composition.Primitives;
	using System.Linq;
	using Caliburn.Micro;
    using System.Windows.Markup;
    using System.Globalization;

    public class AppBootstrapper : BootstrapperBase//<IShell>
	{
		CompositionContainer _container;
	    private Assembly _hostAssembly;
	    /*
        public AppBootstrapper():base(true)
        {
        }
        */
	    public AppBootstrapper(Assembly hostAssembly, bool useApplication) : base(useApplication)
	    {
	        _hostAssembly = hostAssembly;
            base.Initialize();
	    }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            base.DisplayRootViewFor<IShell>(null);
        }

        protected override void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            base.OnUnhandledException(sender, e);
            Debug.WriteLine(e.Exception);
        }
	    /// <summary>
		/// By default, we are configured to use MEF
		/// </summary>
		protected override void Configure() {
            try
            {
                var splashScreen = new SplashScreen(Assembly.GetAssembly(typeof(AppBootstrapper)), "daxstudio-logo_250x250.png");
                splashScreen.Show(true);

                // Fixes the default datetime format in the results listview
                // from: http://stackoverflow.com/questions/1993046/datetime-region-specific-formatting-in-wpf-listview
                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

	            var catalog = new AggregateCatalog(
	                AssemblySource.Instance.Select(x => new AssemblyCatalog(x)).OfType<ComposablePartCatalog>()
	                );
	            _container = new CompositionContainer(catalog);
	            var batch = new CompositionBatch();

	            batch.AddExportedValue<IWindowManager>(new WindowManager());
	            batch.AddExportedValue<IEventAggregator>(new EventAggregator());
	            batch.AddExportedValue<Func<DocumentViewModel>>(() => _container.GetExportedValue<DocumentViewModel>());
	            batch.AddExportedValue<Func<IWindowManager, IEventAggregator, DocumentViewModel>>(
	                (w, e) => _container.GetExportedValue<DocumentViewModel>());
	            batch.AddExportedValue(_container);
	            batch.AddExportedValue(catalog);

	            _container.Compose(batch);

	            // Add AvalonDock binding convetions
	            AvalonDockConventions.Install();

	            // TODO - not working
	            //VisibilityBindingConvention.Install();

	            LogManager.GetLog = type => new DebugLogger(type);
	        }
	        catch (Exception e)
	        {
	            Debug.WriteLine(e);
	        }
		}

		protected override object GetInstance(Type serviceType, string key)
		{
			var contract = string.IsNullOrEmpty(key) ? AttributedModelServices.GetContractName(serviceType) : key;
			var exports = _container.GetExportedValues<object>(contract);

			if (exports.Any())
				return exports.First();

			throw new Exception(string.Format("Could not locate any instances of contract {0}.", contract));
		}

		protected override IEnumerable<object> GetAllInstances(Type serviceType)
		{
			return _container.GetExportedValues<object>(AttributedModelServices.GetContractName(serviceType));
		}

		protected override void BuildUp(object instance)
		{
			_container.SatisfyImportsOnce(instance);
		}

        // This override causes Caliburn Micro to pass this Assembly to MEF
        protected override IEnumerable<Assembly> SelectAssemblies()
        {
            var type = typeof(DaxStudio.Interfaces.IDaxStudioHost);
            var hostType = AppDomain.CurrentDomain.GetAssemblies().ToList()
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p))
                .FirstOrDefault();
            var hostAssembly = Assembly.GetAssembly(hostType);

            return AssemblySource.Instance.Any() ?
                new Assembly[] { } : 
                new[] {
                    Assembly.GetExecutingAssembly()
                    ,hostAssembly
                };
        }


	}
}