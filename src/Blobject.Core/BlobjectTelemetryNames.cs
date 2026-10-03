namespace Blobject.Core
{
    /// <summary>
    /// Stable names emitted by Blobject telemetry: the meter and activity source names, metric instrument names,
    /// span and metric attribute keys, and the bounded values used for operation, provider, outcome, and stage labels.
    /// These names are a public contract consumed by dashboards and alerts; they do not change between minor versions.
    /// Refer to TELEMETRY.md for the full catalog.
    /// </summary>
    public static class BlobjectTelemetryNames
    {
        #region Sources

        /// <summary>
        /// Name of the <see cref="System.Diagnostics.Metrics.Meter"/> that emits every Blobject metric.
        /// </summary>
        public const string MeterName = "Blobject";

        /// <summary>
        /// Name of the <see cref="System.Diagnostics.ActivitySource"/> that emits every Blobject span.
        /// </summary>
        public const string ActivitySourceName = "Blobject";

        #endregion

        #region Metrics

        /// <summary>
        /// Histogram, seconds: duration of each storage operation.  Labels: provider, operation, outcome, error.type (failures only).
        /// </summary>
        public const string OperationDuration = "blobject.operation.duration";

        /// <summary>
        /// UpDownCounter: storage operations currently in flight.  Labels: provider, operation.
        /// </summary>
        public const string OperationActive = "blobject.operation.active";

        /// <summary>
        /// Counter: failed storage operations.  Labels: provider, operation, outcome, error.type.
        /// </summary>
        public const string OperationErrors = "blobject.operation.errors";

        /// <summary>
        /// Counter: operation attempts retried after a dropped connection.  Labels: provider, operation.
        /// </summary>
        public const string OperationRetries = "blobject.operation.retries";

        /// <summary>
        /// Counter, bytes: object bytes read or written.  Labels: provider, operation, direction.
        /// </summary>
        public const string IoBytes = "blobject.io.bytes";

        /// <summary>
        /// Counter: objects returned by enumeration.  Labels: provider.
        /// </summary>
        public const string EnumerateObjects = "blobject.enumerate.objects";

        /// <summary>
        /// Counter: items processed by bulk operations (write_many, delete_many, empty).  Labels: provider, operation, outcome.
        /// </summary>
        public const string BulkItems = "blobject.bulk.items";

        /// <summary>
        /// Histogram, seconds: time a bulk item waited for a concurrency slot (the queued stage).  Labels: provider, operation.
        /// </summary>
        public const string BulkQueueDuration = "blobject.bulk.queue.duration";

        /// <summary>
        /// UpDownCounter: bulk concurrency slots in use.  Labels: provider, operation.
        /// </summary>
        public const string BulkSlotsInUse = "blobject.bulk.slots.in_use";

        /// <summary>
        /// UpDownCounter: bulk concurrency slots available to running bulk operations (sum of MaxConcurrency).  Labels: provider, operation.
        /// </summary>
        public const string BulkSlotsCapacity = "blobject.bulk.slots.capacity";

        /// <summary>
        /// Histogram, seconds: duration of establishing a connection (CIFS, NFS).  Labels: provider, outcome, error.type (failures only).
        /// </summary>
        public const string ConnectionDuration = "blobject.connection.duration";

        /// <summary>
        /// UpDownCounter: open connections (CIFS, NFS).  Labels: provider.
        /// </summary>
        public const string ConnectionOpen = "blobject.connection.open";

        /// <summary>
        /// UpDownCounter: connection pool capacity of live clients (CIFS, NFS).  Labels: provider.
        /// </summary>
        public const string ConnectionPoolCapacity = "blobject.connection.pool.capacity";

        /// <summary>
        /// Counter: connections discarded after a transport failure (CIFS, NFS).  Labels: provider, error.type.
        /// </summary>
        public const string ConnectionResets = "blobject.connection.resets";

        /// <summary>
        /// Counter: BlobCopy jobs.  Labels: source, target, outcome.
        /// </summary>
        public const string CopyJobs = "blobject.copy.jobs";

        /// <summary>
        /// Histogram, seconds: BlobCopy job duration.  Labels: source, target, outcome.
        /// </summary>
        public const string CopyDuration = "blobject.copy.duration";

        /// <summary>
        /// Histogram, seconds: BlobCopy per-stage duration (enumerate, read, write).  Labels: source, target, stage, outcome.
        /// </summary>
        public const string CopyStageDuration = "blobject.copy.stage.duration";

        /// <summary>
        /// Counter: BlobCopy per-stage events (enumerate, read, write).  Labels: source, target, stage, outcome.
        /// </summary>
        public const string CopyStageEvents = "blobject.copy.stage.events";

        /// <summary>
        /// Counter: objects copied by BlobCopy.  Labels: source, target.
        /// </summary>
        public const string CopyObjects = "blobject.copy.objects";

        /// <summary>
        /// Counter, bytes: bytes copied by BlobCopy.  Labels: source, target.
        /// </summary>
        public const string CopyBytes = "blobject.copy.bytes";

        /// <summary>
        /// Gauge, seconds: Unix time of the last successful BlobCopy job.  Labels: source, target.
        /// </summary>
        public const string CopyLastSuccess = "blobject.copy.last_success";

        /// <summary>
        /// Gauge: always 1, labeled with the Blobject.Core version.  Labels: version.
        /// </summary>
        public const string BuildInfo = "blobject.build.info";

        #endregion

        #region Attributes

        /// <summary>
        /// Attribute: storage provider, one of the Provider* values.
        /// </summary>
        public const string AttributeProvider = "blobject.provider";

        /// <summary>
        /// Attribute: operation, one of the Operation* values.
        /// </summary>
        public const string AttributeOperation = "blobject.operation";

        /// <summary>
        /// Attribute: outcome, one of the Outcome* values.
        /// </summary>
        public const string AttributeOutcome = "blobject.outcome";

        /// <summary>
        /// Attribute: error type, the full name of the exception type (OpenTelemetry semantic convention).
        /// </summary>
        public const string AttributeErrorType = "error.type";

        /// <summary>
        /// Attribute: data direction, read or write.
        /// </summary>
        public const string AttributeDirection = "blobject.direction";

        /// <summary>
        /// Attribute: BlobCopy pipeline stage, one of the Stage* values.
        /// </summary>
        public const string AttributeStage = "blobject.stage";

        /// <summary>
        /// Attribute: BlobCopy source provider.
        /// </summary>
        public const string AttributeCopySource = "blobject.copy.source";

        /// <summary>
        /// Attribute: BlobCopy target provider.
        /// </summary>
        public const string AttributeCopyTarget = "blobject.copy.target";

        /// <summary>
        /// Attribute: library version.
        /// </summary>
        public const string AttributeVersion = "blobject.version";

        /// <summary>
        /// Span attribute: bucket, container, share, export, or directory.  Never used on metrics.
        /// </summary>
        public const string AttributeContainer = "blobject.container";

        /// <summary>
        /// Span attribute: object key.  Recorded only when <see cref="BlobjectTelemetry.RecordKeys"/> is true.  Never used on metrics.
        /// </summary>
        public const string AttributeKey = "blobject.key";

        /// <summary>
        /// Span attribute: object bytes read or written by the operation.
        /// </summary>
        public const string AttributeBytes = "blobject.bytes";

        /// <summary>
        /// Span attribute: objects returned by an enumeration.
        /// </summary>
        public const string AttributeObjects = "blobject.objects";

        /// <summary>
        /// Span attribute: items in a bulk operation.
        /// </summary>
        public const string AttributeItems = "blobject.items";

        /// <summary>
        /// Span attribute: items that failed in a bulk operation.
        /// </summary>
        public const string AttributeItemsFailed = "blobject.items.failed";

        /// <summary>
        /// Span attribute: MaxConcurrency of a bulk operation.
        /// </summary>
        public const string AttributeMaxConcurrency = "blobject.max_concurrency";

        /// <summary>
        /// Span attribute: server host name or endpoint (OpenTelemetry semantic convention).
        /// </summary>
        public const string AttributeServerAddress = "server.address";

        /// <summary>
        /// Span event name: a connection was discarded after a transport failure.
        /// </summary>
        public const string EventConnectionReset = "blobject.connection.reset";

        /// <summary>
        /// Span event name: an operation attempt was retried.
        /// </summary>
        public const string EventRetry = "blobject.retry";

        #endregion

        #region Providers

        /// <summary>
        /// Provider: Amazon S3 and S3-compatible storage through AWSSDK (Blobject.AmazonS3).
        /// </summary>
        public const string ProviderAmazonS3 = "aws_s3";

        /// <summary>
        /// Provider: Amazon S3 and S3-compatible storage through S3Lite (Blobject.AmazonS3Lite).
        /// </summary>
        public const string ProviderAmazonS3Lite = "aws_s3_lite";

        /// <summary>
        /// Provider: Microsoft Azure BLOB storage (Blobject.AzureBlob).
        /// </summary>
        public const string ProviderAzureBlob = "azure_blob";

        /// <summary>
        /// Provider: Google Cloud Storage (Blobject.GoogleCloud).
        /// </summary>
        public const string ProviderGoogleCloud = "gcp_storage";

        /// <summary>
        /// Provider: local filesystem (Blobject.Disk).
        /// </summary>
        public const string ProviderDisk = "disk";

        /// <summary>
        /// Provider: CIFS/SMB file shares (Blobject.CIFS).
        /// </summary>
        public const string ProviderCifs = "cifs";

        /// <summary>
        /// Provider: NFS exports (Blobject.NFS).
        /// </summary>
        public const string ProviderNfs = "nfs";

        /// <summary>
        /// Provider: a client derived from BlobClientBase outside this repository that does not override TelemetryProvider.
        /// </summary>
        public const string ProviderCustom = "custom";

        #endregion

        #region Operations

        /// <summary>
        /// Operation: ValidateConnectivity.
        /// </summary>
        public const string OperationValidateConnectivity = "validate_connectivity";

        /// <summary>
        /// Operation: list buckets, containers, or shares.
        /// </summary>
        public const string OperationListContainers = "list_containers";

        /// <summary>
        /// Operation: GetAsync.
        /// </summary>
        public const string OperationGet = "get";

        /// <summary>
        /// Operation: GetStreamAsync.
        /// </summary>
        public const string OperationGetStream = "get_stream";

        /// <summary>
        /// Operation: GetMetadataAsync.
        /// </summary>
        public const string OperationGetMetadata = "get_metadata";

        /// <summary>
        /// Operation: WriteAsync (all overloads).
        /// </summary>
        public const string OperationWrite = "write";

        /// <summary>
        /// Operation: WriteManyAsync.
        /// </summary>
        public const string OperationWriteMany = "write_many";

        /// <summary>
        /// Operation: DeleteAsync.
        /// </summary>
        public const string OperationDelete = "delete";

        /// <summary>
        /// Operation: DeleteManyAsync.
        /// </summary>
        public const string OperationDeleteMany = "delete_many";

        /// <summary>
        /// Operation: one native batch-delete request issued by DeleteManyAsync (Amazon S3, Azure).
        /// </summary>
        public const string OperationDeleteBatch = "delete_batch";

        /// <summary>
        /// Operation: ExistsAsync.
        /// </summary>
        public const string OperationExists = "exists";

        /// <summary>
        /// Operation: Enumerate and EnumerateAsync.
        /// </summary>
        public const string OperationEnumerate = "enumerate";

        /// <summary>
        /// Operation: EmptyAsync.
        /// </summary>
        public const string OperationEmpty = "empty";

        /// <summary>
        /// Operation: establishing a connection (CIFS, NFS).
        /// </summary>
        public const string OperationConnect = "connect";

        #endregion

        #region Outcomes

        /// <summary>
        /// Outcome: the operation succeeded.
        /// </summary>
        public const string OutcomeSuccess = "success";

        /// <summary>
        /// Outcome: the requested object does not exist.
        /// </summary>
        public const string OutcomeNotFound = "not_found";

        /// <summary>
        /// Outcome: the operation was cancelled.
        /// </summary>
        public const string OutcomeCancelled = "cancelled";

        /// <summary>
        /// Outcome: the operation failed.
        /// </summary>
        public const string OutcomeError = "error";

        #endregion

        #region Error-Types

        /// <summary>
        /// error.type on blobject.connection.resets when a pooled connection was found disconnected before use
        /// (for example after a server restart or idle timeout) rather than failing during an operation.
        /// </summary>
        public const string ErrorTypeConnectionLost = "connection_lost";

        #endregion

        #region Directions-and-Stages

        /// <summary>
        /// Direction: bytes read from storage.
        /// </summary>
        public const string DirectionRead = "read";

        /// <summary>
        /// Direction: bytes written to storage.
        /// </summary>
        public const string DirectionWrite = "write";

        /// <summary>
        /// BlobCopy stage: enumerating source objects.
        /// </summary>
        public const string StageEnumerate = "enumerate";

        /// <summary>
        /// BlobCopy stage: opening and reading a source object.
        /// </summary>
        public const string StageRead = "read";

        /// <summary>
        /// BlobCopy stage: writing a target object.
        /// </summary>
        public const string StageWrite = "write";

        /// <summary>
        /// Span name of a BlobCopy job.
        /// </summary>
        public const string SpanCopy = "blobject copy";

        #endregion
    }
}
