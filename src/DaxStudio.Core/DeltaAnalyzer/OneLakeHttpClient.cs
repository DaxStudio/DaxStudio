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
    /// Thin wrapper around a shared <see cref="HttpClient"/> that talks to the OneLake / ADLS Gen2
    /// DFS REST API. All requests carry an <c>Authorization: Bearer</c> header built from the storage
    /// scoped AAD token supplied to the constructor.
    /// </summary>
    public class OneLakeHttpClient
    {
        // A single shared HttpClient instance to avoid socket exhaustion.
        private static readonly HttpClient _httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        private const string DfsApiVersion = "2018-11-09";
        private readonly Func<string> _tokenProvider;

        /// <param name="tokenProvider">
        /// Returns the current storage-scoped bearer token. A delegate is used so a refreshed token
        /// can be picked up on subsequent calls without re-creating this client.
        /// </param>
        public OneLakeHttpClient(Func<string> tokenProvider)
        {
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            var token = _tokenProvider();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            request.Headers.TryAddWithoutValidation("x-ms-version", DfsApiVersion);
            return request;
        }

        /// <summary>
        /// Lists the paths under a directory within a OneLake filesystem using the ADLS Gen2 DFS
        /// "List" (filesystem) operation. Handles continuation tokens.
        /// </summary>
        /// <param name="filesystemUrl">
        /// The filesystem (workspace) root, e.g. <c>https://onelake.dfs.fabric.microsoft.com/{workspace}</c>.
        /// </param>
        /// <param name="directory">The directory (relative to the filesystem) to list.</param>
        /// <param name="recursive">Whether to recurse into sub directories.</param>
        public async Task<List<OneLakePathItem>> ListPathsAsync(string filesystemUrl, string directory, bool recursive, CancellationToken ct)
        {
            var results = new List<OneLakePathItem>();
            var trimmedFsUrl = filesystemUrl.TrimEnd('/');
            var encodedDirectory = Uri.EscapeDataString(directory ?? string.Empty);
            string continuation = null;

            do
            {
                ct.ThrowIfCancellationRequested();
                var url = $"{trimmedFsUrl}?resource=filesystem&recursive={(recursive ? "true" : "false")}&directory={encodedDirectory}&api-version={DfsApiVersion}";
                if (!string.IsNullOrEmpty(continuation))
                {
                    url += $"&continuation={Uri.EscapeDataString(continuation)}";
                }

                using (var request = CreateRequest(HttpMethod.Get, url))
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var body = await SafeReadBodyAsync(response).ConfigureAwait(false);
                        throw new OneLakeHttpException($"Failed to list paths under '{directory}' ({(int)response.StatusCode} {response.ReasonPhrase}). {body}");
                    }

                    continuation = null;
                    if (response.Headers.TryGetValues("x-ms-continuation", out var contValues))
                    {
                        foreach (var v in contValues)
                        {
                            if (!string.IsNullOrEmpty(v)) { continuation = v; break; }
                        }
                    }

                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    ParsePathListing(json, results);
                }
            }
            while (!string.IsNullOrEmpty(continuation));

            return results;
        }

        private static void ParsePathListing(string json, List<OneLakePathItem> results)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            var root = JObject.Parse(json);
            var paths = root["paths"] as JArray;
            if (paths == null) return;
            foreach (var p in paths)
            {
                var item = new OneLakePathItem
                {
                    Name = p.Value<string>("name"),
                    // contentLength is returned as a string in the DFS API
                    ContentLength = ParseLong(p["contentLength"]),
                    IsDirectory = ParseBool(p["isDirectory"])
                };
                results.Add(item);
            }
        }

        private static long ParseLong(JToken token)
        {
            if (token == null) return 0;
            var s = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
            return long.TryParse(s, out var val) ? val : 0;
        }

        private static bool ParseBool(JToken token)
        {
            if (token == null) return false;
            var s = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
            return bool.TryParse(s, out var val) && val;
        }

        /// <summary>Reads the entire contents of a OneLake file as text.</summary>
        public async Task<string> ReadTextAsync(string url, CancellationToken ct)
        {
            using (var request = CreateRequest(HttpMethod.Get, url))
            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    var body = await SafeReadBodyAsync(response).ConfigureAwait(false);
                    throw new OneLakeHttpException($"Failed to read '{url}' ({(int)response.StatusCode} {response.ReasonPhrase}). {body}");
                }
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        /// <summary>Reads a byte range from a OneLake file via an HTTP Range request.</summary>
        public async Task<byte[]> ReadRangeAsync(string url, long offset, long count, CancellationToken ct)
        {
            using (var request = CreateRequest(HttpMethod.Get, url))
            {
                request.Headers.Range = new RangeHeaderValue(offset, offset + count - 1);
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var body = await SafeReadBodyAsync(response).ConfigureAwait(false);
                        throw new OneLakeHttpException($"Failed to read range [{offset},{offset + count - 1}] of '{url}' ({(int)response.StatusCode} {response.ReasonPhrase}). {body}");
                    }
                    return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>Returns the content length of a OneLake file using a HEAD request.</summary>
        public async Task<long> GetContentLengthAsync(string url, CancellationToken ct)
        {
            using (var request = CreateRequest(HttpMethod.Head, url))
            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new OneLakeHttpException($"Failed to HEAD '{url}' ({(int)response.StatusCode} {response.ReasonPhrase}).");
                }
                return response.Content?.Headers?.ContentLength ?? 0;
            }
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
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(OneLakeHttpClient), nameof(SafeReadBodyAsync), "Error reading error response body");
                return string.Empty;
            }
        }
    }

    /// <summary>Exception raised for OneLake / DFS HTTP failures with a descriptive message.</summary>
    public class OneLakeHttpException : Exception
    {
        public OneLakeHttpException(string message) : base(message) { }
        public OneLakeHttpException(string message, Exception inner) : base(message, inner) { }
    }
}
