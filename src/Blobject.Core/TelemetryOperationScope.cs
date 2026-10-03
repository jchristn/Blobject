namespace Blobject.Core
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// One instrumented storage operation: its span, timer, and the details recorded when it completes.
    /// Every method is best-effort and never throws.
    /// </summary>
    internal sealed class TelemetryOperationScope
    {
        #region Internal-Members

        internal object Client { get; }

        internal string Provider { get; }

        internal string Operation { get; }

        internal Activity Activity { get; }

        #endregion

        #region Private-Members

        private readonly long _StartTimestamp;
        private long _Bytes = -1;
        private long _Objects = 0;
        private long _ItemsSucceeded = 0;
        private long _ItemsFailed = 0;
        private int _ItemsReported = 0;
        private int _Completed = 0;

        private Activity _PreviousActivity = null;
        private TelemetryOperationScope _PreviousScope = null;

        #endregion

        #region Constructors-and-Factories

        private TelemetryOperationScope(object client, string provider, string operation, Activity activity)
        {
            Client = client;
            Provider = provider;
            Operation = operation;
            Activity = activity;
            _StartTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Begin an operation.  Returns null when telemetry is disabled or unobserved, or when the same client is
        /// already inside the same operation (an overload delegating to another overload), so it is recorded once.
        /// Leaves Activity.Current unchanged; call <see cref="Enter"/> to make the operation ambient.
        /// </summary>
        internal static TelemetryOperationScope Begin(
            object client,
            string provider,
            string operation,
            string container,
            string serverAddress,
            string key)
        {
            try
            {
                if (!BlobjectInstrumentation.IsObserved) return null;

                TelemetryOperationScope current = BlobjectInstrumentation.CurrentScope.Value;
                if (current != null
                    && ReferenceEquals(current.Client, client)
                    && String.Equals(current.Operation, operation, StringComparison.Ordinal))
                {
                    return null;
                }

                if (String.IsNullOrEmpty(provider)) provider = BlobjectTelemetryNames.ProviderCustom;

                Activity previous = Activity.Current;
                ActivityKind kind = String.Equals(provider, BlobjectTelemetryNames.ProviderDisk, StringComparison.Ordinal)
                    ? ActivityKind.Internal
                    : ActivityKind.Client;

                Activity activity = BlobjectInstrumentation.Source.StartActivity(provider + " " + operation, kind);
                Activity.Current = previous;

                if (activity != null && activity.IsAllDataRequested)
                {
                    activity.SetTag(BlobjectTelemetryNames.AttributeProvider, provider);
                    activity.SetTag(BlobjectTelemetryNames.AttributeOperation, operation);
                    if (!String.IsNullOrEmpty(container)) activity.SetTag(BlobjectTelemetryNames.AttributeContainer, container);
                    if (!String.IsNullOrEmpty(serverAddress)) activity.SetTag(BlobjectTelemetryNames.AttributeServerAddress, serverAddress);
                    if (key != null && BlobjectTelemetry.RecordKeys) activity.SetTag(BlobjectTelemetryNames.AttributeKey, key);
                }

                TelemetryOperationScope scope = new TelemetryOperationScope(client, provider, operation, activity);
                BlobjectInstrumentation.AddOperationCount(BlobjectInstrumentation.OperationActive, provider, operation, 1);
                return scope;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Make this operation the ambient operation and span.
        /// </summary>
        internal void Enter()
        {
            try
            {
                _PreviousScope = BlobjectInstrumentation.CurrentScope.Value;
                _PreviousActivity = Activity.Current;
                BlobjectInstrumentation.CurrentScope.Value = this;
                if (Activity != null) Activity.Current = Activity;
            }
            catch
            {
                // best-effort
            }
        }

        /// <summary>
        /// Restore the ambient operation and span captured by <see cref="Enter"/>.
        /// </summary>
        internal void Exit()
        {
            try
            {
                BlobjectInstrumentation.CurrentScope.Value = _PreviousScope;
                Activity.Current = _PreviousActivity;
            }
            catch
            {
                // best-effort
            }
        }

        internal void SetBytes(long bytes)
        {
            if (bytes >= 0) Interlocked.Exchange(ref _Bytes, bytes);
        }

        internal void AddObjects(long count)
        {
            Interlocked.Add(ref _Objects, count);
        }

        internal void AddItemResult(bool success)
        {
            if (success) Interlocked.Increment(ref _ItemsSucceeded);
            else Interlocked.Increment(ref _ItemsFailed);
        }

        internal void SetItemResults(long succeeded, long failed)
        {
            Interlocked.Exchange(ref _ItemsSucceeded, succeeded);
            Interlocked.Exchange(ref _ItemsFailed, failed);
            Interlocked.Exchange(ref _ItemsReported, 1);
        }

        internal void SetMaxConcurrency(int maxConcurrency)
        {
            try
            {
                if (Activity != null && Activity.IsAllDataRequested)
                    Activity.SetTag(BlobjectTelemetryNames.AttributeMaxConcurrency, maxConcurrency);
            }
            catch
            {
                // best-effort
            }
        }

        /// <summary>
        /// Complete the operation, recording metrics and ending the span.  Only the first call has an effect.
        /// </summary>
        /// <param name="e">Exception that ended the operation, or null on success.</param>
        /// <param name="notFound">True if the exception means the object does not exist.</param>
        internal void Complete(Exception e, bool notFound)
        {
            if (Interlocked.Exchange(ref _Completed, 1) != 0) return;

            Activity ambient = null;

            try
            {
                double seconds = BlobjectInstrumentation.ElapsedSeconds(_StartTimestamp);
                string outcome = BlobjectInstrumentation.ClassifyOutcome(e, notFound);
                bool failed = e != null && !(e is OperationCanceledException);
                string errorType = failed ? BlobjectInstrumentation.ErrorType(e) : null;

                // measurements are recorded while this span is ambient so listeners and exemplars see its trace context
                ambient = Activity.Current;
                if (Activity != null) Activity.Current = Activity;

                RecordMetrics(seconds, outcome, errorType);
                CompleteActivity(e, outcome, errorType);
            }
            catch
            {
                // best-effort
            }
            finally
            {
                try
                {
                    BlobjectInstrumentation.AddOperationCount(BlobjectInstrumentation.OperationActive, Provider, Operation, -1);
                    if (Activity != null) Activity.Dispose();
                    Activity.Current = ReferenceEquals(ambient, Activity) ? _PreviousActivity : ambient;
                }
                catch
                {
                    // best-effort
                }
            }
        }

        #endregion

        #region Private-Methods

        private string Direction
        {
            get
            {
                switch (Operation)
                {
                    case BlobjectTelemetryNames.OperationGet:
                    case BlobjectTelemetryNames.OperationGetStream:
                        return BlobjectTelemetryNames.DirectionRead;
                    case BlobjectTelemetryNames.OperationWrite:
                        return BlobjectTelemetryNames.DirectionWrite;
                    default:
                        return null;
                }
            }
        }

        private void RecordMetrics(double seconds, string outcome, string errorType)
        {
            TagList tags = new TagList();
            tags.Add(BlobjectTelemetryNames.AttributeProvider, Provider);
            tags.Add(BlobjectTelemetryNames.AttributeOperation, Operation);
            tags.Add(BlobjectTelemetryNames.AttributeOutcome, outcome);
            if (errorType != null) tags.Add(BlobjectTelemetryNames.AttributeErrorType, errorType);

            if (BlobjectInstrumentation.OperationDuration.Enabled)
                BlobjectInstrumentation.OperationDuration.Record(seconds, tags);

            if (errorType != null && BlobjectInstrumentation.OperationErrors.Enabled)
                BlobjectInstrumentation.OperationErrors.Add(1, tags);

            long bytes = Interlocked.Read(ref _Bytes);
            string direction = Direction;
            if (bytes > 0 && direction != null && errorType == null && BlobjectInstrumentation.IoBytes.Enabled)
            {
                TagList ioTags = new TagList();
                ioTags.Add(BlobjectTelemetryNames.AttributeProvider, Provider);
                ioTags.Add(BlobjectTelemetryNames.AttributeOperation, Operation);
                ioTags.Add(BlobjectTelemetryNames.AttributeDirection, direction);
                BlobjectInstrumentation.IoBytes.Add(bytes, ioTags);
            }

            long objects = Interlocked.Read(ref _Objects);
            if (objects > 0 && BlobjectInstrumentation.EnumerateObjects.Enabled)
            {
                BlobjectInstrumentation.EnumerateObjects.Add(
                    objects,
                    new System.Collections.Generic.KeyValuePair<string, object>(BlobjectTelemetryNames.AttributeProvider, Provider));
            }

            long succeeded = Interlocked.Read(ref _ItemsSucceeded);
            long itemsFailed = Interlocked.Read(ref _ItemsFailed);
            if (BlobjectInstrumentation.BulkItems.Enabled)
            {
                if (succeeded > 0) BlobjectInstrumentation.BulkItems.Add(succeeded, BulkTags(BlobjectTelemetryNames.OutcomeSuccess));
                if (itemsFailed > 0) BlobjectInstrumentation.BulkItems.Add(itemsFailed, BulkTags(BlobjectTelemetryNames.OutcomeError));
            }
        }

        private TagList BulkTags(string outcome)
        {
            TagList tags = new TagList();
            tags.Add(BlobjectTelemetryNames.AttributeProvider, Provider);
            tags.Add(BlobjectTelemetryNames.AttributeOperation, Operation);
            tags.Add(BlobjectTelemetryNames.AttributeOutcome, outcome);
            return tags;
        }

        private void CompleteActivity(Exception e, string outcome, string errorType)
        {
            if (Activity == null || !Activity.IsAllDataRequested) return;

            Activity.SetTag(BlobjectTelemetryNames.AttributeOutcome, outcome);

            long bytes = Interlocked.Read(ref _Bytes);
            if (bytes >= 0) Activity.SetTag(BlobjectTelemetryNames.AttributeBytes, bytes);

            long objects = Interlocked.Read(ref _Objects);
            if (String.Equals(Operation, BlobjectTelemetryNames.OperationEnumerate, StringComparison.Ordinal))
                Activity.SetTag(BlobjectTelemetryNames.AttributeObjects, objects);

            long succeeded = Interlocked.Read(ref _ItemsSucceeded);
            long itemsFailed = Interlocked.Read(ref _ItemsFailed);
            if (succeeded > 0 || itemsFailed > 0 || Interlocked.CompareExchange(ref _ItemsReported, 0, 0) == 1)
            {
                Activity.SetTag(BlobjectTelemetryNames.AttributeItems, succeeded + itemsFailed);
                Activity.SetTag(BlobjectTelemetryNames.AttributeItemsFailed, itemsFailed);
            }

            if (errorType != null)
            {
                Activity.SetTag(BlobjectTelemetryNames.AttributeErrorType, errorType);
                Activity.SetStatus(ActivityStatusCode.Error, e.Message);
                BlobjectInstrumentation.RecordException(Activity, e);
            }
            else if (e != null)
            {
                // cancelled: not a failure of the storage system
                Activity.SetStatus(ActivityStatusCode.Unset);
            }
            else
            {
                Activity.SetStatus(ActivityStatusCode.Ok);
            }
        }

        #endregion
    }
}
