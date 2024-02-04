﻿using System;
using DaxStudio.UI.ViewModels;
using DaxStudio.Interfaces;

namespace DaxStudio.UI.Model
{
    public class StatusBarMessage : IStatusBarMessage
    {
        private IHaveStatusBar _document;
        public StatusBarMessage(IHaveStatusBar document, string message)
        {
            _document = document;
            _document?.SetStatusBarMessage(message);           
        }

        public void Dispose()
        {
            _document?.SetStatusBarMessage("Ready");
            _document = null;
        }

        public void Update(string message)
        {
            _document.SetStatusBarMessage(message);
        }

        public bool IsDisposed
        {
            get { return _document == null; }
        }
    }
}
