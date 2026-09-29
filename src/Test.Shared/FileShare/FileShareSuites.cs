namespace Test.Shared.FileShare
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.CIFS;
    using Blobject.Core;
    using Blobject.NFS;
    using Touchstone.Core;

    /// <summary>
    /// Protocol semantics, lifecycle, concurrency, and interoperability suites for the CIFS/SMB and NFS providers,
    /// run against managed ephemeral servers (in-process OpenCIFS/OpenNFS servers and Dockerized Samba, nfs-ganesha,
    /// knfsd, and unfs3).
    /// </summary>
    public static class FileShareSuites
    {
        #region Public-Methods

        /// <summary>
        /// Build every suite for the targets named in the options, including the full provider contract suites for each target.
        /// </summary>
        /// <param name="options">Options; <see cref="BlobProviderOptions.FileShareTargets"/> selects targets and defaults to all.</param>
        /// <returns>Suites.</returns>
        public static IReadOnlyList<TestSuiteDescriptor> BuildAll(BlobProviderOptions options)
        {
            if (options == null) options = BlobProviderOptions.FromEnvironment();
            List<string> targets = FileShareServers.ParseTargets(String.IsNullOrEmpty(options.FileShareTargets) ? "all" : options.FileShareTargets);

            List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>();
            suites.AddRange(FileShareApiSuites.Build());

            foreach (string target in targets)
            {
                suites.AddRange(BuildForTarget(options, target, includeContract: true));
            }

            return suites;
        }

        /// <summary>
        /// Build the suites for one managed target.
        /// </summary>
        /// <param name="options">Base options.</param>
        /// <param name="target">Target name.</param>
        /// <param name="includeContract">Include the shared provider contract suites.</param>
        /// <returns>Suites.</returns>
        public static IReadOnlyList<TestSuiteDescriptor> BuildForTarget(BlobProviderOptions options, string target, bool includeContract)
        {
            BlobProviderOptions targetOptions = options.Clone();
            targetOptions.Provider = target;
            targetOptions.FileShareTargets = null;

            string skipReason = null;
            if (FileShareServers.RequiresDocker(target) && !DockerCli.IsAvailable())
                skipReason = "Docker with Linux containers is not available; target '" + target + "' requires it.";

            List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>();

            if (includeContract)
            {
                foreach (TestSuiteDescriptor contract in BlobContractSuites.BuildSuites(targetOptions))
                {
                    suites.Add(Rename(contract, target + "/" + contract.SuiteId, target + ": " + contract.DisplayName, skipReason));
                }
            }

            string protocol = FileShareServers.ProtocolOf(target);

            suites.Add(Suite(targetOptions, target, "Semantics", "Protocol semantics", skipReason, SemanticsCases(protocol)));
            suites.Add(Suite(targetOptions, target, "Enumeration", "Enumeration semantics", skipReason, EnumerationCases()));
            suites.Add(Suite(targetOptions, target, "Concurrency", "Concurrency", skipReason, ConcurrencyCases()));
            suites.Add(Suite(targetOptions, target, "Lifecycle", "Connection lifecycle", skipReason, LifecycleCases(protocol)));
            suites.Add(Suite(targetOptions, target, "Interop", "Server-side interoperability", skipReason, InteropCases(target)));

            if (protocol == "cifs") suites.Add(Suite(targetOptions, target, "Smb", "SMB-specific behavior", skipReason, SmbCases()));
            else suites.Add(Suite(targetOptions, target, "Nfs", "NFS-specific behavior", skipReason, NfsCases(target)));

            return suites;
        }

        #endregion

        #region Case-Model

        private sealed class FileShareCase
        {
            public string CaseId;
            public string DisplayName;
            public Func<FileShareCaseContext, CancellationToken, Task> ExecuteAsync;

            public FileShareCase(string caseId, string displayName, Func<FileShareCaseContext, CancellationToken, Task> executeAsync)
            {
                CaseId = caseId;
                DisplayName = displayName;
                ExecuteAsync = executeAsync;
            }

            public FileShareCase(string caseId, string displayName, Func<FileShareCaseContext, Task> executeAsync)
                : this(caseId, displayName, (ctx, token) => executeAsync(ctx))
            {
            }
        }

        private static TestSuiteDescriptor Suite(
            BlobProviderOptions targetOptions,
            string target,
            string suiteName,
            string displayName,
            string skipReason,
            List<FileShareCase> fileShareCases)
        {
            string suiteId = target + "/" + suiteName;
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            foreach (FileShareCase c in fileShareCases)
            {
                FileShareCase captured = c;
                cases.Add(new TestCaseDescriptor(
                    suiteId,
                    captured.CaseId,
                    captured.DisplayName,
                    async ct =>
                    {
                        using (FileShareCaseContext context = await FileShareCaseContext.CreateAsync(targetOptions, suiteName + "." + captured.CaseId, ct).ConfigureAwait(false))
                        {
                            try
                            {
                                await captured.ExecuteAsync(context, ct).ConfigureAwait(false);
                            }
                            finally
                            {
                                await context.CleanupAsync(ct).ConfigureAwait(false);
                            }
                        }
                    },
                    new List<string> { "fileshare", target, FileShareServers.ProtocolOf(target) },
                    skipReason != null,
                    skipReason));
            }

            return new TestSuiteDescriptor(suiteId, target + ": " + displayName, cases);
        }

        private static TestSuiteDescriptor Rename(TestSuiteDescriptor suite, string suiteId, string displayName, string skipReason)
        {
            List<TestCaseDescriptor> cases = suite.Cases
                .Select(c => new TestCaseDescriptor(
                    suiteId,
                    c.CaseId,
                    c.DisplayName,
                    c.ExecuteAsync,
                    c.Tags,
                    skipReason != null || c.Skip,
                    skipReason ?? c.SkipReason))
                .ToList();

            return new TestSuiteDescriptor(suiteId, displayName, cases, suite.BeforeSuiteAsync, suite.AfterSuiteAsync);
        }

        #endregion

        #region Semantics

        private static List<FileShareCase> SemanticsCases(string protocol)
        {
            return new List<FileShareCase>
            {
                new FileShareCase("LargeObjectBytesRoundTrip", "12 MiB byte-array object round trips across many protocol I/O chunks", LargeObjectBytesRoundTrip),
                new FileShareCase("LargeObjectStreamRoundTrip", "12 MiB non-seekable stream write and stream read round trip", LargeObjectStreamRoundTrip),
                new FileShareCase("StreamReadIsSeekable", "GetStreamAsync returns a seekable stream with correct length and random access", StreamReadIsSeekable),
                new FileShareCase("StreamEarlyDisposeReleasesHandle", "disposing a partially read stream releases the server handle", StreamEarlyDisposeReleasesHandle),
                new FileShareCase("OverwriteLargeWithSmallTruncates", "overwriting a large object with a small one truncates it", OverwriteLargeWithSmallTruncates),
                new FileShareCase("OverwriteViaStreamTruncates", "overwriting through a stream truncates the object", OverwriteViaStreamTruncates),
                new FileShareCase("OverwriteLargeWithEmptyTruncates", "overwriting a large object with empty content truncates it to zero", OverwriteLargeWithEmptyTruncates),
                new FileShareCase("DeepNestedWriteCreatesParents", "writing a deeply nested key creates every parent folder", DeepNestedWriteCreatesParents),
                new FileShareCase("FolderMarkerIsDirectory", "a key ending in '/' creates a real folder that exists and enumerates as a folder", FolderMarkerIsDirectory),
                new FileShareCase("FolderEntriesEnumerated", "empty and non-empty folders enumerate as folder entries after their contents", FolderMarkerWithContentNotListed),
                new FileShareCase("DeleteNonEmptyFolderThrows", "deleting a non-empty folder throws IOException and keeps its contents", DeleteNonEmptyFolderThrows),
                new FileShareCase("DeleteFolderWithoutSlash", "deleting an empty folder by name without a trailing '/' removes it", DeleteFolderWithoutSlash),
                new FileShareCase("DeleteFolderKeyOnFileIsNoOp", "deleting 'name/' when 'name' is a file leaves the file", DeleteFolderKeyOnFileIsNoOp),
                new FileShareCase("GetFolderReturnsEmpty", "reading a folder returns empty content", GetFolderReturnsEmpty),
                new FileShareCase("FolderMetadata", "folder metadata reports IsFolder, zero length, and a '/' key", FolderMetadata),
                new FileShareCase("FolderKeyOnFileSemantics", "'name/' does not match a file for metadata or exists", FolderKeyOnFileSemantics),
                new FileShareCase("FileAsParentFails", "writing beneath a file fails and leaves the file intact", FileAsParentFails),
                new FileShareCase("MissingObjectsThrowKeyNotFound", "missing objects throw KeyNotFoundException for get, stream, and metadata", MissingObjectsThrowKeyNotFound),
                new FileShareCase("KeyNormalization", "backslashes, leading separators, and repeated separators normalize", KeyNormalization),
                new FileShareCase("InvalidKeysRejected", "null, empty, root, and relative-segment keys are rejected before any I/O", InvalidKeysRejected),
                new FileShareCase("SpecialCharacterNames", "names with spaces, symbols, unicode, and emoji round trip and enumerate", SpecialCharacterNames),
                new FileShareCase("CaseSemantics", "lookups follow the server's storage case semantics and filters follow the provider's", CaseSemantics),
                new FileShareCase("Timestamps", "metadata and enumeration timestamps are populated and advance on overwrite", ctx => Timestamps(ctx, protocol)),
                new FileShareCase("ContentLengthLongerThanStream", "a stream shorter than contentLength writes the available bytes", ContentLengthLongerThanStream),
                new FileShareCase("StreamArgumentValidation", "negative length, null stream with length, and unreadable streams are rejected", StreamArgumentValidation),
                new FileShareCase("BinaryContentIntegrity", "every byte value survives a round trip at chunk-boundary sizes", BinaryContentIntegrity)
            };
        }

        private static async Task LargeObjectBytesRoundTrip(FileShareCaseContext ctx, CancellationToken token)
        {
            byte[] data = TestData.Pattern(12 * 1024 * 1024 + 7, 1);
            await ctx.Blobs.WriteAsync("large/bytes.bin", "application/octet-stream", data, token).ConfigureAwait(false);
            Check.Bytes(data, await ctx.Blobs.GetAsync("large/bytes.bin", token).ConfigureAwait(false), "large bytes");
            Check.Equal((long)data.Length, (await ctx.Blobs.GetMetadataAsync("large/bytes.bin", token).ConfigureAwait(false)).ContentLength, "large length");
        }

        private static async Task LargeObjectStreamRoundTrip(FileShareCaseContext ctx, CancellationToken token)
        {
            byte[] data = TestData.Pattern(12 * 1024 * 1024 + 13, 2);

            using (NonSeekableReadStream source = new NonSeekableReadStream(data))
            {
                await ctx.Blobs.WriteAsync("large/stream.bin", "application/octet-stream", data.Length, source, token).ConfigureAwait(false);
            }

            using (BlobData blob = await ctx.Blobs.GetStreamAsync("large/stream.bin", token).ConfigureAwait(false))
            {
                Check.Equal((long)data.Length, blob.ContentLength, "stream content length");
                Check.Bytes(data, await TestData.ReadAllAsync(blob.Data, token).ConfigureAwait(false), "large stream");
            }
        }

        private static async Task StreamReadIsSeekable(FileShareCaseContext ctx, CancellationToken token)
        {
            byte[] data = TestData.Pattern(3 * 1024 * 1024 + 3, 3);
            await ctx.Blobs.WriteAsync("seek.bin", "application/octet-stream", data, token).ConfigureAwait(false);

            using (BlobData blob = await ctx.Blobs.GetStreamAsync("seek.bin", token).ConfigureAwait(false))
            {
                Stream s = blob.Data;
                Check.True(s.CanRead, "stream CanRead");
                Check.True(s.CanSeek, "stream CanSeek");
                Check.Equal((long)data.Length, s.Length, "stream Length");

                long offset = 2 * 1024 * 1024 + 5;
                s.Seek(offset, SeekOrigin.Begin);
                Check.Equal(offset, s.Position, "position after seek");
                byte[] middle = await TestData.ReadExactlyAsync(s, 100000, token).ConfigureAwait(false);
                Check.Bytes(data.Skip((int)offset).Take(100000).ToArray(), middle, "middle range");

                s.Position = 0;
                byte[] head = await TestData.ReadExactlyAsync(s, 1000, token).ConfigureAwait(false);
                Check.Bytes(data.Take(1000).ToArray(), head, "head range");

                s.Seek(-10, SeekOrigin.End);
                byte[] tail = await TestData.ReadExactlyAsync(s, 10, token).ConfigureAwait(false);
                Check.Bytes(data.Skip(data.Length - 10).ToArray(), tail, "tail range");
                Check.Equal(0, await s.ReadAsync(new byte[16], 0, 16, token).ConfigureAwait(false), "read at end of stream");

                s.Seek(data.Length + 100, SeekOrigin.Begin);
                Check.Equal(0, await s.ReadAsync(new byte[16], 0, 16, token).ConfigureAwait(false), "read beyond end of stream");
            }
        }

        private static async Task StreamEarlyDisposeReleasesHandle(FileShareCaseContext ctx, CancellationToken token)
        {
            byte[] data = TestData.Pattern(1024 * 1024, 4);
            await ctx.Blobs.WriteAsync("handle.bin", "application/octet-stream", data, token).ConfigureAwait(false);

            for (int i = 0; i < 25; i++)
            {
                using (BlobData blob = await ctx.Blobs.GetStreamAsync("handle.bin", token).ConfigureAwait(false))
                {
                    await TestData.ReadExactlyAsync(blob.Data, 10, token).ConfigureAwait(false);
                }
            }

            await ctx.Blobs.WriteAsync("handle.bin", "text/plain", "replaced", token).ConfigureAwait(false);
            Check.Equal("replaced", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("handle.bin", token).ConfigureAwait(false)), "overwrite after streams disposed");
            await ctx.Blobs.DeleteAsync("handle.bin", token).ConfigureAwait(false);
            Check.False(await ctx.Blobs.ExistsAsync("handle.bin", token).ConfigureAwait(false), "deleted after streams disposed");
        }

        private static async Task OverwriteLargeWithSmallTruncates(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("truncate.bin", "application/octet-stream", TestData.Pattern(3 * 1024 * 1024, 5), token).ConfigureAwait(false);
            await ctx.Blobs.WriteAsync("truncate.bin", "text/plain", "tiny", token).ConfigureAwait(false);
            Check.Equal("tiny", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("truncate.bin", token).ConfigureAwait(false)), "truncated content");
            Check.Equal(4L, (await ctx.Blobs.GetMetadataAsync("truncate.bin", token).ConfigureAwait(false)).ContentLength, "truncated length");
            Check.Equal(4L, await ctx.ServerSizeAsync("truncate.bin", token).ConfigureAwait(false), "server-side truncated length");
        }

        private static async Task OverwriteViaStreamTruncates(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("truncate-stream.bin", "application/octet-stream", TestData.Pattern(2 * 1024 * 1024, 6), token).ConfigureAwait(false);
            byte[] small = TestData.Pattern(1000, 7);

            using (MemoryStream ms = new MemoryStream(small))
            {
                await ctx.Blobs.WriteAsync("truncate-stream.bin", "application/octet-stream", small.Length, ms, token).ConfigureAwait(false);
            }

            Check.Bytes(small, await ctx.Blobs.GetAsync("truncate-stream.bin", token).ConfigureAwait(false), "stream-truncated content");
            Check.Equal(1000L, await ctx.ServerSizeAsync("truncate-stream.bin", token).ConfigureAwait(false), "server-side stream-truncated length");
        }

        private static async Task OverwriteLargeWithEmptyTruncates(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("truncate-empty.bin", "application/octet-stream", TestData.Pattern(1024 * 1024, 8), token).ConfigureAwait(false);
            await ctx.Blobs.WriteAsync("truncate-empty.bin", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            Check.Equal(0, (await ctx.Blobs.GetAsync("truncate-empty.bin", token).ConfigureAwait(false)).Length, "empty content");
            Check.Equal(0L, (await ctx.Blobs.GetMetadataAsync("truncate-empty.bin", token).ConfigureAwait(false)).ContentLength, "empty length");
            Check.Equal(0L, await ctx.ServerSizeAsync("truncate-empty.bin", token).ConfigureAwait(false), "server-side empty length");
        }

        private static async Task DeepNestedWriteCreatesParents(FileShareCaseContext ctx, CancellationToken token)
        {
            string key = "d1/d2/d3/d4/d5/d6/d7/d8/d9/d10/leaf.txt";
            await ctx.Blobs.WriteAsync(key, "text/plain", "leaf", token).ConfigureAwait(false);
            Check.Equal("leaf", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync(key, token).ConfigureAwait(false)), "deep content");

            string folder = "";
            foreach (string part in key.Split('/').Take(10))
            {
                folder += part + "/";
                Check.True(await ctx.Blobs.ExistsAsync(folder, token).ConfigureAwait(false), "parent folder " + folder);
                Check.True((await ctx.Blobs.GetMetadataAsync(folder, token).ConfigureAwait(false)).IsFolder, "parent metadata " + folder);
            }
        }

        private static async Task FolderMarkerIsDirectory(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("marker/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            Check.True(await ctx.Blobs.ExistsAsync("marker/", token).ConfigureAwait(false), "marker exists");
            Check.True(await ctx.Blobs.ExistsAsync("marker", token).ConfigureAwait(false), "marker exists without slash");
            Check.True(await ctx.ServerIsDirectoryAsync("marker", token).ConfigureAwait(false), "marker is a server-side directory");

            List<BlobMetadata> listed = await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "marker" }, token).ConfigureAwait(false);
            Check.Equal(1, listed.Count, "marker enumeration count");
            Check.Equal("marker/", listed[0].Key, "marker enumeration key");
            Check.True(listed[0].IsFolder, "marker enumerated as folder");
            Check.Equal(0L, listed[0].ContentLength, "marker enumerated length");

            await ctx.Blobs.WriteAsync("marker/inside.txt", "text/plain", "inside", token).ConfigureAwait(false);
            listed = await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "marker" }, token).ConfigureAwait(false);
            Check.Equal("marker/inside.txt", listed[0].Key, "contents first");
            Check.Equal("marker/", listed[1].Key, "folder after contents");
            await ctx.Blobs.DeleteAsync("marker/inside.txt", token).ConfigureAwait(false);

            // writing the marker again is idempotent
            await ctx.Blobs.WriteAsync("marker/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
        }

        private static async Task FolderMarkerWithContentNotListed(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("empty-one/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            await ctx.Blobs.WriteAsync("full-one/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            await ctx.Blobs.WriteAsync("full-one/file.txt", "text/plain", "x", token).ConfigureAwait(false);

            List<BlobMetadata> listed = await TestData.EnumerateAsync(ctx.Blobs, null, token).ConfigureAwait(false);
            Check.Keys(new[] { "empty-one/", "full-one/file.txt", "full-one/" }, listed.Select(m => m.Key), "listing");
            Check.True(listed.Single(m => m.Key == "empty-one/").IsFolder, "empty folder IsFolder");
            Check.True(listed.Single(m => m.Key == "full-one/").IsFolder, "non-empty folder IsFolder");
            Check.False(listed.Single(m => m.Key == "full-one/file.txt").IsFolder, "file IsFolder");
            Check.True(listed.FindIndex(m => m.Key == "full-one/file.txt") < listed.FindIndex(m => m.Key == "full-one/"), "folder follows its contents");
            Check.Equal(0, ctx.Blobs.Enumerate(new EnumerationFilter { Prefix = "empty-one/" }).Count(), "a folder prefix does not return the folder itself (sync)");
        }

        private static async Task DeleteNonEmptyFolderThrows(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("nonempty/a.txt", "text/plain", "a", token).ConfigureAwait(false);
            await Check.ThrowsAsync<IOException>(() => ctx.Blobs.DeleteAsync("nonempty/", token), "delete non-empty folder").ConfigureAwait(false);
            Check.True(await ctx.Blobs.ExistsAsync("nonempty/a.txt", token).ConfigureAwait(false), "contents kept");

            DeleteManyResult result = await ctx.Blobs.DeleteManyAsync(new[] { "nonempty/", "nonempty/a.txt" }, token).ConfigureAwait(false);
            DeleteResult fileResult = result.Results.Single(r => r.Key == "nonempty/a.txt");
            Check.True(fileResult.Success, "file deleted by DeleteMany (" + fileResult.Error + ")");
        }

        private static async Task DeleteFolderWithoutSlash(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("plainfolder/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            await ctx.Blobs.DeleteAsync("plainfolder", token).ConfigureAwait(false);
            Check.False(await ctx.Blobs.ExistsAsync("plainfolder/", token).ConfigureAwait(false), "folder removed");
            Check.False(await ctx.ServerExistsAsync("plainfolder", token).ConfigureAwait(false), "folder removed server-side");
        }

        private static async Task DeleteFolderKeyOnFileIsNoOp(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("keep.txt", "text/plain", "keep", token).ConfigureAwait(false);
            await ctx.Blobs.DeleteAsync("keep.txt/", token).ConfigureAwait(false);
            Check.True(await ctx.Blobs.ExistsAsync("keep.txt", token).ConfigureAwait(false), "file kept");
        }

        private static async Task GetFolderReturnsEmpty(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("readfolder/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            Check.Equal(0, (await ctx.Blobs.GetAsync("readfolder/", token).ConfigureAwait(false)).Length, "folder bytes");

            using (BlobData blob = await ctx.Blobs.GetStreamAsync("readfolder/", token).ConfigureAwait(false))
            {
                Check.Equal(0L, blob.ContentLength, "folder stream length");
                Check.Equal(0, (await TestData.ReadAllAsync(blob.Data, token).ConfigureAwait(false)).Length, "folder stream bytes");
            }
        }

        private static async Task FolderMetadata(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("mdfolder/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);

            foreach (string key in new[] { "mdfolder/", "mdfolder" })
            {
                BlobMetadata md = await ctx.Blobs.GetMetadataAsync(key, token).ConfigureAwait(false);
                Check.True(md.IsFolder, "IsFolder for " + key);
                Check.Equal(0L, md.ContentLength, "length for " + key);
                Check.Equal("mdfolder/", md.Key, "key for " + key);
            }
        }

        private static async Task FolderKeyOnFileSemantics(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("plain.txt", "text/plain", "plain", token).ConfigureAwait(false);
            Check.False(await ctx.Blobs.ExistsAsync("plain.txt/", token).ConfigureAwait(false), "folder key on file exists");
            await Check.ThrowsAsync<KeyNotFoundException>(() => ctx.Blobs.GetMetadataAsync("plain.txt/", token), "folder key on file metadata").ConfigureAwait(false);
            Check.True(await ctx.Blobs.ExistsAsync("plain.txt", token).ConfigureAwait(false), "file key exists");
        }

        private static async Task FileAsParentFails(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("parent.txt", "text/plain", "parent", token).ConfigureAwait(false);
            await Check.ThrowsAsync<Exception>(() => ctx.Blobs.WriteAsync("parent.txt/child.txt", "text/plain", "child", token), "write beneath file").ConfigureAwait(false);
            Check.Equal("parent", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("parent.txt", token).ConfigureAwait(false)), "parent intact");
            Check.False(await ctx.Blobs.ExistsAsync("parent.txt/child.txt", token).ConfigureAwait(false), "child beneath file exists");
        }

        private static async Task MissingObjectsThrowKeyNotFound(FileShareCaseContext ctx, CancellationToken token)
        {
            foreach (string key in new[] { "missing.txt", "missing-dir/missing.txt", "a/b/c/missing.bin" })
            {
                await Check.ThrowsAsync<KeyNotFoundException>(() => ctx.Blobs.GetAsync(key, token), "GetAsync " + key).ConfigureAwait(false);
                await Check.ThrowsAsync<KeyNotFoundException>(() => ctx.Blobs.GetStreamAsync(key, token), "GetStreamAsync " + key).ConfigureAwait(false);
                await Check.ThrowsAsync<KeyNotFoundException>(() => ctx.Blobs.GetMetadataAsync(key, token), "GetMetadataAsync " + key).ConfigureAwait(false);
                Check.False(await ctx.Blobs.ExistsAsync(key, token).ConfigureAwait(false), "ExistsAsync " + key);
                await ctx.Blobs.DeleteAsync(key, token).ConfigureAwait(false);
            }
        }

        private static async Task KeyNormalization(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("norm\\back\\slash.txt", "text/plain", "back", token).ConfigureAwait(false);
            Check.Equal("back", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("norm/back/slash.txt", token).ConfigureAwait(false)), "backslash key");

            await ctx.Blobs.WriteAsync("norm//double///slash.txt", "text/plain", "double", token).ConfigureAwait(false);
            Check.Equal("double", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("norm/double/slash.txt", token).ConfigureAwait(false)), "repeated separators");

            BlobMetadata md = await ctx.Blobs.GetMetadataAsync("norm\\back\\slash.txt", token).ConfigureAwait(false);
            Check.Equal("norm/back/slash.txt", md.Key, "metadata key uses '/'");

            List<BlobMetadata> listed = await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "norm/" }, token).ConfigureAwait(false);
            Check.Keys(new[] { "norm/back/slash.txt", "norm/double/slash.txt" }, TestData.FileKeys(listed), "normalized enumeration");
            Check.Keys(new[] { "norm/back/", "norm/double/" }, TestData.FolderKeys(listed), "normalized folders");
        }

        private static async Task InvalidKeysRejected(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient raw = ctx.CreateRawClient())
            {
                await Check.ThrowsAsync<ArgumentNullException>(() => raw.GetAsync(null, token), "GetAsync null").ConfigureAwait(false);
                await Check.ThrowsAsync<ArgumentNullException>(() => raw.WriteAsync(null, "text/plain", "x", token), "WriteAsync null").ConfigureAwait(false);
                await Check.ThrowsAsync<ArgumentNullException>(() => raw.DeleteAsync(null, token), "DeleteAsync null").ConfigureAwait(false);
                await Check.ThrowsAsync<ArgumentNullException>(() => raw.ExistsAsync(null, token), "ExistsAsync null").ConfigureAwait(false);
                await Check.ThrowsAsync<ArgumentNullException>(() => raw.GetMetadataAsync(null, token), "GetMetadataAsync null").ConfigureAwait(false);

                foreach (string key in new[] { "", "/", "\\", "//", "../escape.txt", "a/../b.txt", "./here.txt", "a/./b.txt", "a/.." })
                {
                    await Check.ThrowsAsync<ArgumentException>(() => raw.GetAsync(key, token), "GetAsync '" + key + "'").ConfigureAwait(false);
                    await Check.ThrowsAsync<ArgumentException>(() => raw.GetStreamAsync(key, token), "GetStreamAsync '" + key + "'").ConfigureAwait(false);
                    await Check.ThrowsAsync<ArgumentException>(() => raw.WriteAsync(key, "text/plain", "x", token), "WriteAsync '" + key + "'").ConfigureAwait(false);
                    await Check.ThrowsAsync<ArgumentException>(() => raw.DeleteAsync(key, token), "DeleteAsync '" + key + "'").ConfigureAwait(false);
                    await Check.ThrowsAsync<ArgumentException>(() => raw.ExistsAsync(key, token), "ExistsAsync '" + key + "'").ConfigureAwait(false);
                    await Check.ThrowsAsync<ArgumentException>(() => raw.GetMetadataAsync(key, token), "GetMetadataAsync '" + key + "'").ConfigureAwait(false);
                }
            }

        }

        private static async Task SpecialCharacterNames(FileShareCaseContext ctx, CancellationToken token)
        {
            string[] names =
            {
                "space in name.txt",
                "  leading-spaces.txt",
                "unicode-日本語-üéñ.txt",
                "emoji-\U0001F600-\U0001F680.txt",
                "symbols #%&+=@!$'(),;[]^_`{}~.txt",
                ".hidden",
                "no-extension",
                "multi.dot.name.tar.gz",
                "folder with space/nested é/file.txt"
            };

            foreach (string name in names)
            {
                await ctx.Blobs.WriteAsync(name, "text/plain", name, token).ConfigureAwait(false);
            }

            foreach (string name in names)
            {
                Check.Equal(name, Encoding.UTF8.GetString(await ctx.Blobs.GetAsync(name, token).ConfigureAwait(false)), "content of '" + name + "'");
                Check.True(await ctx.Blobs.ExistsAsync(name, token).ConfigureAwait(false), "exists '" + name + "'");
                Check.Equal(name, (await ctx.Blobs.GetMetadataAsync(name, token).ConfigureAwait(false)).Key, "metadata key '" + name + "'");
            }

            List<BlobMetadata> listed = await TestData.EnumerateAsync(ctx.Blobs, null, token).ConfigureAwait(false);
            Check.Keys(names, TestData.FileKeys(listed), "special-name enumeration");
            Check.Keys(new[] { "folder with space/", "folder with space/nested \u00E9/" }, TestData.FolderKeys(listed), "special-name folders");

            foreach (string name in names)
            {
                await ctx.Blobs.DeleteAsync(name, token).ConfigureAwait(false);
                Check.False(await ctx.Blobs.ExistsAsync(name, token).ConfigureAwait(false), "deleted '" + name + "'");
            }
        }

        private static async Task CaseSemantics(FileShareCaseContext ctx, CancellationToken token)
        {
            bool storageInsensitive = ctx.Server.CaseInsensitive;
            bool filterInsensitive = ctx.Server.Protocol == "cifs";
            Check.Equal(filterInsensitive, ctx.Options.IsCaseInsensitiveProvider, "provider filter case sensitivity");

            await ctx.Blobs.WriteAsync("CaseTest/File.TXT", "text/plain", "upper", token).ConfigureAwait(false);

            if (storageInsensitive)
            {
                Check.True(await ctx.Blobs.ExistsAsync("casetest/file.txt", token).ConfigureAwait(false), "case-insensitive storage exists");
                Check.Equal("upper", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("casetest/file.txt", token).ConfigureAwait(false)), "case-insensitive storage read");

                await ctx.Blobs.WriteAsync("casetest/file.txt", "text/plain", "lower", token).ConfigureAwait(false);
                List<BlobMetadata> listed = await TestData.EnumerateAsync(ctx.Blobs, null, token).ConfigureAwait(false);
                Check.Equal(1, TestData.FileKeys(listed).Count, "case-insensitive storage keeps one object");
                Check.Keys(new[] { "CaseTest/" }, TestData.FolderKeys(listed), "folder keeps its stored name");
                Check.Equal("lower", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("CaseTest/File.TXT", token).ConfigureAwait(false)), "case-insensitive storage overwrite");
            }
            else
            {
                Check.False(await ctx.Blobs.ExistsAsync("casetest/file.txt", token).ConfigureAwait(false), "case-sensitive storage exists");
                await Check.ThrowsAsync<KeyNotFoundException>(() => ctx.Blobs.GetAsync("CaseTest/file.txt", token), "case-sensitive storage read").ConfigureAwait(false);

                await ctx.Blobs.WriteAsync("CaseTest/file.txt", "text/plain", "lower", token).ConfigureAwait(false);
                Check.Equal("upper", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("CaseTest/File.TXT", token).ConfigureAwait(false)), "distinct upper");
                Check.Equal("lower", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("CaseTest/file.txt", token).ConfigureAwait(false)), "distinct lower");
                Check.Keys(new[] { "CaseTest/File.TXT", "CaseTest/file.txt", "CaseTest/" }, (await TestData.EnumerateAsync(ctx.Blobs, null, token).ConfigureAwait(false)).Select(m => m.Key), "case-sensitive storage listing");
            }

            int objects = storageInsensitive ? 1 : 2;
            Check.Equal(filterInsensitive ? objects : 0, (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "CASETEST/" }, token).ConfigureAwait(false)).Count, "prefix filter with different case");
            Check.Equal(filterInsensitive ? objects : 0, (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "CaseTest/", Suffix = ".TxT" }, token).ConfigureAwait(false)).Count, "suffix filter with different case");
            Check.Equal(objects, (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "CaseTest/" }, token).ConfigureAwait(false)).Count, "prefix filter with exact case");
        }

        private static async Task Timestamps(FileShareCaseContext ctx, string protocol)
        {
            CancellationToken token = CancellationToken.None;
            TimeSpan window = TimeSpan.FromMinutes(10);

            await ctx.Blobs.WriteAsync("times.txt", "text/plain", "first", token).ConfigureAwait(false);
            BlobMetadata first = await ctx.Blobs.GetMetadataAsync("times.txt", token).ConfigureAwait(false);

            Check.RecentUtc(first.LastUpdateUtc, window, "LastUpdateUtc");
            Check.RecentUtc(first.LastAccessUtc, window, "LastAccessUtc");
            Check.Equal(DateTimeKind.Utc, first.LastUpdateUtc.Value.Kind, "LastUpdateUtc kind");

            if (protocol == "cifs")
            {
                Check.RecentUtc(first.CreatedUtc, window, "CreatedUtc");
                Check.True(first.CreatedUtc.Value <= first.LastUpdateUtc.Value.AddSeconds(2), "CreatedUtc not after LastUpdateUtc");
            }
            else
            {
                Check.Null(first.CreatedUtc, "NFSv3 CreatedUtc");
            }

            BlobMetadata listed = (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "times" }, token).ConfigureAwait(false)).Single();
            Check.True((listed.LastUpdateUtc.Value - first.LastUpdateUtc.Value).Duration() < TimeSpan.FromSeconds(2), "enumerated LastUpdateUtc matches metadata");

            await Task.Delay(2100).ConfigureAwait(false);
            await ctx.Blobs.WriteAsync("times.txt", "text/plain", "second", token).ConfigureAwait(false);
            BlobMetadata second = await ctx.Blobs.GetMetadataAsync("times.txt", token).ConfigureAwait(false);
            Check.True(second.LastUpdateUtc.Value > first.LastUpdateUtc.Value, "LastUpdateUtc advances on overwrite (" + first.LastUpdateUtc.Value.ToString("o") + " -> " + second.LastUpdateUtc.Value.ToString("o") + ")");

            if (protocol == "cifs")
            {
                Check.True((second.CreatedUtc.Value - first.CreatedUtc.Value).Duration() < TimeSpan.FromSeconds(1), "CreatedUtc stable across overwrite");
            }
        }

        private static async Task ContentLengthLongerThanStream(FileShareCaseContext ctx, CancellationToken token)
        {
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("abc")))
            {
                await ctx.Blobs.WriteAsync("longer.txt", "text/plain", 10, ms, token).ConfigureAwait(false);
            }

            Check.Equal("abc", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("longer.txt", token).ConfigureAwait(false)), "available bytes written");
        }

        private static async Task StreamArgumentValidation(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient raw = ctx.CreateRawClient())
            {
                await Check.ThrowsAsync<ArgumentOutOfRangeException>(() => raw.WriteAsync(ctx.Prefix + "neg.txt", "text/plain", -1, new MemoryStream(), token), "negative content length").ConfigureAwait(false);
                await Check.ThrowsAsync<ArgumentNullException>(() => raw.WriteAsync(ctx.Prefix + "nullstream.txt", "text/plain", 5, null, token), "null stream with length").ConfigureAwait(false);

                using (MemoryStream closed = new MemoryStream(new byte[] { 1, 2, 3 }))
                {
                    closed.Dispose();
                    await Check.ThrowsAsync<ArgumentException>(() => raw.WriteAsync(ctx.Prefix + "unreadable.txt", "text/plain", 3, closed, token), "unreadable stream").ConfigureAwait(false);
                }

                Check.False(await raw.ExistsAsync(ctx.Prefix + "neg.txt", token).ConfigureAwait(false), "nothing written for negative length");
                Check.False(await raw.ExistsAsync(ctx.Prefix + "nullstream.txt", token).ConfigureAwait(false), "nothing written for null stream");
            }
        }

        private static async Task BinaryContentIntegrity(FileShareCaseContext ctx, CancellationToken token)
        {
            int[] sizes = { 1, 255, 256, 4095, 4096, 4097, 65535, 65536, 65537, 1048575, 1048576, 1048577 };

            foreach (int size in sizes)
            {
                byte[] data = new byte[size];
                for (int i = 0; i < size; i++) data[i] = (byte)(i % 256);
                string key = "sizes/" + size + ".bin";

                await ctx.Blobs.WriteAsync(key, "application/octet-stream", data, token).ConfigureAwait(false);
                Check.Bytes(data, await ctx.Blobs.GetAsync(key, token).ConfigureAwait(false), "size " + size);
                Check.Equal((long)size, (await ctx.Blobs.GetMetadataAsync(key, token).ConfigureAwait(false)).ContentLength, "metadata size " + size);
            }

            List<BlobMetadata> listed = await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "sizes/" }, token).ConfigureAwait(false);
            Check.Equal(sizes.Length, listed.Count, "sized object count");
            foreach (int size in sizes)
            {
                Check.Equal((long)size, listed.Single(m => m.Key == "sizes/" + size + ".bin").ContentLength, "enumerated size " + size);
            }
        }

        #endregion

        #region Enumeration

        private static List<FileShareCase> EnumerationCases()
        {
            return new List<FileShareCase>
            {
                new FileShareCase("LargeDirectory", "a directory of 1,500 long-named objects enumerates completely (paged directory reads)", LargeDirectory),
                new FileShareCase("WideAndDeepTree", "a wide and deep tree enumerates every object exactly once", WideAndDeepTree),
                new FileShareCase("PrefixPruning", "nested prefixes return only matching objects", PrefixPruning),
                new FileShareCase("MissingPrefixDirectory", "a prefix naming a missing directory enumerates nothing", MissingPrefixDirectory),
                new FileShareCase("PrefixThroughFile", "a prefix that passes through a file enumerates nothing", PrefixThroughFile),
                new FileShareCase("SizeFiltersOnTree", "size filters apply across the whole tree, with folders treated as zero-length", SizeFiltersOnTree),
                new FileShareCase("EnumerationCancellation", "cancelling during enumeration throws OperationCanceledException", EnumerationCancellation),
                new FileShareCase("SyncEnumerationParity", "sync and async enumeration match on a large tree", SyncEnumerationParity),
                new FileShareCase("EmptyRemovesFilesAndFolders", "EmptyAsync removes every file and folder in the share", EmptyRemovesFilesAndFolders)
            };
        }

        private static async Task LargeDirectory(FileShareCaseContext ctx, CancellationToken token)
        {
            const int count = 1500;
            List<WriteRequest> requests = new List<WriteRequest>();
            List<string> keys = new List<string>();

            for (int i = 0; i < count; i++)
            {
                string key = "big/an-entry-with-a-deliberately-long-file-name-to-fill-directory-pages-" + i.ToString("D5") + ".bin";
                keys.Add(key);
                requests.Add(new WriteRequest(key, "application/octet-stream", new byte[] { (byte)(i % 256) }));
            }

            ctx.Blobs.MaxConcurrency = 16;
            await ctx.Blobs.WriteManyAsync(requests, token).ConfigureAwait(false);

            List<BlobMetadata> listed = await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "big/" }, token).ConfigureAwait(false);
            Check.Keys(keys, listed.Select(m => m.Key), "large directory");
            Check.True(listed.All(m => m.ContentLength == 1), "large directory lengths");

            List<BlobMetadata> partial = await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "big/an-entry-with-a-deliberately-long-file-name-to-fill-directory-pages-010" }, token).ConfigureAwait(false);
            Check.Equal(100, partial.Count, "partial prefix across pages");

            List<BlobMetadata> suffix = await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "big/", Suffix = "7.bin" }, token).ConfigureAwait(false);
            Check.Equal(150, suffix.Count, "suffix across pages");
        }

        private static async Task WideAndDeepTree(FileShareCaseContext ctx, CancellationToken token)
        {
            List<string> keys = new List<string>();
            List<WriteRequest> requests = new List<WriteRequest>();

            for (int a = 0; a < 5; a++)
            {
                for (int b = 0; b < 3; b++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        string key = "tree/a" + a + "/b" + b + "/c" + c + ".txt";
                        keys.Add(key);
                        requests.Add(new WriteRequest(key, "text/plain", Encoding.UTF8.GetBytes(key)));
                    }

                    string mid = "tree/a" + a + "/b" + b + ".txt";
                    keys.Add(mid);
                    requests.Add(new WriteRequest(mid, "text/plain", Encoding.UTF8.GetBytes(mid)));
                }
            }

            ctx.Blobs.MaxConcurrency = 8;
            await ctx.Blobs.WriteManyAsync(requests, token).ConfigureAwait(false);

            List<BlobMetadata> listed = await TestData.EnumerateAsync(ctx.Blobs, null, token).ConfigureAwait(false);
            Check.Keys(keys, TestData.FileKeys(listed), "tree files");
            Check.Keys(TestData.AncestorFolders(keys), TestData.FolderKeys(listed), "tree folders");
            Check.Equal(21, TestData.FolderKeys(listed).Count, "tree folder count");

            foreach (BlobMetadata md in listed.Where(m => !m.IsFolder))
            {
                Check.Equal((long)Encoding.UTF8.GetByteCount(md.Key), md.ContentLength, "length of " + md.Key);
            }
        }

        private static async Task PrefixPruning(FileShareCaseContext ctx, CancellationToken token)
        {
            string[] keys =
            {
                "p/alpha/one.txt", "p/alpha/two.txt", "p/alpha/deep/three.txt",
                "p/alphabet/four.txt", "p/alp.txt", "p/beta/five.txt", "p/alpha-sibling.txt"
            };

            foreach (string key in keys) await ctx.Blobs.WriteAsync(key, "text/plain", key, token).ConfigureAwait(false);

            Check.Keys(
                new[] { "p/alpha/one.txt", "p/alpha/two.txt", "p/alpha/deep/three.txt", "p/alpha/deep/" },
                (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "p/alpha/" }, token).ConfigureAwait(false)).Select(m => m.Key),
                "folder prefix");

            Check.Keys(
                new[] { "p/alpha/one.txt", "p/alpha/two.txt", "p/alpha/deep/three.txt", "p/alphabet/four.txt", "p/alpha-sibling.txt", "p/alpha/", "p/alpha/deep/", "p/alphabet/" },
                (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "p/alpha" }, token).ConfigureAwait(false)).Select(m => m.Key),
                "partial prefix spanning folders and files");

            Check.Keys(
                new[] { "p/alpha/deep/three.txt" },
                (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "p/alpha/deep/th" }, token).ConfigureAwait(false)).Select(m => m.Key),
                "deep partial prefix");

            Check.Keys(
                new[] { "p/alpha/one.txt" },
                (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "p/alpha/o", Suffix = ".txt" }, token).ConfigureAwait(false)).Select(m => m.Key),
                "prefix and suffix");

            Check.Keys(
                new[] { "p/alp.txt", "p/alpha/one.txt", "p/alpha/two.txt", "p/alpha/deep/three.txt", "p/alphabet/four.txt", "p/alpha-sibling.txt", "p/alpha/", "p/alpha/deep/", "p/alphabet/" },
                (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "p/alp" }, token).ConfigureAwait(false)).Select(m => m.Key),
                "short prefix");
        }

        private static async Task MissingPrefixDirectory(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("present/file.txt", "text/plain", "x", token).ConfigureAwait(false);
            Check.Equal(0, (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "absent/" }, token).ConfigureAwait(false)).Count, "missing folder prefix");
            Check.Equal(0, (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "absent/deeper/still/" }, token).ConfigureAwait(false)).Count, "missing deep folder prefix");
            Check.Equal(0, ctx.Blobs.Enumerate(new EnumerationFilter { Prefix = "absent/" }).Count(), "missing folder prefix (sync)");
        }

        private static async Task PrefixThroughFile(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("afile.txt", "text/plain", "x", token).ConfigureAwait(false);
            Check.Equal(0, (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "afile.txt/" }, token).ConfigureAwait(false)).Count, "prefix through a file");
        }

        private static async Task SizeFiltersOnTree(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("sz/small.bin", "application/octet-stream", new byte[10], token).ConfigureAwait(false);
            await ctx.Blobs.WriteAsync("sz/deep/medium.bin", "application/octet-stream", new byte[1000], token).ConfigureAwait(false);
            await ctx.Blobs.WriteAsync("sz/deeper/still/large.bin", "application/octet-stream", new byte[100000], token).ConfigureAwait(false);
            await ctx.Blobs.WriteAsync("sz/emptyfolder/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);

            Check.Keys(
                new[] { "sz/deep/medium.bin", "sz/deeper/still/large.bin" },
                (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "sz/", MinimumSize = 100 }, token).ConfigureAwait(false)).Select(m => m.Key),
                "minimum size");

            Check.Keys(
                new[] { "sz/small.bin", "sz/deep/medium.bin", "sz/deep/", "sz/deeper/", "sz/deeper/still/", "sz/emptyfolder/" },
                (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "sz/", MaximumSize = 5000 }, token).ConfigureAwait(false)).Select(m => m.Key),
                "maximum size");

            Check.Keys(
                new[] { "sz/deep/medium.bin" },
                (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "sz/", MinimumSize = 500, MaximumSize = 5000 }, token).ConfigureAwait(false)).Select(m => m.Key),
                "size range");
        }

        private static async Task EnumerationCancellation(FileShareCaseContext ctx, CancellationToken token)
        {
            for (int i = 0; i < 20; i++)
            {
                await ctx.Blobs.WriteAsync("cancel/d" + (i % 4) + "/f" + i + ".txt", "text/plain", "x", token).ConfigureAwait(false);
            }

            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                int seen = 0;

                await Check.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    await foreach (BlobMetadata md in ctx.Blobs.EnumerateAsync(null, cts.Token).ConfigureAwait(false))
                    {
                        seen++;
                        if (seen == 3) cts.Cancel();
                    }
                }, "cancelled enumeration").ConfigureAwait(false);

                Check.True(seen >= 3 && seen < 20, "enumeration stopped early (" + seen + ")");
            }

            using (CancellationTokenSource pre = new CancellationTokenSource())
            {
                pre.Cancel();
                await Check.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    await foreach (BlobMetadata md in ctx.Blobs.EnumerateAsync(null, pre.Token).ConfigureAwait(false)) { }
                }, "pre-cancelled enumeration").ConfigureAwait(false);
            }
        }

        private static async Task SyncEnumerationParity(FileShareCaseContext ctx, CancellationToken token)
        {
            List<WriteRequest> requests = new List<WriteRequest>();
            for (int i = 0; i < 120; i++) requests.Add(new WriteRequest("parity/g" + (i % 7) + "/h" + (i % 3) + "/item-" + i + ".dat", "application/octet-stream", new byte[i]));
            requests.Add(new WriteRequest("parity/empty-folder/", "application/octet-stream", Array.Empty<byte>()));

            ctx.Blobs.MaxConcurrency = 8;
            await ctx.Blobs.WriteManyAsync(requests, token).ConfigureAwait(false);

            List<string> asyncKeys = (await TestData.EnumerateAsync(ctx.Blobs, null, token).ConfigureAwait(false)).Select(m => m.Key).ToList();
            List<string> syncKeys = ctx.Blobs.Enumerate().Select(m => m.Key).ToList();

            List<string> expectedFiles = requests.Where(r => !r.Key.EndsWith("/")).Select(r => r.Key).ToList();
            List<string> expectedFolders = TestData.AncestorFolders(expectedFiles);
            expectedFolders.Add("parity/empty-folder/");

            Check.Keys(expectedFiles.Concat(expectedFolders), asyncKeys, "async keys");
            Check.Keys(asyncKeys, syncKeys, "sync parity");
        }

        private static async Task EmptyRemovesFilesAndFolders(FileShareCaseContext ctx, CancellationToken token)
        {
            // Operates on the whole share, which is safe because managed servers are dedicated to this test run
            // and cases run one at a time.
            using (RawFileShareClient raw = ctx.CreateRawClient())
            {
                await raw.WriteAsync("empty-root/a.txt", "text/plain", "a", token).ConfigureAwait(false);
                await raw.WriteAsync("empty-root/b/c/d.txt", "text/plain", "d", token).ConfigureAwait(false);
                await raw.WriteAsync("empty-root/e/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
                await raw.WriteAsync("top.txt", "text/plain", "top", token).ConfigureAwait(false);

                EmptyResult result = await raw.EmptyAsync(token).ConfigureAwait(false);
                Check.True(result.Blobs.Any(b => b.Key == "empty-root/b/c/d.txt"), "nested file reported");
                Check.True(result.Blobs.Any(b => b.Key == "empty-root/b/c/" && b.IsFolder), "nested folder reported");
                Check.True(result.Blobs.Any(b => b.Key == "top.txt"), "top-level file reported");

                Check.Equal(0, (await TestData.EnumerateAsync(raw, null, token).ConfigureAwait(false)).Count, "share empty");
                Check.False(await raw.ExistsAsync("empty-root/", token).ConfigureAwait(false), "folders removed");
                Check.False(await raw.ExistsAsync("empty-root/e/", token).ConfigureAwait(false), "folder marker removed");
                Check.False(await ctx.ServerExistsAtRootAsync("empty-root", token).ConfigureAwait(false), "folders removed server-side");

                EmptyResult again = await raw.EmptyAsync(token).ConfigureAwait(false);
                Check.Equal(0, again.Blobs.Count, "empty on empty share");
            }
        }

        #endregion

        #region Concurrency

        private static List<FileShareCase> ConcurrencyCases()
        {
            return new List<FileShareCase>
            {
                new FileShareCase("ParallelWriteManyAndReads", "200 objects written with WriteManyAsync at concurrency 16 and read back in parallel", ParallelWriteManyAndReads),
                new FileShareCase("ParallelMixedOperations", "32 tasks interleave write, read, stream, metadata, exists, and delete on one client", ParallelMixedOperations),
                new FileShareCase("ParallelStreams", "16 read streams open simultaneously on one connection", ParallelStreams),
                new FileShareCase("EnumerateDuringWrites", "enumeration while writes are in flight completes without error", EnumerateDuringWrites),
                new FileShareCase("SameKeyContention", "concurrent overwrites of one key complete or fail cleanly and leave a full-length object (file share semantics: not atomic)", SameKeyContention),
                new FileShareCase("ManySequentialOperations", "1,000 sequential operations reuse one connection", ManySequentialOperations)
            };
        }

        private static async Task ParallelWriteManyAndReads(FileShareCaseContext ctx, CancellationToken token)
        {
            ctx.Blobs.MaxConcurrency = 16;
            List<WriteRequest> requests = new List<WriteRequest>();
            Dictionary<string, byte[]> expected = new Dictionary<string, byte[]>();

            for (int i = 0; i < 200; i++)
            {
                string key = "par/g" + (i % 10) + "/obj-" + i + ".bin";
                byte[] data = TestData.Pattern(1000 + i * 331, 100 + i);
                expected[key] = data;
                requests.Add(i % 2 == 0
                    ? new WriteRequest(key, "application/octet-stream", data)
                    : new WriteRequest(key, "application/octet-stream", data.Length, new MemoryStream(data)));
            }

            await ctx.Blobs.WriteManyAsync(requests, token).ConfigureAwait(false);

            await Task.WhenAll(expected.Select(async kvp =>
            {
                Check.Bytes(kvp.Value, await ctx.Blobs.GetAsync(kvp.Key, token).ConfigureAwait(false), kvp.Key);
            })).ConfigureAwait(false);

            DeleteManyResult deleted = await ctx.Blobs.DeleteManyAsync(expected.Keys, token).ConfigureAwait(false);
            Check.Equal(200, deleted.Deleted.Count(), "deleted count");
            Check.Equal(0, deleted.Failed.Count(), "failed deletes");
        }

        private static async Task ParallelMixedOperations(FileShareCaseContext ctx, CancellationToken token)
        {
            await Task.WhenAll(Enumerable.Range(0, 32).Select(async worker =>
            {
                for (int round = 0; round < 8; round++)
                {
                    string key = "mixed/w" + worker + "/r" + round + ".bin";
                    byte[] data = TestData.Pattern(512 + worker * 97 + round, worker * 1000 + round);

                    await ctx.Blobs.WriteAsync(key, "application/octet-stream", data, token).ConfigureAwait(false);
                    Check.True(await ctx.Blobs.ExistsAsync(key, token).ConfigureAwait(false), "exists " + key);
                    Check.Equal((long)data.Length, (await ctx.Blobs.GetMetadataAsync(key, token).ConfigureAwait(false)).ContentLength, "length " + key);
                    Check.Bytes(data, await ctx.Blobs.GetAsync(key, token).ConfigureAwait(false), "bytes " + key);

                    using (BlobData blob = await ctx.Blobs.GetStreamAsync(key, token).ConfigureAwait(false))
                    {
                        Check.Bytes(data, await TestData.ReadAllAsync(blob.Data, token).ConfigureAwait(false), "stream " + key);
                    }

                    if (round % 2 == 0)
                    {
                        await ctx.Blobs.DeleteAsync(key, token).ConfigureAwait(false);
                        Check.False(await ctx.Blobs.ExistsAsync(key, token).ConfigureAwait(false), "deleted " + key);
                    }
                }
            })).ConfigureAwait(false);

            List<BlobMetadata> surviving = await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "mixed/" }, token).ConfigureAwait(false);
            Check.Equal(32 * 4, TestData.FileKeys(surviving).Count, "surviving objects");
            Check.Equal(32, TestData.FolderKeys(surviving).Count, "worker folders");
        }

        private static async Task ParallelStreams(FileShareCaseContext ctx, CancellationToken token)
        {
            Dictionary<string, byte[]> expected = new Dictionary<string, byte[]>();
            for (int i = 0; i < 16; i++)
            {
                byte[] data = TestData.Pattern(256 * 1024 + i, 500 + i);
                expected["streams/s" + i + ".bin"] = data;
                await ctx.Blobs.WriteAsync("streams/s" + i + ".bin", "application/octet-stream", data, token).ConfigureAwait(false);
            }

            List<BlobData> open = new List<BlobData>();

            try
            {
                foreach (string key in expected.Keys) open.Add(await ctx.Blobs.GetStreamAsync(key, token).ConfigureAwait(false));

                byte[][] results = await Task.WhenAll(open.Select(b => TestData.ReadAllAsync(b.Data, token))).ConfigureAwait(false);
                int index = 0;
                foreach (string key in expected.Keys)
                {
                    Check.Bytes(expected[key], results[index++], key);
                }
            }
            finally
            {
                foreach (BlobData b in open) b.Dispose();
            }
        }

        private static async Task EnumerateDuringWrites(FileShareCaseContext ctx, CancellationToken token)
        {
            for (int i = 0; i < 50; i++) await ctx.Blobs.WriteAsync("during/base-" + i + ".txt", "text/plain", "x", token).ConfigureAwait(false);

            Task writer = Task.Run(async () =>
            {
                for (int i = 0; i < 100; i++) await ctx.Blobs.WriteAsync("during/new/n-" + i + ".txt", "text/plain", "y", token).ConfigureAwait(false);
            });

            for (int pass = 0; pass < 5; pass++)
            {
                List<BlobMetadata> listed = await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "during/" }, token).ConfigureAwait(false);
                Check.True(TestData.FileKeys(listed).Count >= 50, "at least the base objects (pass " + pass + ")");
            }

            await writer.ConfigureAwait(false);
            List<BlobMetadata> final = await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "during/" }, token).ConfigureAwait(false);
            Check.Equal(150, TestData.FileKeys(final).Count, "final object count");
            Check.Keys(new[] { "during/new/" }, TestData.FolderKeys(final), "final folders");
        }

        private static async Task SameKeyContention(FileShareCaseContext ctx, CancellationToken token)
        {
            byte[][] versions = Enumerable.Range(0, 8).Select(i => TestData.Pattern(64 * 1024, 900 + i)).ToArray();

            int succeeded = 0;

            await Task.WhenAll(versions.Select(async v =>
            {
                try
                {
                    await ctx.Blobs.WriteAsync("contended.bin", "application/octet-stream", v, token).ConfigureAwait(false);
                    Interlocked.Increment(ref succeeded);
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    // a sharing violation from a concurrent writer is acceptable
                }
            })).ConfigureAwait(false);

            // Writes happen in place, as with any file share client, so concurrent writers to one key are not atomic:
            // on NFS the result can interleave writers' bytes, and SMB may reject writers with a sharing violation.
            Check.True(succeeded > 0, "at least one concurrent write succeeded");
            byte[] final = await ctx.Blobs.GetAsync("contended.bin", token).ConfigureAwait(false);
            Check.Equal(64 * 1024, final.Length, "final object length");

            // once writers stop, a single write replaces the object completely
            await ctx.Blobs.WriteAsync("contended.bin", "application/octet-stream", versions[0], token).ConfigureAwait(false);
            Check.Bytes(versions[0], await ctx.Blobs.GetAsync("contended.bin", token).ConfigureAwait(false), "content after an uncontended write");
        }

        private static async Task ManySequentialOperations(FileShareCaseContext ctx, CancellationToken token)
        {
            for (int i = 0; i < 250; i++)
            {
                string key = "seq/" + (i % 25) + ".txt";
                await ctx.Blobs.WriteAsync(key, "text/plain", "v" + i, token).ConfigureAwait(false);
                Check.Equal("v" + i, Encoding.UTF8.GetString(await ctx.Blobs.GetAsync(key, token).ConfigureAwait(false)), "sequential read " + i);
                Check.True(await ctx.Blobs.ExistsAsync(key, token).ConfigureAwait(false), "sequential exists " + i);
                await ctx.Blobs.GetMetadataAsync(key, token).ConfigureAwait(false);
            }
        }

        #endregion

        #region Lifecycle

        private static List<FileShareCase> LifecycleCases(string protocol)
        {
            List<FileShareCase> cases = new List<FileShareCase>
            {
                new FileShareCase("LazyConnection", "constructing a client performs no I/O; an unreachable server fails validation", LazyConnection),
                new FileShareCase("ValidateConnectivity", "ValidateConnectivity succeeds against the server and is repeatable", ValidateConnectivity),
                new FileShareCase("ListShares", "ListShares includes the configured share", ListShares),
                new FileShareCase("DisposeSemantics", "operations after Dispose throw ObjectDisposedException; Dispose is idempotent", DisposeSemantics),
                new FileShareCase("DisposeAsyncSemantics", "DisposeAsync disconnects and is idempotent", DisposeAsyncSemantics),
                new FileShareCase("ReconnectAfterServerRestart", "the client reconnects transparently after the server restarts", ReconnectAfterServerRestart),
                new FileShareCase("LoggerReceivesMessages", "the Logger receives provider-prefixed messages", ctx => LoggerReceivesMessages(ctx, protocol)),
                new FileShareCase("PreCancelledOperations", "every operation honors an already-cancelled token", PreCancelledOperations),
                new FileShareCase("CancelDuringLargeWrite", "cancelling a large write throws and the client remains usable", CancelDuringLargeWrite),
                new FileShareCase("IndependentClients", "two clients observe each other's writes", IndependentClients)
            };

            if (protocol == "cifs")
            {
                cases.Add(new FileShareCase("WrongPassword", "a wrong password fails validation and operations", WrongPassword));
                cases.Add(new FileShareCase("MissingShare", "a missing share fails validation and operations", MissingShare));
            }
            else
            {
                cases.Add(new FileShareCase("MissingExport", "a missing export fails validation and operations", MissingExport));
            }

            return cases;
        }

        private static async Task LazyConnection(FileShareCaseContext ctx, CancellationToken token)
        {
            int closedPort = DockerCli.GetFreeTcpPort();

            using (RawFileShareClient unreachable = ctx.CreateRawClient(
                cifs => { cifs.Port = closedPort; cifs.ConnectTimeoutMs = 2000; },
                nfs => { nfs.Port = closedPort; nfs.MountPort = closedPort; nfs.ConnectTimeoutMs = 2000; nfs.ResponseTimeoutMs = 2000; }))
            {
                DateTime start = DateTime.UtcNow;
                Check.False(await unreachable.ValidateConnectivity(token).ConfigureAwait(false), "unreachable validation");
                Check.True(DateTime.UtcNow - start < TimeSpan.FromSeconds(30), "unreachable validation completes promptly");
                await Check.ThrowsAsync<Exception>(() => unreachable.GetAsync("anything.txt", token), "unreachable read").ConfigureAwait(false);
            }
        }

        private static async Task ValidateConnectivity(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient raw = ctx.CreateRawClient())
            {
                for (int i = 0; i < 3; i++)
                {
                    Check.True(await raw.ValidateConnectivity(token).ConfigureAwait(false), "validation " + i);
                }
            }
        }

        private static async Task ListShares(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient raw = ctx.CreateRawClient())
            {
                List<string> shares;
                string expected;

                if (raw.Inner is CifsBlobClient cifs)
                {
                    shares = await cifs.ListShares(token).ConfigureAwait(false);
                    expected = ctx.Options.CifsShare;
                }
                else
                {
                    shares = await ((NfsBlobClient)raw.Inner).ListShares(token).ConfigureAwait(false);
                    expected = ctx.Options.NfsShare;
                }

                Check.True(shares.Any(s => String.Equals(s.TrimEnd('/'), expected.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)),
                    "share list [" + String.Join(", ", shares) + "] contains " + expected);
            }
        }

        private static async Task DisposeSemantics(FileShareCaseContext ctx, CancellationToken token)
        {
            RawFileShareClient raw = ctx.CreateRawClient();
            await raw.WriteAsync(ctx.Prefix + "dispose.txt", "text/plain", "x", token).ConfigureAwait(false);
            ((IDisposable)raw).Dispose();
            ((IDisposable)raw).Dispose();

            await Check.ThrowsAsync<ObjectDisposedException>(() => raw.GetAsync(ctx.Prefix + "dispose.txt", token), "get after dispose").ConfigureAwait(false);
            await Check.ThrowsAsync<ObjectDisposedException>(() => raw.WriteAsync(ctx.Prefix + "dispose.txt", "text/plain", "y", token), "write after dispose").ConfigureAwait(false);
            await Check.ThrowsAsync<ObjectDisposedException>(() => raw.ExistsAsync(ctx.Prefix + "dispose.txt", token), "exists after dispose").ConfigureAwait(false);
            await Check.ThrowsAsync<ObjectDisposedException>(() => raw.ValidateConnectivity(token), "validate after dispose").ConfigureAwait(false);
            await Check.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                await foreach (BlobMetadata md in raw.EnumerateAsync(null, token).ConfigureAwait(false)) { }
            }, "enumerate after dispose").ConfigureAwait(false);

            // disposing a client that never connected is also fine
            RawFileShareClient unused = ctx.CreateRawClient();
            ((IDisposable)unused).Dispose();
        }

        private static async Task DisposeAsyncSemantics(FileShareCaseContext ctx, CancellationToken token)
        {
            RawFileShareClient raw = ctx.CreateRawClient();
            await raw.WriteAsync(ctx.Prefix + "dispose-async.txt", "text/plain", "x", token).ConfigureAwait(false);
            await ((IAsyncDisposable)raw).DisposeAsync().ConfigureAwait(false);
            await ((IAsyncDisposable)raw).DisposeAsync().ConfigureAwait(false);
            ((IDisposable)raw).Dispose();
            await Check.ThrowsAsync<ObjectDisposedException>(() => raw.GetAsync(ctx.Prefix + "dispose-async.txt", token), "get after DisposeAsync").ConfigureAwait(false);

            using (RawFileShareClient fresh = ctx.CreateRawClient())
            {
                Check.Equal("x", Encoding.UTF8.GetString(await fresh.GetAsync(ctx.Prefix + "dispose-async.txt", token).ConfigureAwait(false)), "data persisted after DisposeAsync");
            }
        }

        private static async Task ReconnectAfterServerRestart(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient raw = ctx.CreateRawClient())
            {
                await raw.WriteAsync(ctx.Prefix + "restart/before.txt", "text/plain", "before", token).ConfigureAwait(false);
                Check.True(await raw.ValidateConnectivity(token).ConfigureAwait(false), "connected before restart");

                await ctx.Server.RestartAsync(token).ConfigureAwait(false);

                Check.Equal("before", Encoding.UTF8.GetString(await raw.GetAsync(ctx.Prefix + "restart/before.txt", token).ConfigureAwait(false)), "read after restart");
                await raw.WriteAsync(ctx.Prefix + "restart/after.txt", "text/plain", "after", token).ConfigureAwait(false);
                Check.Equal("after", Encoding.UTF8.GetString(await raw.GetAsync(ctx.Prefix + "restart/after.txt", token).ConfigureAwait(false)), "write after restart");
                Check.Equal(2, (await TestData.EnumerateAsync(raw, new EnumerationFilter { Prefix = ctx.Prefix + "restart/" }, token).ConfigureAwait(false)).Count, "enumerate after restart");
            }
        }

        private static async Task LoggerReceivesMessages(FileShareCaseContext ctx, string protocol)
        {
            List<string> messages = new List<string>();

            using (RawFileShareClient raw = ctx.CreateRawClient())
            {
                raw.Inner.Logger = m => { lock (messages) messages.Add(m); };
                await raw.ValidateConnectivity(CancellationToken.None).ConfigureAwait(false);
                await TestData.EnumerateAsync(raw, new EnumerationFilter { Prefix = ctx.Prefix }, CancellationToken.None).ConfigureAwait(false);
            }

            string header = protocol == "cifs" ? "[CifsBlobClient] " : "[NfsBlobClient] ";
            Check.True(messages.Count > 0, "log messages received");
            Check.True(messages.All(m => m.StartsWith(header, StringComparison.Ordinal)), "log messages prefixed with " + header.Trim());
            Check.True(messages.Any(m => m.Contains("connecting")), "connection logged");
        }

        private static async Task PreCancelledOperations(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("precancel.txt", "text/plain", "x", token).ConfigureAwait(false);

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.Cancel();
                CancellationToken c = cts.Token;

                await Check.ThrowsAsync<OperationCanceledException>(() => ctx.Blobs.GetAsync("precancel.txt", c), "GetAsync").ConfigureAwait(false);
                await Check.ThrowsAsync<OperationCanceledException>(() => ctx.Blobs.GetStreamAsync("precancel.txt", c), "GetStreamAsync").ConfigureAwait(false);
                await Check.ThrowsAsync<OperationCanceledException>(() => ctx.Blobs.GetMetadataAsync("precancel.txt", c), "GetMetadataAsync").ConfigureAwait(false);
                await Check.ThrowsAsync<OperationCanceledException>(() => ctx.Blobs.ExistsAsync("precancel.txt", c), "ExistsAsync").ConfigureAwait(false);
                await Check.ThrowsAsync<OperationCanceledException>(() => ctx.Blobs.WriteAsync("precancel.txt", "text/plain", "y", c), "WriteAsync").ConfigureAwait(false);
                await Check.ThrowsAsync<OperationCanceledException>(() => ctx.Blobs.DeleteAsync("precancel.txt", c), "DeleteAsync").ConfigureAwait(false);
                await Check.ThrowsAsync<OperationCanceledException>(() => ctx.Blobs.WriteManyAsync(new List<WriteRequest> { new WriteRequest("precancel-2.txt", "text/plain", new byte[] { 1 }) }, c), "WriteManyAsync").ConfigureAwait(false);
            }

            Check.Equal("x", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("precancel.txt", token).ConfigureAwait(false)), "unchanged by cancelled operations");
        }

        private static async Task CancelDuringLargeWrite(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient raw = ctx.CreateRawClient())
            {
                using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                using (SlowReadStream slow = new SlowReadStream(64 * 1024 * 1024, 64 * 1024, TimeSpan.FromMilliseconds(20)))
                {
                    cts.CancelAfter(TimeSpan.FromMilliseconds(750));
                    await Check.ThrowsAsync<OperationCanceledException>(
                        () => raw.WriteAsync(ctx.Prefix + "cancelled-large.bin", "application/octet-stream", slow.Length, slow, cts.Token),
                        "cancelled large write").ConfigureAwait(false);
                }

                await raw.WriteAsync(ctx.Prefix + "after-cancel.txt", "text/plain", "still usable", token).ConfigureAwait(false);
                Check.Equal("still usable", Encoding.UTF8.GetString(await raw.GetAsync(ctx.Prefix + "after-cancel.txt", token).ConfigureAwait(false)), "client usable after cancellation");

                // the partially written object can be overwritten and deleted
                await raw.WriteAsync(ctx.Prefix + "cancelled-large.bin", "text/plain", "replaced", token).ConfigureAwait(false);
                Check.Equal("replaced", Encoding.UTF8.GetString(await raw.GetAsync(ctx.Prefix + "cancelled-large.bin", token).ConfigureAwait(false)), "overwrite after cancellation");
            }
        }

        private static async Task IndependentClients(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient a = ctx.CreateRawClient())
            using (RawFileShareClient b = ctx.CreateRawClient())
            {
                await a.WriteAsync(ctx.Prefix + "shared.txt", "text/plain", "from a", token).ConfigureAwait(false);
                Check.Equal("from a", Encoding.UTF8.GetString(await b.GetAsync(ctx.Prefix + "shared.txt", token).ConfigureAwait(false)), "b reads a");
                await b.WriteAsync(ctx.Prefix + "shared.txt", "text/plain", "from b", token).ConfigureAwait(false);
                Check.Equal("from b", Encoding.UTF8.GetString(await a.GetAsync(ctx.Prefix + "shared.txt", token).ConfigureAwait(false)), "a reads b");
                await a.DeleteAsync(ctx.Prefix + "shared.txt", token).ConfigureAwait(false);
                Check.False(await b.ExistsAsync(ctx.Prefix + "shared.txt", token).ConfigureAwait(false), "b sees delete");
            }
        }

        private static async Task WrongPassword(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient bad = ctx.CreateRawClient(cifs => cifs.Password = "definitely-not-the-password", null))
            {
                Check.False(await bad.ValidateConnectivity(token).ConfigureAwait(false), "wrong password validation");
                Exception e = await Check.ThrowsAsync<Exception>(() => bad.GetAsync("anything.txt", token), "wrong password read").ConfigureAwait(false);
                Check.False(e is KeyNotFoundException, "authentication failure is not reported as not found");
            }
        }

        private static async Task MissingShare(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient bad = ctx.CreateRawClient(cifs => cifs.Share = "no-such-share-" + Guid.NewGuid().ToString("N").Substring(0, 6), null))
            {
                Check.False(await bad.ValidateConnectivity(token).ConfigureAwait(false), "missing share validation");
                await Check.ThrowsAsync<Exception>(() => bad.WriteAsync("x.txt", "text/plain", "x", token), "missing share write").ConfigureAwait(false);
            }
        }

        private static async Task MissingExport(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient bad = ctx.CreateRawClient(null, nfs => nfs.Share = "/no-such-export-" + Guid.NewGuid().ToString("N").Substring(0, 6)))
            {
                Check.False(await bad.ValidateConnectivity(token).ConfigureAwait(false), "missing export validation");
                await Check.ThrowsAsync<Exception>(() => bad.WriteAsync("x.txt", "text/plain", "x", token), "missing export write").ConfigureAwait(false);
            }
        }

        #endregion

        #region Interop

        private static List<FileShareCase> InteropCases(string target)
        {
            return new List<FileShareCase>
            {
                new FileShareCase("ServerSeesClientWrites", "objects written by the client are byte-identical on the server's file system", ServerSeesClientWrites),
                new FileShareCase("ClientSeesServerWrites", "files and folders created on the server are visible to the client", ClientSeesServerWrites),
                new FileShareCase("ServerSeesClientDeletes", "deletes by the client remove files and folders on the server", ServerSeesClientDeletes)
            };
        }

        private static async Task ServerSeesClientWrites(FileShareCaseContext ctx, CancellationToken token)
        {
            byte[] data = TestData.Pattern(2 * 1024 * 1024 + 11, 42);
            await ctx.Blobs.WriteAsync("interop/data.bin", "application/octet-stream", data, token).ConfigureAwait(false);
            await ctx.Blobs.WriteAsync("interop/text.txt", "text/plain", "hello server", token).ConfigureAwait(false);

            Check.Equal((long)data.Length, await ctx.ServerSizeAsync("interop/data.bin", token).ConfigureAwait(false), "server size");
            Check.Equal(TestData.Sha256(data), await ctx.ServerSha256Async("interop/data.bin", token).ConfigureAwait(false), "server sha256");
            Check.Equal(TestData.Sha256(Encoding.UTF8.GetBytes("hello server")), await ctx.ServerSha256Async("interop/text.txt", token).ConfigureAwait(false), "server text sha256");
            Check.True(await ctx.ServerIsDirectoryAsync("interop", token).ConfigureAwait(false), "server directory");
        }

        private static async Task ClientSeesServerWrites(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.ServerWriteTextAsync("from-server/one.txt", "server one", token).ConfigureAwait(false);
            await ctx.ServerWriteTextAsync("from-server/nested/two.txt", "server two", token).ConfigureAwait(false);
            await ctx.ServerCreateDirectoryAsync("from-server/empty", token).ConfigureAwait(false);

            Check.Equal("server one", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("from-server/one.txt", token).ConfigureAwait(false)), "client reads server file");
            Check.Equal(10L, (await ctx.Blobs.GetMetadataAsync("from-server/nested/two.txt", token).ConfigureAwait(false)).ContentLength, "client metadata of server file");
            Check.Keys(
                new[] { "from-server/one.txt", "from-server/nested/two.txt", "from-server/nested/", "from-server/empty/" },
                (await TestData.EnumerateAsync(ctx.Blobs, new EnumerationFilter { Prefix = "from-server/" }, token).ConfigureAwait(false)).Select(m => m.Key),
                "client enumerates server tree");
            Check.True(await ctx.Blobs.ExistsAsync("from-server/empty/", token).ConfigureAwait(false), "client sees server folder");

            await ctx.ServerWriteTextAsync("from-server/one.txt", "changed", token).ConfigureAwait(false);
            Check.Equal("changed", Encoding.UTF8.GetString(await ctx.Blobs.GetAsync("from-server/one.txt", token).ConfigureAwait(false)), "client reads server change");
        }

        private static async Task ServerSeesClientDeletes(FileShareCaseContext ctx, CancellationToken token)
        {
            await ctx.Blobs.WriteAsync("gone/file.txt", "text/plain", "x", token).ConfigureAwait(false);
            await ctx.Blobs.WriteAsync("gone/folder/", "application/octet-stream", Array.Empty<byte>(), token).ConfigureAwait(false);
            Check.True(await ctx.ServerExistsAsync("gone/file.txt", token).ConfigureAwait(false), "server file exists");

            await ctx.Blobs.DeleteAsync("gone/file.txt", token).ConfigureAwait(false);
            await ctx.Blobs.DeleteAsync("gone/folder/", token).ConfigureAwait(false);

            Check.False(await ctx.ServerExistsAsync("gone/file.txt", token).ConfigureAwait(false), "server file removed");
            Check.False(await ctx.ServerExistsAsync("gone/folder", token).ConfigureAwait(false), "server folder removed");
        }

        #endregion

        #region Smb

        private static List<FileShareCase> SmbCases()
        {
            return new List<FileShareCase>
            {
                new FileShareCase("SigningRequired", "operations succeed with SMB signing required", SigningRequired),
                new FileShareCase("EncryptionNotPreferred", "operations succeed with SMB 3.x encryption not preferred", EncryptionNotPreferred),
                new FileShareCase("DomainInUsername", "a domain\\username credential authenticates", DomainInUsername),
                new FileShareCase("SmbGenerateUrl", "GenerateUrl returns a UNC path", SmbGenerateUrl),
                new FileShareCase("SingleConnectionConcurrency", "with MaxConnections = 1, concurrent operations share one serialized connection correctly", ctx => ConnectionCountConcurrency(ctx, 1)),
                new FileShareCase("ManyConnectionsConcurrency", "with MaxConnections = 16, concurrent operations spread across connections correctly", ctx => ConnectionCountConcurrency(ctx, 16)),
                new FileShareCase("SmbReservedCharacters", "keys containing SMB-reserved characters are rejected before any I/O", SmbForbiddenCharacters)
            };
        }

        private static async Task RoundTripWith(FileShareCaseContext ctx, RawFileShareClient client, string name, CancellationToken token)
        {
            byte[] data = TestData.Pattern(300 * 1024, name.Length);
            await client.WriteAsync(ctx.Prefix + name, "application/octet-stream", data, token).ConfigureAwait(false);
            Check.Bytes(data, await client.GetAsync(ctx.Prefix + name, token).ConfigureAwait(false), name);
            Check.True((await TestData.EnumerateAsync(client, new EnumerationFilter { Prefix = ctx.Prefix + name }, token).ConfigureAwait(false)).Count == 1, name + " enumerated");
            await client.DeleteAsync(ctx.Prefix + name, token).ConfigureAwait(false);
        }

        private static async Task SigningRequired(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient client = ctx.CreateRawClient(cifs => cifs.RequireSigning = true, null))
            {
                await RoundTripWith(ctx, client, "signed.bin", token).ConfigureAwait(false);
            }
        }

        private static async Task EncryptionNotPreferred(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient client = ctx.CreateRawClient(cifs => cifs.PreferEncryption = false, null))
            {
                await RoundTripWith(ctx, client, "unencrypted.bin", token).ConfigureAwait(false);
            }
        }

        private static async Task DomainInUsername(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient client = ctx.CreateRawClient(cifs =>
            {
                cifs.Domain = "";
                cifs.Username = "WORKGROUP\\" + ctx.Options.CifsUsername;
            }, null))
            {
                Check.True(await client.ValidateConnectivity(token).ConfigureAwait(false), "domain\\username validation");
                await RoundTripWith(ctx, client, "domain-user.bin", token).ConfigureAwait(false);
            }
        }

        private static async Task ConnectionCountConcurrency(FileShareCaseContext ctx, int connections)
        {
            CancellationToken token = CancellationToken.None;

            using (RawFileShareClient client = ctx.CreateRawClient(cifs => cifs.MaxConnections = connections, null))
            {
                client.Inner.MaxConcurrency = 16;
                List<WriteRequest> requests = Enumerable.Range(0, 64)
                    .Select(i => new WriteRequest(ctx.Prefix + "conn" + connections + "/o" + i + ".bin", "application/octet-stream", TestData.Pattern(20000 + i, i)))
                    .ToList();

                await client.WriteManyAsync(requests, token).ConfigureAwait(false);

                await Task.WhenAll(Enumerable.Range(0, 64).Select(async i =>
                {
                    Check.Bytes(TestData.Pattern(20000 + i, i), await client.GetAsync(ctx.Prefix + "conn" + connections + "/o" + i + ".bin", token).ConfigureAwait(false), "object " + i);
                })).ConfigureAwait(false);

                Check.Equal(64, (await TestData.EnumerateAsync(client, new EnumerationFilter { Prefix = ctx.Prefix + "conn" + connections + "/" }, token).ConfigureAwait(false)).Count, "object count");
            }
        }

        private static Task SmbGenerateUrl(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient client = ctx.CreateRawClient())
            {
                Check.Equal("\\\\127.0.0.1\\" + ctx.Options.CifsShare + "\\dir\\file.txt", client.GenerateUrl("dir/file.txt", token), "UNC url");
            }

            return Task.CompletedTask;
        }

        private static async Task SmbForbiddenCharacters(FileShareCaseContext ctx, CancellationToken token)
        {
            foreach (string name in new[] { "bad<name.txt", "bad>name.txt", "bad:name.txt", "stream:$DATA", "bad\"name.txt", "bad|name.txt", "bad?name.txt", "bad*name.txt", "dir:x/file.txt", "tab\tname.txt", "nul\u0001name.txt" })
            {
                await Check.ThrowsAsync<ArgumentException>(() => ctx.Blobs.WriteAsync(name, "text/plain", "x", token), "write '" + name + "'").ConfigureAwait(false);
                await Check.ThrowsAsync<ArgumentException>(() => ctx.Blobs.GetAsync(name, token), "get '" + name + "'").ConfigureAwait(false);
                await Check.ThrowsAsync<ArgumentException>(() => ctx.Blobs.ExistsAsync(name, token), "exists '" + name + "'").ConfigureAwait(false);
            }

            Check.False(await ctx.ServerExistsAsync("bad", token).ConfigureAwait(false), "no alternate data stream host file created");

            Check.Equal(0, (await TestData.EnumerateAsync(ctx.Blobs, null, token).ConfigureAwait(false)).Count, "no objects created");
        }

        #endregion

        #region Nfs

        private static List<FileShareCase> NfsCases(string target)
        {
            List<FileShareCase> cases = new List<FileShareCase>
            {
                new FileShareCase("WriteStabilityUnstable", "UNSTABLE writes are committed and round trip", ctx => WriteStability(ctx, NfsWriteStabilityEnum.Unstable)),
                new FileShareCase("WriteStabilityDataSync", "DATA_SYNC writes round trip", ctx => WriteStability(ctx, NfsWriteStabilityEnum.DataSync)),
                new FileShareCase("WriteStabilityFileSync", "FILE_SYNC writes round trip", ctx => WriteStability(ctx, NfsWriteStabilityEnum.FileSync)),
                new FileShareCase("NfsGenerateUrl", "GenerateUrl returns an RFC 2224 nfs:// URL", NfsGenerateUrl),
                new FileShareCase("ExplicitMountPort", "an explicitly configured MOUNT port is used", ExplicitMountPort),
                new FileShareCase("NamesWithReservedWindowsCharacters", "names containing characters reserved on Windows round trip over NFS", NamesWithReservedWindowsCharacters)
            };

            if (FileShareServers.RequiresDocker(target))
            {
                cases.Add(new FileShareCase("AuthSysIdentityApplied", "files are created with the configured AUTH_SYS uid and gid", AuthSysIdentityApplied));
            }

            if (target == FileShareServers.NfsKnfsd)
            {
                cases.Add(new FileShareCase("PortmapperDiscovery", "the MOUNT port is discovered through the portmapper", PortmapperDiscovery));
                cases.Add(new FileShareCase("PortmapperUnreachable", "an unreachable portmapper fails validation without hanging", PortmapperUnreachable));
            }

            return cases;
        }

        private static async Task WriteStability(FileShareCaseContext ctx, NfsWriteStabilityEnum stability)
        {
            CancellationToken token = CancellationToken.None;

            using (RawFileShareClient client = ctx.CreateRawClient(null, nfs => nfs.WriteStability = stability))
            {
                byte[] data = TestData.Pattern(3 * 1024 * 1024 + 1, (int)stability);
                string key = ctx.Prefix + "stability-" + stability + ".bin";
                await client.WriteAsync(key, "application/octet-stream", data, token).ConfigureAwait(false);
                Check.Bytes(data, await client.GetAsync(key, token).ConfigureAwait(false), "stability " + stability);
                Check.Equal(TestData.Sha256(data), await ctx.ServerSha256Async("stability-" + stability + ".bin", token).ConfigureAwait(false), "server copy for " + stability);

                using (MemoryStream ms = new MemoryStream(data))
                {
                    await client.WriteAsync(key, "application/octet-stream", 1000, ms, token).ConfigureAwait(false);
                }

                Check.Equal(1000L, await ctx.ServerSizeAsync("stability-" + stability + ".bin", token).ConfigureAwait(false), "truncated by stream write with " + stability);
            }
        }

        private static Task NfsGenerateUrl(FileShareCaseContext ctx, CancellationToken token)
        {
            using (RawFileShareClient client = ctx.CreateRawClient())
            {
                string expected = "nfs://127.0.0.1" + (ctx.Options.NfsPort != 2049 ? ":" + ctx.Options.NfsPort : "") + ctx.Options.NfsShare.TrimEnd('/') + "/dir/file.txt";
                Check.Equal(expected, client.GenerateUrl("dir/file.txt", token), "nfs url");
            }

            return Task.CompletedTask;
        }

        private static async Task ExplicitMountPort(FileShareCaseContext ctx, CancellationToken token)
        {
            int mountPort = ctx.Server is NfsDockerServer docker ? docker.MountPort : ((OpenNfsEphemeralServer)ctx.Server).MountPort;

            using (RawFileShareClient client = ctx.CreateRawClient(null, nfs => nfs.MountPort = mountPort))
            {
                Check.True(await client.ValidateConnectivity(token).ConfigureAwait(false), "explicit mount port");
            }

            int wrongPort = DockerCli.GetFreeTcpPort();
            using (RawFileShareClient client = ctx.CreateRawClient(null, nfs => { nfs.MountPort = wrongPort; nfs.ConnectTimeoutMs = 2000; nfs.ResponseTimeoutMs = 2000; }))
            {
                Check.False(await client.ValidateConnectivity(token).ConfigureAwait(false), "wrong mount port");
            }
        }

        private static async Task NamesWithReservedWindowsCharacters(FileShareCaseContext ctx, CancellationToken token)
        {
            if (ctx.Server.LocalSharePath != null && OperatingSystem.IsWindows())
            {
                // the in-process server stores data on the Windows file system, which reserves these characters
                return;
            }

            string[] names = { "colon:name.txt", "pipe|name.txt", "question?name.txt", "star*name.txt", "angle<>name.txt", "quote\"name.txt", "back\\slash-is-a-separator.txt" };

            foreach (string name in names.Take(6))
            {
                await ctx.Blobs.WriteAsync(name, "text/plain", name, token).ConfigureAwait(false);
                Check.Equal(name, Encoding.UTF8.GetString(await ctx.Blobs.GetAsync(name, token).ConfigureAwait(false)), "content of '" + name + "'");
            }

            Check.Keys(names.Take(6), (await TestData.EnumerateAsync(ctx.Blobs, null, token).ConfigureAwait(false)).Select(m => m.Key), "reserved-name enumeration");
        }

        private static async Task AuthSysIdentityApplied(FileShareCaseContext ctx, CancellationToken token)
        {
            NfsDockerServer server = (NfsDockerServer)ctx.Server;

            // folders created by root are not writable by other users, so give the unprivileged user a writable folder
            await ctx.ServerCreateDirectoryAsync("identity", token).ConfigureAwait(false);

            using (RawFileShareClient client = ctx.CreateRawClient(null, nfs => { nfs.UserId = 1234; nfs.GroupId = 5678; }))
            {
                await client.WriteAsync(ctx.Prefix + "identity/owned.txt", "text/plain", "owned", token).ConfigureAwait(false);
                await client.WriteAsync(ctx.Prefix + "identity/sub/nested.txt", "text/plain", "nested", token).ConfigureAwait(false);
            }

            Check.Equal("1234:5678", await server.GetOwnerAsync(ctx.Prefix + "identity/owned.txt", token).ConfigureAwait(false), "file owner");
            Check.Equal("1234:5678", await server.GetOwnerAsync(ctx.Prefix + "identity/sub", token).ConfigureAwait(false), "created folder owner");
            Check.Equal("1234:5678", await server.GetOwnerAsync(ctx.Prefix + "identity/sub/nested.txt", token).ConfigureAwait(false), "nested file owner");

            using (RawFileShareClient other = ctx.CreateRawClient(null, nfs => { nfs.UserId = 4321; nfs.GroupId = 8765; }))
            {
                // POSIX permissions are enforced for other users: the folder created by 1234 is not writable by 4321
                await Check.ThrowsAsync<Exception>(() => other.WriteAsync(ctx.Prefix + "identity/sub/denied.txt", "text/plain", "x", token), "write by another user").ConfigureAwait(false);
            }
        }

        private static async Task PortmapperDiscovery(FileShareCaseContext ctx, CancellationToken token)
        {
            Check.Equal(0, ctx.Options.NfsMountPort, "target relies on discovery");

            using (RawFileShareClient client = ctx.CreateRawClient())
            {
                Check.True(await client.ValidateConnectivity(token).ConfigureAwait(false), "discovered mount port");
                await RoundTripWith(ctx, client, "discovered.bin", token).ConfigureAwait(false);
            }
        }

        private static async Task PortmapperUnreachable(FileShareCaseContext ctx, CancellationToken token)
        {
            int closedPort = DockerCli.GetFreeTcpPort();

            using (RawFileShareClient client = ctx.CreateRawClient(null, nfs => { nfs.MountPort = 0; nfs.PortmapperPort = closedPort; nfs.ConnectTimeoutMs = 2000; nfs.ResponseTimeoutMs = 2000; }))
            {
                DateTime start = DateTime.UtcNow;
                Check.False(await client.ValidateConnectivity(token).ConfigureAwait(false), "unreachable portmapper");
                Check.True(DateTime.UtcNow - start < TimeSpan.FromSeconds(30), "fails promptly");
            }
        }

        #endregion
    }
}
