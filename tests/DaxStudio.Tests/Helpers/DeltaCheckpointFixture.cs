using DaxStudio.Core.DeltaAnalyzer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.Tests.Helpers
{
    /// <summary>
    /// Offline DFS storage. Keys are escaped URLs, while listing names are decoded,
    /// filesystem-relative paths (the workspace must not be included in Name).
    /// No data-file content or network client is needed to reconstruct the Delta log.
    /// </summary>
    internal sealed class DeltaCheckpointFixture
    {
        internal const long Version = 10;
        internal const string PhysicalStore = "col-11111111-2222-3333-4444-555555555555";
        internal const string PhysicalOld = "col-aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        internal const long Modified = 1700000000000;
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _listing = new Dictionary<string, long>(StringComparer.Ordinal);

        internal DeltaCheckpointFixture(string directory = "Lakehouse.Lakehouse/Tables/dbo/Stores",
            string filesystem = "https://onelake.dfs.fabric.microsoft.com/offline-workspace")
        {
            Location = new OneLakeLocation { FilesystemUrl = filesystem, Directory = directory };
            TableUrl = Location.BuildFileUrl(directory);
        }

        internal OneLakeLocation Location { get; }
        internal string TableUrl { get; }
        internal string LogDirectory => Location.Directory + "/_delta_log";
        internal List<string> Reads { get; } = new List<string>();
        internal List<Tuple<string, string>> Listings { get; } = new List<Tuple<string, string>>();
        internal Action<string, CancellationToken> BeforeRead { get; set; }

        internal DeltaLogReader CreateReader() => new DeltaLogReader(ListPaths, ReadText, OpenRead);

        internal Task<DeltaLogResult> ReadAsync(CancellationToken ct = default(CancellationToken))
            => CreateReader().ReadAsync(TableUrl, ct);

        internal Task<List<OneLakePathItem>> ListPaths(string filesystem, string directory, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Listings.Add(Tuple.Create(filesystem, directory));
            if (filesystem != Location.FilesystemUrl || directory != LogDirectory)
                throw new InvalidDataException("The reader must request the filesystem root and decoded log directory.");

            // Deliberately not in commit/part order. Sidecars are not returned by a
            // non-recursive _delta_log listing and must be followed from the checkpoint.
            return Task.FromResult(_listing.OrderByDescending(p => p.Key, StringComparer.Ordinal)
                .Select(p => new OneLakePathItem { Name = p.Key, ContentLength = p.Value, IsDirectory = false })
                .ToList());
        }

        internal Task<string> ReadText(string url, CancellationToken ct)
            => Task.FromResult(Encoding.UTF8.GetString(ReadBytes(url, ct)));

        internal Stream OpenRead(string url, CancellationToken ct)
        {
            // Never return a previously consumed/disposed stream.
            return new MemoryStream(ReadBytes(url, ct), false);
        }

        private byte[] ReadBytes(string url, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Reads.Add(url);
            BeforeRead?.Invoke(url, ct);
            ct.ThrowIfCancellationRequested();
            if (!_files.TryGetValue(url, out var bytes))
                throw new FileNotFoundException("Missing offline Delta fixture.", url);
            return bytes;
        }

        internal string Url(string logRelativeName) => Location.BuildFileUrl(LogDirectory + "/" + logRelativeName);

        internal string AbfssUrl(string logRelativeName)
        {
            var filesystem = new Uri(Location.FilesystemUrl);
            var file = new Uri(Url(logRelativeName));
            return "abfss://" + filesystem.AbsolutePath.Trim('/') + "@" + filesystem.Host
                + file.AbsolutePath.Substring(filesystem.AbsolutePath.Length);
        }

        internal void Put(string logRelativeName, byte[] bytes, bool listed = true)
        {
            _files[Url(logRelativeName)] = bytes;
            if (listed && !logRelativeName.Contains("/"))
                _listing[LogDirectory + "/" + logRelativeName] = bytes.LongLength;
        }

        internal void Delete(string name, bool retainListing = false)
        {
            _files.Remove(Url(name));
            if (!retainListing) _listing.Remove(LogDirectory + "/" + name);
        }

        internal void Json(string name, params JObject[] actions)
            => Put(name, Encoding.UTF8.GetBytes(string.Join("\n", actions.Select(a => a.ToString(Formatting.None)))));

        internal void Commit(long version, params JObject[] actions) => Json(CommitName(version), actions);

        internal void Pointer(int? parts = null, string v2Path = null, long size = 3)
        {
            var pointer = new JObject { ["version"] = Version, ["size"] = size };
            if (parts.HasValue) pointer["parts"] = parts.Value;
            if (v2Path != null)
                pointer["v2Checkpoint"] = new JObject { ["path"] = v2Path };
            Json("_last_checkpoint", pointer);
        }

        internal async Task ParquetAsync(string name, params JObject[][] rowGroups)
            => Put(name, await WriteParquetAsync(rowGroups));

        internal async Task TopLevelAsync(string format, string name, params JObject[] actions)
        {
            if (format == "json") Json(name, actions);
            else await ParquetAsync(name, actions);
        }

        internal static string CommitName(long version) => version.ToString("D20", CultureInfo.InvariantCulture) + ".json";
        internal static string V1Name => Version.ToString("D20", CultureInfo.InvariantCulture) + ".checkpoint.parquet";
        internal static string PartName(int part, int total) => string.Format(CultureInfo.InvariantCulture,
            "{0:D20}.checkpoint.{1:D10}.{2:D10}.parquet", Version, part, total);
        internal static string V2Name(string format, bool alternate = false) => Version.ToString("D20", CultureInfo.InvariantCulture)
            + ".checkpoint." + (alternate ? "bbbbbbbb-2222-3333-4444-555555555555" : "aaaaaaaa-2222-3333-4444-555555555555")
            + "." + format;

        internal static JObject Protocol(bool v2 = false) => new JObject
        {
            ["protocol"] = v2
                ? new JObject { ["minReaderVersion"] = 3, ["minWriterVersion"] = 7,
                    ["readerFeatures"] = new JArray("columnMapping", "v2Checkpoint"),
                    ["writerFeatures"] = new JArray("columnMapping", "v2Checkpoint") }
                : new JObject { ["minReaderVersion"] = 2, ["minWriterVersion"] = 5 }
        };

        internal static JObject CheckpointMetadata(long version = Version) => new JObject
        {
            ["checkpointMetadata"] = new JObject { ["version"] = version }
        };

        internal static string Schema(string logical = "Store_Code", bool mapped = true, bool includeOld = false)
        {
            var fields = new JArray(SchemaField(logical, mapped ? PhysicalStore : null));
            if (includeOld) fields.Add(SchemaField("Removed_Column", PhysicalOld));
            return new JObject { ["type"] = "struct", ["fields"] = fields }.ToString(Formatting.None);
        }

        private static JObject SchemaField(string name, string physical) => new JObject
        {
            ["name"] = name, ["type"] = "string", ["nullable"] = true,
            ["metadata"] = physical == null ? new JObject() : new JObject
            {
                ["delta.columnMapping.id"] = physical == PhysicalStore ? 1 : 2,
                ["delta.columnMapping.physicalName"] = physical
            }
        };

        internal static JObject Metadata(string schema = null) => new JObject
        {
            ["metaData"] = new JObject
            {
                ["id"] = "00000000-1111-2222-3333-444444444444",
                ["format"] = new JObject { ["provider"] = "parquet", ["options"] = new JObject() },
                ["schemaString"] = schema ?? Schema(), ["partitionColumns"] = new JArray(),
                ["configuration"] = new JObject(), ["createdTime"] = Modified
            }
        };

        internal static JObject Add(string path = "part-a.parquet", long size = 100, long rows = 12,
            long? deleted = null, bool parsedStats = false)
        {
            var add = new JObject
            {
                ["path"] = path, ["size"] = size, ["modificationTime"] = Modified,
                ["dataChange"] = false, ["partitionValues"] = new JObject()
            };
            if (parsedStats) add["stats_parsed"] = new JObject { ["numRecords"] = rows };
            else add["stats"] = new JObject { ["numRecords"] = rows }.ToString(Formatting.None);
            if (deleted.HasValue)
                add["deletionVector"] = new JObject
                {
                    ["storageType"] = "u", ["pathOrInlineDv"] = "ab^-aqEH.-t@S}K{vb[*k^",
                    ["offset"] = 1, ["sizeInBytes"] = 32, ["cardinality"] = deleted.Value
                };
            return new JObject { ["add"] = add };
        }

        internal static JObject Remove(string path) => new JObject
        {
            ["remove"] = new JObject { ["path"] = path, ["deletionTimestamp"] = Modified, ["dataChange"] = false }
        };

        internal static JObject Sidecar(string path, long size) => new JObject
        {
            ["sidecar"] = new JObject { ["path"] = path, ["sizeInBytes"] = size, ["modificationTime"] = Modified }
        };

        internal long Length(string name) => _files[Url(name)].LongLength;

        /// <summary>
        /// Real Parquet.Net 5.4 writer, with nested structs rather than dotted leaf
        /// names. Each JObject is one sparse Delta action row. Explicit definition
        /// levels preserve null ancestor structs and alignment across action rows.
        /// Only supplied action types are included, so valid sidecar fixtures contain
        /// file actions only. Invalid sidecars can explicitly supply forbidden actions.
        /// </summary>
        internal static async Task<byte[]> WriteParquetAsync(params JObject[][] rowGroups)
        {
            var actionNames = new HashSet<string>(rowGroups.SelectMany(g => g).SelectMany(a => a.Properties()).Select(p => p.Name));
            var fields = new Field[]
            {
                new StructField("metaData", Text("id"), Text("schemaString"), Number("createdTime")),
                new StructField("protocol", new DataField<int?>("minReaderVersion"), new DataField<int?>("minWriterVersion")),
                new StructField("checkpointMetadata", Number("version")),
                new StructField("sidecar", Text("path"), Number("sizeInBytes"), Number("modificationTime")),
                new StructField("add", Text("path"), Number("size"), Text("stats"), Number("modificationTime"),
                    new DataField<bool?>("dataChange"),
                    new StructField("stats_parsed", Number("numRecords")),
                    new StructField("deletionVector", Text("storageType"), Text("pathOrInlineDv"),
                        new DataField<int?>("offset"), new DataField<int?>("sizeInBytes"), Number("cardinality"))),
                new StructField("remove", Text("path"), Number("deletionTimestamp"), new DataField<bool?>("dataChange"))
            }.Where(f => actionNames.Contains(f.Name)).ToArray();
            var schema = new ParquetSchema(fields);
            var leaves = fields.SelectMany(f => LeafPaths(f, new List<Field>())).ToList();

            using (var stream = new MemoryStream())
            {
                using (var writer = await ParquetWriter.CreateAsync(schema, stream))
                {
                    writer.CompressionMethod = CompressionMethod.None;
                    foreach (var rows in rowGroups)
                    {
                        using (var group = writer.CreateRowGroup())
                        {
                            foreach (var chain in leaves)
                            {
                                var leaf = (DataField)chain[chain.Count - 1];
                                var values = new List<object>();
                                var levels = new int[rows.Length];
                                for (var i = 0; i < rows.Length; i++)
                                {
                                    JToken value = rows[i];
                                    foreach (var field in chain)
                                    {
                                        value = (value as JObject)?[field.Name];
                                        if (value == null || value.Type == JTokenType.Null) break;
                                        if (field.IsNullable) levels[i]++;
                                    }
                                    if (value != null && value.Type != JTokenType.Null)
                                        values.Add(value.ToObject(leaf.ClrType));
                                }
                                var defined = Array.CreateInstance(leaf.ClrType, values.Count);
                                for (var i = 0; i < values.Count; i++) defined.SetValue(values[i], i);
                                await group.WriteColumnAsync(new DataColumn(leaf, defined, definitionLevels: levels, repetitionLevels: null));
                            }
                        }
                    }
                }
                return stream.ToArray();
            }
        }

        private static DataField Text(string name) => new DataField<string>(name);
        private static DataField Number(string name) => new DataField<long?>(name);

        private static IEnumerable<List<Field>> LeafPaths(Field field, List<Field> parents)
        {
            var chain = new List<Field>(parents) { field };
            if (field is StructField structure)
            {
                foreach (var child in structure.Fields)
                    foreach (var path in LeafPaths(child, chain)) yield return path;
            }
            else yield return chain;
        }
    }
}
