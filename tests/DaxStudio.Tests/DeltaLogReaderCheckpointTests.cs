using DaxStudio.Core.DeltaAnalyzer;
using DaxStudio.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Parquet;
using Parquet.Schema;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static DaxStudio.Tests.Helpers.DeltaCheckpointFixture;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DeltaLogReaderCheckpointTests
    {
        private DeltaCheckpointFixture _storage;

        [TestInitialize]
        public void Initialize()
        {
            _storage = new DeltaCheckpointFixture();
        }

        [TestMethod]
        public async Task V1SchemaOnlyCheckpointRestoresStoreCodeMapping()
        {
            await _storage.ParquetAsync(V1Name, new[] { Protocol(), Metadata() });
            _storage.Pointer(size: 2);

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            AssertStoreMapping(result);
            Assert.AreEqual(0, result.ActiveFiles.Count);
            Assert.AreEqual(_storage.Location.FilesystemUrl, _storage.Listings.Single().Item1);
            Assert.AreEqual(_storage.LogDirectory, _storage.Listings.Single().Item2);
        }

        [TestMethod]
        public async Task V1ReadsMappingFromLaterRowGroup()
        {
            await _storage.ParquetAsync(V1Name,
                new[] { Protocol(), Add() },
                new[] { Metadata(), Add("part-b.parquet", 250, 25, 3) });
            _storage.Pointer(size: 4);

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            AssertStoreMapping(result);
            AssertTwoFiles(result);
        }

        [TestMethod]
        public async Task V1ReadsMappingFromLaterMultipartPart()
        {
            await _storage.ParquetAsync(PartName(1, 2), new[] { Protocol(), Add() });
            await _storage.ParquetAsync(PartName(2, 2), new[] { Metadata(), Add("part-b.parquet", 250, 25, 3) });
            _storage.Pointer(parts: 2, size: 4);

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            AssertStoreMapping(result);
            AssertTwoFiles(result);
            CollectionAssert.Contains(_storage.Reads, _storage.Url(PartName(1, 2)));
            CollectionAssert.Contains(_storage.Reads, _storage.Url(PartName(2, 2)));
        }

        [TestMethod]
        public async Task V1DiscoversCompleteMultipartCheckpointWithoutPointer()
        {
            await _storage.ParquetAsync(PartName(1, 2), new[] { Protocol(), Add() });
            await _storage.ParquetAsync(PartName(2, 2), new[] { Metadata() });

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            AssertStoreMapping(result);
            Assert.AreEqual("part-a.parquet", result.ActiveFiles.Single().Path);
        }

        [TestMethod]
        [DataRow("v1")]
        [DataRow("json")]
        [DataRow("parquet")]
        [DataRow("sidecars")]
        public async Task WithoutPointerLatestCheckpointIsDiscoveredAndOlderHistoryIsNotReplayed(string format)
        {
            await SeedCheckpointAsync(format);
            _storage.Delete("_last_checkpoint");
            const string older = "00000000000000000009.checkpoint.parquet";
            await _storage.ParquetAsync(older, new[] { Protocol(), Metadata(Schema("Old_Name")), Add("old.parquet") });
            _storage.Commit(0, Protocol(), Metadata(Schema("Original_Name")), Add("original.parquet"));
            _storage.Commit(11, Remove("part-a.parquet"), Add("latest.parquet", 321, 27));

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            AssertStoreMapping(result);
            Assert.AreEqual("latest.parquet", result.ActiveFiles.Single().Path);
            Assert.AreEqual(321L, result.ActiveFiles.Single().Size);
            Assert.AreEqual(27L, result.RowCountFromStats);
            CollectionAssert.DoesNotContain(_storage.Reads, _storage.Url(older));
            CollectionAssert.DoesNotContain(_storage.Reads, _storage.Url(CommitName(0)));
        }

        [TestMethod]
        public async Task WithoutCheckpointContiguousCommitsReplayFromVersionZero()
        {
            _storage.Commit(0, Protocol(), Metadata(), Add(deleted: 2));
            _storage.Commit(1, Add("part-b.parquet", 250, 25, 3));
            _storage.Commit(2, Remove("part-a.parquet"), Add("part-c.parquet", 75, 5));

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            AssertStoreMapping(result);
            CollectionAssert.AreEquivalent(new[] { "part-b.parquet", "part-c.parquet" },
                result.ActiveFiles.Select(f => f.Path).ToArray());
            Assert.AreEqual(325L, result.ActiveFiles.Sum(f => f.Size));
            Assert.AreEqual(30L, result.RowCountFromStats);
            Assert.AreEqual(1L, result.DeletionVectorFileCount);
            Assert.AreEqual(3L, result.DeletionVectorRowCount);
            CollectionAssert.AreEqual(new[]
            {
                _storage.Url(CommitName(0)), _storage.Url(CommitName(1)), _storage.Url(CommitName(2))
            }, _storage.Reads);
        }

        [TestMethod]
        public async Task WithoutCheckpointMissingVersionZeroFailsClosed()
        {
            _storage.Commit(1, Protocol(), Metadata(), Add());

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
            Assert.AreEqual(0, _storage.Reads.Count);
        }

        [TestMethod]
        [DataRow("json", "bare")]
        [DataRow("json", "relative")]
        [DataRow("json", "absolute")]
        [DataRow("json", "abfss")]
        [DataRow("parquet", "bare")]
        [DataRow("parquet", "relative")]
        [DataRow("parquet", "absolute")]
        [DataRow("parquet", "abfss")]
        public async Task V2FollowsFileOnlySidecarsAndPreservesMappingAndStatistics(string format, string pathKind)
        {
            var sidecar = "sidecar-01.parquet";
            await _storage.ParquetAsync("_sidecars/" + sidecar, new[] { Add() });
            await _storage.ParquetAsync("_sidecars/sidecar-02.parquet",
                new[] { Remove("expired.parquet") }, new[] { Add("part-b.parquet", 250, 25, 3) });
            var reference = pathKind == "absolute" ? _storage.Url("_sidecars/" + sidecar)
                : pathKind == "abfss" ? _storage.AbfssUrl("_sidecars/" + sidecar)
                : pathKind == "relative" ? "_sidecars/" + sidecar : sidecar;
            await _storage.TopLevelAsync(format, V2Name(format),
                CheckpointMetadata(), Protocol(true), Metadata(),
                Sidecar(reference, _storage.Length("_sidecars/" + sidecar)),
                Sidecar("sidecar-02.parquet", _storage.Length("_sidecars/sidecar-02.parquet")));
            _storage.Pointer(v2Path: V2Name(format), size: 6);

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            AssertStoreMapping(result);
            AssertTwoFiles(result);
            Assert.AreEqual(1, _storage.Reads.Count(u => u == _storage.Url("_sidecars/" + sidecar)));
            Assert.AreEqual(1, _storage.Reads.Count(u => u == _storage.Url("_sidecars/sidecar-02.parquet")));
        }

        [TestMethod]
        [DataRow("json")]
        [DataRow("parquet")]
        public async Task V2InlineFileActionsDoNotRequireSidecars(string format)
        {
            await _storage.TopLevelAsync(format, V2Name(format),
                CheckpointMetadata(), Protocol(true), Metadata(), Add(), Add("part-b.parquet", 250, 25, 3));
            _storage.Pointer(v2Path: V2Name(format), size: 5);

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            AssertStoreMapping(result);
            AssertTwoFiles(result);
            Assert.IsFalse(_storage.Reads.Any(u => u.Contains("/_sidecars/")));
        }

        [TestMethod]
        [DataRow("json", false)]
        [DataRow("parquet", false)]
        [DataRow("json", true)]
        [DataRow("parquet", true)]
        public async Task V2PointerSelectsOneOfSameVersionAlternatives(string format, bool absolutePointer)
        {
            var selected = V2Name(format);
            var other = V2Name(format, true);
            await _storage.TopLevelAsync(format, selected,
                CheckpointMetadata(), Protocol(true), Metadata(), Add("selected.parquet", 123, 7));
            await _storage.TopLevelAsync(format, other,
                CheckpointMetadata(), Protocol(true), Metadata(Schema("Wrong_Alternative")), Add("wrong.parquet"));
            await _storage.ParquetAsync(V1Name, new[] { Protocol(), Metadata(Schema("Wrong_V1")), Add("wrong-v1.parquet") });
            _storage.Pointer(v2Path: absolutePointer ? _storage.Url(selected) : selected, size: 4);

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            AssertStoreMapping(result);
            Assert.AreEqual("selected.parquet", result.ActiveFiles.Single().Path);
            CollectionAssert.DoesNotContain(_storage.Reads, _storage.Url(other));
            CollectionAssert.DoesNotContain(_storage.Reads, _storage.Url(V1Name));
        }

        [TestMethod]
        [DataRow("v1")]
        [DataRow("json")]
        [DataRow("parquet")]
        [DataRow("sidecars")]
        public async Task LaterDataOnlyCommitsRetainCheckpointMapping(string format)
        {
            await SeedCheckpointAsync(format);
            // Listing order is reversed; replay must still apply 11 before 12.
            _storage.Commit(11, Remove("part-a.parquet"), Add("transient.parquet", 50, 4, 1));
            _storage.Commit(12, Remove("transient.parquet"), Add("latest.parquet", 450, 40));

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            AssertStoreMapping(result);
            Assert.AreEqual("latest.parquet", result.ActiveFiles.Single().Path);
            Assert.AreEqual(450L, result.ActiveFiles.Single().Size);
            Assert.AreEqual(40L, result.RowCountFromStats);
            Assert.IsFalse(result.HasDeletionVectors);
            Assert.AreEqual(0L, result.DeletionVectorRowCount);
        }

        [TestMethod]
        [DataRow("v1")]
        [DataRow("json")]
        [DataRow("parquet")]
        [DataRow("sidecars")]
        public async Task LaterSchemaRenameReplacesMappingAndRemovesStaleEntries(string format)
        {
            await SeedCheckpointAsync(format, Schema(includeOld: true));
            _storage.Commit(11, Metadata(Schema("Store Identifier")));
            _storage.Commit(12, Add("new.parquet", 25, 2));

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            Assert.AreEqual(1, result.PhysicalToLogicalColumn.Count);
            Assert.AreEqual("Store Identifier", result.TranslateColumnName(PhysicalStore));
            Assert.IsFalse(result.PhysicalToLogicalColumn.ContainsKey(PhysicalOld));
            Assert.AreEqual(PhysicalOld, result.TranslateColumnName(PhysicalOld));
            Assert.AreEqual(2, result.ActiveFiles.Count);
            Assert.AreEqual(14L, result.RowCountFromStats);
        }

        [TestMethod]
        [DataRow("v1")]
        [DataRow("json")]
        [DataRow("parquet")]
        [DataRow("sidecars")]
        public async Task PlainNonmappedCheckpointSchemaKeepsOriginalColumnNames(string format)
        {
            await SeedCheckpointAsync(format, Schema(mapped: false));

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            Assert.AreEqual(0, result.PhysicalToLogicalColumn.Count);
            Assert.AreEqual("Store_Code", result.TranslateColumnName("Store_Code"));
            Assert.AreEqual(PhysicalStore, result.TranslateColumnName(PhysicalStore));
            Assert.AreEqual(12L, result.RowCountFromStats);
        }

        [TestMethod]
        public async Task LaterPlainSchemaClearsPreviousColumnMapping()
        {
            await SeedCheckpointAsync("v1");
            _storage.Commit(11, Metadata(Schema(mapped: false)));

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            Assert.AreEqual(0, result.PhysicalToLogicalColumn.Count);
            Assert.AreEqual(PhysicalStore, result.TranslateColumnName(PhysicalStore));
            Assert.AreEqual(1, result.ActiveFiles.Count);
        }

        [TestMethod]
        [DataRow("json", "bare")]
        [DataRow("json", "absolute")]
        [DataRow("parquet", "relative")]
        [DataRow("parquet", "absolute")]
        public async Task EscapedTableSidecarAndDataNamesAreResolvedWithoutDoubleEscaping(string format, string pathKind)
        {
            _storage = new DeltaCheckpointFixture("Sales & Ops.Lakehouse/Tables/dbo/Store # + % café",
                "https://onelake.dfs.fabric.microsoft.com/Offline%20Workspace");
            const string sidecar = "snapshot # + % café.parquet";
            const string dataPath = "region=North West/part # + % café.parquet";
            const string logical = "Store Code # + % café";
            await _storage.ParquetAsync("_sidecars/" + sidecar, new[] { Add(EscapePath(dataPath), 345, 19, 2) });
            var reference = pathKind == "absolute" ? _storage.Url("_sidecars/" + sidecar)
                : (pathKind == "relative" ? "_sidecars/" : "") + Uri.EscapeDataString(sidecar);
            await _storage.TopLevelAsync(format, V2Name(format), CheckpointMetadata(), Protocol(true),
                Metadata(Schema(logical)), Sidecar(reference, _storage.Length("_sidecars/" + sidecar)));
            _storage.Pointer(v2Path: V2Name(format), size: 4);

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            Assert.AreEqual(logical, result.TranslateColumnName(PhysicalStore));
            Assert.AreEqual(dataPath, result.ActiveFiles.Single().Path);
            Assert.AreEqual(345L, result.ActiveFiles.Single().Size);
            Assert.AreEqual(19L, result.RowCountFromStats);
            Assert.AreEqual(2L, result.DeletionVectorRowCount);
            Assert.AreEqual(_storage.LogDirectory, _storage.Listings.Single().Item2);
            CollectionAssert.Contains(_storage.Reads, _storage.Url("_sidecars/" + sidecar));
        }

        [TestMethod]
        public async Task ParsedCheckpointStatsAndDeletionVectorsStayAlignedAcrossSparseRows()
        {
            await _storage.ParquetAsync(V1Name, new[]
            {
                Add("deleted.parquet", 200, 20, 4, parsedStats: true),
                Metadata(), Protocol(),
                Add("plain.parquet", 80, 8, parsedStats: true)
            });
            _storage.Pointer(size: 4);

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            var deleted = result.ActiveFiles.Single(f => f.Path == "deleted.parquet");
            var plain = result.ActiveFiles.Single(f => f.Path == "plain.parquet");
            Assert.AreEqual(20L, deleted.NumRecords.Value);
            Assert.AreEqual(200L, deleted.Size);
            Assert.IsTrue(deleted.HasDeletionVector);
            Assert.AreEqual(4L, deleted.DeletionVectorCardinality);
            Assert.AreEqual(8L, plain.NumRecords.Value);
            Assert.AreEqual(80L, plain.Size);
            Assert.IsFalse(plain.HasDeletionVector);
            Assert.AreEqual(0L, plain.DeletionVectorCardinality);
            Assert.AreEqual(28L, result.RowCountFromStats);
            Assert.AreEqual(1L, result.DeletionVectorFileCount);
        }

        [TestMethod]
        [DataRow("{\"numRecords\":12}", 12L)]
        [DataRow("{}", 99L)]
        [DataRow("{", 99L)]
        [DataRow(null, 99L)]
        public async Task ParsedCheckpointStatsAreOnlyUsedWhenJsonRowCountIsUnavailable(string stats, long expectedRows)
        {
            var add = Add(rows: 99, deleted: 3, parsedStats: true);
            add["add"]["stats"] = stats;
            await _storage.ParquetAsync(V1Name, new[] { Protocol(), Metadata(), add });
            _storage.Pointer(size: 3);

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            Assert.AreEqual(expectedRows, result.ActiveFiles.Single().NumRecords.Value);
            Assert.AreEqual(expectedRows, result.RowCountFromStats);
            Assert.IsTrue(result.AllFilesHaveRowCount);
            Assert.AreEqual(3L, result.DeletionVectorRowCount);
        }

        [TestMethod]
        public async Task CheckpointFileWithoutEitherStatsRepresentationKeepsUnknownRowCount()
        {
            var add = Add();
            ((JObject)add["add"]).Remove("stats");
            await _storage.ParquetAsync(V1Name, new[] { Protocol(), Metadata(), add });
            _storage.Pointer(size: 3);

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            Assert.AreEqual(100L, result.ActiveFiles.Single().Size);
            Assert.IsNull(result.ActiveFiles.Single().NumRecords);
            Assert.IsFalse(result.AllFilesHaveRowCount);
            Assert.AreEqual(0L, result.RowCountFromStats);
        }

        [TestMethod]
        [DataRow("v1")]
        [DataRow("json")]
        [DataRow("parquet")]
        [DataRow("sidecars")]
        public async Task CheckpointTombstoneDoesNotRemoveActiveAddForSameDataPath(string format)
        {
            var add = Add(deleted: 3);
            var tombstone = Remove("part-a.parquet");
            if (format == "v1")
            {
                await _storage.ParquetAsync(V1Name,
                    new[] { Protocol(), Metadata(), add }, new[] { tombstone });
                _storage.Pointer(size: 4);
            }
            else if (format == "sidecars")
            {
                await _storage.ParquetAsync("_sidecars/adds.parquet", new[] { add });
                await _storage.ParquetAsync("_sidecars/tombstones.parquet", new[] { tombstone });
                await _storage.TopLevelAsync("json", V2Name("json"), CheckpointMetadata(), Protocol(true), Metadata(),
                    Sidecar("adds.parquet", _storage.Length("_sidecars/adds.parquet")),
                    Sidecar("tombstones.parquet", _storage.Length("_sidecars/tombstones.parquet")));
                _storage.Pointer(v2Path: V2Name("json"), size: 5);
            }
            else
            {
                await _storage.TopLevelAsync(format, V2Name(format),
                    CheckpointMetadata(), Protocol(true), Metadata(), add, tombstone);
                _storage.Pointer(v2Path: V2Name(format), size: 5);
            }

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            AssertStoreMapping(result);
            Assert.AreEqual("part-a.parquet", result.ActiveFiles.Single().Path);
            Assert.AreEqual(100L, result.ActiveFiles.Single().Size);
            Assert.AreEqual(12L, result.RowCountFromStats);
            Assert.AreEqual(1L, result.DeletionVectorFileCount);
            Assert.AreEqual(3L, result.DeletionVectorRowCount);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task MissingMultipartPartFailsClosed(bool stillListed)
        {
            await _storage.ParquetAsync(PartName(1, 2), new[] { Protocol(), Metadata(), Add() });
            await _storage.ParquetAsync(PartName(2, 2), new[] { Add("missing.parquet") });
            _storage.Delete(PartName(2, 2), stillListed);
            _storage.Pointer(parts: 2, size: 4);

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
        }

        [TestMethod]
        [DataRow("json")]
        [DataRow("parquet")]
        public async Task MissingSidecarAfterReadableSidecarFailsClosed(string format)
        {
            await _storage.ParquetAsync("_sidecars/present.parquet", new[] { Add() });
            await _storage.TopLevelAsync(format, V2Name(format), CheckpointMetadata(), Protocol(true), Metadata(),
                Sidecar("present.parquet", _storage.Length("_sidecars/present.parquet")),
                Sidecar("missing.parquet", 123));
            _storage.Pointer(v2Path: V2Name(format), size: 5);

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
            Assert.IsTrue(result.LogDirectoryFound, "A missing sidecar must not trigger table-directory fallback.");
            CollectionAssert.Contains(_storage.Reads, _storage.Url("_sidecars/present.parquet"));
            CollectionAssert.Contains(_storage.Reads, _storage.Url("_sidecars/missing.parquet"));
        }

        [TestMethod]
        public async Task FailedLogDirectoryListingDoesNotMarkDirectoryAsFound()
        {
            var listed = false;
            var reader = new DeltaLogReader((filesystem, directory, ct) =>
            {
                listed = true;
                Assert.AreEqual(_storage.Location.FilesystemUrl, filesystem);
                Assert.AreEqual(_storage.LogDirectory, directory);
                return Task.FromException<System.Collections.Generic.List<OneLakePathItem>>(
                    new DirectoryNotFoundException("Offline _delta_log directory not found (404)."));
            }, _storage.ReadText, _storage.OpenRead);

            var result = await reader.ReadAsync(_storage.TableUrl, CancellationToken.None);

            Assert.IsTrue(listed);
            AssertFailsClosed(result);
            Assert.IsFalse(result.LogDirectoryFound, "A failed listing must permit table-directory fallback.");
            Assert.AreEqual(0, _storage.Reads.Count);
        }

        [TestMethod]
        public async Task EmptySuccessfulLogDirectoryListingStillMarksDirectoryAsFound()
        {
            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
            Assert.IsTrue(result.LogDirectoryFound, "An empty existing log directory is not a missing table directory.");
            Assert.AreEqual(1, _storage.Listings.Count);
            Assert.AreEqual(0, _storage.Reads.Count);
        }

        [TestMethod]
        [DataRow("v1", "{")]
        [DataRow("v1", "{\"type\":\"struct\"}")]
        [DataRow("json", "{")]
        [DataRow("parquet", "{")]
        public async Task MalformedCheckpointSchemaFailsClosed(string format, string malformedSchema)
        {
            await SeedCheckpointAsync(format, malformedSchema);

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
        }

        [TestMethod]
        public async Task MalformedLaterSchemaFailsClosedEvenAfterValidCheckpointFiles()
        {
            await SeedCheckpointAsync("v1");
            _storage.Commit(11, Add("new.parquet"), Metadata("{"));

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
        }

        [TestMethod]
        [DataRow("json", "missing")]
        [DataRow("parquet", "missing")]
        [DataRow("json", "wrongVersion")]
        [DataRow("parquet", "wrongVersion")]
        [DataRow("json", "missingVersion")]
        [DataRow("parquet", "missingVersion")]
        [DataRow("json", "duplicate")]
        [DataRow("parquet", "duplicate")]
        public async Task InvalidV2CheckpointMetadataFailsClosed(string format, string defect)
        {
            var actions = new System.Collections.Generic.List<JObject> { Protocol(true), Metadata(), Add() };
            if (defect != "missing")
                actions.Add(CheckpointMetadata(defect == "wrongVersion" ? DeltaCheckpointFixture.Version - 1 : DeltaCheckpointFixture.Version));
            if (defect == "missingVersion") ((JObject)actions.Last()["checkpointMetadata"]).Remove("version");
            if (defect == "duplicate") actions.Add(CheckpointMetadata());
            await _storage.TopLevelAsync(format, V2Name(format), actions.ToArray());
            _storage.Pointer(v2Path: V2Name(format), size: actions.Count);

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
        }

        [TestMethod]
        [DataRow("v1")]
        [DataRow("json")]
        [DataRow("parquet")]
        public async Task CheckpointWithoutTableSchemaFailsClosed(string format)
        {
            if (format == "v1")
            {
                await _storage.ParquetAsync(V1Name, new[] { Protocol(), Add() });
                _storage.Pointer(size: 2);
            }
            else
            {
                await _storage.TopLevelAsync(format, V2Name(format), CheckpointMetadata(), Protocol(true), Add());
                _storage.Pointer(v2Path: V2Name(format), size: 3);
            }

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
        }

        [TestMethod]
        [DataRow("rowGroups")]
        [DataRow("parts")]
        [DataRow("json")]
        [DataRow("parquet")]
        public async Task DuplicateCheckpointSchemaFailsClosedAcrossAllCheckpointSections(string layout)
        {
            if (layout == "rowGroups")
            {
                await _storage.ParquetAsync(V1Name,
                    new[] { Protocol(), Metadata(), Add() }, new[] { Metadata(Schema("Duplicate")) });
                _storage.Pointer(size: 4);
            }
            else if (layout == "parts")
            {
                await _storage.ParquetAsync(PartName(1, 2), new[] { Protocol(), Metadata(), Add() });
                await _storage.ParquetAsync(PartName(2, 2), new[] { Metadata(Schema("Duplicate")) });
                _storage.Pointer(parts: 2, size: 4);
            }
            else
            {
                await _storage.TopLevelAsync(layout, V2Name(layout),
                    CheckpointMetadata(), Protocol(true), Metadata(), Add(), Metadata(Schema("Duplicate")));
                _storage.Pointer(v2Path: V2Name(layout), size: 5);
            }

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
        }

        [TestMethod]
        [DataRow("json", "schema")]
        [DataRow("parquet", "schema")]
        [DataRow("json", "checkpointMetadata")]
        [DataRow("parquet", "checkpointMetadata")]
        [DataRow("json", "sidecar")]
        [DataRow("parquet", "sidecar")]
        public async Task SidecarWithNonFileActionFailsClosed(string format, string actionKind)
        {
            await _storage.ParquetAsync("_sidecars/nested.parquet", new[] { Add("nested.parquet") });
            var forbidden = actionKind == "schema" ? Metadata()
                : actionKind == "checkpointMetadata" ? CheckpointMetadata()
                : Sidecar("nested.parquet", _storage.Length("_sidecars/nested.parquet"));
            await _storage.ParquetAsync("_sidecars/files.parquet", new[] { Add() }, new[] { forbidden });
            await _storage.TopLevelAsync(format, V2Name(format), CheckpointMetadata(), Protocol(true), Metadata(),
                Sidecar("files.parquet", _storage.Length("_sidecars/files.parquet")));
            _storage.Pointer(v2Path: V2Name(format), size: 5);

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
            CollectionAssert.Contains(_storage.Reads, _storage.Url("_sidecars/files.parquet"));
            CollectionAssert.DoesNotContain(_storage.Reads, _storage.Url("_sidecars/nested.parquet"));
        }

        [TestMethod]
        public async Task NullNonFileActionColumnsInSidecarDoNotCountAsActions()
        {
            var add = Add();
            add["metaData"] = null;
            add["checkpointMetadata"] = null;
            add["sidecar"] = null;
            await _storage.ParquetAsync("_sidecars/files.parquet", new[] { add });
            await _storage.TopLevelAsync("parquet", V2Name("parquet"), CheckpointMetadata(), Protocol(true), Metadata(),
                Sidecar("files.parquet", _storage.Length("_sidecars/files.parquet")));
            _storage.Pointer(v2Path: V2Name("parquet"), size: 4);

            var result = await _storage.ReadAsync();

            AssertSuccess(result);
            AssertStoreMapping(result);
            Assert.AreEqual("part-a.parquet", result.ActiveFiles.Single().Path);
            Assert.AreEqual(12L, result.RowCountFromStats);
        }

        [TestMethod]
        [DataRow("json", "add")]
        [DataRow("parquet", "add")]
        [DataRow("json", "remove")]
        [DataRow("parquet", "remove")]
        public async Task V2CannotMixInlineFileActionsAndSidecars(string format, string action)
        {
            await _storage.ParquetAsync("_sidecars/files.parquet", new[] { Add("sidecar.parquet") });
            var inline = action == "add" ? Add() : Remove("expired.parquet");
            await _storage.TopLevelAsync(format, V2Name(format), CheckpointMetadata(), Protocol(true), Metadata(), inline,
                Sidecar("files.parquet", _storage.Length("_sidecars/files.parquet")));
            _storage.Pointer(v2Path: V2Name(format), size: 5);

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
        }

        [TestMethod]
        [DataRow("json", "files.parquet")]
        [DataRow("parquet", "files.parquet")]
        [DataRow("json", "_sidecars/files.parquet")]
        [DataRow("parquet", "_sidecars/files.parquet")]
        public async Task DuplicateSidecarReferencesFailClosedEvenWithDifferentPathSpellings(string format, string duplicate)
        {
            await _storage.ParquetAsync("_sidecars/files.parquet", new[] { Add() });
            var size = _storage.Length("_sidecars/files.parquet");
            await _storage.TopLevelAsync(format, V2Name(format), CheckpointMetadata(), Protocol(true), Metadata(),
                Sidecar("files.parquet", size), Sidecar(duplicate, size));
            _storage.Pointer(v2Path: V2Name(format), size: 5);

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
            Assert.AreEqual(1, _storage.Reads.Count(u => u == _storage.Url("_sidecars/files.parquet")));
        }

        [TestMethod]
        [DataRow("../outside.parquet")]
        [DataRow("%2e%2e%2foutside.parquet")]
        [DataRow("_sidecars/../outside.parquet")]
        [DataRow("nested/outside.parquet")]
        [DataRow("..\\outside.parquet")]
        [DataRow("https://elsewhere.invalid/offline-workspace/outside.parquet")]
        public async Task InvalidSidecarPathFailsClosedWithoutOpeningIt(string path)
        {
            await _storage.TopLevelAsync("json", V2Name("json"),
                CheckpointMetadata(), Protocol(true), Metadata(), Sidecar(path, 100));
            _storage.Pointer(v2Path: V2Name("json"), size: 4);

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
            CollectionAssert.AreEquivalent(new[] { _storage.Url("_last_checkpoint"), _storage.Url(V2Name("json")) },
                _storage.Reads);
        }

        [TestMethod]
        [DataRow("parent")]
        [DataRow("encodedParent")]
        [DataRow("otherTable")]
        [DataRow("query")]
        [DataRow("wrongVersion")]
        public async Task InvalidV2CheckpointPointerFailsClosedBeforeOpeningCheckpoint(string kind)
        {
            var name = V2Name("json");
            await _storage.TopLevelAsync("json", name, CheckpointMetadata(), Protocol(true), Metadata(), Add());
            string path;
            switch (kind)
            {
                case "parent": path = "../" + name; break;
                case "encodedParent": path = "%2e%2e%2f" + name; break;
                case "otherTable": path = _storage.Location.BuildFileUrl("Other.Lakehouse/Tables/Stores/_delta_log/" + name); break;
                case "query": path = _storage.Url(name) + "?unexpected=1"; break;
                default: path = name.Replace("00000000000000000010", "00000000000000000009"); break;
            }
            _storage.Pointer(v2Path: path, size: 4);

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
            CollectionAssert.AreEqual(new[] { _storage.Url("_last_checkpoint") }, _storage.Reads);
        }

        [TestMethod]
        [DataRow("v1")]
        [DataRow("json")]
        [DataRow("parquet")]
        [DataRow("sidecars")]
        public async Task CommitGapAfterCheckpointFailsClosed(string format)
        {
            await SeedCheckpointAsync(format);
            _storage.Commit(11, Add("valid-later.parquet"));
            _storage.Commit(13, Add("after-gap.parquet"));

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
            CollectionAssert.Contains(_storage.Reads, _storage.Url(CommitName(11)));
            CollectionAssert.DoesNotContain(_storage.Reads, _storage.Url(CommitName(13)));
        }

        [TestMethod]
        public async Task CommitGapWithoutCheckpointFailsClosed()
        {
            _storage.Commit(0, Protocol(), Metadata(), Add());
            _storage.Commit(2, Add("after-gap.parquet"));

            var result = await _storage.ReadAsync();

            AssertFailsClosed(result);
        }

        [TestMethod]
        public async Task CancellationBeforeListingPropagates()
        {
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();

                await AssertCancellationAsync(() => _storage.ReadAsync(source.Token));

                Assert.AreEqual(0, _storage.Listings.Count);
                Assert.AreEqual(0, _storage.Reads.Count);
            }
        }

        [TestMethod]
        [DataRow("pointer")]
        [DataRow("checkpoint")]
        [DataRow("sidecar")]
        [DataRow("commit")]
        public async Task CancellationDuringStorageAccessPropagatesRatherThanReturningError(string stage)
        {
            await _storage.ParquetAsync("_sidecars/files.parquet", new[] { Add() });
            await _storage.TopLevelAsync("parquet", V2Name("parquet"), CheckpointMetadata(), Protocol(true), Metadata(),
                Sidecar("files.parquet", _storage.Length("_sidecars/files.parquet")));
            _storage.Pointer(v2Path: V2Name("parquet"), size: 4);
            _storage.Commit(11, Add("later.parquet"));
            var target = stage == "pointer" ? "_last_checkpoint"
                : stage == "checkpoint" ? V2Name("parquet")
                : stage == "sidecar" ? "_sidecars/files.parquet" : CommitName(11);
            using (var source = new CancellationTokenSource())
            {
                _storage.BeforeRead = (url, ct) =>
                {
                    Assert.AreEqual(source.Token, ct);
                    if (url == _storage.Url(target)) source.Cancel();
                };

                await AssertCancellationAsync(() => _storage.ReadAsync(source.Token));

                CollectionAssert.Contains(_storage.Reads, _storage.Url(target));
            }
        }

        [TestMethod]
        public async Task CanceledReadTextTaskPropagatesCancellation()
        {
            await SeedCheckpointAsync("v1");
            using (var source = new CancellationTokenSource())
            {
                var called = false;
                var reader = new DeltaLogReader(_storage.ListPaths, (url, ct) =>
                {
                    Assert.AreEqual(_storage.Url("_last_checkpoint"), url);
                    Assert.AreEqual(source.Token, ct);
                    called = true;
                    source.Cancel();
                    return Task.FromCanceled<string>(source.Token);
                }, _storage.OpenRead);

                await AssertCancellationAsync(() => reader.ReadAsync(_storage.TableUrl, source.Token));

                Assert.IsTrue(called);
            }
        }

        [TestMethod]
        public async Task RepeatedReadsOpenFreshCheckpointStreams()
        {
            await SeedCheckpointAsync("v1");
            var reader = _storage.CreateReader();

            var first = await reader.ReadAsync(_storage.TableUrl, CancellationToken.None);
            var second = await reader.ReadAsync(_storage.TableUrl, CancellationToken.None);

            AssertSuccess(first);
            AssertSuccess(second);
            AssertStoreMapping(second);
            Assert.AreEqual(first.RowCountFromStats, second.RowCountFromStats);
            Assert.AreEqual(1, second.ActiveFiles.Count);
            Assert.AreEqual(2, _storage.Reads.Count(u => u == _storage.Url(V1Name)));
        }

        [TestMethod]
        public async Task ParquetFixtureHasNestedDeltaPathsAndSparseRowGroups()
        {
            // Guard the fixture itself against accidentally writing flat dotted column names
            // or collapsing null action rows, either of which would weaken the regressions.
            var bytes = await WriteParquetAsync(
                new[] { Add(deleted: 3), Protocol() }, new[] { Metadata(), Add("second.parquet") });
            using (var stream = new MemoryStream(bytes))
            using (var reader = await ParquetReader.CreateAsync(stream))
            {
                Assert.AreEqual(2, reader.RowGroupCount);
                var fields = reader.Schema.GetDataFields();
                var schemaField = fields.Single(f => f.Path.Equals(new FieldPath("metaData", "schemaString")));
                var addPath = fields.Single(f => f.Path.Equals(new FieldPath("add", "path")));
                Assert.AreEqual("schemaString", schemaField.Name);
                Assert.IsTrue(fields.Any(f => f.Path.Equals(new FieldPath("add", "deletionVector", "cardinality"))));
                using (var group = reader.OpenRowGroupReader(1))
                {
                    var schemas = (await group.ReadColumnAsync(schemaField)).Data;
                    var paths = (await group.ReadColumnAsync(addPath)).Data;
                    Assert.AreEqual(2, schemas.Length);
                    Assert.AreEqual(Schema(), schemas.GetValue(0));
                    Assert.IsNull(schemas.GetValue(1));
                    Assert.IsNull(paths.GetValue(0));
                    Assert.AreEqual("second.parquet", paths.GetValue(1));
                }
            }
        }

        [TestMethod]
        public async Task ParquetFixturePreservesNullableNestedStructDefinitionLevelsAndRowAlignment()
        {
            var nullCardinality = Add("null-cardinality.parquet", deleted: 2);
            nullCardinality["add"]["deletionVector"]["cardinality"] = null;
            var bytes = await WriteParquetAsync(new[]
            {
                Metadata(), Add(deleted: 3), Add("plain.parquet"),
                Add("zero.parquet", deleted: 0), nullCardinality
            });
            using (var stream = new MemoryStream(bytes))
            using (var reader = await ParquetReader.CreateAsync(stream))
            using (var group = reader.OpenRowGroupReader(0))
            {
                var fields = reader.Schema.GetDataFields();
                var cardinalities = await group.ReadColumnAsync(fields.Single(f => f.Path.Equals(new FieldPath("add", "deletionVector", "cardinality"))));
                var storageTypes = await group.ReadColumnAsync(fields.Single(f => f.Path.Equals(new FieldPath("add", "deletionVector", "storageType"))));
                var paths = await group.ReadColumnAsync(fields.Single(f => f.Path.Equals(new FieldPath("add", "path"))));

                CollectionAssert.AreEqual(new long?[] { null, 3, null, 0, null }, cardinalities.Data);
                CollectionAssert.AreEqual(new[] { 0, 3, 1, 3, 2 }, cardinalities.DefinitionLevels);
                CollectionAssert.AreEqual(new string[] { null, "u", null, "u", "u" }, storageTypes.Data);
                CollectionAssert.AreEqual(new[] { 0, 3, 1, 3, 3 }, storageTypes.DefinitionLevels);
                CollectionAssert.AreEqual(new[] { null, "part-a.parquet", "plain.parquet", "zero.parquet", "null-cardinality.parquet" },
                    paths.Data);
                CollectionAssert.AreEqual(new[] { 0, 2, 2, 2, 2 }, paths.DefinitionLevels);
            }
        }

        private async Task SeedCheckpointAsync(string format, string schema = null)
        {
            if (format == "v1")
            {
                await _storage.ParquetAsync(V1Name, new[] { Protocol(), Add(), Metadata(schema) });
                _storage.Pointer(size: 3);
            }
            else if (format == "sidecars")
            {
                await _storage.ParquetAsync("_sidecars/files.parquet", new[] { Add() });
                await _storage.TopLevelAsync("parquet", V2Name("parquet"), CheckpointMetadata(), Protocol(true), Metadata(schema),
                    Sidecar("files.parquet", _storage.Length("_sidecars/files.parquet")));
                _storage.Pointer(v2Path: V2Name("parquet"), size: 4);
            }
            else
            {
                await _storage.TopLevelAsync(format, V2Name(format),
                    CheckpointMetadata(), Protocol(true), Add(), Metadata(schema));
                _storage.Pointer(v2Path: V2Name(format), size: 4);
            }
        }

        private static void AssertSuccess(DeltaLogResult result)
        {
            Assert.IsTrue(string.IsNullOrEmpty(result.Error), result.Error);
            Assert.IsTrue(result.LogDirectoryFound);
        }

        private static void AssertFailsClosed(DeltaLogResult result)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Error), "An incomplete snapshot must report an error.");
            Assert.AreEqual(0, result.ActiveFiles.Count, "An incomplete snapshot must never expose partial active files.");
        }

        private static void AssertStoreMapping(DeltaLogResult result)
        {
            Assert.AreEqual(1, result.PhysicalToLogicalColumn.Count);
            Assert.AreEqual("Store_Code", result.PhysicalToLogicalColumn[PhysicalStore]);
            Assert.AreEqual("Store_Code", result.TranslateColumnName(PhysicalStore));
            Assert.AreEqual("Store_Code", result.TranslateColumnName(PhysicalStore.Substring("col-".Length)));
            Assert.AreEqual("unknown", result.TranslateColumnName("unknown"));
        }

        private static void AssertTwoFiles(DeltaLogResult result)
        {
            Assert.AreEqual(2, result.ActiveFiles.Count);
            var first = result.ActiveFiles.Single(f => f.Path == "part-a.parquet");
            var second = result.ActiveFiles.Single(f => f.Path == "part-b.parquet");
            Assert.AreEqual(100L, first.Size);
            Assert.AreEqual(12L, first.NumRecords.Value);
            Assert.IsFalse(first.HasDeletionVector);
            Assert.AreEqual(250L, second.Size);
            Assert.AreEqual(25L, second.NumRecords.Value);
            Assert.IsTrue(second.HasDeletionVector);
            Assert.AreEqual(3L, second.DeletionVectorCardinality);
            Assert.AreEqual(350L, result.ActiveFiles.Sum(f => f.Size));
            Assert.AreEqual(37L, result.RowCountFromStats);
            Assert.IsTrue(result.AllFilesHaveRowCount);
            Assert.IsTrue(result.HasDeletionVectors);
            Assert.AreEqual(1L, result.DeletionVectorFileCount);
            Assert.AreEqual(3L, result.DeletionVectorRowCount);
            Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(Modified), result.LastModifiedUtc.Value);
        }

        private static string EscapePath(string path) => string.Join("/", path.Split('/').Select(Uri.EscapeDataString));

        private static async Task AssertCancellationAsync(Func<Task<DeltaLogResult>> action)
        {
            // Accept derived TaskCanceledException as well, without relying on newer MSTest-only APIs.
            try { await action(); }
            catch (OperationCanceledException) { return; }
            Assert.Fail("ReadAsync must propagate cancellation, not return a DeltaLogResult with Error.");
        }
    }
}
