using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Utils.Intellisense
{
    /// <summary>
    /// Creates the appropriate intellisense provider based on the current options. When the
    /// <c>UseAntlrCodeCompletion</c> preview option is enabled the new ANTLR grammar-based provider is
    /// returned, otherwise the established regex-based provider is used.
    /// </summary>
    public static class IntellisenseProviderFactory
    {
        public static IDaxIntellisenseProvider Create(IDaxDocument document, IEventAggregator eventAggregator, IGlobalOptions options)
        {
            if (options != null && options.UseAntlrCodeCompletion)
            {
                return new AntlrIntellisenseProvider(document, eventAggregator, options);
            }
            return new DaxIntellisenseProvider(document, eventAggregator, options);
        }
    }
}
