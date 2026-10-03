# Change Log

## Current Version

v6.1.x

### All packages v6.1.2

- Dependency updates: `Timestamps` 1.0.12 -> 1.0.13 (`Blobject.Core`)
- New `CopyStatsTime` contract test verifies `CopyStatistics.Time` start, end, and elapsed values from `BlobCopy`
- Test harness: the in-process OpenCIFS test server no longer requests SMB 3.x encryption on platforms without AES-CCM (macOS), matching the telemetry suites; the `nfs-unfs3` test image pins `linux/amd64` so it builds on arm64 hosts

### All packages v6.1.1

- Dependency updates: `AWSSDK.S3` 4.0.103.4 -> 4.0.104.1, `Azure.Storage.Blobs` 12.29.2 -> 12.30.0, `Azure.Storage.Blobs.Batch` 12.26.1 -> 12.27.0, `OpenNFS.Client` 0.1.1 -> 0.2.0, `S3Lite` 1.2.3 -> 1.3.0
- `OpenNFS.Client` 0.2.0 and `S3Lite` 1.3.0 add their own metrics and traces (`Meter`/`ActivitySource` named `OpenNFS.Client` and `S3Lite`); their spans nest under Blobject's `nfs <operation>` and `aws_s3_lite <operation>` spans.  Refer to TELEMETRY.md
- Test dependencies updated: `OpenNFS.Server` 0.2.0, `Touchstone.*` 0.2.0
- New telemetry test cases verify that `S3Lite` and `OpenNFS.Client` spans nest under the Blobject operation span that issued them

### All packages v6.1.0

- Added built-in telemetry: metrics through the `System.Diagnostics.Metrics.Meter` named `Blobject` and traces through the `System.Diagnostics.ActivitySource` named `Blobject`.  The library has no exporter or SDK dependency; hosts subscribe to the names (OpenTelemetry `AddMeter`/`AddSource`, Radiant `Sources.AddMeter`/`AddActivitySource`).  Refer to TELEMETRY.md
- Every public storage operation (`ValidateConnectivity`, list buckets/containers/shares, `GetAsync`, `GetStreamAsync`, `GetMetadataAsync`, `WriteAsync`, `DeleteAsync`, `ExistsAsync`, `Enumerate`/`EnumerateAsync`, `WriteManyAsync`, `DeleteManyAsync`, `EmptyAsync`) emits a `<provider> <operation>` span and records `blobject.operation.duration`, `blobject.operation.active`, `blobject.operation.errors` (by `error.type`), and `blobject.io.bytes`, with `success`, `not_found`, `cancelled`, or `error` outcomes
- Bulk operations record per-item outcomes (`blobject.bulk.items`), the time items wait for a concurrency slot (`blobject.bulk.queue.duration`), and slots in use and capacity; native S3 and Azure batch deletes get a `delete_batch` span per request
- CIFS and NFS record connection establishment (`<provider> connect` span, `blobject.connection.duration`), open connections, pool capacity, connection resets (including connections found dropped after a server restart, `error.type=connection_lost`), and retries
- `BlobCopy` records a `blobject copy` job span with `stage:read`/`stage:write` child spans, job and per-stage (`enumerate`, `read`, `write`) duration histograms and counters, objects and bytes copied, and a last-success timestamp gauge
- `blobject.build.info` gauge labeled with the library version
- New `BlobjectTelemetry` settings: `Enabled` (default true) and `RecordKeys` (default false; object keys are never metric labels)
- New `BlobjectTelemetryNames` constants for every meter, instrument, attribute, and label value
- New protected helpers on `BlobClientBase` (`InstrumentAsync`, `InstrumentEnumerateAsync`, `ForEachConcurrentAsync`, `TelemetryProvider`, and others) so custom providers can emit the same telemetry
- `Blobject.Core` adds a `System.Diagnostics.DiagnosticSource` 10.0.12 dependency for `netstandard2.1` only; `net8.0` and `net10.0` use the in-box version
- Instrumentation is best-effort: telemetry failures, including exceptions thrown by listeners, never affect storage operations

## Previous Versions

v6.0.x

### Blobject.CIFS v6.0.0

