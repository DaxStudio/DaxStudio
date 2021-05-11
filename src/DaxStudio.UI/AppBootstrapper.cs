using System.Diagnostics;
using System.Reflection;
using System.Windows;
using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Linq;
using Caliburn.Micro;
using System.Windows.Markup;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;
using Serilog;
using System.Windows.Input;
using DaxStudio.UI.Triggers;
using DaxStudio.UI.Utils;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.Interfaces;
using DaxStudio.UI.Model;
using MLib;
using MLib.Interfaces;


namespace DaxStudio.UI
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public class AppBootstrapper : BootstrapperBase//<IShell>
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
		CompositionContainer _container;
	    private readonly Assembly _hostAssembly;
        
	    public AppBootstrapper(Assembly hostAssembly, bool useApplication) : base(useApplication)
	    {
	        _hostAssembly = hostAssembly;
            base.Initialize();
	    }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            AssemblyLoader.PreJitControls();
            
        }

        public void DisplayShell()
        {
            Application.Dispatcher.Invoke(() => base.DisplayRootViewFor<IShell>(null));
        }

        protected override void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (e.Exception is ArgumentOutOfRangeException)
            {
                var st = new StackTrace(e.Exception);
                var sf = st.GetFrame(0);
                if (sf.GetMethod().Name == "GetLineByOffset")
                {
                    var eventAggregator = _container.GetExportedValue<IEventAggregator>();
                    if (eventAggregator != null) eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "Editor syntax highlighting attempted to scan byond the end of the current line"));
                    Log.Warning(e.Exception, "{class} {method} AvalonEdit TextDocument.GetLineByOffset: {message}", "EntryPoint", "Main", "Argument out of range exception");
                    e.Handled = true;
                    return;
                }
            }


            base.OnUnhandledException(sender, e);
            Debug.WriteLine(e.Exception);
            Log.Error("{Class} {Method} {Exception}", "AppBootstrapper", "OnUnhandledException", e.Exception);
            Log.Error("{Class} {Method} {InnerException}", "AppBootstrapper", "OnUnhandledException-InnerException", e.Exception.InnerException);
        }

	    /// <summary>
		/// By default, we are configured to use MEF
		/// </summary>
		protected override void Configure() {
            try
            {
                var splashScreen = new SplashScreen(Assembly.GetAssembly(typeof(AppBootstrapper)), "daxstudio-splash.png");
                splashScreen.Show(true);

                // Tell Caliburn Micro how to find controls in Fluent Ribbon
                /*
                defaultElementLookup = BindingScope.GetNamedElements;
                BindingScope.GetNamedElements = new Func<System.Windows.DependencyObject, IEnumerable<System.Windows.FrameworkElement>>(
                    k =>
                    {
                        List<FrameworkElement> namedElements = new List<FrameworkElement>();
                        namedElements.AddRange(defaultElementLookup(k));
                        Fluent.Ribbon ribbon = LookForRibbon(k);
                        if (null != ribbon)
                            AppendRibbonNamedItem(ribbon, namedElements);
                        return namedElements;
                    }
                    );
                */

                ConventionManager.AddElementConvention<Fluent.Spinner>(Fluent.Spinner.ValueProperty, "Value", "ValueChanged");

                // Add Fluent Ribbon resolver
                BindingScope.AddChildResolver<Fluent.Ribbon>(FluentRibbonChildResolver);


                // Fixes the default datetime format in the results ListView
                // from: https://stackoverflow.com/questions/1993046/datetime-region-specific-formatting-in-wpf-listview
                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

	            var catalog = new AggregateCatalog(
	                AssemblySource.Instance.Select(x => new AssemblyCatalog(x)).OfType<ComposablePartCatalog>()
	                );
	            //_container = new CompositionContainer(catalog,true);
                _container = new CompositionContainer(catalog);
	            var batch = new CompositionBatch();



                
	            batch.AddExportedValue<IWindowManager>(new WindowManager());
	            batch.AddExportedValue<IEventAggregator>(new EventAggregator());
	            batch.AddExportedValue<Func<DocumentViewModel>>(() => _container.GetExportedValue<DocumentViewModel>());
	            batch.AddExportedValue<Func<IWindowManager, IEventAggregator, DocumentViewModel>>(
	                (w, e) => _container.GetExportedValue<DocumentViewModel>());
	            batch.AddExportedValue(_container);
	            batch.AddExportedValue(catalog);

                ISettingProvider settingProvider = SettingsProviderFactory.GetSettingProvider();

                batch.AddExportedValue<ISettingProvider>(settingProvider);

                // add support for MLib themes
                batch.AddExportedValue<IAppearanceManager>(AppearanceManager.GetInstance());

                _container.Compose(batch);

	            // Add AvalonDock binding conventions
	            AvalonDockConventions.Install();

                //var settingFactory = _container.GetExport<Func<ISettingProvider>>();

                

                ConfigureKeyBindingConvention();

	            // TODO - not working
	            //VisibilityBindingConvention.Install();

                // Enable Caliburn.Micro debug logging
	            //LogManager.GetLog = type => new DebugLogger(type);

                // Add Application object to MEF catalog
                _container.ComposeExportedValue<Application>("System.Windows.Application", Application.Current);
            }
	        catch (Exception e)
	        {
	            Debug.WriteLine(e);
	        }
		}

        public IThemeManager GetThemeManager()
        {
            return GetInstance(typeof(IThemeManager), null) as IThemeManager;
        }

        public IEventAggregator GetEventAggregator()
        {
            return GetInstance(typeof(IEventAggregator), null) as IEventAggregator;
        }

        public IGlobalOptions GetOptions()
        {
            return GetInstance(typeof(IGlobalOptions), null) as IGlobalOptions;
        }

        protected override object GetInstance(Type serviceType, string key)
		{
			var contract = string.IsNullOrEmpty(key) ? AttributedModelServices.GetContractName(serviceType) : key;
			var exports = _container.GetExportedValues<object>(contract);

			if (exports.Any())
				return exports.First();

			throw new Exception($"Could not locate any instances of contract {contract}.");
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
            var type = typeof(IDaxStudioHost);
            var hostType = AppDomain.CurrentDomain.GetAssemblies()
                .ToList()
                .SelectMany(s => s.GetTypes())
                .FirstOrDefault(p => type.IsAssignableFrom(p));
            var hostAssembly = Assembly.GetAssembly(hostType);

            return AssemblySource.Instance.Any() ?
                Array.Empty<Assembly>() : 
                new[] {
                    Assembly.GetExecutingAssembly()
                    ,hostAssembly
                };
        }

        private Fluent.Ribbon LookForRibbon(DependencyObject k)
        {
            Fluent.Ribbon foundRibbon = null;
            if (k is ContentControl contentControl)
            {
                if (contentControl.Content is DependencyObject child)
                {
                    foundRibbon = child as Fluent.Ribbon;
                    if (null != foundRibbon)
                    {
                        return foundRibbon;
                    }
                    else
                    {
                        foundRibbon = LookForRibbon(child);
                        if (null != foundRibbon)
                            return foundRibbon;
                    }
                }
                //return LookForRibbon(child);
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(k); ++i)
            {
                var child = VisualTreeHelper.GetChild(k, i);
                foundRibbon = child as Fluent.Ribbon;
                if (null != foundRibbon)
                {
                    return foundRibbon;
                }
                else
                {
                    foundRibbon = LookForRibbon(child);
                    if (null != foundRibbon)
                        return foundRibbon;
                }
            }
            return null;
        }

        //private void AppendRibbonNamedItem(Fluent.Ribbon ribbon, List<FrameworkElement> namedElements)
        //{
        //    foreach (var ti in ribbon.Tabs)
        //    {
        //        foreach (var group in ti.Groups)
        //        {
        //            namedElements.AddRange(defaultElementLookup(group));
        //        }
        //    }
        //}

        private void ConfigureKeyBindingConvention()
        {
            var trigger = Parser.CreateTrigger;

            Parser.CreateTrigger = (target, triggerText) =>
            {
                if (triggerText == null)
                {
                    var defaults = ConventionManager.GetElementConvention(target.GetType());
                    return defaults.CreateTrigger();
                }

                var triggerDetail = triggerText
                    .Replace("[", string.Empty)
                    .Replace("]", string.Empty);

                var splits = triggerDetail.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (splits[0] == "Key")
                {
                    var key = (Key)Enum.Parse(typeof(Key), splits[1], true);
                    return new KeyTrigger { Key = key };
                }

                return trigger(target, triggerText);
            };
        }


        static IEnumerable<System.Windows.DependencyObject> FluentRibbonChildResolver(Fluent.Ribbon ribbon)
        {
            /*
            foreach (var ti in ribbon.Tabs)
            {
                foreach (var group in ti.Groups)
                {
                    foreach (var obj in BindingScope.GetNamedElements(group))
                        yield return obj;
                }
            }
            */
            
            var backstage = ribbon.Menu as Fluent.Backstage;
            var backstageTabs = backstage.Content as Fluent.BackstageTabControl;
            BindingScope.GetNamedElements(backstageTabs);

            foreach (var backstageTab in backstageTabs.Items)
            {
                ///foreach (var obj in BindingScope.GetNamedElements(backstageTab))
                if (backstageTab is ContentControl control)
                    foreach (var obj in BindingScope.GetNamedElements(control))
                        yield return obj;
            }
            
            
        }
    }
}