using System;
using System.Security;
using Caliburn.Micro;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Extensions;

namespace DaxStudio.UI.ViewModels
{
    public class ExportDataWizardSqlConnBuilderViewModel: ExportDataWizardBasePageViewModel
    {

        public ExportDataWizardSqlConnBuilderViewModel( ExportDataWizardViewModel parent):base(parent)
        {

        }

        #region Properties
        public string ServerName {
            get { return Wizard.ServerName; }
            set {
                Wizard.ServerName = value;
                NotifyOfPropertyChange(() => CanNext);
            }
        }
        public string Username {
            get { return Wizard.Username; }
            set {
                Wizard.Username = value;
                NotifyOfPropertyChange(() => CanNext);
            }
        }
        public SecureString SecurePassword {
            get { return Wizard.SecurePassword; }
            set {
                Wizard.SecurePassword = value;
                NotifyOfPropertyChange(() => CanNext);
            }
        }

        public string Database
        {
            get { return Wizard.Database; }
            set
            {
                Wizard.Database = value;
                NotifyOfPropertyChange(() => CanNext);
            }
        }

        public string Schema
        {
            get { return Wizard.Schema; }
            set
            {
                Wizard.Schema = value;
                NotifyOfPropertyChange(() => CanNext);
            }
        }

        public bool TruncateTables
        {
            get { return Wizard.TruncateTables; }
            set { Wizard.TruncateTables = value; }
        }

        public SqlAuthenticationType AuthenticationType {
            get { return Wizard.AuthenticationType; }
            set {
                Wizard.AuthenticationType = value;
                NotifyOfPropertyChange(() => CanNext);
                NotifyOfPropertyChange(() => IsWindowsAuth);
                NotifyOfPropertyChange(() => IsSqlAuth);
                NotifyOfPropertyChange(() => CanNext);
            }
        }

        public bool IsWindowsAuth
        {
            get { return AuthenticationType == SqlAuthenticationType.Windows; }
            set {
                if (value) { AuthenticationType = SqlAuthenticationType.Windows; }
                else { AuthenticationType = SqlAuthenticationType.Sql; }
            }
        }

        public bool IsSqlAuth
        {
            get { return AuthenticationType == SqlAuthenticationType.Sql; }
            set {
                if (value) { AuthenticationType = SqlAuthenticationType.Sql; }
                else { AuthenticationType = SqlAuthenticationType.Windows; }
            }
        }

        #endregion

        #region Methods

        public void ManualConnectionString()
        {
            BuildConnectionString();
            NextPage = ExportDataWizardPage.ManualConnectionString;
            TryClose();
        }

        private void BuildConnectionString()
        {
            if (AuthenticationType == SqlAuthenticationType.Windows)
            {
                Wizard.SqlConnectionString = $"Server={ServerName};Database={Database};Trusted_Connection=True;";
            }
            else
            {
                Wizard.SqlConnectionString = $"Server={ServerName};Database={Database};User Id={Username};Password={SecurePassword.ConvertToUnsecureString()}";
            }
        }

        public bool CanNext
        {
            get
            {
                return ServerName.Length > 0 
                    && Database.Length > 0
                    && Schema.Length > 0
                    && (AuthenticationType == SqlAuthenticationType.Windows || (Username.Length > 0 && SecurePassword.Length > 0));
            }
        }

        public void Next()
        {
            BuildConnectionString();
            NextPage = ExportDataWizardPage.ChooseTables;
            TryClose();
        }
        #endregion
    }
}