- Migrated from EzSmb to [OpenCIFS](https://github.com/jchristn/OpenCIFS) 0.1.1 (MIT), supporting SMB 2.0.2 through SMB 3.x with signing and SMB 3.x encryption
- Persistent SMB connections, pooled (`CifsSettings.MaxConnections`, default 4) because OpenCIFS runs one request at a time per connection, with automatic reconnect after a dropped connection or server restart
- `GetStreamAsync` returns a seekable stream that reads from the server on demand instead of buffering the whole object; stream writes are sent in chunks
- Enumeration reads directories with paged queries, so large directories are listed completely, and returns keys with the names as stored on the server
- `CancellationToken` is honored by every operation; enumeration no longer blocks on `.Result`
- Fixed `CifsSettings.Hostname` setter not updating `Hostname`, and metadata reporting the last-access time as `LastUpdateUtc`; `LastUpdateUtc` is now the last-write time
- New settings: `Port`, `Domain` (a `domain\username` value in `Username` still works), `RequireSigning`, `PreferEncryption`, `ConnectTimeoutMs`, `MaxConnections`, and a constructor accepting the port
- `CifsBlobClient` implements `IAsyncDisposable`
- Breaking: targets `net8.0` and `net10.0` only (`netstandard2.1` dropped, as OpenCIFS requires .NET 8 or later)
- Breaking: `GetAsync` and `GetStreamAsync` throw `KeyNotFoundException` for a missing key instead of returning empty content, matching `GetMetadataAsync`
- Breaking: keys containing characters SMB reserves (`< > : " | ? *` and control characters) are rejected with `ArgumentException`; on Windows servers `:` would otherwise silently address an NTFS alternate data stream
- Breaking: keys that are empty, consist only of separators, or contain `.` or `..` segments are rejected with `ArgumentException`
- Deleting a folder that is not empty throws `IOException`; deleting a missing key succeeds

### Blobject.NFS v6.0.0

- Migrated from NFS-Client to [OpenNFS](https://github.com/jchristn/OpenNFS) 0.1.1 (MIT)
- Fully asynchronous with concurrency support (the previous implementation was synchronous and forced `MaxConcurrency = 1`), using persistent pooled connections with automatic reconnect
- `CancellationToken` is honored by every operation
- Overwriting an object now truncates it, writes to new keys create the file and any missing parent folders, and `Unstable` writes are committed to stable storage once each object is written
- `GetStreamAsync` returns a seekable stream that reads from the server on demand
- The MOUNT port is discovered through the portmapper by default (`NfsSettings.MountPort = 0`), or can be set explicitly
- Fixed `NfsSettings.Hostname` setter not updating `Hostname`
- New settings: `Port`, `MountPort`, `PortmapperPort`, `MachineName`, `WriteStability` (new `NfsWriteStabilityEnum`), `ConnectTimeoutMs`, `ResponseTimeoutMs`
- `NfsBlobClient` implements `IAsyncDisposable`; the connection is established on first use rather than in the constructor
- Breaking: targets `net8.0` and `net10.0` only (`netstandard2.1` dropped, as OpenNFS requires .NET 8 or later)
- Breaking: only NFSv3 is supported; constructing a client with `NfsVersionEnum.V2` or `V4` throws `NotSupportedException`
- Breaking: `GetAsync` and `GetStreamAsync` throw `KeyNotFoundException` for a missing key instead of returning `null`
- Breaking: `GenerateUrl` returns an RFC 2224 URL such as `nfs://server/export/key` instead of `/ip/share/key`
- Breaking: keys that are empty, consist only of separators, or contain `.` or `..` segments are rejected with `ArgumentException`
- `CreatedUtc` is not populated, as NFSv3 does not record a creation time

### Both CIFS and NFS

- Enumeration returns files and folders; each folder is returned with `IsFolder` set and a key ending in `/`, after its contents, and the folder named exactly by an enumeration prefix is not returned
- `EmptyAsync` removes every file and folder
- A key ending in `/` creates a folder; reading a folder returns empty content
- Objects are written in place, as with any file share client: concurrent writes to the same key are not atomic (on NFS the result can interleave writers' data; SMB may reject a concurrent writer with a sharing violation), and a reader can observe an object while it is being written

### Blobject.AmazonS3 v6.0.0

- Fixed `WriteAsync(key, contentType, contentLength, stream)` ignoring `contentLength`: the whole stream was uploaded, and a non-seekable stream failed with "Could not determine content length"; exactly `contentLength` bytes are now uploaded
- Fixed `BlobCopy` with an S3 target, which failed because source streams are non-seekable
- A negative `contentLength` now throws `ArgumentOutOfRangeException` instead of being treated as empty
- `DeleteManyAsync` treats a `NoSuchKey` batch-delete error as a successful deletion, matching `DeleteAsync`; AWS reports missing keys as deleted, but some S3-compatible servers (e.g. Less3) return `NoSuchKey`
- Updated `AWSSDK.S3` to 4.0.103.4

### Blobject.GoogleCloud v6.0.0

- Fixed `WriteAsync(key, contentType, contentLength, stream)` ignoring `contentLength` and uploading the whole stream
- Updated `Google.Cloud.Storage.V1` to 5.0.0

### Blobject.AzureBlob, Blobject.AmazonS3Lite, Blobject.Core, Blobject.Disk v6.0.0

- `Blobject.Core` adds `LengthLimitedReadStream`, used by providers to honor `contentLength` on stream writes, and updates `System.Text.Json` to 10.0.12
- `Blobject.AzureBlob` updates `Azure.Storage.Blobs` to 12.29.2 and `Azure.Storage.Blobs.Batch` to 12.26.1
- `Blobject.AmazonS3Lite` updates `S3Lite` to 1.2.3
- `Blobject.Disk` has no functional changes

### Tests

- New CIFS and NFS test harness that starts ephemeral servers automatically: in-process OpenCIFS and OpenNFS servers, and Dockerized Samba, nfs-ganesha, Linux knfsd, and unfs3; containers are labeled, backed by anonymous volumes, and always removed
- For each server, runs the provider contract suites plus protocol semantics, enumeration, concurrency, connection lifecycle, server-side interoperability, and SMB- or NFS-specific suites, and a server-independent API surface suite, from `Test.Automated` (`--provider fileshare`), xUnit, and NUnit
- Contract suites recognize hierarchical providers (CIFS and NFS), which enumerate folder entries; new `FolderEntries` contract case
- `Test.Automated` accepts `--filter` to run matching cases only, and `--s3-path-style` for S3 Lite against S3-compatible servers addressed by IP
- New contract cases `NonSeekableShortContentLength` and `NegativeContentLengthRejected`; the default suite now has 108 cases
- Verified against Google Cloud Storage, Azurite, MinIO, and Less3 in addition to the CIFS and NFS servers
- Updated test dependencies: `Microsoft.NET.Test.Sdk` 18.10.1, `NUnit` 5.0.0, `NUnit3TestAdapter` 6.3.0, `SerializationHelper` 2.1.0

v5.1.x

- `Blobject.Core` v5.1.0 adds `DeleteManyAsync`, a bulk delete API returning a per-key `DeleteManyResult` (with `Success`, `Deleted`, and `Failed` helpers), fanned out over `DeleteAsync` using `BlobClientBase.MaxConcurrency`
- `Blobject.AmazonS3` v5.1.0 overrides `DeleteManyAsync` to use the native S3 `DeleteObjects` batch delete (up to 1000 keys per request)
- `Blobject.AzureBlob` v5.1.0 overrides `DeleteManyAsync` to use the native Azure Blob Batch delete (up to 256 keys per request) and adds the `Azure.Storage.Blobs.Batch` dependency
- `Blobject.AmazonS3Lite`, `Blobject.GoogleCloud`, `Blobject.Disk`, `Blobject.CIFS`, and `Blobject.NFS` v5.1.0 expose `DeleteManyAsync` using the shared fan-out behavior
- Deleting a key that does not exist is treated as a successful deletion, matching `DeleteAsync`
- Expanded contract test coverage with positive and negative `DeleteManyAsync` cases

v5.0.x

- `Blobject.Core` v5.0.19 adds `BlobCopy.StartAsync`, fixes prefix handling and normal termination in `BlobCopy`, and makes copy streaming-first
- `Blobject.Core` v5.0.19 adds shared bounded-concurrency behavior for `WriteManyAsync` and `EmptyAsync` via `BlobClientBase.MaxConcurrency`
- `Blobject.Core` v5.0.19 preserves enumeration filter casing, adds filter cloning, and centralizes AWS region normalization
- `Blobject.AmazonS3` v5.0.20 fixes stream reads, empty writes, filter handling, and shared bulk behavior
- `Blobject.AmazonS3Lite` v5.0.20 implements `GetStreamAsync` and fixes empty writes, stream length handling, filter handling, and shared bulk behavior
- `Blobject.AzureBlob` v5.0.19 fixes empty writes, async enumeration, filter handling, and shared bulk behavior
- `Blobject.CIFS` v5.0.19 fixes `GetStreamAsync`, empty writes, filter handling, and shared bulk behavior
- `Blobject.Disk` v5.0.19 fixes overwrite truncation, empty writes, filter handling, duplicate enumeration, and folder marker cleanup in `EmptyAsync`
- `Blobject.GoogleCloud` v5.0.19 fixes empty writes, filter handling, and shared bulk behavior
- `Blobject.NFS` v5.0.19 fixes empty writes, case-sensitive filtering, and shared bulk behavior
- Added Touchstone-based automated contract tests: `Test.Shared`, `Test.Automated`, `Test.Xunit`, and `Test.Nunit`
- Expanded `Test.Shared` to 80 default provider contract cases, plus optional stress cases, with Disk temporary storage by default and provider-specific overrides for remote repositories
- Updated and consolidated NuGet package references across library and test projects, including provider SDKs, `System.Text.Json`, `Timestamps`, console test helpers, and test adapters
- `Blobject.GoogleCloud` now uses the current service-account credential factory path required by the updated Google auth packages
- `Blobject.AmazonS3` and `Blobject.AmazonS3Lite` v5.0.19 normalize AWS region aliases such as `USEast2` to DNS-safe names such as `us-east-2`
- Updated S3 region normalization for the current Amazon S3 regular endpoint region list
- Default AWS S3 settings to HTTPS unless explicitly disabled
- Rename from `BlobHelper` to `Blobject`
- Added support for CIFS, NFS, and Google Cloud Storage
- Remove use of continuation tokens for disk
- Add `S3Lite` variant, not dependent on AWSSDK
- Refactor

v4.1.x

- Refactor recommendation by @Revazashvili to interface and implementation
- Minor class name change; `Blobs` becomes `BlobClient`

v4.0.x

- Migrated from deprecated `Microsoft.WindowsAzure.Storage` to `Azure.Storage.Blobs`
- Removed `Newtonsoft.Json` dependency
- Add targeting for `net48`
- Fixed issues where certain operations were not using `CancellationToken`
- Added `Empty` API, which is a destructive API to delete all objects in the container
- Validated `WriteMany` and `Empty` on all storage providers
- Added `EuWest2`, thank you @DanielHarman

v2.3.x

- Dependency update and bugfixes

v2.2.0

- BlobCopy class to copy objects from one repository to another (thank you @phpfui!)

v2.1.4

- Dependency update
- Disk fixes (thank you @teh-random-name)

v2.1.1

- Enhancements to `EnumerationResult`
- Minor refactor

v2.0.4.2

- Retarget to support .NET Standard 2.0, .NET Core 2.0, and .NET Framework 4.6.1

v2.0.4

- Added support for Komodo as a storage repository

v2.0.3

- Added AwsS3 property `BaseUrl` for returning BLOB URLs

v2.0.2

- Added support for writing strings

v2.0.1

- Breaking changes
- Fully async APIs
- Separation of `Get` and `GetStream` APIs
- `BlobData` object returned when using `GetStream` API to download objects to stream (contains content length and stream)
- `EnumerationResult` object returned for enumeration results including continuation token and list of `BlobMetadata` objects
- Internal consistency amongst APIs
- Dependency updates

v1.3.5

- Added `string GenerateUrl(string key)` API
- Fixed test project issue with AWS instantiation when no endpoint is supplied

v1.3.x

- Enumerate by object prefix
- Better support for creating S3 folders using objects with keys ending in '/' and no data
- New constructor to better support S3-compatible storage, update to test app
- Fix to allow non-SSL connections to S3
- Added enumeration capabilities to list contents of a bucket or container
- Added metadata capabilities to retrieve metadata for a given BLOB
- Stream support for object read and write
- Reworked test client exercising read, write, upload, download, metadata, exists, and enumeration
- Added continuation token support for enumeration, supply `null` to begin enumeration, and if more records exist, `nextContinuationToken` will be populated with the value that should be sent in on a subsequent enumeration call to continue enumerating

v1.2.x

- Breaking change, add ContentType to Write method
- Added missing AWS regions

v1.1.x

- Breaking change; async methods
- Retarget to .NET Core 2.0 and .NET Framework 4.6.2

v1.0.x

- Serialize enums as strings
- Added `Exists` method
- Improve S3 client resource utilization
- Support for Azure, AWS S3, Kvpbase, and disk
- Initial release
