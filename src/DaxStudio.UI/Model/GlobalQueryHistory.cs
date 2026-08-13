using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaxStudio.Core.Events;
using DaxStudio.UI.Events;
using System.ComponentModel.Composition;
using System.IO;
using Newtonsoft.Json;
using Serilog;
using DaxStudio.Interfaces;
using System.Diagnostics.Contracts;
using DaxStudio.Common;
using System.Threading;

namespace DaxStudio.UI.Model
{
    [Export]
    public class GlobalQueryHistory : 
        IHandle<QueryHistoryEvent>,
        IHandle<LoadQueryHistoryAsyncEvent>
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly string _queryHistoryPath;
        private readonly IGlobalOptions _globalOptions;
        private bool _isLoaded = false;
        private readonly object _loadingLock = new object();

        [ImportingConstructor]
        public GlobalQueryHistory(IEventAggregator eventAggregator, IGlobalOptions globalOptions )
        {
            Contract.Requires(eventAggregator != null, "The eventAggregator paramter must not be null");
            _globalOptions = globalOptions;
            _eventAggregator = eventAggregator;
            _eventAggregator.SubscribeOnUIThread(this);
            QueryHistory = new BindableCollection<QueryHistoryEvent>();

            _queryHistoryPath = ApplicationPaths.QueryHistoryPath;
            Log.Debug("{class} {method} {message} {value}", "GlobalQueryHistory", "Constructor", "Setting Query History Path", _queryHistoryPath);
            
        }

