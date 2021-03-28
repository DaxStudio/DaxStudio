using System.Data.SqlClient;
using System.Security;
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
            get => Wizard.ServerName;
            set {
                Wizard.ServerName = value;
                NotifyOfPropertyChange(() => CanNext);
            }
        }
        public string Username {
            get => Wizard.Username;
            set {
                Wizard.Username = value;
                NotifyOfPropertyChange(() => CanNext);
            }
        }
        public SecureString SecurePassword {
            get => Wizard.SecurePassword;
            set {
                Wizard.SecurePassword = value;
                NotifyOfPropertyChange(() => CanNext);
            }
        }

        public string Database
        {
            get => Wizard.Database;
            set
            {
                Wizard.Database = value;
                NotifyOfPropertyChange(() => CanNext);
            }
        }

        public string Schema
        {
            get => Wizard.Schema;
            set
            {
                Wizard.Schema = value;
                NotifyOfPropertyChange(() => CanNext);
            }
        }

        public bool TruncateTables
        {
            get => Wizard.TruncateTables;
            set => Wizard.TruncateTables = value;
        }

        public SqlAuthenticationType AuthenticationType {
            get => Wizard.AuthenticationType;
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
            get => AuthenticationType == SqlAuthenticationType.Windows;
            set => AuthenticationType = value ? SqlAuthenticationType.Windows : SqlAuthenticationType.Sql;
        }

        public bool IsSqlAuth
        {
            get => AuthenticationType == SqlAuthenticationType.Sql;
            set => AuthenticationType = value ? SqlAuthenticationType.Sql : SqlAuthenticationType.Windows;
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
            var builder = new SqlConnectionStringBuilder
            {
                ApplicationName = "DAX Studio", 
                DataSource = ServerName, 
                InitialCatalog = Database
            };

            if (AuthenticationType == SqlAuthenticationType.Windows)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = Username;
                builder.Password = SecurePassword.ConvertToUnsecureString();
            }

            Wizard.SqlConnectionString = builder.ConnectionString;
        }

        public bool CanNext =>
            ServerName.Length > 0 
            && Database.Length > 0
            && Schema.Length > 0
            && (AuthenticationType == SqlAuthenticationType.Windows || (Username.Length > 0 && SecurePassword.Length > 0));

        public void Next()
        {
            BuildConnectionString();
            NextPage = ExportDataWizardPage.ChooseTables;
            TryClose();
        }
        #endregion
    }
}
