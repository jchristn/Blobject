namespace Test.Shared.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.CIFS;
    using Blobject.Core;
    using Blobject.Disk;
    using Blobject.NFS;
    using Test.Shared.FileShare;
    using Touchstone.Core;

    /// <summary>
    /// Telemetry test descriptors proving that Blobject emits the documented spans and metrics, including failure paths.
    /// Cases must run sequentially with respect to each other because some toggle process-wide telemetry settings;
    /// every host runs a suite's cases sequentially.  Spans and measurements are filtered to each case's own trace.
    /// </summary>
    public static class TelemetrySuites
    {
        #region Public-Members

        /// <summary>
        /// All telemetry suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return Build(); }
        }

        #endregion

        #region Private-Members

        private const string _Suite = "Telemetry";
        private const string _FileShareSuite = "TelemetryFileShare";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the telemetry suites.
        /// </summary>
        /// <returns>Suites.</returns>
        public static IReadOnlyList<TestSuiteDescriptor> Build()
        {
            return new List<TestSuiteDescriptor>
            {
                new TestSuiteDescriptor(
                    suiteId: _Suite,
                    displayName: "Telemetry",
                    cases: new List<TestCaseDescriptor>
                    {
                        Case(_Suite, "StableNames", "meter and activity source names are stable", StableNames),
                        Case(_Suite, "BuildInfo", "build info gauge reports the library version", BuildInfo),
                        Case(_Suite, "DiskOperationSpans", "disk operations emit spans, durations, and bytes", DiskOperationSpans),
                        Case(_Suite, "OverloadRecordedOnce", "delegating write overloads are recorded once", OverloadRecordedOnce),
                        Case(_Suite, "NotFoundOutcome", "missing objects report not_found with error status", NotFoundOutcome),
                        Case(_Suite, "ErrorOutcome", "failures report error.type, error status, and the errors counter", ErrorOutcome),
                        Case(_Suite, "CancelledOutcome", "cancellation reports cancelled and is not counted as an error", CancelledOutcome),
                        Case(_Suite, "CustomProvider", "clients without an override report the custom provider", CustomProvider),
                        Case(_Suite, "KeysOmittedByDefault", "object keys are omitted from spans unless RecordKeys is set", KeysOmittedByDefault),
                        Case(_Suite, "NoHighCardinalityMetricLabels", "metric labels never carry keys or containers", NoHighCardinalityMetricLabels),
                        Case(_Suite, "EnumerateAsyncSpan", "async enumeration emits one span and counts objects", EnumerateAsyncSpan),
                        Case(_Suite, "EnumerateSyncSpan", "sync enumeration emits one span and counts objects", EnumerateSyncSpan),
                        Case(_Suite, "EnumerateAbandoned", "abandoned enumeration still ends its span", EnumerateAbandoned),
                        Case(_Suite, "EnumerateFailure", "enumeration failure ends its span with an error", EnumerateFailure),
                        Case(_Suite, "WriteManyBulk", "WriteManyAsync emits child spans, queue waits, slots, and item counts", WriteManyBulk),
                        Case(_Suite, "DeleteManyItemFailures", "DeleteManyAsync counts failed items", DeleteManyItemFailures),
                        Case(_Suite, "EmptyBulk", "EmptyAsync nests enumerate and delete spans", EmptyBulk),
                        Case(_Suite, "CopyPipeline", "BlobCopy emits a job span, stage spans, stage metrics, and last success", CopyPipeline),
                        Case(_Suite, "CopyFailure", "BlobCopy failure reports the failing stage and job outcome", CopyFailure),
                        Case(_Suite, "RetryAfterReset", "transport failures record resets, retries, and span events", RetryAfterReset),
                        Case(_Suite, "ActiveReturnsToZero", "in-flight operations return to zero", ActiveReturnsToZero),
                        Case(_Suite, "DisabledEmitsNothing", "BlobjectTelemetry.Enabled = false emits nothing", DisabledEmitsNothing),
                        Case(_Suite, "NoListenerDoesNotThrow", "operations succeed with no listener attached", NoListenerDoesNotThrow),
                        Case(_Suite, "ThrowingListenerDoesNotBreak", "a throwing listener never breaks operations", ThrowingListenerDoesNotBreak)
                    }),
                new TestSuiteDescriptor(
                    suiteId: _FileShareSuite,
                    displayName: "Telemetry (CIFS/NFS connections)",
                    cases: new List<TestCaseDescriptor>
                    {
                        Case(_FileShareSuite, "CifsConnectionMetrics", "CIFS emits connect spans, pool capacity, and open connections", CifsConnectionMetrics),
                        Case(_FileShareSuite, "NfsConnectionMetrics", "NFS emits connect spans, pool capacity, and open connections", NfsConnectionMetrics),
                        Case(_FileShareSuite, "CifsReconnectAfterRestart", "CIFS reconnect after a server restart is visible", CifsReconnectAfterRestart),
                        Case(_FileShareSuite, "CifsConnectFailure", "CIFS connection failure reports connect and operation errors", CifsConnectFailure),
                        Case(_FileShareSuite, "NfsConnectFailure", "NFS connection failure reports connect and operation errors", NfsConnectFailure)
                    })
            };
        }

        #endregion

        #region Private-Methods

        private static TestCaseDescriptor Case(string suiteId, string caseId, string displayName, Func<CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor(suiteId, caseId, displayName, executeAsync);
        }

        private static Dictionary<string, string> Tags(params string[] pairs)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();
            for (int i = 0; i + 1 < pairs.Length; i += 2) ret[pairs[i]] = pairs[i + 1];
            return ret;
        }

        private static string Tag(Activity activity, string key)
        {
            object value = activity.GetTagItem(key);
            return value != null ? value.ToString() : null;
        }

        private static void Require(bool condition, string message, TelemetryCollector collector = null)
        {
            if (!condition) throw new InvalidOperationException(message + (collector != null ? " | " + collector.Describe() : ""));
        }

        private static string NewDirectory()
        {
            string dir = Path.Combine(Path.GetTempPath(), "blobject-telemetry-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DeleteDirectory(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }

        private static async Task WithDisk(Func<DiskBlobClient, Task> action)
        {
            string dir = NewDirectory();

            try
            {
                using (DiskBlobClient client = new DiskBlobClient(new DiskSettings(dir)))
                {
                    await action(client).ConfigureAwait(false);
                }
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }

        private static Task StableNames(CancellationToken token)
        {
            Require(BlobjectTelemetryNames.MeterName == "Blobject", "meter name");
            Require(BlobjectTelemetryNames.ActivitySourceName == "Blobject", "activity source name");
            Require(BlobjectTelemetryNames.OperationDuration == "blobject.operation.duration", "operation duration name");
            return Task.CompletedTask;
        }

        private static Task BuildInfo(CancellationToken token)
        {
            // the gauge is registered when Blobject is first used
            string version = BlobjectTelemetry.Version;

            using (TelemetryCollector collector = new TelemetryCollector())
            {
                collector.CollectObservable();
                List<RecordedMeasurement> info = collector.MeasurementsOf(BlobjectTelemetryNames.BuildInfo);
                Require(info.Count == 1, "one build info measurement", collector);
                Require(info[0].Value == 1, "build info value is 1", collector);
                Require(info[0].Tag(BlobjectTelemetryNames.AttributeVersion) == version, "build info version", collector);
                Require(!String.IsNullOrEmpty(BlobjectTelemetry.Version) && BlobjectTelemetry.Version != "unknown", "version resolved");
            }

            return Task.CompletedTask;
        }

        private static async Task DiskOperationSpans(CancellationToken token)
        {
            await WithDisk(async client =>
            {
                byte[] payload = Encoding.UTF8.GetBytes("hello telemetry");

                using (TelemetryCollector collector = new TelemetryCollector())
                {
                    await client.WriteAsync("a/b.txt", "text/plain", payload, token).ConfigureAwait(false);
                    byte[] read = await client.GetAsync("a/b.txt", token).ConfigureAwait(false);
                    using (BlobData data = await client.GetStreamAsync("a/b.txt", token).ConfigureAwait(false)) { }
                    await client.GetMetadataAsync("a/b.txt", token).ConfigureAwait(false);
                    bool exists = await client.ExistsAsync("a/b.txt", token).ConfigureAwait(false);
                    bool valid = await client.ValidateConnectivity(token).ConfigureAwait(false);
                    await client.DeleteAsync("a/b.txt", token).ConfigureAwait(false);

                    Require(read.Length == payload.Length && exists && valid, "operations succeeded");

                    foreach (string op in new[] { "write", "get", "get_stream", "get_metadata", "exists", "validate_connectivity", "delete" })
                    {
                        Activity span = collector.Span("disk " + op);
                        Require(span.Kind == ActivityKind.Internal, "disk spans are internal: " + op, collector);
                        Require(span.Status == ActivityStatusCode.Ok, "status ok: " + op, collector);
                        Require(span.ParentSpanId == collector.RootSpanId, "span parented to caller: " + op, collector);
                        Require(Tag(span, BlobjectTelemetryNames.AttributeProvider) == "disk", "provider tag: " + op, collector);
                        Require(Tag(span, BlobjectTelemetryNames.AttributeOutcome) == "success", "outcome tag: " + op, collector);
                        Require(!String.IsNullOrEmpty(Tag(span, BlobjectTelemetryNames.AttributeContainer)), "container tag: " + op, collector);

                        List<RecordedMeasurement> durations = collector.MeasurementsOf(
                            BlobjectTelemetryNames.OperationDuration,
                            Tags("blobject.provider", "disk", "blobject.operation", op, "blobject.outcome", "success"));
                        Require(durations.Count == 1, "one duration measurement: " + op, collector);
                        Require(durations[0].Value >= 0, "non-negative duration: " + op, collector);
                    }

                    Require(collector.Sum(BlobjectTelemetryNames.IoBytes, Tags("blobject.operation", "write", "blobject.direction", "write")) == payload.Length, "bytes written", collector);
                    Require(collector.Sum(BlobjectTelemetryNames.IoBytes, Tags("blobject.operation", "get", "blobject.direction", "read")) == payload.Length, "bytes read", collector);
                    Require(collector.Sum(BlobjectTelemetryNames.IoBytes, Tags("blobject.operation", "get_stream", "blobject.direction", "read")) == payload.Length, "bytes streamed", collector);
                    Require(Tag(collector.Span("disk write"), BlobjectTelemetryNames.AttributeBytes) == payload.Length.ToString(), "bytes span attribute", collector);
                    Require(!collector.MeasurementsOf(BlobjectTelemetryNames.OperationErrors).Any(), "no errors", collector);
                }
            }).ConfigureAwait(false);
        }

        private static async Task OverloadRecordedOnce(CancellationToken token)
        {
            await WithDisk(async client =>
            {
                using (TelemetryCollector collector = new TelemetryCollector())
                {
                    // string -> byte[] -> stream overloads
                    await client.WriteAsync("x.txt", "text/plain", "abc", token).ConfigureAwait(false);
                    Require(collector.SpansNamed("disk write").Count == 1, "one write span", collector);
                    Require(collector.MeasurementsOf(BlobjectTelemetryNames.OperationDuration, Tags("blobject.operation", "write")).Count == 1, "one write duration", collector);
                    Require(collector.Sum(BlobjectTelemetryNames.IoBytes) == 3, "bytes counted once", collector);
                }
            }).ConfigureAwait(false);
        }

        private static async Task NotFoundOutcome(CancellationToken token)
        {
            await WithDisk(async client =>
            {
                using (TelemetryCollector collector = new TelemetryCollector())
                {
                    bool threw = false;
                    try { await client.GetAsync("missing.txt", token).ConfigureAwait(false); }
                    catch (FileNotFoundException) { threw = true; }
                    Require(threw, "missing object throws");

                    Activity span = collector.Span("disk get");
                    Require(span.Status == ActivityStatusCode.Error, "error status", collector);
                    Require(Tag(span, BlobjectTelemetryNames.AttributeOutcome) == "not_found", "not_found outcome", collector);
                    Require(Tag(span, BlobjectTelemetryNames.AttributeErrorType) == "System.IO.FileNotFoundException", "error.type on span", collector);
                    Require(span.Events.Any(e => e.Name == "exception"), "exception event", collector);

                    Require(collector.MeasurementsOf(
                        BlobjectTelemetryNames.OperationDuration,
                        Tags("blobject.operation", "get", "blobject.outcome", "not_found", "error.type", "System.IO.FileNotFoundException")).Count == 1,
                        "not_found duration", collector);
                    Require(collector.Sum(BlobjectTelemetryNames.OperationErrors, Tags("blobject.outcome", "not_found")) == 1, "errors counter", collector);
                }
            }).ConfigureAwait(false);
        }

        private static async Task ErrorOutcome(CancellationToken token)
        {
            FaultyBlobClient client = new FaultyBlobClient();
            client.FailingKeys.Add("bad");

            using (TelemetryCollector collector = new TelemetryCollector())
            {
                bool threw = false;
                try { await client.WriteAsync("bad", "text/plain", "x", token).ConfigureAwait(false); }
                catch (IOException) { threw = true; }
                Require(threw, "failure propagates unchanged");

                Activity span = collector.Span("custom write");
                Require(span.Kind == ActivityKind.Client, "client span", collector);
                Require(span.Status == ActivityStatusCode.Error, "error status", collector);
                Require(span.StatusDescription == "Simulated storage failure.", "status description", collector);
                Require(Tag(span, BlobjectTelemetryNames.AttributeOutcome) == "error", "error outcome", collector);
                Require(Tag(span, BlobjectTelemetryNames.AttributeErrorType) == "System.IO.IOException", "error.type", collector);

                ActivityEvent exception = span.Events.Single(e => e.Name == "exception");
                Require(exception.Tags.Any(t => t.Key == "exception.type" && (string)t.Value == "System.IO.IOException"), "exception.type", collector);

                Require(collector.Sum(BlobjectTelemetryNames.OperationErrors, Tags("blobject.operation", "write", "blobject.outcome", "error", "error.type", "System.IO.IOException")) == 1, "errors counter by error.type", collector);
                Require(collector.Sum(BlobjectTelemetryNames.IoBytes) == 0, "no bytes counted on failure", collector);
            }
        }

        private static async Task CancelledOutcome(CancellationToken token)
        {
            FaultyBlobClient client = new FaultyBlobClient();

            using (CancellationTokenSource cts = new CancellationTokenSource())
            using (TelemetryCollector collector = new TelemetryCollector())
            {
                cts.Cancel();
                bool threw = false;
                try { await client.WriteAsync("k", "text/plain", new byte[] { 1 }, cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { threw = true; }
                Require(threw, "cancellation propagates");

                Activity span = collector.Span("custom write");
                Require(span.Status == ActivityStatusCode.Unset, "cancelled span is not an error", collector);
                Require(Tag(span, BlobjectTelemetryNames.AttributeOutcome) == "cancelled", "cancelled outcome", collector);
                Require(collector.MeasurementsOf(BlobjectTelemetryNames.OperationDuration, Tags("blobject.outcome", "cancelled")).Count == 1, "cancelled duration", collector);
                Require(!collector.MeasurementsOf(BlobjectTelemetryNames.OperationErrors).Any(), "cancellation is not an error", collector);
            }
        }

        private static async Task CustomProvider(CancellationToken token)
        {
            FaultyBlobClient client = new FaultyBlobClient();

            using (TelemetryCollector collector = new TelemetryCollector())
            {
                await client.ExistsAsync("k", token).ConfigureAwait(false);
                Require(Tag(collector.Span("custom exists"), BlobjectTelemetryNames.AttributeProvider) == "custom", "custom provider", collector);
            }
        }

        private static async Task KeysOmittedByDefault(CancellationToken token)
        {
            FaultyBlobClient client = new FaultyBlobClient();
            bool original = BlobjectTelemetry.RecordKeys;

            try
            {
                using (TelemetryCollector collector = new TelemetryCollector())
                {
                    BlobjectTelemetry.RecordKeys = false;
                    await client.ExistsAsync("secret/file.txt", token).ConfigureAwait(false);
                    Require(Tag(collector.Span("custom exists"), BlobjectTelemetryNames.AttributeKey) == null, "key omitted by default", collector);
                }

                using (TelemetryCollector collector = new TelemetryCollector())
                {
                    BlobjectTelemetry.RecordKeys = true;
                    await client.ExistsAsync("secret/file.txt", token).ConfigureAwait(false);
                    Require(Tag(collector.Span("custom exists"), BlobjectTelemetryNames.AttributeKey) == "secret/file.txt", "key recorded when enabled", collector);
                    Require(collector.Measurements.All(m => !m.Tags.Values.Contains("secret/file.txt")), "key never on metrics", collector);
                }
            }
            finally
            {
                BlobjectTelemetry.RecordKeys = original;
            }
        }

        private static async Task NoHighCardinalityMetricLabels(CancellationToken token)
        {
            await WithDisk(async client =>
            {
                using (TelemetryCollector collector = new TelemetryCollector())
                {
                    await client.WriteAsync("unique-" + Guid.NewGuid().ToString("N"), "text/plain", "x", token).ConfigureAwait(false);
                    await client.WriteManyAsync(new List<WriteRequest> { new WriteRequest("m1", "text/plain", Encoding.UTF8.GetBytes("1")) }, token).ConfigureAwait(false);

                    HashSet<string> allowed = new HashSet<string>
                    {
                        BlobjectTelemetryNames.AttributeProvider,
                        BlobjectTelemetryNames.AttributeOperation,
                        BlobjectTelemetryNames.AttributeOutcome,
                        BlobjectTelemetryNames.AttributeErrorType,
                        BlobjectTelemetryNames.AttributeDirection,
                        BlobjectTelemetryNames.AttributeStage,
                        BlobjectTelemetryNames.AttributeCopySource,
                        BlobjectTelemetryNames.AttributeCopyTarget,
                        BlobjectTelemetryNames.AttributeVersion
                    };

                    foreach (RecordedMeasurement m in collector.Measurements)
                    {
                        foreach (string key in m.Tags.Keys) Require(allowed.Contains(key), "unexpected metric label " + key + " on " + m.Name, collector);
                    }

                    Require(collector.Measurements.Any(), "measurements captured", collector);
                }
            }).ConfigureAwait(false);
        }

        private static async Task EnumerateAsyncSpan(CancellationToken token)
        {
            await WithDisk(async client =>
            {
                for (int i = 0; i < 3; i++) await client.WriteAsync("e" + i, "text/plain", "x", token).ConfigureAwait(false);

                using (TelemetryCollector collector = new TelemetryCollector())
                {
                    int count = 0;
                    await foreach (BlobMetadata md in client.EnumerateAsync(null, token).ConfigureAwait(false))
                    {
                        count++;
                        Require(Activity.Current == null || Activity.Current.Source.Name != BlobjectTelemetryNames.ActivitySourceName, "enumerate span is not ambient for the caller", collector);
                    }

                    Require(count == 3, "enumerated 3");
                    Activity span = collector.Span("disk enumerate");
                    Require(Tag(span, BlobjectTelemetryNames.AttributeObjects) == "3", "objects attribute", collector);
                    Require(span.Status == ActivityStatusCode.Ok, "ok status", collector);
                    Require(collector.Sum(BlobjectTelemetryNames.EnumerateObjects, Tags("blobject.provider", "disk")) == 3, "objects counter", collector);
                }
            }).ConfigureAwait(false);
        }

        private static async Task EnumerateSyncSpan(CancellationToken token)
        {
            await WithDisk(async client =>
            {
                for (int i = 0; i < 2; i++) await client.WriteAsync("s" + i, "text/plain", "x", token).ConfigureAwait(false);

                using (TelemetryCollector collector = new TelemetryCollector())
                {
                    Activity before = Activity.Current;
                    int count = client.Enumerate().Count();
                    Require(count == 2, "enumerated 2");
                    Require(ReferenceEquals(Activity.Current, before), "ambient span restored after sync enumeration", collector);
                    Require(Tag(collector.Span("disk enumerate"), BlobjectTelemetryNames.AttributeObjects) == "2", "objects attribute", collector);
                }
            }).ConfigureAwait(false);
        }

        private static async Task EnumerateAbandoned(CancellationToken token)
        {
            await WithDisk(async client =>
            {
                for (int i = 0; i < 3; i++) await client.WriteAsync("a" + i, "text/plain", "x", token).ConfigureAwait(false);

                using (TelemetryCollector collector = new TelemetryCollector())
                {
                    await foreach (BlobMetadata md in client.EnumerateAsync(null, token).ConfigureAwait(false)) break;
                    BlobMetadata first = client.Enumerate().First();

                    List<Activity> spans = collector.SpansNamed("disk enumerate");
                    Require(spans.Count == 2, "both abandoned enumerations ended their spans", collector);
                    Require(spans.All(s => Tag(s, BlobjectTelemetryNames.AttributeObjects) == "1"), "objects counted to the point of abandonment", collector);
                }
            }).ConfigureAwait(false);
        }

        private static async Task EnumerateFailure(CancellationToken token)
        {
            FaultyBlobClient client = new FaultyBlobClient();
            await client.WriteAsync("1", "text/plain", "x", token).ConfigureAwait(false);
            await client.WriteAsync("2", "text/plain", "x", token).ConfigureAwait(false);
            client.FailEnumeration = true;

            using (TelemetryCollector collector = new TelemetryCollector())
            {
                bool threw = false;
                try { await foreach (BlobMetadata md in client.EnumerateAsync(null, token).ConfigureAwait(false)) { } }
                catch (IOException) { threw = true; }
                Require(threw, "enumeration failure propagates");

                Activity span = collector.Span("custom enumerate");
                Require(span.Status == ActivityStatusCode.Error, "error status", collector);
                Require(Tag(span, BlobjectTelemetryNames.AttributeObjects) == "1", "objects before failure", collector);
                Require(collector.Sum(BlobjectTelemetryNames.OperationErrors, Tags("blobject.operation", "enumerate")) == 1, "errors counter", collector);
            }
        }

        private static async Task WriteManyBulk(CancellationToken token)
        {
            FaultyBlobClient client = new FaultyBlobClient();
            client.MaxConcurrency = 2;

            List<WriteRequest> requests = new List<WriteRequest>();
            for (int i = 0; i < 6; i++) requests.Add(new WriteRequest("w" + i, "text/plain", Encoding.UTF8.GetBytes("data" + i)));

            using (TelemetryCollector collector = new TelemetryCollector())
            {
                await client.WriteManyAsync(requests, token).ConfigureAwait(false);

                Activity parent = collector.Span("custom write_many");
                List<Activity> writes = collector.SpansNamed("custom write");
                Require(writes.Count == 6, "one child span per item", collector);
                Require(writes.All(w => w.ParentSpanId == parent.SpanId), "item spans nest under the bulk span across Task.Run", collector);
                Require(Tag(parent, BlobjectTelemetryNames.AttributeItems) == "6", "items attribute", collector);
                Require(Tag(parent, BlobjectTelemetryNames.AttributeItemsFailed) == "0", "items failed attribute", collector);
                Require(Tag(parent, BlobjectTelemetryNames.AttributeMaxConcurrency) == "2", "max concurrency attribute", collector);

                Dictionary<string, string> op = Tags("blobject.provider", "custom", "blobject.operation", "write_many");
                Require(collector.MeasurementsOf(BlobjectTelemetryNames.BulkQueueDuration, op).Count == 6, "queue wait per item", collector);
                Require(collector.Sum(BlobjectTelemetryNames.BulkSlotsCapacity, op) == 0, "capacity returns to zero", collector);
                Require(collector.MeasurementsOf(BlobjectTelemetryNames.BulkSlotsCapacity, op).Any(m => m.Value == 2), "capacity raised by MaxConcurrency", collector);
                Require(collector.Sum(BlobjectTelemetryNames.BulkSlotsInUse, op) == 0, "slots in use return to zero", collector);
                Require(collector.MeasurementsOf(BlobjectTelemetryNames.BulkSlotsInUse, op).Count(m => m.Value == 1) == 6, "slot taken per item", collector);
                Require(collector.Sum(BlobjectTelemetryNames.BulkItems, Tags("blobject.operation", "write_many", "blobject.outcome", "success")) == 6, "items success counter", collector);
            }
        }

        private static async Task DeleteManyItemFailures(CancellationToken token)
        {
            FaultyBlobClient client = new FaultyBlobClient();
            await client.WriteAsync("ok1", "text/plain", "x", token).ConfigureAwait(false);
            await client.WriteAsync("ok2", "text/plain", "x", token).ConfigureAwait(false);
            client.FailingKeys.Add("bad");

            using (TelemetryCollector collector = new TelemetryCollector())
            {
                DeleteManyResult result = await client.DeleteManyAsync(new[] { "ok1", "ok2", "bad" }, token).ConfigureAwait(false);
                Require(result.Results.Count(r => !r.Success) == 1, "one failed item");

                Activity parent = collector.Span("custom delete_many");
                Require(parent.Status == ActivityStatusCode.Ok, "bulk call itself succeeded", collector);
                Require(Tag(parent, BlobjectTelemetryNames.AttributeItems) == "3", "items", collector);
                Require(Tag(parent, BlobjectTelemetryNames.AttributeItemsFailed) == "1", "failed items", collector);
                Require(collector.Sum(BlobjectTelemetryNames.BulkItems, Tags("blobject.operation", "delete_many", "blobject.outcome", "success")) == 2, "success items", collector);
                Require(collector.Sum(BlobjectTelemetryNames.BulkItems, Tags("blobject.operation", "delete_many", "blobject.outcome", "error")) == 1, "failed items counter", collector);
                Require(collector.SpansNamed("custom delete").Count(s => s.Status == ActivityStatusCode.Error) == 1, "failing item span", collector);
            }
        }

        private static async Task EmptyBulk(CancellationToken token)
        {
            await WithDisk(async client =>
            {
                await client.WriteAsync("f/1", "text/plain", "x", token).ConfigureAwait(false);
                await client.WriteAsync("f/2", "text/plain", "x", token).ConfigureAwait(false);

                using (TelemetryCollector collector = new TelemetryCollector())
                {
                    EmptyResult result = await client.EmptyAsync(token).ConfigureAwait(false);
                    Require(result.Blobs.Count == 2, "emptied two");

                    List<Activity> empties = collector.SpansNamed("disk empty");
                    Require(empties.Count == 1, "disk override and base recorded once", collector);
                    Activity empty = empties[0];
                    Require(collector.Span("disk enumerate").ParentSpanId == empty.SpanId, "enumerate nested", collector);
                    Require(collector.SpansNamed("disk delete").All(d => d.ParentSpanId == empty.SpanId), "deletes nested", collector);
                    Require(collector.Sum(BlobjectTelemetryNames.BulkItems, Tags("blobject.operation", "empty")) == 2, "items counted", collector);
                }
            }).ConfigureAwait(false);
        }

        private static async Task CopyPipeline(CancellationToken token)
        {
            string fromDir = NewDirectory();
            string toDir = NewDirectory();

            try
            {
                using (DiskBlobClient from = new DiskBlobClient(new DiskSettings(fromDir)))
                using (DiskBlobClient to = new DiskBlobClient(new DiskSettings(toDir)))
                {
                    await from.WriteAsync("c1", "text/plain", "one", token).ConfigureAwait(false);
                    await from.WriteAsync("c2", "text/plain", "two!", token).ConfigureAwait(false);

                    using (TelemetryCollector collector = new TelemetryCollector())
                    {
                        CopyStatistics stats;
                        using (BlobCopy copy = new BlobCopy(from, to))
                        {
                            stats = await copy.StartAsync(-1, null, token).ConfigureAwait(false);
                        }

                        Require(stats.Success && stats.BlobsWritten == 2, "copied");

                        Activity job = collector.Span("blobject copy");
                        Require(job.Status == ActivityStatusCode.Ok, "job ok", collector);
                        Require(job.ParentSpanId == collector.RootSpanId, "job is the caller's child", collector);
                        Require(Tag(job, BlobjectTelemetryNames.AttributeCopySource) == "disk" && Tag(job, BlobjectTelemetryNames.AttributeCopyTarget) == "disk", "job providers", collector);

                        List<Activity> reads = collector.SpansNamed("stage:read");
                        List<Activity> writes = collector.SpansNamed("stage:write");
                        Require(reads.Count == 2 && writes.Count == 2, "stage spans per object", collector);
                        Require(reads.Concat(writes).All(s => s.ParentSpanId == job.SpanId), "stages nest under the job", collector);
                        Require(collector.Span("disk enumerate").ParentSpanId == job.SpanId, "enumeration nests under the job", collector);
                        Require(collector.SpansNamed("disk get_stream").All(s => reads.Any(r => r.SpanId == s.ParentSpanId)), "provider read nests under stage:read", collector);
                        Require(collector.SpansNamed("disk write").All(s => writes.Any(w => w.SpanId == s.ParentSpanId)), "provider write nests under stage:write", collector);

                        Dictionary<string, string> pair = Tags("blobject.copy.source", "disk", "blobject.copy.target", "disk");
                        Require(collector.Sum(BlobjectTelemetryNames.CopyJobs, Tags("blobject.outcome", "success")) == 1, "job counter", collector);
                        Require(collector.MeasurementsOf(BlobjectTelemetryNames.CopyDuration, pair).Count == 1, "job duration", collector);
                        Require(collector.MeasurementsOf(BlobjectTelemetryNames.CopyStageDuration, Tags("blobject.stage", "enumerate")).Count == 2, "enumerate stage per object", collector);
                        Require(collector.MeasurementsOf(BlobjectTelemetryNames.CopyStageDuration, Tags("blobject.stage", "read")).Count == 2, "read stage", collector);
                        Require(collector.MeasurementsOf(BlobjectTelemetryNames.CopyStageDuration, Tags("blobject.stage", "write")).Count == 2, "write stage", collector);
                        Require(collector.Sum(BlobjectTelemetryNames.CopyStageEvents, Tags("blobject.stage", "write", "blobject.outcome", "success")) == 2, "stage counter", collector);
                        Require(collector.Sum(BlobjectTelemetryNames.CopyObjects, pair) == 2, "objects copied", collector);
                        Require(collector.Sum(BlobjectTelemetryNames.CopyBytes, pair) == 7, "bytes copied", collector);

                        collector.CollectObservable();
                        RecordedMeasurement last = collector.MeasurementsOf(BlobjectTelemetryNames.CopyLastSuccess, pair).SingleOrDefault();
                        Require(last != null, "last success gauge", collector);
                        Require(Math.Abs(last.Value - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) < 120, "last success is recent", collector);
                    }
                }
            }
            finally
            {
                DeleteDirectory(fromDir);
                DeleteDirectory(toDir);
            }
        }

        private static async Task CopyFailure(CancellationToken token)
        {
            FaultyBlobClient from = new FaultyBlobClient();
            FaultyBlobClient to = new FaultyBlobClient();
            await from.WriteAsync("ok", "text/plain", "x", token).ConfigureAwait(false);
            await from.WriteAsync("poison", "text/plain", "x", token).ConfigureAwait(false);
            from.FailingKeys.Add("poison");

            using (TelemetryCollector collector = new TelemetryCollector())
            {
                CopyStatistics stats;
                using (BlobCopy copy = new BlobCopy(from, to))
                {
                    stats = await copy.StartAsync(-1, null, token).ConfigureAwait(false);
                }

                Require(!stats.Success, "copy reports failure");

                Activity job = collector.Span("blobject copy");
                Require(job.Status == ActivityStatusCode.Error, "job error status", collector);
                Require(Tag(job, BlobjectTelemetryNames.AttributeErrorType) == "System.IO.IOException", "job error.type", collector);
                Require(collector.SpansNamed("stage:read").Count(s => s.Status == ActivityStatusCode.Error) == 1, "failing stage span", collector);
                Require(collector.Sum(BlobjectTelemetryNames.CopyStageEvents, Tags("blobject.stage", "read", "blobject.outcome", "error")) == 1, "failing stage counter", collector);
                Require(collector.Sum(BlobjectTelemetryNames.CopyJobs, Tags("blobject.outcome", "error", "error.type", "System.IO.IOException")) == 1, "failed job counter", collector);
            }
        }

        private static async Task RetryAfterReset(CancellationToken token)
        {
            FaultyBlobClient client = new FaultyBlobClient();
            await client.WriteAsync("k", "text/plain", "x", token).ConfigureAwait(false);
            client.TransientFailures = 2;

            using (TelemetryCollector collector = new TelemetryCollector())
            {
                byte[] data = await client.GetAsync("k", token).ConfigureAwait(false);
                Require(data.Length == 1, "read succeeded after retries");

                Activity get = collector.Span("custom get");
                Require(get.Status == ActivityStatusCode.Ok, "operation ok", collector);
                Require(get.Events.Count(e => e.Name == BlobjectTelemetryNames.EventConnectionReset) == 2, "reset events", collector);
                Require(get.Events.Count(e => e.Name == BlobjectTelemetryNames.EventRetry) == 2, "retry events", collector);
                Require(collector.Sum(BlobjectTelemetryNames.ConnectionResets, Tags("blobject.provider", "custom", "error.type", "System.Net.Sockets.SocketException")) == 2, "resets by error.type", collector);
                Require(collector.Sum(BlobjectTelemetryNames.OperationRetries, Tags("blobject.provider", "custom", "blobject.operation", "get")) == 2, "retries by operation", collector);
            }
        }

        private static async Task ActiveReturnsToZero(CancellationToken token)
        {
            FaultyBlobClient client = new FaultyBlobClient();
            client.FailingKeys.Add("bad");

            using (TelemetryCollector collector = new TelemetryCollector())
            {
                await client.WriteAsync("good", "text/plain", "x", token).ConfigureAwait(false);
                try { await client.GetAsync("bad", token).ConfigureAwait(false); } catch (IOException) { }

                List<RecordedMeasurement> active = collector.MeasurementsOf(BlobjectTelemetryNames.OperationActive);
                Require(active.Count(m => m.Value == 1) == 2, "two operations started", collector);
                Require(active.Sum(m => m.Value) == 0, "in-flight returns to zero, including failures", collector);
            }
        }

        private static async Task DisabledEmitsNothing(CancellationToken token)
        {
            FaultyBlobClient client = new FaultyBlobClient();
            bool original = BlobjectTelemetry.Enabled;

            try
            {
                using (TelemetryCollector collector = new TelemetryCollector())
                {
                    BlobjectTelemetry.Enabled = false;
                    await client.WriteAsync("k", "text/plain", "x", token).ConfigureAwait(false);
                    await client.GetAsync("k", token).ConfigureAwait(false);
                    await client.WriteManyAsync(new List<WriteRequest> { new WriteRequest("m", "text/plain", new byte[] { 1 }) }, token).ConfigureAwait(false);
                    foreach (BlobMetadata md in client.Enumerate()) { }

                    Require(!collector.Spans.Any(), "no spans when disabled", collector);
                    Require(!collector.Measurements.Any(), "no measurements when disabled", collector);
                }
            }
            finally
            {
                BlobjectTelemetry.Enabled = original;
            }
        }

        private static async Task NoListenerDoesNotThrow(CancellationToken token)
        {
            // no collector: the instrumentation must run the operations unchanged when nothing subscribes
            FaultyBlobClient client = new FaultyBlobClient();
            await client.WriteAsync("k", "text/plain", "x", token).ConfigureAwait(false);
            byte[] data = await client.GetAsync("k", token).ConfigureAwait(false);
            await client.WriteManyAsync(new List<WriteRequest> { new WriteRequest("m", "text/plain", new byte[] { 1 }) }, token).ConfigureAwait(false);
            DeleteManyResult result = await client.DeleteManyAsync(new[] { "k", "m" }, token).ConfigureAwait(false);
            int count = 0;
            await foreach (BlobMetadata md in client.EnumerateAsync(null, token).ConfigureAwait(false)) count++;

            bool threw = false;
            try { await client.GetAsync("missing", token).ConfigureAwait(false); }
            catch (KeyNotFoundException) { threw = true; }

            Require(data.Length == 1 && result.Success && count == 0 && threw, "operations unaffected without listeners");
        }

        private static async Task ThrowingListenerDoesNotBreak(CancellationToken token)
        {
            ActivityListener activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == BlobjectTelemetryNames.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = a => throw new InvalidOperationException("listener failure"),
                ActivityStopped = a => throw new InvalidOperationException("listener failure")
            };

            MeterListener meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == BlobjectTelemetryNames.MeterName) listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<double>((i, v, t, s) => throw new InvalidOperationException("listener failure"));
            meterListener.SetMeasurementEventCallback<long>((i, v, t, s) => throw new InvalidOperationException("listener failure"));

            ActivitySource.AddActivityListener(activityListener);
            meterListener.Start();

            try
            {
                await WithDisk(async client =>
                {
                    await client.WriteAsync("k", "text/plain", "x", token).ConfigureAwait(false);
                    byte[] data = await client.GetAsync("k", token).ConfigureAwait(false);
                    await client.WriteManyAsync(new List<WriteRequest> { new WriteRequest("m", "text/plain", new byte[] { 1 }) }, token).ConfigureAwait(false);
                    int count = client.Enumerate().Count();

                    bool threw = false;
                    try { await client.GetAsync("missing", token).ConfigureAwait(false); }
                    catch (FileNotFoundException) { threw = true; }

                    Require(data.Length == 1 && count == 2 && threw, "operations unaffected by listener failures");
                }).ConfigureAwait(false);
            }
            finally
            {
                meterListener.Dispose();
                activityListener.Dispose();
            }
        }

        private static async Task CifsConnectionMetrics(CancellationToken token)
        {
            OpenCifsEphemeralServer server = await OpenCifsEphemeralServer.StartAsync(token).ConfigureAwait(false);

            try
            {
                CifsSettings settings = BlobProviderFactory.CreateCifsSettings(server.ApplyTo(new BlobProviderOptions()));
                settings.MaxConnections = 3;
                settings.PreferEncryption = AesCcm.IsSupported;
                await ConnectionMetrics(
                    collector => new CifsBlobClient(settings),
                    "cifs",
                    3,
                    token).ConfigureAwait(false);
            }
            finally
            {
                await server.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static async Task NfsConnectionMetrics(CancellationToken token)
        {
            OpenNfsEphemeralServer server = await OpenNfsEphemeralServer.StartAsync(token).ConfigureAwait(false);

            try
            {
                NfsSettings settings = BlobProviderFactory.CreateNfsSettings(server.ApplyTo(new BlobProviderOptions()));
                await ConnectionMetrics(
                    collector => new NfsBlobClient(settings),
                    "nfs",
                    1,
                    token).ConfigureAwait(false);
            }
            finally
            {
                await server.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static async Task ConnectionMetrics(
            Func<TelemetryCollector, BlobClientBase> factory,
            string provider,
            int capacity,
            CancellationToken token)
        {
            using (TelemetryCollector collector = new TelemetryCollector())
            {
                BlobClientBase client = factory(collector);
                string key = "telemetry-" + Guid.NewGuid().ToString("N") + ".txt";

                try
                {
                    await client.WriteAsync(key, "text/plain", "hello", token).ConfigureAwait(false);
                    byte[] data = await client.GetAsync(key, token).ConfigureAwait(false);
                    await client.DeleteAsync(key, token).ConfigureAwait(false);
                    Require(data.Length == 5, "round trip");
                }
                finally
                {
                    await ((IAsyncDisposable)client).DisposeAsync().ConfigureAwait(false);
                }

                Dictionary<string, string> p = Tags("blobject.provider", provider);
                List<Activity> connects = collector.SpansNamed(provider + " connect");
                Require(connects.Any(), "connect span", collector);
                Require(connects.All(c => c.Status == ActivityStatusCode.Ok && c.Kind == ActivityKind.Client), "connect ok", collector);
                Require(connects.All(c => Tag(c, BlobjectTelemetryNames.AttributeServerAddress) == "127.0.0.1"), "server.address", collector);
                Require(connects.All(c => collector.Spans.Any(o => o.SpanId == c.ParentSpanId && o.DisplayName.StartsWith(provider + " "))), "connect nests under the operation that needed it", collector);

                Activity write = collector.Span(provider + " write");
                Require(!String.IsNullOrEmpty(Tag(write, BlobjectTelemetryNames.AttributeContainer)), "container attribute", collector);
                Require(Tag(write, BlobjectTelemetryNames.AttributeServerAddress) == "127.0.0.1", "server attribute", collector);

                Require(collector.MeasurementsOf(BlobjectTelemetryNames.ConnectionDuration, Tags("blobject.provider", provider, "blobject.outcome", "success")).Count == connects.Count, "connect durations", collector);
                Require(collector.MeasurementsOf(BlobjectTelemetryNames.ConnectionOpen, p).Count(m => m.Value == 1) == connects.Count, "connections opened", collector);
                Require(collector.Sum(BlobjectTelemetryNames.ConnectionOpen, p) == 0, "connections closed on dispose", collector);
                Require(collector.MeasurementsOf(BlobjectTelemetryNames.ConnectionPoolCapacity, p).Any(m => m.Value == capacity), "pool capacity", collector);
                Require(collector.Sum(BlobjectTelemetryNames.ConnectionPoolCapacity, p) == 0, "pool capacity released on dispose", collector);
                Require(collector.Sum(BlobjectTelemetryNames.IoBytes, Tags("blobject.provider", provider, "blobject.direction", "read")) == 5, "bytes read", collector);
            }
        }

        private static async Task CifsReconnectAfterRestart(CancellationToken token)
        {
            OpenCifsEphemeralServer server = await OpenCifsEphemeralServer.StartAsync(token).ConfigureAwait(false);

            try
            {
                CifsSettings settings = BlobProviderFactory.CreateCifsSettings(server.ApplyTo(new BlobProviderOptions()));
                settings.MaxConnections = 1;
                settings.PreferEncryption = AesCcm.IsSupported;

                await using (CifsBlobClient client = new CifsBlobClient(settings))
                {
                    await client.WriteAsync("before.txt", "text/plain", "x", token).ConfigureAwait(false);
                    await server.RestartAsync(token).ConfigureAwait(false);

                    using (TelemetryCollector collector = new TelemetryCollector())
                    {
                        byte[] data = await client.GetAsync("before.txt", token).ConfigureAwait(false);
                        Require(data.Length == 1, "read after restart");

                        Activity get = collector.Span("cifs get");
                        Require(get.Status == ActivityStatusCode.Ok, "operation succeeded after reconnect", collector);

                        List<Activity> connects = collector.SpansNamed("cifs connect");
                        Require(connects.Any(c => c.ParentSpanId == get.SpanId && c.Status == ActivityStatusCode.Ok), "reconnect visible under the operation", collector);

                        double resets = collector.Sum(BlobjectTelemetryNames.ConnectionResets, Tags("blobject.provider", "cifs"));
                        double retries = collector.Sum(BlobjectTelemetryNames.OperationRetries, Tags("blobject.provider", "cifs", "blobject.operation", "get"));
                        Require(resets >= 1, "connection reset counted", collector);
                        Require(retries <= resets, "every retry follows a reset", collector);
                        Require(get.Events.Any(e => e.Name == BlobjectTelemetryNames.EventConnectionReset), "reset event on span", collector);
                    }
                }
            }
            finally
            {
                await server.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static async Task CifsConnectFailure(CancellationToken token)
        {
            CifsSettings settings = new CifsSettings("127.0.0.1", DockerCli.GetFreeTcpPort(), "user", "password", "share");
            settings.ConnectTimeoutMs = 2000;
            settings.MaxConnections = 1;

            await using (CifsBlobClient client = new CifsBlobClient(settings))
            {
                await ConnectFailure(client, "cifs", token).ConfigureAwait(false);
            }
        }

        private static async Task NfsConnectFailure(CancellationToken token)
        {
            NfsSettings settings = new NfsSettings("127.0.0.1", 0, 0, "/export", NfsVersionEnum.V3);
            settings.Port = DockerCli.GetFreeTcpPort();
            settings.MountPort = settings.Port;
            settings.ConnectTimeoutMs = 2000;
            settings.ResponseTimeoutMs = 2000;

            await using (NfsBlobClient client = new NfsBlobClient(settings))
            {
                await ConnectFailure(client, "nfs", token).ConfigureAwait(false);
            }
        }

        private static async Task ConnectFailure(BlobClientBase client, string provider, CancellationToken token)
        {
            using (TelemetryCollector collector = new TelemetryCollector())
            {
                bool threw = false;
                try { await client.GetAsync("any.txt", token).ConfigureAwait(false); }
                catch (Exception e) when (!(e is OperationCanceledException) || !token.IsCancellationRequested) { threw = true; }
                Require(threw, "operation fails without a server");

                bool valid = await client.ValidateConnectivity(token).ConfigureAwait(false);
                Require(!valid, "connectivity validation reports false");

                List<Activity> connects = collector.SpansNamed(provider + " connect");
                Require(connects.Any() && connects.All(c => c.Status == ActivityStatusCode.Error), "connect spans are errors", collector);
                Require(connects.All(c => !String.IsNullOrEmpty(Tag(c, BlobjectTelemetryNames.AttributeErrorType))), "connect error.type", collector);
                Require(collector.MeasurementsOf(BlobjectTelemetryNames.ConnectionDuration, Tags("blobject.provider", provider, "blobject.outcome", "error")).Count == connects.Count, "connect error durations", collector);
                Require(!collector.MeasurementsOf(BlobjectTelemetryNames.ConnectionOpen).Any(), "no connection opened", collector);

                Activity get = collector.Span(provider + " get");
                Require(get.Status == ActivityStatusCode.Error, "operation span error", collector);
                Require(collector.Sum(BlobjectTelemetryNames.OperationErrors, Tags("blobject.provider", provider, "blobject.operation", "get", "blobject.outcome", "error")) == 1, "operation errors counter", collector);

                Activity validate = collector.Span(provider + " validate_connectivity");
                Require(Tag(validate, BlobjectTelemetryNames.AttributeOutcome) == "success", "validate_connectivity handled the failure", collector);
            }
        }

        #endregion
    }
}
