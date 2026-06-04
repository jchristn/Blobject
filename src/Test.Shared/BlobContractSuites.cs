namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;
    using Touchstone.Core;

    /// <summary>
    /// Shared BLOB contract test descriptors.
    /// </summary>
    public static class BlobContractSuites
    {
        private static BlobProviderOptions _Options = BlobProviderOptions.FromEnvironment();

        /// <summary>
        /// Configure the provider used by generated suites.
        /// </summary>
        /// <param name="options">Provider options.</param>
        public static void Configure(BlobProviderOptions options)
        {
            _Options = options ?? BlobProviderOptions.FromEnvironment();
        }

        /// <summary>
        /// All automated suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return BuildSuites(_Options); }
        }

        /// <summary>
        /// Build suites for the supplied options.
        /// </summary>
        /// <param name="options">Provider options.</param>
        /// <returns>Suites.</returns>
        public static IReadOnlyList<TestSuiteDescriptor> BuildSuites(BlobProviderOptions options)
        {
            if (options == null) options = BlobProviderOptions.FromEnvironment();

            List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>
            {
                CoreSuite(options),
                WriteReadSuite(options),
                MetadataSuite(options),
                EnumerationSuite(options),
                BulkSuite(options),
                CopySuite(options)
            };

            if (options.IncludeStress)
            {
                suites.Add(StressSuite(options));
            }

            return suites;
        }

        private static TestSuiteDescriptor CoreSuite(BlobProviderOptions options)
        {
            const string suiteId = "Core";

            return new TestSuiteDescriptor(
                suiteId: suiteId,
                displayName: "Core Contracts",
                cases: new List<TestCaseDescriptor>
                {
                    ProviderCase(options, suiteId, "ValidateConnectivity", "ValidateConnectivity returns true", ValidateConnectivity),
                    ProviderCase(options, suiteId, "GenerateUrl", "GenerateUrl returns a non-empty string", GenerateUrl),
                    LocalCase(suiteId, "NormalizeAwsRegion", "AWS region aliases normalize", NormalizeAwsRegion),
                    LocalCase(suiteId, "HexRoundTrip", "hex conversion round trips bytes", HexRoundTrip),
                    LocalCase(suiteId, "EnumerationFilterClone", "EnumerationFilter.Clone preserves values and casing", EnumerationFilterClone),
                    LocalCase(suiteId, "EnumerationFilterRejectsNegativeMinimum", "EnumerationFilter rejects negative minimum size", EnumerationFilterRejectsNegativeMinimum),
                    LocalCase(suiteId, "EnumerationFilterRejectsNegativeMaximum", "EnumerationFilter rejects negative maximum size", EnumerationFilterRejectsNegativeMaximum),
                    LocalCase(suiteId, "BlobDataRejectsNegativeLength", "BlobData rejects negative content length", BlobDataRejectsNegativeLength),
                    LocalCase(suiteId, "BlobDataDisposeDisposesStream", "BlobData.Dispose disposes owned stream", BlobDataDisposeDisposesStream),
                    LocalCase(suiteId, "WriteRequestRejectsEmptyKey", "WriteRequest rejects empty key", WriteRequestRejectsEmptyKey),
                    LocalCase(suiteId, "WriteRequestDefaultsContentType", "WriteRequest defaults empty content type", WriteRequestDefaultsContentType),
                    LocalCase(suiteId, "WriteRequestSeeksEofStream", "WriteRequest seeks EOF stream back to start", WriteRequestSeeksEofStream),
                    ProviderCase(options, suiteId, "MaxConcurrencyRejectsZero", "MaxConcurrency rejects zero", MaxConcurrencyRejectsZero),
                    ProviderCase(options, suiteId, "StreamBufferSizeRejectsZero", "StreamBufferSize rejects zero", StreamBufferSizeRejectsZero)
                });
        }

        private static TestSuiteDescriptor WriteReadSuite(BlobProviderOptions options)
        {
            const string suiteId = "WriteRead";

            return new TestSuiteDescriptor(
                suiteId: suiteId,
                displayName: "Write and Read Contracts",
                cases: new List<TestCaseDescriptor>
                {
                    ProviderCase(options, suiteId, "EmptyStringWrite", "WriteAsync accepts empty string", EmptyStringWrite),
                    ProviderCase(options, suiteId, "EmptyBytesWrite", "WriteAsync accepts empty byte array", EmptyBytesWrite),
                    ProviderCase(options, suiteId, "NullStringWrite", "WriteAsync treats null string as empty", NullStringWrite),
                    ProviderCase(options, suiteId, "NullBytesWrite", "WriteAsync treats null bytes as empty", NullBytesWrite),
                    ProviderCase(options, suiteId, "AsciiStringRoundTrip", "ASCII string round trips", AsciiStringRoundTrip),
                    ProviderCase(options, suiteId, "BinaryRoundTrip", "binary payload round trips", BinaryRoundTrip),
                    ProviderCase(options, suiteId, "LargeBinaryRoundTrip", "large binary payload round trips", LargeBinaryRoundTrip),
                    ProviderCase(options, suiteId, "NestedKeyRoundTrip", "nested key round trips", NestedKeyRoundTrip),
                    ProviderCase(options, suiteId, "DeepNestedKeyRoundTrip", "deep nested key round trips", DeepNestedKeyRoundTrip),
                    ProviderCase(options, suiteId, "KeyWithSpacesRoundTrip", "key with spaces round trips", KeyWithSpacesRoundTrip),
                    ProviderCase(options, suiteId, "StreamRoundTrip", "stream write and stream read preserve content", StreamRoundTrip),
                    ProviderCase(options, suiteId, "EmptyStreamWrite", "empty stream write creates empty blob", EmptyStreamWrite),
                    ProviderCase(options, suiteId, "NullStreamZeroLengthWrite", "null stream with zero length creates empty blob", NullStreamZeroLengthWrite),
                    ProviderCase(options, suiteId, "NonSeekableStreamWrite", "non-seekable stream writes correctly", NonSeekableStreamWrite),
                    ProviderCase(options, suiteId, "ShortContentLengthWritesPrefix", "contentLength shorter than stream writes prefix only", ShortContentLengthWritesPrefix),
                    ProviderCase(options, suiteId, "ZeroContentLengthIgnoresStream", "zero contentLength writes empty blob", ZeroContentLengthIgnoresStream),
                    ProviderCase(options, suiteId, "GetStreamEmptyBlob", "GetStreamAsync returns readable empty stream", GetStreamEmptyBlob),
                    ProviderCase(options, suiteId, "OverwriteShorterTruncates", "overwriting with shorter data truncates", OverwriteShorterTruncates),
                    ProviderCase(options, suiteId, "OverwriteLongerReplaces", "overwriting with longer data replaces content", OverwriteLongerReplaces),
                    ProviderCase(options, suiteId, "OverwriteWithEmpty", "overwriting with empty data clears content", OverwriteWithEmpty),
                    ProviderCase(options, suiteId, "FolderMarkerWrite", "folder marker write is supported", FolderMarkerWrite)
                });
        }

        private static TestSuiteDescriptor MetadataSuite(BlobProviderOptions options)
        {
            const string suiteId = "Metadata";

            return new TestSuiteDescriptor(
                suiteId: suiteId,
                displayName: "Metadata, Exists, and Delete Contracts",
                cases: new List<TestCaseDescriptor>
                {
                    ProviderCase(options, suiteId, "MetadataAfterWrite", "metadata length is correct after write", MetadataAfterWrite),
                    ProviderCase(options, suiteId, "MetadataAfterEmptyWrite", "metadata length is zero after empty write", MetadataAfterEmptyWrite),
                    ProviderCase(options, suiteId, "MetadataMissingThrows", "missing metadata throws", MetadataMissingThrows),
                    ProviderCase(options, suiteId, "ExistsMissingFalse", "ExistsAsync returns false for missing blob", ExistsMissingFalse),
                    ProviderCase(options, suiteId, "ExistsAfterWriteTrue", "ExistsAsync returns true after write", ExistsAfterWriteTrue),
                    ProviderCase(options, suiteId, "DeleteExistingRemoves", "DeleteAsync removes existing blob", DeleteExistingRemoves),
                    ProviderCase(options, suiteId, "DeleteMissingNoThrow", "DeleteAsync is idempotent for missing blob", DeleteMissingNoThrow),
                    ProviderCase(options, suiteId, "DeleteFolderMarker", "DeleteAsync removes folder marker", DeleteFolderMarker),
                    ProviderCase(options, suiteId, "NestedMetadataLength", "nested metadata length is correct", NestedMetadataLength)
                });
        }

        private static TestSuiteDescriptor EnumerationSuite(BlobProviderOptions options)
        {
            const string suiteId = "Enumeration";

            return new TestSuiteDescriptor(
                suiteId: suiteId,
                displayName: "Enumeration Contracts",
                cases: new List<TestCaseDescriptor>
                {
                    ProviderCase(options, suiteId, "EnumerateEmpty", "empty repository enumerates zero blobs", EnumerateEmpty),
                    ProviderCase(options, suiteId, "EnumerateAllCount", "enumeration returns all blobs", EnumerateAllCount),
                    ProviderCase(options, suiteId, "SyncAsyncParity", "sync and async enumeration produce same keys", SyncAsyncParity),
                    ProviderCase(options, suiteId, "PrefixExact", "exact prefix filters objects", PrefixExact),
                    ProviderCase(options, suiteId, "PrefixFolder", "folder prefix filters nested objects", PrefixFolder),
                    ProviderCase(options, suiteId, "PrefixPartial", "partial prefix filters objects", PrefixPartial),
                    ProviderCase(options, suiteId, "SuffixFilter", "suffix filters objects", SuffixFilter),
                    ProviderCase(options, suiteId, "MinimumSizeFilter", "minimum size filters objects", MinimumSizeFilter),
                    ProviderCase(options, suiteId, "MaximumSizeFilter", "maximum size filters objects", MaximumSizeFilter),
                    ProviderCase(options, suiteId, "SizeRangeFilter", "size range filters objects", SizeRangeFilter),
                    ProviderCase(options, suiteId, "FilterNotMutated", "enumeration does not mutate caller filter", FilterNotMutated),
                    ProviderCase(options, suiteId, "ProviderCaseSensitivity", "provider case sensitivity is respected", ProviderCaseSensitivity),
                    ProviderCase(options, suiteId, "EnumerationAfterDelete", "deleted blobs do not enumerate", EnumerationAfterDelete),
                    ProviderCase(options, suiteId, "EnumerationReturnsMetadataLengths", "enumeration metadata includes lengths", EnumerationReturnsMetadataLengths),
                    ProviderCase(options, suiteId, "EnumerationOfEmptyBlob", "empty blob enumerates with zero length", EnumerationOfEmptyBlob)
                });
        }

        private static TestSuiteDescriptor BulkSuite(BlobProviderOptions options)
        {
            const string suiteId = "Bulk";

            return new TestSuiteDescriptor(
                suiteId: suiteId,
                displayName: "Bulk Operation Contracts",
                cases: new List<TestCaseDescriptor>
                {
                    ProviderCase(options, suiteId, "WriteManyEmptyList", "WriteManyAsync accepts empty list", WriteManyEmptyList),
                    ProviderCase(options, suiteId, "WriteManyByteArrays", "WriteManyAsync writes byte-array requests", WriteManyByteArrays),
                    ProviderCase(options, suiteId, "WriteManyStreams", "WriteManyAsync writes stream requests", WriteManyStreams),
                    ProviderCase(options, suiteId, "WriteManyMixed", "WriteManyAsync writes mixed requests", WriteManyMixed),
                    ProviderCase(options, suiteId, "WriteManyEmptyObjects", "WriteManyAsync writes empty objects", WriteManyEmptyObjects),
                    ProviderCase(options, suiteId, "WriteManyConcurrencyOne", "WriteManyAsync works with MaxConcurrency one", WriteManyConcurrencyOne),
                    ProviderCase(options, suiteId, "EmptyAsyncEmpty", "EmptyAsync handles empty repository", EmptyAsyncEmpty),
                    ProviderCase(options, suiteId, "EmptyAsyncObjects", "EmptyAsync removes objects", EmptyAsyncObjects),
                    ProviderCase(options, suiteId, "EmptyAsyncNestedObjects", "EmptyAsync removes nested objects", EmptyAsyncNestedObjects),
                    ProviderCase(options, suiteId, "EmptyAsyncAfterFolderMarker", "EmptyAsync removes folder markers", EmptyAsyncAfterFolderMarker)
                });
        }

        private static TestSuiteDescriptor CopySuite(BlobProviderOptions options)
        {
            const string suiteId = "Copy";

            return new TestSuiteDescriptor(
                suiteId: suiteId,
                displayName: "Copy Contracts",
                cases: new List<TestCaseDescriptor>
                {
                    CopyCase(options, suiteId, "CopyAll", "BlobCopy copies all blobs", CopyAll),
                    CopyCase(options, suiteId, "CopyPrefixOnly", "BlobCopy copies only requested prefix", CopyPrefixOnly),
                    CopyCase(options, suiteId, "CopyStopAfter", "BlobCopy honors stopAfter", CopyStopAfter),
                    CopyCase(options, suiteId, "CopyEmptyBlob", "BlobCopy copies empty blob", CopyEmptyBlob),
                    CopyCase(options, suiteId, "CopyNestedBlob", "BlobCopy copies nested blob", CopyNestedBlob),
                    CopyCase(options, suiteId, "CopyStatsBytes", "BlobCopy reports byte statistics", CopyStatsBytes),
                    CopyCase(options, suiteId, "CopyMissingPrefixZero", "BlobCopy with missing prefix writes zero blobs", CopyMissingPrefixZero),
                    CopyCase(options, suiteId, "CopyOverwritesExisting", "BlobCopy overwrites existing target", CopyOverwritesExisting),
                    CopyCase(options, suiteId, "CopyPreservesCaseKey", "BlobCopy preserves key casing", CopyPreservesCaseKey),
                    CopyCase(options, suiteId, "CopyStartCompatibility", "BlobCopy.Start remains compatible", CopyStartCompatibility),
                    CopyCase(options, suiteId, "CopyFilterSuffix", "BlobCopy honors supplied suffix filter", CopyFilterSuffix)
                });
        }

        private static TestSuiteDescriptor StressSuite(BlobProviderOptions options)
        {
            const string suiteId = "Stress";

            return new TestSuiteDescriptor(
                suiteId: suiteId,
                displayName: "Stress and Pagination Contracts",
                cases: new List<TestCaseDescriptor>
                {
                    ProviderCase(options, suiteId, "LargeEnumerationSet", "large enumeration set returns all objects", LargeEnumerationSet),
                    ProviderCase(options, suiteId, "LargeWriteManySet", "large WriteManyAsync set returns all objects", LargeWriteManySet)
                });
        }

        private static TestCaseDescriptor ProviderCase(
            BlobProviderOptions options,
            string suiteId,
            string caseId,
            string displayName,
            Func<BlobClientBase, BlobProviderOptions, CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor(
                suiteId,
                caseId,
                displayName,
                async ct =>
                {
                    await WithClient(options, suiteId + "." + caseId, executeAsync, ct).ConfigureAwait(false);
                });
        }

        private static TestCaseDescriptor CopyCase(
            BlobProviderOptions options,
            string suiteId,
            string caseId,
            string displayName,
            Func<BlobClientBase, BlobClientBase, BlobProviderOptions, CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor(
                suiteId,
                caseId,
                displayName,
                async ct =>
                {
                    await WithTwoClients(options, suiteId + "." + caseId, executeAsync, ct).ConfigureAwait(false);
                });
        }

        private static TestCaseDescriptor LocalCase(
            string suiteId,
            string caseId,
            string displayName,
            Func<CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor(suiteId, caseId, displayName, executeAsync);
        }

        private static async Task WithClient(
            BlobProviderOptions options,
            string caseId,
            Func<BlobClientBase, BlobProviderOptions, CancellationToken, Task> executeAsync,
            CancellationToken token)
        {
            using (BlobProviderContext context = BlobProviderFactory.Create(options, caseId))
            {
                try
                {
                    await executeAsync(context.Client, options, token).ConfigureAwait(false);
                }
                finally
                {
                    if (context.CleanupAsync != null)
                        await context.CleanupAsync(token).ConfigureAwait(false);
                }
            }
        }

        private static async Task WithTwoClients(
            BlobProviderOptions options,
            string caseId,
            Func<BlobClientBase, BlobClientBase, BlobProviderOptions, CancellationToken, Task> executeAsync,
            CancellationToken token)
        {
            using (BlobProviderContext source = BlobProviderFactory.Create(options, caseId + ".source"))
            using (BlobProviderContext target = BlobProviderFactory.Create(options, caseId + ".target"))
            {
                try
                {
                    await executeAsync(source.Client, target.Client, options, token).ConfigureAwait(false);
                }
                finally
                {
                    if (source.CleanupAsync != null) await source.CleanupAsync(token).ConfigureAwait(false);
                    if (target.CleanupAsync != null) await target.CleanupAsync(token).ConfigureAwait(false);
                }
            }
        }

        #region Core

        private static async Task ValidateConnectivity(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            AssertTrue(await blobs.ValidateConnectivity(token).ConfigureAwait(false), "connectivity");
        }

        private static async Task GenerateUrl(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("url.txt", "text/plain", "url", token).ConfigureAwait(false);
            string url = blobs.GenerateUrl("url.txt", token);
            AssertFalse(String.IsNullOrEmpty(url), "url is empty");
        }

        private static Task NormalizeAwsRegion(CancellationToken token)
        {
            AssertEqual("us-east-2", Common.NormalizeAwsRegion("USEast2"), "USEast2");
            AssertEqual("us-west-1", Common.NormalizeAwsRegion("us_west_1"), "us_west_1");
            AssertEqual("eu-central-2", Common.NormalizeAwsRegion("EU Central 2"), "EU Central 2");
            return Task.CompletedTask;
        }

        private static Task HexRoundTrip(CancellationToken token)
        {
            byte[] expected = new byte[] { 0, 1, 2, 127, 128, 255 };
            byte[] actual = Common.BytesFromHexString(Common.BytesToHexString(expected));
            AssertBytes(expected, actual, "hex bytes");
            return Task.CompletedTask;
        }

        private static Task EnumerationFilterClone(CancellationToken token)
        {
            EnumerationFilter filter = new EnumerationFilter { MinimumSize = 1, MaximumSize = 9, Prefix = "Case/", Suffix = ".TXT" };
            EnumerationFilter clone = filter.Clone();
            AssertEqual(filter.MinimumSize, clone.MinimumSize, "minimum");
            AssertEqual(filter.MaximumSize, clone.MaximumSize, "maximum");
            AssertEqual(filter.Prefix, clone.Prefix, "prefix");
            AssertEqual(filter.Suffix, clone.Suffix, "suffix");
            return Task.CompletedTask;
        }

        private static Task EnumerationFilterRejectsNegativeMinimum(CancellationToken token)
        {
            AssertThrows<ArgumentOutOfRangeException>(() => new EnumerationFilter { MinimumSize = -1 }, "negative minimum");
            return Task.CompletedTask;
        }

        private static Task EnumerationFilterRejectsNegativeMaximum(CancellationToken token)
        {
            AssertThrows<ArgumentOutOfRangeException>(() => new EnumerationFilter { MaximumSize = -1 }, "negative maximum");
            return Task.CompletedTask;
        }

        private static Task BlobDataRejectsNegativeLength(CancellationToken token)
        {
            BlobData data = new BlobData();
            AssertThrows<ArgumentException>(() => data.ContentLength = -1, "negative content length");
            return Task.CompletedTask;
        }

        private static Task BlobDataDisposeDisposesStream(CancellationToken token)
        {
            TrackingStream stream = new TrackingStream(new byte[] { 1, 2, 3 });
            BlobData data = new BlobData(3, stream);
            data.Dispose();
            AssertTrue(stream.WasDisposed, "stream disposed");
            return Task.CompletedTask;
        }

        private static Task WriteRequestRejectsEmptyKey(CancellationToken token)
        {
            AssertThrows<ArgumentNullException>(() => new WriteRequest("", "text/plain", Array.Empty<byte>()), "empty key");
            return Task.CompletedTask;
        }

        private static Task WriteRequestDefaultsContentType(CancellationToken token)
        {
            WriteRequest request = new WriteRequest("key", "", Array.Empty<byte>());
            AssertEqual("application/octet-stream", request.ContentType, "content type");
            return Task.CompletedTask;
        }

        private static Task WriteRequestSeeksEofStream(CancellationToken token)
        {
            MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));
            stream.Seek(0, SeekOrigin.End);
            WriteRequest request = new WriteRequest("key", "text/plain", 3, stream);
            AssertEqual(0L, request.DataStream.Position, "stream position");
            return Task.CompletedTask;
        }

        private static Task MaxConcurrencyRejectsZero(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            AssertThrows<ArgumentOutOfRangeException>(() => blobs.MaxConcurrency = 0, "max concurrency");
            return Task.CompletedTask;
        }

        private static Task StreamBufferSizeRejectsZero(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            AssertThrows<ArgumentOutOfRangeException>(() => blobs.StreamBufferSize = 0, "stream buffer size");
            return Task.CompletedTask;
        }

        #endregion

        #region WriteRead

        private static async Task EmptyStringWrite(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("empty-string.txt", "text/plain", "", token).ConfigureAwait(false);
            AssertEqual(0, (await blobs.GetAsync("empty-string.txt", token).ConfigureAwait(false)).Length, "empty string length");
        }

        private static async Task EmptyBytesWrite(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("empty-bytes.bin", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            AssertEqual(0, (await blobs.GetAsync("empty-bytes.bin", token).ConfigureAwait(false)).Length, "empty bytes length");
        }

        private static async Task NullStringWrite(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("null-string.txt", "text/plain", (string)null, token).ConfigureAwait(false);
            AssertEqual(0, (await blobs.GetAsync("null-string.txt", token).ConfigureAwait(false)).Length, "null string length");
        }

        private static async Task NullBytesWrite(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("null-bytes.bin", "application/octet-stream", (byte[])null, token).ConfigureAwait(false);
            AssertEqual(0, (await blobs.GetAsync("null-bytes.bin", token).ConfigureAwait(false)).Length, "null bytes length");
        }

        private static async Task AsciiStringRoundTrip(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("string.txt", "text/plain", "hello world", token).ConfigureAwait(false);
            AssertEqual("hello world", Encoding.UTF8.GetString(await blobs.GetAsync("string.txt", token).ConfigureAwait(false)), "string payload");
        }

        private static async Task BinaryRoundTrip(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            byte[] expected = PatternBytes(4096);
            await blobs.WriteAsync("binary.bin", "application/octet-stream", expected, token).ConfigureAwait(false);
            AssertBytes(expected, await blobs.GetAsync("binary.bin", token).ConfigureAwait(false), "binary payload");
        }

        private static async Task LargeBinaryRoundTrip(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            byte[] expected = PatternBytes(256 * 1024);
            await blobs.WriteAsync("large.bin", "application/octet-stream", expected, token).ConfigureAwait(false);
            AssertBytes(expected, await blobs.GetAsync("large.bin", token).ConfigureAwait(false), "large payload");
        }

        private static async Task NestedKeyRoundTrip(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("folder/file.txt", "text/plain", "nested", token).ConfigureAwait(false);
            AssertEqual("nested", Encoding.UTF8.GetString(await blobs.GetAsync("folder/file.txt", token).ConfigureAwait(false)), "nested payload");
        }

        private static async Task DeepNestedKeyRoundTrip(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("a/b/c/d/e.txt", "text/plain", "deep", token).ConfigureAwait(false);
            AssertTrue(await blobs.ExistsAsync("a/b/c/d/e.txt", token).ConfigureAwait(false), "deep exists");
        }

        private static async Task KeyWithSpacesRoundTrip(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("folder/file with spaces.txt", "text/plain", "spaces", token).ConfigureAwait(false);
            AssertEqual("spaces", Encoding.UTF8.GetString(await blobs.GetAsync("folder/file with spaces.txt", token).ConfigureAwait(false)), "spaces payload");
        }

        private static async Task StreamRoundTrip(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            byte[] expected = Encoding.UTF8.GetBytes("stream-payload");
            using (MemoryStream input = new MemoryStream(expected))
            {
                await blobs.WriteAsync("streams/data.txt", "text/plain", expected.Length, input, token).ConfigureAwait(false);
            }

            using (BlobData data = await blobs.GetStreamAsync("streams/data.txt", token).ConfigureAwait(false))
            {
                AssertEqual(expected.Length, data.ContentLength, "stream content length");
                AssertBytes(expected, await ReadAllAsync(data.Data, token).ConfigureAwait(false), "stream payload");
            }
        }

        private static async Task EmptyStreamWrite(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            using (MemoryStream stream = new MemoryStream(Array.Empty<byte>()))
            {
                await blobs.WriteAsync("empty-stream.bin", "application/octet-stream", 0, stream, token).ConfigureAwait(false);
            }

            AssertEqual(0, (await blobs.GetAsync("empty-stream.bin", token).ConfigureAwait(false)).Length, "empty stream");
        }

        private static async Task NullStreamZeroLengthWrite(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("null-stream.bin", "application/octet-stream", 0, null, token).ConfigureAwait(false);
            AssertEqual(0, (await blobs.GetAsync("null-stream.bin", token).ConfigureAwait(false)).Length, "null stream");
        }

        private static async Task NonSeekableStreamWrite(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            byte[] expected = Encoding.UTF8.GetBytes("nonseekable");
            using (NonSeekableReadStream stream = new NonSeekableReadStream(expected))
            {
                await blobs.WriteAsync("nonseekable.txt", "text/plain", expected.Length, stream, token).ConfigureAwait(false);
            }

            AssertBytes(expected, await blobs.GetAsync("nonseekable.txt", token).ConfigureAwait(false), "nonseekable payload");
        }

        private static async Task ShortContentLengthWritesPrefix(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("abcdef")))
            {
                await blobs.WriteAsync("short-length.txt", "text/plain", 3, stream, token).ConfigureAwait(false);
            }

            AssertEqual("abc", Encoding.UTF8.GetString(await blobs.GetAsync("short-length.txt", token).ConfigureAwait(false)), "short content length");
        }

        private static async Task ZeroContentLengthIgnoresStream(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("abcdef")))
            {
                await blobs.WriteAsync("zero-length.txt", "text/plain", 0, stream, token).ConfigureAwait(false);
            }

            AssertEqual(0, (await blobs.GetAsync("zero-length.txt", token).ConfigureAwait(false)).Length, "zero content length");
        }

        private static async Task GetStreamEmptyBlob(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("empty-stream-read.bin", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            using (BlobData data = await blobs.GetStreamAsync("empty-stream-read.bin", token).ConfigureAwait(false))
            {
                AssertEqual(0L, data.ContentLength, "empty stream length");
                AssertEqual(0, (await ReadAllAsync(data.Data, token).ConfigureAwait(false)).Length, "empty stream data");
            }
        }

        private static async Task OverwriteShorterTruncates(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("overwrite.txt", "text/plain", "abcdef", token).ConfigureAwait(false);
            await blobs.WriteAsync("overwrite.txt", "text/plain", "xy", token).ConfigureAwait(false);
            AssertEqual("xy", Encoding.UTF8.GetString(await blobs.GetAsync("overwrite.txt", token).ConfigureAwait(false)), "short overwrite");
        }

        private static async Task OverwriteLongerReplaces(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("overwrite-long.txt", "text/plain", "xy", token).ConfigureAwait(false);
            await blobs.WriteAsync("overwrite-long.txt", "text/plain", "abcdef", token).ConfigureAwait(false);
            AssertEqual("abcdef", Encoding.UTF8.GetString(await blobs.GetAsync("overwrite-long.txt", token).ConfigureAwait(false)), "long overwrite");
        }

        private static async Task OverwriteWithEmpty(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("overwrite-empty.txt", "text/plain", "abcdef", token).ConfigureAwait(false);
            await blobs.WriteAsync("overwrite-empty.txt", "text/plain", Array.Empty<byte>(), token).ConfigureAwait(false);
            AssertEqual(0, (await blobs.GetAsync("overwrite-empty.txt", token).ConfigureAwait(false)).Length, "empty overwrite");
        }

        private static async Task FolderMarkerWrite(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("folder-marker/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            AssertTrue(await blobs.ExistsAsync("folder-marker/", token).ConfigureAwait(false), "folder marker exists");
        }

        #endregion

        #region Metadata

        private static async Task MetadataAfterWrite(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("metadata.txt", "text/plain", "metadata", token).ConfigureAwait(false);
            BlobMetadata metadata = await blobs.GetMetadataAsync("metadata.txt", token).ConfigureAwait(false);
            AssertEqual(8L, metadata.ContentLength, "metadata length");
            AssertEqual("metadata.txt", metadata.Key, "metadata key");
        }

        private static async Task MetadataAfterEmptyWrite(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("metadata-empty.txt", "text/plain", "", token).ConfigureAwait(false);
            BlobMetadata metadata = await blobs.GetMetadataAsync("metadata-empty.txt", token).ConfigureAwait(false);
            AssertEqual(0L, metadata.ContentLength, "empty metadata length");
        }

        private static async Task MetadataMissingThrows(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await AssertThrowsAsync<Exception>(() => blobs.GetMetadataAsync("missing.txt", token), "missing metadata").ConfigureAwait(false);
        }

        private static async Task ExistsMissingFalse(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            AssertFalse(await blobs.ExistsAsync("missing.txt", token).ConfigureAwait(false), "missing exists");
        }

        private static async Task ExistsAfterWriteTrue(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("exists.txt", "text/plain", "exists", token).ConfigureAwait(false);
            AssertTrue(await blobs.ExistsAsync("exists.txt", token).ConfigureAwait(false), "exists after write");
        }

        private static async Task DeleteExistingRemoves(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("delete.txt", "text/plain", "delete", token).ConfigureAwait(false);
            await blobs.DeleteAsync("delete.txt", token).ConfigureAwait(false);
            AssertFalse(await blobs.ExistsAsync("delete.txt", token).ConfigureAwait(false), "exists after delete");
        }

        private static async Task DeleteMissingNoThrow(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.DeleteAsync("missing-delete.txt", token).ConfigureAwait(false);
        }

        private static async Task DeleteFolderMarker(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("folder-delete/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            await blobs.DeleteAsync("folder-delete/", token).ConfigureAwait(false);
            AssertFalse(await blobs.ExistsAsync("folder-delete/", token).ConfigureAwait(false), "folder marker deleted");
        }

        private static async Task NestedMetadataLength(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("nested/metadata.bin", "application/octet-stream", new byte[] { 1, 2, 3, 4 }, token).ConfigureAwait(false);
            BlobMetadata metadata = await blobs.GetMetadataAsync("nested/metadata.bin", token).ConfigureAwait(false);
            AssertEqual(4L, metadata.ContentLength, "nested metadata length");
        }

        #endregion

        #region Enumeration

        private static async Task EnumerateEmpty(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            AssertEqual(0, (await EnumerateAsync(blobs, null, token).ConfigureAwait(false)).Count, "empty enumeration");
        }

        private static async Task EnumerateAllCount(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await SeedEnumeration(blobs, token).ConfigureAwait(false);
            AssertEqual(5, (await EnumerateAsync(blobs, null, token).ConfigureAwait(false)).Count, "all count");
        }

        private static async Task SyncAsyncParity(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await SeedEnumeration(blobs, token).ConfigureAwait(false);
            List<string> sync = blobs.Enumerate().Select(b => b.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
            List<string> asyncKeys = (await EnumerateAsync(blobs, null, token).ConfigureAwait(false)).Select(b => b.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
            AssertSequence(sync, asyncKeys, "sync async keys");
        }

        private static async Task PrefixExact(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await SeedEnumeration(blobs, token).ConfigureAwait(false);
            AssertKeys(await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "alpha/file-a" }, token).ConfigureAwait(false), "alpha/file-a.txt");
        }

        private static async Task PrefixFolder(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await SeedEnumeration(blobs, token).ConfigureAwait(false);
            AssertKeys(await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "alpha/" }, token).ConfigureAwait(false), "alpha/file-a.txt", "alpha/file-b.log");
        }

        private static async Task PrefixPartial(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await SeedEnumeration(blobs, token).ConfigureAwait(false);
            AssertKeys(await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "alp" }, token).ConfigureAwait(false), "alpha/file-a.txt", "alpha/file-b.log");
        }

        private static async Task SuffixFilter(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await SeedEnumeration(blobs, token).ConfigureAwait(false);
            List<string> expected = new List<string> { "alpha/file-a.txt", "beta/file-c.txt" };
            if (options.IsCaseInsensitiveProvider) expected.Add("gamma/case.TXT");
            AssertKeys(await EnumerateAsync(blobs, new EnumerationFilter { Suffix = ".txt" }, token).ConfigureAwait(false), expected.ToArray());
        }

        private static async Task MinimumSizeFilter(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await SeedEnumeration(blobs, token).ConfigureAwait(false);
            AssertKeys(await EnumerateAsync(blobs, new EnumerationFilter { MinimumSize = 4 }, token).ConfigureAwait(false), "alpha/file-b.log", "beta/file-c.txt");
        }

        private static async Task MaximumSizeFilter(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await SeedEnumeration(blobs, token).ConfigureAwait(false);
            AssertKeys(await EnumerateAsync(blobs, new EnumerationFilter { MaximumSize = 1 }, token).ConfigureAwait(false), "beta/empty.bin", "gamma/case.TXT");
        }

        private static async Task SizeRangeFilter(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await SeedEnumeration(blobs, token).ConfigureAwait(false);
            AssertKeys(await EnumerateAsync(blobs, new EnumerationFilter { MinimumSize = 2, MaximumSize = 3 }, token).ConfigureAwait(false), "alpha/file-a.txt");
        }

        private static async Task FilterNotMutated(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await SeedEnumeration(blobs, token).ConfigureAwait(false);
            EnumerationFilter filter = new EnumerationFilter { Prefix = "alpha/", Suffix = ".txt", MinimumSize = 1, MaximumSize = 3 };
            await EnumerateAsync(blobs, filter, token).ConfigureAwait(false);
            AssertEqual("alpha/", filter.Prefix, "prefix not mutated");
            AssertEqual(".txt", filter.Suffix, "suffix not mutated");
            AssertEqual(1L, filter.MinimumSize, "minimum not mutated");
            AssertEqual(3L, filter.MaximumSize, "maximum not mutated");
        }

        private static async Task ProviderCaseSensitivity(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("Case/Alpha.txt", "text/plain", "a", token).ConfigureAwait(false);
            await blobs.WriteAsync("Case/beta.TXT", "text/plain", "b", token).ConfigureAwait(false);

            List<BlobMetadata> matches = await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "case/", Suffix = ".txt" }, token).ConfigureAwait(false);
            int expected = options.IsCaseInsensitiveProvider ? 2 : 0;
            AssertEqual(expected, matches.Count, "case sensitivity count");
        }

        private static async Task EnumerationAfterDelete(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("delete-enum/a.txt", "text/plain", "a", token).ConfigureAwait(false);
            await blobs.WriteAsync("delete-enum/b.txt", "text/plain", "b", token).ConfigureAwait(false);
            await blobs.DeleteAsync("delete-enum/a.txt", token).ConfigureAwait(false);
            AssertKeys(await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "delete-enum/" }, token).ConfigureAwait(false), "delete-enum/b.txt");
        }

        private static async Task EnumerationReturnsMetadataLengths(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("lengths/a.txt", "text/plain", "abc", token).ConfigureAwait(false);
            BlobMetadata metadata = (await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "lengths/" }, token).ConfigureAwait(false)).First();
            AssertEqual(3L, metadata.ContentLength, "enumerated length");
        }

        private static async Task EnumerationOfEmptyBlob(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("enum-empty.bin", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            BlobMetadata metadata = (await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "enum-empty" }, token).ConfigureAwait(false)).First();
            AssertEqual(0L, metadata.ContentLength, "empty enumerated length");
        }

        #endregion

        #region Bulk

        private static async Task WriteManyEmptyList(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteManyAsync(new List<WriteRequest>(), token).ConfigureAwait(false);
            AssertEqual(0, (await EnumerateAsync(blobs, null, token).ConfigureAwait(false)).Count, "empty write many");
        }

        private static async Task WriteManyByteArrays(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            List<WriteRequest> requests = Enumerable.Range(0, 10)
                .Select(i => new WriteRequest("bytes/" + i.ToString("D2") + ".txt", "text/plain", Encoding.UTF8.GetBytes(i.ToString())))
                .ToList();

            await blobs.WriteManyAsync(requests, token).ConfigureAwait(false);
            AssertEqual(10, (await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "bytes/" }, token).ConfigureAwait(false)).Count, "byte write many count");
        }

        private static async Task WriteManyStreams(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            List<MemoryStream> streams = new List<MemoryStream>();

            try
            {
                List<WriteRequest> requests = new List<WriteRequest>();
                for (int i = 0; i < 8; i++)
                {
                    byte[] data = Encoding.UTF8.GetBytes("stream-" + i);
                    MemoryStream stream = new MemoryStream(data);
                    streams.Add(stream);
                    requests.Add(new WriteRequest("streams/" + i.ToString("D2") + ".txt", "text/plain", data.Length, stream));
                }

                await blobs.WriteManyAsync(requests, token).ConfigureAwait(false);
                AssertEqual(8, (await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "streams/" }, token).ConfigureAwait(false)).Count, "stream write many count");
            }
            finally
            {
                foreach (MemoryStream stream in streams) stream.Dispose();
            }
        }

        private static async Task WriteManyMixed(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("stream"));
            try
            {
                List<WriteRequest> requests = new List<WriteRequest>
                {
                    new WriteRequest("mixed/bytes.txt", "text/plain", Encoding.UTF8.GetBytes("bytes")),
                    new WriteRequest("mixed/stream.txt", "text/plain", 6, stream)
                };

                await blobs.WriteManyAsync(requests, token).ConfigureAwait(false);
                AssertEqual(2, (await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "mixed/" }, token).ConfigureAwait(false)).Count, "mixed write many count");
            }
            finally
            {
                stream.Dispose();
            }
        }

        private static async Task WriteManyEmptyObjects(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            List<WriteRequest> requests = new List<WriteRequest>
            {
                new WriteRequest("empty/bytes.bin", "application/octet-stream", Array.Empty<byte>()),
                new WriteRequest("empty/string.txt", "text/plain", Encoding.UTF8.GetBytes(""))
            };

            await blobs.WriteManyAsync(requests, token).ConfigureAwait(false);
            foreach (BlobMetadata metadata in await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "empty/" }, token).ConfigureAwait(false))
            {
                AssertEqual(0L, metadata.ContentLength, "empty write many length");
            }
        }

        private static async Task WriteManyConcurrencyOne(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            blobs.MaxConcurrency = 1;
            await WriteManyByteArrays(blobs, options, token).ConfigureAwait(false);
        }

        private static async Task EmptyAsyncEmpty(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            EmptyResult result = await blobs.EmptyAsync(token).ConfigureAwait(false);
            AssertEqual(0L, result.Count, "empty count");
        }

        private static async Task EmptyAsyncObjects(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("empty/a.txt", "text/plain", "a", token).ConfigureAwait(false);
            await blobs.WriteAsync("empty/b.txt", "text/plain", "b", token).ConfigureAwait(false);
            EmptyResult result = await blobs.EmptyAsync(token).ConfigureAwait(false);
            AssertEqual(2L, result.Count, "empty objects count");
            AssertEqual(0, (await EnumerateAsync(blobs, null, token).ConfigureAwait(false)).Count, "post empty count");
        }

        private static async Task EmptyAsyncNestedObjects(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("a/b/c.txt", "text/plain", "c", token).ConfigureAwait(false);
            await blobs.WriteAsync("a/d/e.txt", "text/plain", "e", token).ConfigureAwait(false);
            await blobs.EmptyAsync(token).ConfigureAwait(false);
            AssertEqual(0, (await EnumerateAsync(blobs, null, token).ConfigureAwait(false)).Count, "nested post empty count");
        }

        private static async Task EmptyAsyncAfterFolderMarker(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            await blobs.WriteAsync("folder/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            await blobs.EmptyAsync(token).ConfigureAwait(false);
            AssertFalse(await blobs.ExistsAsync("folder/", token).ConfigureAwait(false), "folder marker removed");
        }

        #endregion

        #region Copy

        private static async Task CopyAll(BlobClientBase source, BlobClientBase target, BlobProviderOptions options, CancellationToken token)
        {
            await source.WriteAsync("a.txt", "text/plain", "a", token).ConfigureAwait(false);
            await source.WriteAsync("b.txt", "text/plain", "b", token).ConfigureAwait(false);
            CopyStatistics stats = await new BlobCopy(source, target).StartAsync(token: token).ConfigureAwait(false);
            AssertTrue(stats.Success, "copy success");
            AssertEqual(2L, stats.BlobsWritten, "copy all count");
            AssertTrue(await target.ExistsAsync("a.txt", token).ConfigureAwait(false), "a copied");
            AssertTrue(await target.ExistsAsync("b.txt", token).ConfigureAwait(false), "b copied");
        }

        private static async Task CopyPrefixOnly(BlobClientBase source, BlobClientBase target, BlobProviderOptions options, CancellationToken token)
        {
            await source.WriteAsync("copy/included.txt", "text/plain", "included", token).ConfigureAwait(false);
            await source.WriteAsync("other/excluded.txt", "text/plain", "excluded", token).ConfigureAwait(false);
            CopyStatistics stats = await new BlobCopy(source, target, "copy/").StartAsync(token: token).ConfigureAwait(false);
            AssertEqual(1L, stats.BlobsWritten, "prefix copy count");
            AssertTrue(await target.ExistsAsync("copy/included.txt", token).ConfigureAwait(false), "included copied");
            AssertFalse(await target.ExistsAsync("other/excluded.txt", token).ConfigureAwait(false), "excluded not copied");
        }

        private static async Task CopyStopAfter(BlobClientBase source, BlobClientBase target, BlobProviderOptions options, CancellationToken token)
        {
            await source.WriteAsync("one.txt", "text/plain", "1", token).ConfigureAwait(false);
            await source.WriteAsync("two.txt", "text/plain", "2", token).ConfigureAwait(false);
            CopyStatistics stats = await new BlobCopy(source, target).StartAsync(stopAfter: 1, token: token).ConfigureAwait(false);
            AssertEqual(1L, stats.BlobsWritten, "stop after count");
        }

        private static async Task CopyEmptyBlob(BlobClientBase source, BlobClientBase target, BlobProviderOptions options, CancellationToken token)
        {
            await source.WriteAsync("empty.bin", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            CopyStatistics stats = await new BlobCopy(source, target).StartAsync(token: token).ConfigureAwait(false);
            AssertEqual(1L, stats.BlobsWritten, "empty copy count");
            AssertEqual(0, (await target.GetAsync("empty.bin", token).ConfigureAwait(false)).Length, "empty copied length");
        }

        private static async Task CopyNestedBlob(BlobClientBase source, BlobClientBase target, BlobProviderOptions options, CancellationToken token)
        {
            await source.WriteAsync("nested/path/blob.txt", "text/plain", "nested", token).ConfigureAwait(false);
            await new BlobCopy(source, target).StartAsync(token: token).ConfigureAwait(false);
            AssertEqual("nested", Encoding.UTF8.GetString(await target.GetAsync("nested/path/blob.txt", token).ConfigureAwait(false)), "nested copied");
        }

        private static async Task CopyStatsBytes(BlobClientBase source, BlobClientBase target, BlobProviderOptions options, CancellationToken token)
        {
            await source.WriteAsync("bytes.txt", "text/plain", "abcd", token).ConfigureAwait(false);
            CopyStatistics stats = await new BlobCopy(source, target).StartAsync(token: token).ConfigureAwait(false);
            AssertEqual(4L, stats.BytesRead, "bytes read");
            AssertEqual(4L, stats.BytesWritten, "bytes written");
        }

        private static async Task CopyMissingPrefixZero(BlobClientBase source, BlobClientBase target, BlobProviderOptions options, CancellationToken token)
        {
            await source.WriteAsync("other.txt", "text/plain", "other", token).ConfigureAwait(false);
            CopyStatistics stats = await new BlobCopy(source, target, "missing/").StartAsync(token: token).ConfigureAwait(false);
            AssertTrue(stats.Success, "missing prefix success");
            AssertEqual(0L, stats.BlobsWritten, "missing prefix writes");
        }

        private static async Task CopyOverwritesExisting(BlobClientBase source, BlobClientBase target, BlobProviderOptions options, CancellationToken token)
        {
            await source.WriteAsync("same.txt", "text/plain", "source", token).ConfigureAwait(false);
            await target.WriteAsync("same.txt", "text/plain", "target", token).ConfigureAwait(false);
            await new BlobCopy(source, target).StartAsync(token: token).ConfigureAwait(false);
            AssertEqual("source", Encoding.UTF8.GetString(await target.GetAsync("same.txt", token).ConfigureAwait(false)), "overwritten target");
        }

        private static async Task CopyPreservesCaseKey(BlobClientBase source, BlobClientBase target, BlobProviderOptions options, CancellationToken token)
        {
            await source.WriteAsync("Case/Name.TXT", "text/plain", "case", token).ConfigureAwait(false);
            await new BlobCopy(source, target).StartAsync(token: token).ConfigureAwait(false);
            AssertTrue(await target.ExistsAsync("Case/Name.TXT", token).ConfigureAwait(false), "case key exists");
        }

        private static async Task CopyStartCompatibility(BlobClientBase source, BlobClientBase target, BlobProviderOptions options, CancellationToken token)
        {
            await source.WriteAsync("compat.txt", "text/plain", "compat", token).ConfigureAwait(false);
            CopyStatistics stats = await new BlobCopy(source, target).Start(token: token).ConfigureAwait(false);
            AssertEqual(1L, stats.BlobsWritten, "compat copy count");
        }

        private static async Task CopyFilterSuffix(BlobClientBase source, BlobClientBase target, BlobProviderOptions options, CancellationToken token)
        {
            await source.WriteAsync("a.txt", "text/plain", "a", token).ConfigureAwait(false);
            await source.WriteAsync("b.log", "text/plain", "b", token).ConfigureAwait(false);
            CopyStatistics stats = await new BlobCopy(source, target).StartAsync(filter: new EnumerationFilter { Suffix = ".txt" }, token: token).ConfigureAwait(false);
            AssertEqual(1L, stats.BlobsWritten, "suffix copy count");
            AssertTrue(await target.ExistsAsync("a.txt", token).ConfigureAwait(false), "txt copied");
            AssertFalse(await target.ExistsAsync("b.log", token).ConfigureAwait(false), "log not copied");
        }

        #endregion

        #region Stress

        private static async Task LargeEnumerationSet(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            List<WriteRequest> requests = new List<WriteRequest>();
            for (int i = 0; i < 1005; i++)
            {
                requests.Add(new WriteRequest("large-enum/" + i.ToString("D4") + ".txt", "text/plain", Encoding.UTF8.GetBytes(i.ToString())));
            }

            await blobs.WriteManyAsync(requests, token).ConfigureAwait(false);
            AssertEqual(1005, (await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "large-enum/" }, token).ConfigureAwait(false)).Count, "large enum count");
        }

        private static async Task LargeWriteManySet(BlobClientBase blobs, BlobProviderOptions options, CancellationToken token)
        {
            List<WriteRequest> requests = new List<WriteRequest>();
            for (int i = 0; i < 500; i++)
            {
                requests.Add(new WriteRequest("large-write/" + i.ToString("D4") + ".txt", "text/plain", Encoding.UTF8.GetBytes("x")));
            }

            await blobs.WriteManyAsync(requests, token).ConfigureAwait(false);
            AssertEqual(500, (await EnumerateAsync(blobs, new EnumerationFilter { Prefix = "large-write/" }, token).ConfigureAwait(false)).Count, "large write count");
        }

        #endregion

        private static async Task SeedEnumeration(BlobClientBase blobs, CancellationToken token)
        {
            await blobs.WriteAsync("alpha/file-a.txt", "text/plain", "aaa", token).ConfigureAwait(false);
            await blobs.WriteAsync("alpha/file-b.log", "text/plain", "bbbb", token).ConfigureAwait(false);
            await blobs.WriteAsync("beta/file-c.txt", "text/plain", "ccccc", token).ConfigureAwait(false);
            await blobs.WriteAsync("beta/empty.bin", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            await blobs.WriteAsync("gamma/case.TXT", "text/plain", "g", token).ConfigureAwait(false);
        }

        private static async Task<List<BlobMetadata>> EnumerateAsync(BlobClientBase blobs, EnumerationFilter filter, CancellationToken token)
        {
            List<BlobMetadata> ret = new List<BlobMetadata>();
            await foreach (BlobMetadata metadata in blobs.EnumerateAsync(filter, token).ConfigureAwait(false))
            {
                ret.Add(metadata);
            }

            return ret;
        }

        private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken token)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms, 81920, token).ConfigureAwait(false);
                return ms.ToArray();
            }
        }

        private static byte[] PatternBytes(int count)
        {
            byte[] data = new byte[count];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 251);
            return data;
        }

        private static void AssertKeys(List<BlobMetadata> metadata, params string[] expected)
        {
            List<string> actual = metadata.Select(m => m.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
            List<string> sortedExpected = expected.OrderBy(k => k, StringComparer.Ordinal).ToList();
            AssertSequence(sortedExpected, actual, "keys");
        }

        private static void AssertSequence(List<string> expected, List<string> actual, string name)
        {
            if (expected.Count != actual.Count)
            {
                throw new InvalidOperationException("Expected " + name + " count " + expected.Count + " but found " + actual.Count + ". Actual: " + String.Join(", ", actual));
            }

            for (int i = 0; i < expected.Count; i++)
            {
                if (!String.Equals(expected[i], actual[i], StringComparison.Ordinal))
                    throw new InvalidOperationException("Expected " + name + "[" + i + "] to be '" + expected[i] + "' but found '" + actual[i] + "'.");
            }
        }

        private static void AssertTrue(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Expected true for " + name + ".");
        }

        private static void AssertFalse(bool condition, string name)
        {
            if (condition) throw new InvalidOperationException("Expected false for " + name + ".");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("Expected " + name + " to be '" + expected + "' but found '" + actual + "'.");
        }

        private static void AssertBytes(byte[] expected, byte[] actual, string name)
        {
            if (expected == null && actual == null) return;
            if (expected == null || actual == null)
                throw new InvalidOperationException("Expected " + name + " byte arrays to both be non-null.");
            if (expected.Length != actual.Length)
                throw new InvalidOperationException("Expected " + name + " length " + expected.Length + " but found " + actual.Length + ".");

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                    throw new InvalidOperationException("Expected " + name + " byte " + i + " to be " + expected[i] + " but found " + actual[i] + ".");
            }
        }

        private static void AssertThrows<T>(Action action, string name) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Expected " + name + " to throw " + typeof(T).Name + " but found " + ex.GetType().Name + ".");
            }

            throw new InvalidOperationException("Expected " + name + " to throw " + typeof(T).Name + ".");
        }

        private static async Task AssertThrowsAsync<T>(Func<Task> action, string name) where T : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (T)
            {
                return;
            }
            catch (Exception ex)
            {
                if (typeof(T) == typeof(Exception)) return;
                throw new InvalidOperationException("Expected " + name + " to throw " + typeof(T).Name + " but found " + ex.GetType().Name + ".");
            }

            throw new InvalidOperationException("Expected " + name + " to throw " + typeof(T).Name + ".");
        }

        private sealed class NonSeekableReadStream : Stream
        {
            private readonly MemoryStream _Inner;

            public NonSeekableReadStream(byte[] data)
            {
                _Inner = new MemoryStream(data ?? Array.Empty<byte>());
            }

            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position { get { return _Inner.Position; } set { throw new NotSupportedException(); } }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) { return _Inner.Read(buffer, offset, count); }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            protected override void Dispose(bool disposing)
            {
                if (disposing) _Inner.Dispose();
                base.Dispose(disposing);
            }
        }

        private sealed class TrackingStream : MemoryStream
        {
            public bool WasDisposed { get; private set; }

            public TrackingStream(byte[] data) : base(data)
            {
            }

            protected override void Dispose(bool disposing)
            {
                WasDisposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
