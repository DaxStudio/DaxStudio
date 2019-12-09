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

namespace DaxStudio.UI.Model
{
    [Export]
    public class GlobalQueryHistory : IHandle<QueryHistoryEvent>
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly string _queryHistoryPath;
        private readonly IGlobalOptions _globalOptions;

        [ImportingConstructor]
        public GlobalQueryHistory(IEventAggregator eventAggregator, IGlobalOptions globalOptions )
        {
            Contract.Requires(eventAggregator != null, "The eventAggregator paramter must not be null");
            _globalOptions = globalOptions;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            QueryHistory = new BindableCollection<QueryHistoryEvent>();

            _queryHistoryPath = ApplicationPaths.QueryHistoryPath;
            Log.Debug("{class} {method} {message} {value}", "GlobalQueryHistory", "Constructor", "Setting Query History Path", _queryHistoryPath);
            EnsureQueryHistoryFolderExists();
            LoadHistoryFilesAsync();
    
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

        private void LoadHistoryFilesAsync()
        {
            _ = Task.Run(() =>
              {
                  Log.Debug("{class} {method} {message}", "GlobalQueryHistory", "LoadHistoryFilesAsync", "Start Load");
                  FileInfo[] fileList = null;
                  try
                  {
                      DirectoryInfo d = new DirectoryInfo(_queryHistoryPath);
                      fileList = d.GetFiles("*-query-history.json", SearchOption.TopDirectoryOnly);
                      List<QueryHistoryEvent> tempHist = new List<QueryHistoryEvent>(_globalOptions.QueryHistoryMaxItems);
                      foreach (var fileInfo in fileList)
                      {
                          using (StreamReader file = File.OpenText(fileInfo.FullName))
                          {
                              JsonSerializer serializer = new JsonSerializer();
                              QueryHistoryEvent queryHistory = (QueryHistoryEvent)serializer.Deserialize(file, typeof(QueryHistoryEvent));
                              tempHist.Add(queryHistory);
                          }
                      }
                      QueryHistory.AddRange(tempHist);
                  }
                  catch (Exception ex)
                  {
                      Log.Error(ex, "{class} {method} {message}", nameof(GlobalQueryHistory), nameof(LoadHistoryFilesAsync), $"Error loading query history files: {ex.Message}");
                  }
                  Log.Debug("{class} {method} {message}", "GlobalQueryHistory", "LoadHistoryFilesAsync", "End Load (" + fileList?.Length + " files)");
              });
        }

        public void Handle(QueryHistoryEvent message)
        {
            QueryHistory.Add(message);
            while (QueryHistory.Count > _globalOptions.QueryHistoryMaxItems)
            {
                QueryHistory.RemoveAt(0);
            }
            SaveHistoryFile(message);
        }

        private void SaveHistoryFile(QueryHistoryEvent message)
        {
            try
            {
                File.WriteAllText(UniqueFilePath(message), Newtonsoft.Json.JsonConvert.SerializeObject(message));
            }
            catch( Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(GlobalQueryHistory), nameof(SaveHistoryFile), $"Error Saving History File: {ex.Message}");
            }
            
            EnsureFileLimitAsync();
            
        }

        private Task EnsureFileLimitAsync()
        {
            var task = Task.Run(() =>
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
            return task;
        }

        private string UniqueFilePath(QueryHistoryEvent message)
        {
            IFormatProvider fmt = System.Globalization.CultureInfo.InvariantCulture;
            return Path.Combine(_queryHistoryPath,
                string.Format(fmt,"{0}-query-history.json"
                , message.StartTime.ToString("yyyyMMddHHmmssfff",fmt)));
        }

        public BindableCollection<QueryHistoryEvent> QueryHistory { get; }
    }
}