        private async Task EnsureQueryHistoryFolderExistsAsync()
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(_queryHistoryPath))
                {
                    Log.Debug("{class} {method} {message} {value}", nameof(GlobalQueryHistory), nameof(EnsureQueryHistoryFolderExistsAsync), "Creating Query History Path", _queryHistoryPath);
                    try
                    {
                        Directory.CreateDirectory(_queryHistoryPath);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "{class} {method} {message}", nameof(GlobalQueryHistory), nameof(EnsureQueryHistoryFolderExistsAsync), $"Error creating query history folder: {ex.Message}");
                    }
                }
            });
        }

        private async Task LoadHistoryFilesAsync()
        {
            if (_isLoaded) return;
            lock (_loadingLock)
            {
                if (_isLoaded) return;
                _isLoaded = true;
            }

            var result = await LoadHistoryFilesFromDiskAsync().ConfigureAwait(false);
            QueryHistory.AddRange(result.History);

            if (result.ErrorCount > 0) { await _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, $"Not all Query History records could be loaded, {result.ErrorCount} error{(result.ErrorCount == 1 ? " has" : "s have")} been written to the log file")).ConfigureAwait(false); }
            Log.Debug("{class} {method} {message}", "GlobalQueryHistory", "LoadHistoryFilesAsync", "End Load (" + result.FileCount + " files)");
        }

        private async Task<LoadHistoryResult> LoadHistoryFilesFromDiskAsync()
        {
            Log.Debug("{class} {method} {message}", "GlobalQueryHistory", "LoadHistoryFilesAsync", "Start Load");

            // The whole scan/read/deserialize runs as a single background work item. Reading the files
            // individually with async I/O previously queued one thread-pool continuation per file, which
            // is very slow during startup while the thread pool is still ramping up. The per-file cost is
            // dominated by opening the handle (filter drivers / AV), so the reads are done in parallel.
            return await Task.Run(() =>
            {
                int errorCnt = 0;
                FileInfo[] fileList = null;
                QueryHistoryEvent[] loaded = null;

                try
                {
                    var d = new DirectoryInfo(_queryHistoryPath);
                    fileList = d.GetFiles("*-query-history.json", SearchOption.TopDirectoryOnly);

                    Log.Debug(Constants.LogMessageTemplate, nameof(GlobalQueryHistory), nameof(LoadHistoryFilesAsync), $"Starting load of {fileList.Length} history files");

                    // Writing into a pre-sized array by index keeps the original (filename/chronological)
                    // ordering regardless of the order the parallel reads complete in.
                    loaded = new QueryHistoryEvent[fileList.Length];
                    Parallel.For(0, fileList.Length, i =>
                    {
                        try
                        {
                            loaded[i] = JsonConvert.DeserializeObject<QueryHistoryEvent>(File.ReadAllText(fileList[i].FullName));
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "{class} {method} {message}", nameof(GlobalQueryHistory), nameof(LoadHistoryFilesAsync), $"Error loading History file: {fileList[i].FullName}, Message: {ex.Message}");
                            Interlocked.Increment(ref errorCnt);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{class} {method} {message}", nameof(GlobalQueryHistory), nameof(LoadHistoryFilesAsync), $"Error loading query history files: {ex.Message}");
                }
                finally
                {
                    _isLoaded = true;
                }

                var tempHist = new List<QueryHistoryEvent>(fileList?.Length ?? 0);
                if (loaded != null)
                {
                    // Slots for files that failed to load stay null and are skipped.
                    foreach (var item in loaded)
                    {
                        if (item != null) tempHist.Add(item);
                    }
                }

                return new LoadHistoryResult(tempHist, errorCnt, fileList?.Length ?? 0);
            }).ConfigureAwait(false);
        }



        public async Task HandleAsync(QueryHistoryEvent message, CancellationToken cancellationToken)
        {
            // don't add a history record if the query text is empty
            if (string.IsNullOrWhiteSpace(message.QueryText) && string.IsNullOrWhiteSpace(message.QueryBuilderJson))
            {
                Log.Debug("{class} {method} {message}", nameof(GlobalQueryHistory), "Handle<QueryHistoryEvent>", "Skipping saving Query History as QueryText is empty");
                return;
            }
            QueryHistory.Add(message);
            while (QueryHistory.Count > _globalOptions.QueryHistoryMaxItems)
            {
                QueryHistory.RemoveAt(0);
            }
            await SaveHistoryFileAsync(message);
        }

        private async Task SaveHistoryFileAsync(QueryHistoryEvent message)
        {
            try
            {
                using (var stream = new FileStream(UniqueFilePath(message), FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                using (var writer = new StreamWriter(stream))
                {
                    await writer.WriteAsync(JsonConvert.SerializeObject(message)).ConfigureAwait(false);
                }
            }
            catch( Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(GlobalQueryHistory), nameof(SaveHistoryFileAsync), $"Error Saving History File: {ex.Message}");
            }
            
            await EnsureFileLimitAsync();
            
        }

        private async Task EnsureFileLimitAsync()
        {
            await Task.Run(() =>
                        {
                            try
                            {
                                foreach (var fi in new DirectoryInfo(_queryHistoryPath)
                                     .GetFiles()
                                     .OrderByDescending(x => x.LastWriteTime)
                                     .Skip(_globalOptions.QueryHistoryMaxItems))
                                    fi.Delete();
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "{class} {method} {message}", nameof(GlobalQueryHistory), nameof(EnsureFileLimitAsync), $"Error Removing Old History Files: {ex.Message}");
                            }
                        });
            //return task;
        }

        private string UniqueFilePath(QueryHistoryEvent message)
        {
            IFormatProvider fmt = System.Globalization.CultureInfo.InvariantCulture;
            return Path.Combine(_queryHistoryPath,
                string.Format(fmt,"{0}-query-history.json"
                , message.StartTime.ToString("yyyyMMddHHmmssfff",fmt)));
        }

        public async Task HandleAsync(LoadQueryHistoryAsyncEvent message, CancellationToken cancellationToken)
        {
            await LoadQueryHistoryAsync();
        }

        public async Task LoadQueryHistoryAsync()
        {
            // ConfigureAwait(false) keeps the load off the UI dispatcher queue. The handler is invoked on
            // the UI thread, so without this the continuations get queued behind the rest of the startup
            // work (window placement, opening the first document) before the load even begins.
            // QueryHistory is a BindableCollection, whose AddRange marshals back to the UI thread itself.
            await EnsureQueryHistoryFolderExistsAsync().ConfigureAwait(false);
            await LoadHistoryFilesAsync().ConfigureAwait(false);
        }

        public BindableCollection<QueryHistoryEvent> QueryHistory { get; }

        private class LoadHistoryResult
        {
            public LoadHistoryResult(List<QueryHistoryEvent> history, int errorCount, int fileCount)
            {
                History = history;
                ErrorCount = errorCount;
                FileCount = fileCount;
            }

            public List<QueryHistoryEvent> History { get; }
            public int ErrorCount { get; }
            public int FileCount { get; }
        }
    }
}
