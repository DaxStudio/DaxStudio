using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Reflection;
using System.Xml;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// Loads and caches .xshd syntax highlighting definitions for xmSQL and DirectQuery SQL.
    /// Automatically applies the current theme colors when definitions are first loaded.
    /// </summary>
    internal static class SyntaxHighlightingHelper
    {
        private static IHighlightingDefinition _xmSqlHighlighting;
        private static IHighlightingDefinition _directQuerySqlHighlighting;

        public static IHighlightingDefinition XmSqlHighlighting
        {
            get
            {
                if (_xmSqlHighlighting == null)
                {
                    _xmSqlHighlighting = LoadHighlighting("DaxStudio.UI.Resources.xmSQL.xshd");
                    SetColorTheme(_xmSqlHighlighting, GetCurrentTheme());
                }
                return _xmSqlHighlighting;
            }
        }

        public static IHighlightingDefinition DirectQuerySqlHighlighting
        {
            get
            {
                if (_directQuerySqlHighlighting == null)
                {
                    _directQuerySqlHighlighting = LoadHighlighting("DaxStudio.UI.Resources.DirectQuerySql.xshd");
                    SetColorTheme(_directQuerySqlHighlighting, GetCurrentTheme());
                }
                return _directQuerySqlHighlighting;
            }
        }

        private static string GetCurrentTheme()
        {
            try
            {
                var theme = ModernWpf.ThemeManager.Current.ActualApplicationTheme;
                return theme == ModernWpf.ApplicationTheme.Dark ? "Dark" : "Light";
            }
            catch
            {
                return "Light";
            }
        }

        private static IHighlightingDefinition LoadHighlighting(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new XmlTextReader(stream) { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit })
            {
                return HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }

        /// <summary>
        /// Updates the syntax highlighting colors to match the current theme.
        /// Call this when the theme changes (Light/Dark).
        /// </summary>
        public static void SetColorTheme(IHighlightingDefinition highlighting, string theme)
        {
            if (highlighting == null) return;
            var prefix = theme + ".";
            foreach (var syntaxHighlight in highlighting.NamedHighlightingColors)
            {
                if (syntaxHighlight.Name.StartsWith(prefix, System.StringComparison.InvariantCultureIgnoreCase))
                {
                    var suffix = syntaxHighlight.Name.Replace(prefix, "");
                    var baseColor = highlighting.GetNamedColor(suffix);
                    if (baseColor != null)
                    {
                        baseColor.Foreground = syntaxHighlight.Foreground;
                        baseColor.Background = syntaxHighlight.Background;
                        baseColor.FontWeight = syntaxHighlight.FontWeight;
                        baseColor.FontStyle = syntaxHighlight.FontStyle;
                    }
                }
            }
        }

        /// <summary>
        /// Updates all cached highlighting definitions to the specified theme.
        /// </summary>
        public static void SetAllColorThemes(string theme)
        {
            SetColorTheme(XmSqlHighlighting, theme);
            SetColorTheme(DirectQuerySqlHighlighting, theme);
        }
    }
}
