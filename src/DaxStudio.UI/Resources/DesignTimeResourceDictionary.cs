using System;
using System.ComponentModel;
using System.Windows;

namespace DaxStudio.UI.Resources
{
    public class DesignTimeResourceDictionary : ResourceDictionary
    {
        /// <summary>
        /// Local field storing info about designtime source.
        /// </summary>
        private string designTimeSource;

        /// <summary>
        /// Gets or sets the design time source.
        /// </summary>
        /// <value>
        /// The design time source.
        /// </value>
        public string DesignTimeSource
        {
            get
            {
                return this.designTimeSource;
            }

            set
            {
                this.designTimeSource = value;
                if ((bool)DesignerProperties.IsInDesignModeProperty.GetMetadata(typeof(DependencyObject)).DefaultValue)
                {
                    base.Source = new Uri(designTimeSource);
                }
            }
        }

        /// <summary>
        /// Gets or sets the uniform resource identifier (URI) to load resources from.
        /// </summary>
        /// <returns>The source location of an external resource dictionary. </returns>
        public new Uri Source
        {
            get
            {
                return base.Source;
            }

            set
            {
                throw new Exception("Use DesignTimeSource instead Source!");
            }
        }
    }
}
