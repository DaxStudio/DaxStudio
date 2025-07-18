﻿using Caliburn.Micro;
using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    class GettingStartedViewModel:BaseDialogViewModel, IDisposable
    {
        public GettingStartedViewModel(DocumentViewModel document, IGlobalOptions options)
        {
            Options = options;
            Document = document;
        }

        public override void Close()
        {
            System.Diagnostics.Debug.WriteLine("Dialog Close");
            this.TryCloseAsync();
        }

        public void OpenQueryBuilder()
        {
            Document.OpenQueryBuilder();
            TryCloseAsync();
        }

        private bool _showHelpWatermark = true;
        public bool ShowHelpWatermark
        {
            get => _showHelpWatermark && !NeverShowHelpWatermark;
            set
            {
                if (value == _showHelpWatermark) return;
                _showHelpWatermark = value;
                NotifyOfPropertyChange(nameof(ShowHelpWatermark));

            }
        }

        public bool NeverShowHelpWatermark
        {
            get => !Options.ShowHelpWatermark;
            set
            {
                Options.ShowHelpWatermark = !value;
                NotifyOfPropertyChange(nameof(ShowHelpWatermark));
            }
        }

        public IGlobalOptions Options { get; }
        public DocumentViewModel Document { get; }

        public void Dispose()
        {
            // do nothing
        }
    }
}
