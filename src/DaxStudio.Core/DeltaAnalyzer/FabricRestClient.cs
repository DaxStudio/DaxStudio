using DaxStudio.Common;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.Core.DeltaAnalyzer
{
    /// <summary>
    /// Minimal client for the Fabric REST API. Used to map a SQL analytics endpoint id to the OneLake
    /// item (lakehouse) that actually stores the delta tables. Direct Lake on SQL models reference the
    /// SQL endpoint item via <c>Sql.Database(...)</c>, but the SQL endpoint is a distinct item from the
    /// lakehouse that hosts the OneLake <c>/Tables</c> data, so its id cannot be used directly in a
    /// OneLake path. All calls are best-effort and return empty results on failure.
    /// </summary>
    public class FabricRestClient
    {
        private const string FabricApiBase = "https://api.fabric.microsoft.com/v1";

        // A single shared HttpClient instance to avoid socket exhaustion.
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        private readonly Func<string> _tokenProvider;

        /// <param name="tokenProvider">
        /// Returns the current Power BI / Fabric-scoped bearer token (audience
        /// <c>https://analysis.windows.net/powerbi/api</c>, which the Fabric REST API accepts).
        /// </param>
        public FabricRestClient(Func<string> tokenProvider)
        {
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        /// <summary>
        /// Returns a map of SQL analytics endpoint id -&gt; OneLake <c>.../Tables</c> path for every
        /// lakehouse in the workspace. Returns an empty map on any failure (best-effort).
        /// </summary>
        public async Task<Dictionary<string, string>> GetSqlEndpointToTablesPathMapAsync(string workspaceId, CancellationToken ct)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(workspaceId)) return map;

            try
            {
                string continuation = null;
                do
                {
                    ct.ThrowIfCancellationRequested();
                    var url = $"{FabricApiBase}/workspaces/{Uri.EscapeDataString(workspaceId)}/lakehouses";
                    if (!string.IsNullOrEmpty(continuation))
                    {
                        url += $"?continuationToken={Uri.EscapeDataString(continuation)}";
                    }

                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        var token = _tokenProvider();
                        if (!string.IsNullOrEmpty(token))
                        {
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        }

                        using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false))
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                var body = await SafeReadBodyAsync(response).ConfigureAwait(false);
                                Log.Warning(Constants.LogMessageTemplate, nameof(FabricRestClient), nameof(GetSqlEndpointToTablesPathMapAsync),
                                    $"Fabric lakehouse list failed for workspace {workspaceId} ({(int)response.StatusCode} {response.ReasonPhrase}). {body}");
                                return map;
                            }

                            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            continuation = ParseLakehouses(json, map);
                        }
                    }
                }
                while (!string.IsNullOrEmpty(continuation));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(FabricRestClient), nameof(GetSqlEndpointToTablesPathMapAsync), $"Error listing lakehouses for workspace {workspaceId}");
            }

            return map;
        }

        private static string ParseLakehouses(string json, Dictionary<string, string> map)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var root = JObject.Parse(json);
            var values = root["value"] as JArray;
            if (values != null)
            {
                foreach (var lh in values)
                {
                    var props = lh["properties"];
                    var tablesPath = props?["oneLakeTablesPath"]?.Value<string>();
                    var endpointId = props?["sqlEndpointProperties"]?["id"]?.Value<string>();
                    if (!string.IsNullOrEmpty(endpointId) && !string.IsNullOrEmpty(tablesPath))
                    {
                        map[endpointId] = tablesPath;
                    }
                }
            }
            return root["continuationToken"]?.Value<string>();
        }

        private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response)
        {
            try
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (body != null && body.Length > 500) body = body.Substring(0, 500);
                return body;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(FabricRestClient), nameof(SafeReadBodyAsync), "Error reading error response body");
                return string.Empty;
            }
        }
    }
}
