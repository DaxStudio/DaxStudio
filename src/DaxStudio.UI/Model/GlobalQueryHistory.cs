using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            _eventAggregator.SubscribeOnPublishedThread(this);
            QueryHistory = new BindableCollection<QueryHistoryEvent>();

            _queryHistoryPath = ApplicationPaths.QueryHistoryPath;
            Log.Debug("{class} {method} {message} {value}", "GlobalQueryHistory", "Constructor", "Setting Query History Path", _queryHistoryPath);
            
        }

        private void EnsureQueryHistoryFolderExists()
        {
            if (!Directory.Exists(_queryHistoryPath))
            {
                Log.Debug("{class} {method} {message} {value}", nameof(GlobalQueryHistory), nameof(EnsureQueryHistoryFolderExists), "Creating Query History Path", _queryHistoryPath);
                try
                {
                    Directory.CreateDirectory(_queryHistoryPath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{class} {method} {message}", nameof(GlobalQueryHistory), nameof(EnsureQueryHistoryFolderExists), $"Error creating query history folder: {ex.Message}");
                }
            }
        }

        private async Task LoadHistoryFilesAsync()
        {
            if (_isLoaded) return;
            lock (_loadingLock)
            {
                if (_isLoaded) return;
                _isLoaded = true;
            }

            await Task.Run(async () =>
                {
                    int errorCnt = 0;
                    FileInfo[] fileList = null;

                    Log.Debug("{class} {method} {message}", "GlobalQueryHistory", "LoadHistoryFilesAsync", "Start Load");
                                                
                    try
                    {
                        DirectoryInfo d = new DirectoryInfo(_queryHistoryPath);
                        fileList = d.GetFiles("*-query-history.json", SearchOption.TopDirectoryOnly);
                        Log.Debug(Constants.LogMessageTemplate, nameof(GlobalQueryHistory), nameof(LoadHistoryFilesAsync), $"Starting load of {fileList.Length} history files");
                        List<QueryHistoryEvent> tempHist = new List<QueryHistoryEvent>(_globalOptions.QueryHistoryMaxItems);
                        foreach (var fileInfo in fileList)
                        {
                            try
                            {
                                using (StreamReader file = File.OpenText(fileInfo.FullName))
                                {
                                    JsonSerializer serializer = new JsonSerializer();
                                    QueryHistoryEvent queryHistory = (QueryHistoryEvent)serializer.Deserialize(file, typeof(QueryHistoryEvent));
                                    tempHist.Add(queryHistory);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "{class} {method} {message}", nameof(GlobalQueryHistory), nameof(LoadHistoryFilesAsync), $"Error loading History file: {fileInfo.FullName}, Message: {ex.Message}");
                                errorCnt++;
                            }
                        }
                        QueryHistory.AddRange(tempHist);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "{class} {method} {message}", nameof(GlobalQueryHistory), nameof(LoadHistoryFilesAsync), $"Error loading query history files: {ex.Message}");
                    }
                    finally
                    {
                        _isLoaded = true;
                    }
                    

                    if (errorCnt > 0) { await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, $"Not all Query History records could be loaded, {errorCnt} error{(errorCnt == 1 ? " has" : "s have")} been written to the log file")); }
                    Log.Debug("{class} {method} {message}", "GlobalQueryHistory", "LoadHistoryFilesAsync", "End Load (" + fileList?.Length + " files)");
                    
                });
            
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
                File.WriteAllText(UniqueFilePath(message), Newtonsoft.Json.JsonConvert.SerializeObject(message));
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
            EnsureQueryHistoryFolderExists();
            await LoadHistoryFilesAsync();
        }

        public BindableCollection<QueryHistoryEvent> QueryHistory { get; }
    }
}
