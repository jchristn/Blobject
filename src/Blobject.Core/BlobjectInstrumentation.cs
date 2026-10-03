namespace Blobject.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Reflection;
    using System.Threading;

    /// <summary>
    /// Owns the Blobject meter, activity source, and instruments, and records measurements.
    /// Every method is best-effort and never throws.
    /// </summary>
    internal static class BlobjectInstrumentation
    {
        #region Internal-Members

        internal static readonly string Version = ResolveVersion();

        internal static readonly ActivitySource Source = new ActivitySource(BlobjectTelemetryNames.ActivitySourceName, Version);

        internal static readonly Meter Meter = new Meter(BlobjectTelemetryNames.MeterName, Version);

        internal static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
            BlobjectTelemetryNames.OperationDuration, "s", "Duration of Blobject storage operations.");

        internal static readonly UpDownCounter<long> OperationActive = Meter.CreateUpDownCounter<long>(
            BlobjectTelemetryNames.OperationActive, "{operation}", "Blobject storage operations in flight.");

        internal static readonly Counter<long> OperationErrors = Meter.CreateCounter<long>(
            BlobjectTelemetryNames.OperationErrors, "{error}", "Failed Blobject storage operations.");

        internal static readonly Counter<long> OperationRetries = Meter.CreateCounter<long>(
            BlobjectTelemetryNames.OperationRetries, "{retry}", "Operation attempts retried after a dropped connection.");

        internal static readonly Counter<long> IoBytes = Meter.CreateCounter<long>(
            BlobjectTelemetryNames.IoBytes, "By", "Object bytes read from or written to storage.");

        internal static readonly Counter<long> EnumerateObjects = Meter.CreateCounter<long>(
            BlobjectTelemetryNames.EnumerateObjects, "{object}", "Objects returned by enumeration.");

        internal static readonly Counter<long> BulkItems = Meter.CreateCounter<long>(
            BlobjectTelemetryNames.BulkItems, "{item}", "Items processed by bulk operations.");

        internal static readonly Histogram<double> BulkQueueDuration = Meter.CreateHistogram<double>(
            BlobjectTelemetryNames.BulkQueueDuration, "s", "Time a bulk item waited for a concurrency slot.");

        internal static readonly UpDownCounter<long> BulkSlotsInUse = Meter.CreateUpDownCounter<long>(
            BlobjectTelemetryNames.BulkSlotsInUse, "{slot}", "Bulk concurrency slots in use.");

        internal static readonly UpDownCounter<long> BulkSlotsCapacity = Meter.CreateUpDownCounter<long>(
            BlobjectTelemetryNames.BulkSlotsCapacity, "{slot}", "Bulk concurrency slots available to running bulk operations.");

        internal static readonly Histogram<double> ConnectionDuration = Meter.CreateHistogram<double>(
            BlobjectTelemetryNames.ConnectionDuration, "s", "Duration of establishing a storage connection.");

        internal static readonly UpDownCounter<long> ConnectionOpen = Meter.CreateUpDownCounter<long>(
            BlobjectTelemetryNames.ConnectionOpen, "{connection}", "Open storage connections.");

        internal static readonly UpDownCounter<long> ConnectionPoolCapacity = Meter.CreateUpDownCounter<long>(
            BlobjectTelemetryNames.ConnectionPoolCapacity, "{connection}", "Connection pool capacity of live clients.");

        internal static readonly Counter<long> ConnectionResets = Meter.CreateCounter<long>(
            BlobjectTelemetryNames.ConnectionResets, "{reset}", "Connections discarded after a transport failure.");

        internal static readonly Counter<long> CopyJobs = Meter.CreateCounter<long>(
            BlobjectTelemetryNames.CopyJobs, "{job}", "BlobCopy jobs.");

        internal static readonly Histogram<double> CopyDuration = Meter.CreateHistogram<double>(
            BlobjectTelemetryNames.CopyDuration, "s", "BlobCopy job duration.");

        internal static readonly Histogram<double> CopyStageDuration = Meter.CreateHistogram<double>(
            BlobjectTelemetryNames.CopyStageDuration, "s", "BlobCopy per-stage duration.");

        internal static readonly Counter<long> CopyStageEvents = Meter.CreateCounter<long>(
            BlobjectTelemetryNames.CopyStageEvents, "{event}", "BlobCopy per-stage events.");

        internal static readonly Counter<long> CopyObjects = Meter.CreateCounter<long>(
            BlobjectTelemetryNames.CopyObjects, "{object}", "Objects copied by BlobCopy.");

        internal static readonly Counter<long> CopyBytes = Meter.CreateCounter<long>(
            BlobjectTelemetryNames.CopyBytes, "By", "Bytes copied by BlobCopy.");

        internal static readonly AsyncLocal<TelemetryOperationScope> CurrentScope = new AsyncLocal<TelemetryOperationScope>();

        #endregion

        #region Private-Members

        private const char _PairSeparator = '\u001f';
        private static readonly ConcurrentDictionary<string, long> _CopyLastSuccess = new ConcurrentDictionary<string, long>();

        #endregion

        #region Constructors-and-Factories

        static BlobjectInstrumentation()
        {
            try
            {
                Meter.CreateObservableGauge<int>(
                    BlobjectTelemetryNames.BuildInfo,
                    ObserveBuildInfo,
                    null,
                    "Blobject build information; the value is always 1.");

                Meter.CreateObservableGauge<long>(
                    BlobjectTelemetryNames.CopyLastSuccess,
                    ObserveCopyLastSuccess,
                    "s",
                    "Unix time of the last successful BlobCopy job.");
            }
            catch
            {
                // best-effort
            }
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// True when telemetry is enabled and something listens to the source or the core instruments.
        /// </summary>
        internal static bool IsObserved
        {
            get
            {
                if (!BlobjectTelemetry.Enabled) return false;

                try
                {
                    return Source.HasListeners()
                        || OperationDuration.Enabled
                        || OperationErrors.Enabled
                        || OperationActive.Enabled
                        || IoBytes.Enabled
                        || EnumerateObjects.Enabled
                        || BulkItems.Enabled;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal static double ElapsedSeconds(long startTimestamp)
        {
            long elapsed = Stopwatch.GetTimestamp() - startTimestamp;
            if (elapsed < 0) elapsed = 0;
            return (double)elapsed / Stopwatch.Frequency;
        }

        internal static string ClassifyOutcome(Exception e, bool notFound)
        {
            if (e == null) return BlobjectTelemetryNames.OutcomeSuccess;
            if (e is OperationCanceledException) return BlobjectTelemetryNames.OutcomeCancelled;
            if (notFound) return BlobjectTelemetryNames.OutcomeNotFound;
            return BlobjectTelemetryNames.OutcomeError;
        }

        internal static string ErrorType(Exception e)
        {
            if (e == null) return null;
            Type type = e.GetType();
            return type.FullName ?? type.Name;
        }

        internal static void RecordException(Activity activity, Exception e)
        {
            if (activity == null || e == null) return;

            try
            {
                if (!activity.IsAllDataRequested) return;

                ActivityTagsCollection tags = new ActivityTagsCollection();
                tags.Add("exception.type", ErrorType(e));
                tags.Add("exception.message", e.Message);
                tags.Add("exception.stacktrace", e.ToString());
                activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));
            }
            catch
            {
                // best-effort
            }
        }

        internal static void AddEvent(string name, string provider, Exception e)
        {
            try
            {
                Activity activity = Activity.Current;
                if (activity == null || !activity.IsAllDataRequested) return;
                if (!String.Equals(activity.Source.Name, BlobjectTelemetryNames.ActivitySourceName, StringComparison.Ordinal)) return;

                ActivityTagsCollection tags = new ActivityTagsCollection();
                tags.Add(BlobjectTelemetryNames.AttributeProvider, provider);
                if (e != null) tags.Add(BlobjectTelemetryNames.AttributeErrorType, ErrorType(e));
                activity.AddEvent(new ActivityEvent(name, DateTimeOffset.UtcNow, tags));
            }
            catch
            {
                // best-effort
            }
        }

        internal static void RecordRetry(string provider, string operation)
        {
            if (!BlobjectTelemetry.Enabled) return;

            try
            {
                if (String.IsNullOrEmpty(operation))
                {
                    TelemetryOperationScope scope = CurrentScope.Value;
                    operation = scope != null ? scope.Operation : BlobjectTelemetryNames.OperationConnect;
                }

                if (OperationRetries.Enabled)
                {
                    TagList tags = new TagList();
                    tags.Add(BlobjectTelemetryNames.AttributeProvider, provider);
                    tags.Add(BlobjectTelemetryNames.AttributeOperation, operation);
                    OperationRetries.Add(1, tags);
                }

                AddEvent(BlobjectTelemetryNames.EventRetry, provider, null);
            }
            catch
            {
                // best-effort
            }
        }

        internal static void RecordConnectionReset(string provider, Exception e)
        {
            if (!BlobjectTelemetry.Enabled) return;

            try
            {
                if (ConnectionResets.Enabled)
                {
                    TagList tags = new TagList();
                    tags.Add(BlobjectTelemetryNames.AttributeProvider, provider);
                    tags.Add(BlobjectTelemetryNames.AttributeErrorType, e != null ? ErrorType(e) : BlobjectTelemetryNames.ErrorTypeConnectionLost);
                    ConnectionResets.Add(1, tags);
                }

                AddEvent(BlobjectTelemetryNames.EventConnectionReset, provider, e);
            }
            catch
            {
                // best-effort
            }
        }

        internal static void AddProviderCount(UpDownCounter<long> instrument, string provider, long delta)
        {
            if (!BlobjectTelemetry.Enabled || delta == 0) return;

            try
            {
                if (!instrument.Enabled) return;
                instrument.Add(delta, new KeyValuePair<string, object>(BlobjectTelemetryNames.AttributeProvider, provider));
            }
            catch
            {
                // best-effort
            }
        }

        internal static void AddOperationCount(UpDownCounter<long> instrument, string provider, string operation, long delta)
        {
            if (!BlobjectTelemetry.Enabled || delta == 0) return;

            try
            {
                if (!instrument.Enabled) return;
                instrument.Add(
                    delta,
                    new KeyValuePair<string, object>(BlobjectTelemetryNames.AttributeProvider, provider),
                    new KeyValuePair<string, object>(BlobjectTelemetryNames.AttributeOperation, operation));
            }
            catch
            {
                // best-effort
            }
        }

        internal static void RecordQueueWait(string provider, string operation, double seconds)
        {
            if (!BlobjectTelemetry.Enabled) return;

            try
            {
                if (!BulkQueueDuration.Enabled) return;
                BulkQueueDuration.Record(
                    seconds,
                    new KeyValuePair<string, object>(BlobjectTelemetryNames.AttributeProvider, provider),
                    new KeyValuePair<string, object>(BlobjectTelemetryNames.AttributeOperation, operation));
            }
            catch
            {
                // best-effort
            }
        }

        internal static void RecordCopyStage(string source, string target, string stage, double seconds, Exception e)
        {
            if (!BlobjectTelemetry.Enabled) return;

            try
            {
                TagList tags = new TagList();
                tags.Add(BlobjectTelemetryNames.AttributeCopySource, source);
                tags.Add(BlobjectTelemetryNames.AttributeCopyTarget, target);
                tags.Add(BlobjectTelemetryNames.AttributeStage, stage);
                tags.Add(BlobjectTelemetryNames.AttributeOutcome, ClassifyOutcome(e, false));
                if (CopyStageDuration.Enabled) CopyStageDuration.Record(seconds, tags);
                if (CopyStageEvents.Enabled) CopyStageEvents.Add(1, tags);
            }
            catch
            {
                // best-effort
            }
        }

        internal static void RecordCopyJob(string source, string target, double seconds, Exception e, long objects, long bytes)
        {
            if (!BlobjectTelemetry.Enabled) return;

            try
            {
                TagList pair = new TagList();
                pair.Add(BlobjectTelemetryNames.AttributeCopySource, source);
                pair.Add(BlobjectTelemetryNames.AttributeCopyTarget, target);

                TagList tags = pair;
                tags.Add(BlobjectTelemetryNames.AttributeOutcome, ClassifyOutcome(e, false));
                if (e != null && !(e is OperationCanceledException)) tags.Add(BlobjectTelemetryNames.AttributeErrorType, ErrorType(e));

                if (CopyJobs.Enabled) CopyJobs.Add(1, tags);
                if (CopyDuration.Enabled) CopyDuration.Record(seconds, tags);
                if (objects > 0 && CopyObjects.Enabled) CopyObjects.Add(objects, pair);
                if (bytes > 0 && CopyBytes.Enabled) CopyBytes.Add(bytes, pair);

                if (e == null)
                {
                    _CopyLastSuccess[source + _PairSeparator + target] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }
            }
            catch
            {
                // best-effort
            }
        }

        #endregion

        #region Private-Methods

        private static string ResolveVersion()
        {
            try
            {
                Assembly assembly = typeof(BlobjectInstrumentation).Assembly;
                AssemblyInformationalVersionAttribute info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                string version = info != null ? info.InformationalVersion : null;
                if (String.IsNullOrEmpty(version)) version = assembly.GetName().Version?.ToString();
                if (String.IsNullOrEmpty(version)) return "unknown";

                int plus = version.IndexOf('+');
                return plus > 0 ? version.Substring(0, plus) : version;
            }
            catch
            {
                return "unknown";
            }
        }

        private static IEnumerable<Measurement<int>> ObserveBuildInfo()
        {
            if (!BlobjectTelemetry.Enabled) return Array.Empty<Measurement<int>>();

            return new Measurement<int>[]
            {
                new Measurement<int>(1, new KeyValuePair<string, object>(BlobjectTelemetryNames.AttributeVersion, Version))
            };
        }

        private static IEnumerable<Measurement<long>> ObserveCopyLastSuccess()
        {
            List<Measurement<long>> ret = new List<Measurement<long>>();
            if (!BlobjectTelemetry.Enabled) return ret;

            foreach (KeyValuePair<string, long> entry in _CopyLastSuccess)
            {
                int separator = entry.Key.IndexOf(_PairSeparator);
                if (separator < 0) continue;

                ret.Add(new Measurement<long>(
                    entry.Value,
                    new KeyValuePair<string, object>(BlobjectTelemetryNames.AttributeCopySource, entry.Key.Substring(0, separator)),
                    new KeyValuePair<string, object>(BlobjectTelemetryNames.AttributeCopyTarget, entry.Key.Substring(separator + 1))));
            }

            return ret;
        }

        #endregion
    }
}
