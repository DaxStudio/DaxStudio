using ADOTabular;
using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.Core.Events;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    /// <summary>
    /// UI-aware ConnectionManager subclass. Adds metadata pane / tree view / view-as functionality
    /// that depends on UI-specific types (TreeViewTable, IMetadataPane, ITraceWatcher). The
    /// connection plumbing itself lives in <see cref="DaxStudio.Core.Connections.ConnectionManager"/>.
    /// </summary>
    public class ConnectionManager : DaxStudio.Core.Connections.ConnectionManager, IMetadataProvider
    {
        public ConnectionManager(IEventAggregator eventAggregator) : base(eventAggregator) { }

        public IEnumerable<IFilterableTreeViewItem> GetTreeViewTables(IMetadataPane metadataPane, IGlobalOptions options)
        {
            return _retry.Execute(() => {

                ADOTabularModel tmpModel;
                if (_dmvConnection.ServerMode == "Offline")
                {
                    // if we are in offline mode there is no need to clone the connection
                    tmpModel = _connection.Database.Models[SelectedModel.Name];
                }
                else
                {
                    // in online mode we clone the connection to try and avoid
                    // XmlReader in use errors

                    tmpModel = _dmvConnection.Database.Models[SelectedModel.Name];

                }

                var tvt = tmpModel.TreeViewTables(options, _eventAggregator, metadataPane);
                return tvt;
            });
        }

        private ADOTabularConnection _tableStatsConnection;
        private readonly object _tableStatsLock = new object();
        public async Task UpdateTableBasicStatsAsync(TreeViewTable table)
        {
            table.UpdatingBasicStats = true;
            try
            {
                await Task.Run(() => {
                    _tableStatsConnection?.TryCancel(); // cancel any existing table stats connection
                    lock (_tableStatsLock)
                    {
                        using (_tableStatsConnection = _dmvConnection.Clone())
                        {
                            table.UpdateBasicStats(_tableStatsConnection);
                        }
                    }
                });
            }
            catch (Microsoft.AnalysisServices.AdomdClient.AdomdErrorResponseException)
            {
                // An error response is expected if the tooltip is cancelled
            }
            catch (Exception ex)
            {
                await _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, $"Error populating tooltip basic statistics data: {ex.Message}"));
            }
            finally
            {
                table.UpdatingBasicStats = false;
            }
        }

        public void CancelUpdatingTableBasicStats()
        {
            if (_tableStatsConnection != null)
            {
                _tableStatsConnection.Cancel();
                _tableStatsConnection.Close();
                _tableStatsConnection.Dispose();
                _tableStatsConnection = null;
            }
        }

        /// <summary>
        /// Attempts to set the ViewAs user.
        /// Warning: this uses settings that are not documented by Microsoft and so could be subject to changes at any time
        /// </summary>
        internal async Task SetViewAsAsync(string userName, string roles, List<ITraceWatcher> activeTraces)
        {
            Log.Information(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetViewAsAsync), $"Setting ViewAs User: '{userName}' Roles: '{roles}'");
            /*
             * ;Authentication Scheme=ActAs;
             * Ext Auth Info="<Properties><UserName>test</UserName><BypassAuthorization>true</BypassAuthorization><RestrictCatalog>29530e54-5667-46ab-9c6a-d5b494347966</RestrictCatalog></Properties>";
             */
            if (string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(roles)) throw new ArgumentException("You must specify either a Username or Roles to activate the ViewAs functionality");

            var builder = new OleDbConnectionStringBuilder(this.ConnectionString);

            // set catalog
            builder["Initial Catalog"] = this.DatabaseName;
            var catalogElement = $"<RestrictCatalog>{this.DatabaseName}</RestrictCatalog>";

            string userElement = string.Empty;
            string rolesElement = string.Empty;

            if (!string.IsNullOrEmpty(userName))
            {
                userElement = $"<UserName>{userName}</UserName><BypassAuthorization>true</BypassAuthorization>";

                if (!string.IsNullOrEmpty(roles))
                {
                    // set Roles= on connstr
                    // add roles restriction to ExtAuth
                    builder.Add("Roles", roles);
                    rolesElement = $"<RestrictRoles>{roles}</RestrictRoles>";
                }

                var extAuthInfo = $"<Properties>{userElement}{catalogElement}{rolesElement}</Properties>";

                // if data source does not support ActAs we should try Effective Username
                if (SupportsActAs())
                {
                    // ExtAuth works on PBI or ASAzure
                    builder.Add("Authentication Scheme", "ActAs");
                    builder.Add("Ext Auth Info", extAuthInfo);
                }
                else
                {
                    builder.Add("EffectiveUsername", userName);
                }
            }

            if (!string.IsNullOrEmpty(roles))
            {
                // set Roles= on connstr
                // add roles restriction to ExtAuth
                builder["Roles"] = roles;
            }

            var connEvent = new ConnectEvent(builder.ConnectionString, IsPowerPivot, this.ApplicationName, FileName, ServerType, true, this.DatabaseName, this.AccessToken);
            connEvent.ActiveTraces = activeTraces?.Cast<object>().ToList();
            await _eventAggregator.PublishAsync(connEvent);
        }

        private bool SupportsActAs()
        {
            // todo - does the SSDT engine also support ActAs or is it just desktop??
            return this.IsPowerBIorSSDT;
        }

        public void StopViewAs(List<ITraceWatcher> activeTraces)
        {
            var builder = new OleDbConnectionStringBuilder(this.ConnectionString);
            builder.Remove("Authentication Scheme");
            builder.Remove("Ext Auth Info");
            builder.Remove("Roles");
            builder.Remove("EffectiveUsername");

            var connEvent = new ConnectEvent(builder.ConnectionString, IsPowerPivot, this.ApplicationName, FileName, ServerType, true, DatabaseName, AccessToken);
            connEvent.ActiveTraces = activeTraces?.Cast<object>().ToList();
            _eventAggregator.PublishAsync(connEvent);
        }
    }
}
