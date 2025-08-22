using DaxStudio.UI.Enums;
using DaxStudio.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Security;


namespace DaxStudio.UI.Interfaces
{
    internal interface IExportDataDetails
    {
        string ServerName { get; set; }
        string Database { get; set; }
        string Schema { get; set; } 
        string Username { get; set; }
        // should consider serializing this using this technique: https://stackoverflow.com/questions/12657792/how-to-securely-save-username-password-local
        SecureString SecurePassword { get; set; }
        SqlAuthenticationType AuthenticationType { get; set; }
        string SqlConnectionString { get; set; }
        string CsvDelimiter { get; set; }
        bool CsvQuoteStrings { get; set; }
        string CsvFolder { get; set; }
        CsvEncoding CsvEncoding { get; set; }
        ObservableCollection<SelectedTable> Tables { get; set; } 
        bool TruncateTables { get; set; }
    }
}
