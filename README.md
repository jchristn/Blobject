![](https://github.com/jchristn/Blobject/blob/main/assets/icon.png?raw=true)

# Blobject

Blobject is a common, consistent storage interface for Microsoft Azure, Amazon S3, S3 compatible storage (i.e. Minio, Less3, View), CIFS (Windows file shares), NFS (Linux and UNIX file shares), Google Cloud Storage, and local filesystem written in C#.

| Library | Version | Downloads |
|---|---|---|
| Blobject.Core | [![NuGet Version](https://img.shields.io/nuget/v/Blobject.Core.svg?style=flat)](https://www.nuget.org/packages/Blobject.Core/)  | [![NuGet](https://img.shields.io/nuget/dt/Blobject.Core.svg)](https://www.nuget.org/packages/Blobject.Core) |
| Blobject.AmazonS3 | [![NuGet Version](https://img.shields.io/nuget/v/Blobject.AmazonS3.svg?style=flat)](https://www.nuget.org/packages/Blobject.AmazonS3/)  | [![NuGet](https://img.shields.io/nuget/dt/Blobject.AmazonS3.svg)](https://www.nuget.org/packages/Blobject.AmazonS3) |
| Blobject.AmazonS3Lite | [![NuGet Version](https://img.shields.io/nuget/v/Blobject.AmazonS3Lite.svg?style=flat)](https://www.nuget.org/packages/Blobject.AmazonS3Lite/)  | [![NuGet](https://img.shields.io/nuget/dt/Blobject.AmazonS3Lite.svg)](https://www.nuget.org/packages/Blobject.AmazonS3Lite) |
| Blobject.AzureBlob | [![NuGet Version](https://img.shields.io/nuget/v/Blobject.AzureBlob.svg?style=flat)](https://www.nuget.org/packages/Blobject.AzureBlob/)  | [![NuGet](https://img.shields.io/nuget/dt/Blobject.AzureBlob.svg)](https://www.nuget.org/packages/Blobject.AzureBlob) |
| Blobject.CIFS | [![NuGet Version](https://img.shields.io/nuget/v/Blobject.CIFS.svg?style=flat)](https://www.nuget.org/packages/Blobject.CIFS/)  | [![NuGet](https://img.shields.io/nuget/dt/Blobject.CIFS.svg)](https://www.nuget.org/packages/Blobject.CIFS) |
| Blobject.Disk | [![NuGet Version](https://img.shields.io/nuget/v/Blobject.Disk.svg?style=flat)](https://www.nuget.org/packages/Blobject.Disk/)  | [![NuGet](https://img.shields.io/nuget/dt/Blobject.Disk.svg)](https://www.nuget.org/packages/Blobject.Disk) |
| Blobject.GoogleCloud | [![NuGet Version](https://img.shields.io/nuget/v/Blobject.GoogleCloud.svg?style=flat)](https://www.nuget.org/packages/Blobject.GoogleCloud/)  | [![NuGet](https://img.shields.io/nuget/dt/Blobject.GoogleCloud.svg)](https://www.nuget.org/packages/Blobject.GoogleCloud) |
| Blobject.NFS | [![NuGet Version](https://img.shields.io/nuget/v/Blobject.NFS.svg?style=flat)](https://www.nuget.org/packages/Blobject.NFS/)  | [![NuGet](https://img.shields.io/nuget/dt/Blobject.NFS.svg)](https://www.nuget.org/packages/Blobject.NFS) |

## Help, Feedback, Contribute

If you have any issues or feedback, please file an issue here in Github. We'd love to have you help by contributing code for new features, optimization to the existing codebase, ideas for future releases, or fixes!

## Overview

This project was built to provide a simple interface over external storage to help support projects that need to work with potentially multiple storage providers.  It is by no means a comprehensive interface, rather, it supports core methods for creation, retrieval, deletion, metadata, and enumeration.

## Contributors

- @phpfui for adding the original code for BLOB copy functionality
- @Revazashvili for fixes related to byte array instantiation, Azure, and refactoring
- @courtzzz for keeping the region list updated

## Dependencies

Though this library is MIT licensed, it is dependent upon other libraries, some of which carry a different license.  Each of these libraries are included by reference, that is, none of their code has been modified.

| Package | URL | License |
|---------|-----|---------|
| AWSSDK.S3 | https://github.com/aws/aws-sdk-net | Apache 2.0 |
| Azure.Storage.Blobs | https://github.com/Azure/azure-sdk-for-net | MIT |
| Google.Cloud.Storage.V1 | https://github.com/googleapis/google-cloud-dotnet | Apache 2.0 |
| OpenCIFS.Client | https://github.com/jchristn/OpenCIFS | MIT |
| OpenNFS.Client | https://github.com/jchristn/OpenNFS | MIT |
| S3Lite | https://github.com/jchristn/S3Lite | MIT |

## New in v6.1.x

- Built-in metrics and traces for every package, emitted through the .NET `Meter` and `ActivitySource` named `Blobject`, with no exporter or SDK dependency and near-zero cost when nothing subscribes
- Every storage operation gets a span (`aws_s3 get`, `cifs write`, ...) and duration, outcome, error, and byte metrics; bulk operations report per-item outcomes, concurrency-slot waits, and slot usage; CIFS/NFS report connection time, open connections, pool capacity, resets, and retries; `BlobCopy` reports job and per-stage (enumerate, read, write) metrics, stage spans, and a last-success timestamp
- `BlobjectTelemetry.Enabled` and `BlobjectTelemetry.RecordKeys` (object keys are left off spans unless enabled, and never appear on metrics)
- Refer to [TELEMETRY.md](https://github.com/jchristn/Blobject/blob/main/TELEMETRY.md) for the metric and span catalog, subscription examples, PromQL alerts, and dashboard queries
- All packages move to v6.1.0
- v6.1.1: dependency updates (`AWSSDK.S3` 4.0.104.1, `Azure.Storage.Blobs` 12.30.0, `Azure.Storage.Blobs.Batch` 12.27.0, `OpenNFS.Client` 0.2.0, `S3Lite` 1.3.0); `OpenNFS.Client` and `S3Lite` now emit their own spans, which nest under Blobject's operation spans

## New in v6.0.x

- `Blobject.CIFS` now uses [OpenCIFS](https://github.com/jchristn/OpenCIFS) instead of EzSmb, and `Blobject.NFS` now uses [OpenNFS](https://github.com/jchristn/OpenNFS) instead of NFS-Client; both are MIT licensed
- Persistent, pooled connections with automatic reconnect after server restarts or dropped connections
- True streaming: `GetStreamAsync` returns a seekable stream that reads on demand, and stream writes are sent in chunks without buffering the whole object
- Cancellation tokens are honored throughout, and operations are safe to run concurrently (`MaxConcurrency`)
- Complete enumeration of large directories, overwrites always truncate, and parent folders are created on demand
- New settings: `CifsSettings.Port`, `Domain`, `RequireSigning`, `PreferEncryption`, `ConnectTimeoutMs`, `MaxConnections`; `NfsSettings.Port`, `MountPort`, `PortmapperPort`, `MachineName`, `WriteStability`, `ConnectTimeoutMs`, `ResponseTimeoutMs`
- Breaking changes, refer to CHANGELOG.md for details: `Blobject.CIFS` and `Blobject.NFS` target `net8.0` and `net10.0` only; `Blobject.NFS` supports NFSv3 only; missing objects throw `KeyNotFoundException`; CIFS keys may not contain SMB-reserved characters; `NfsBlobClient.GenerateUrl` returns an `nfs://` URL
- Exhaustive CIFS/NFS test suites run against in-process OpenCIFS and OpenNFS servers and Dockerized Samba, nfs-ganesha, Linux knfsd, and unfs3 servers
- `Blobject.AmazonS3` and `Blobject.GoogleCloud` now honor `contentLength` on stream writes (previously the whole stream was uploaded; for S3, non-seekable streams and `BlobCopy` to S3 failed)
- Dependencies updated, including `Google.Cloud.Storage.V1` 5.0.0, `AWSSDK.S3` 4.0.103.4, `Azure.Storage.Blobs` 12.29.2, and `System.Text.Json` 10.0.12
- All packages move to v6.0.0

## New in v5.1.x

- Added `DeleteManyAsync`, a bulk delete API returning a per-key `DeleteManyResult`
- Amazon S3 and Azure Blob use their native batch-delete APIs; all other providers fan out over `DeleteAsync` using `MaxConcurrency`
- Expanded contract test coverage, including positive and negative `DeleteManyAsync` cases

## New in v5.0.x

- Rename from `BlobHelper` to `Blobject`
- Added support for CIFS, NFS, and Google Cloud Storage
- Remove use of continuation tokens for disk
- Add `S3Lite` variant, not dependent on AWSSDK
- Enumerate APIs now return an `IEnumerable<BlobMetadata>`, no pagination required
- `Blobject.AmazonS3` and `Blobject.AmazonS3Lite` v5.0.19 normalize AWS region aliases such as `USEast2` to DNS-safe names such as `us-east-2` and default AWS S3 settings to HTTPS
- `Blobject.Core` v5.0.19 fixes streaming-first copy, bounded bulk operations, empty-write consistency, filter cloning, and case handling
- Provider patch releases v5.0.19/v5.0.20 align stream APIs, empty writes, filter matching, and shared bulk behavior
- Provider packages now share common `WriteManyAsync` and `EmptyAsync` behavior with configurable `MaxConcurrency`
- Refactor

## Telemetry

Blobject emits OpenTelemetry-compatible metrics and traces through the BCL `Meter` and `ActivitySource` named `Blobject`. Subscribe to both from your host to send them to Prometheus, Tempo, Grafana, or any OTLP backend:

```csharp
// OpenTelemetry .NET
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("Blobject"))
    .WithTracing(t => t.AddSource("Blobject"));

// Radiant
settings.Sources.AddMeter("Blobject");
settings.Sources.AddActivitySource("Blobject");
```

Storage calls nest under your request spans automatically. Refer to [TELEMETRY.md](https://github.com/jchristn/Blobject/blob/main/TELEMETRY.md) for every metric and span, the configuration options, recommended alerts, and dashboard queries.

## Example Project

Refer to the `Test` project for exercising the library.

## Automated Tests

The repository includes Touchstone-based automated contract tests. `Test.Shared` contains the runner-agnostic provider contract definitions. By default, the tests run against `Blobject.Disk` in a temporary directory and clean up after each case.

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj --framework net10.0
dotnet test src/Test.Xunit/Test.Xunit.csproj --framework net10.0
dotnet test src/Test.Nunit/Test.Nunit.csproj --framework net10.0
```

The default suite contains 108 contract cases, including positive and negative `DeleteManyAsync` coverage and folder-entry semantics for hierarchical providers (CIFS and NFS). Add `--include-stress true` to `Test.Automated` to include large enumeration, large `WriteManyAsync`, and large `DeleteManyAsync` coverage.

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj --framework net10.0 -- --include-stress true
```

`Test.Automated` accepts provider-specific command-line overrides. Remote providers are isolated by a generated prefix and cleaned up by default.

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj --framework net10.0 -- \
  --provider s3 \
  --s3-access-key <access-key> \
  --s3-secret-key <secret-key> \
  --s3-region us-east-2 \
  --s3-bucket <bucket> \
  --prefix blobject-contract-tests
```

For S3-compatible storage, also supply `--s3-endpoint`, `--s3-ssl`, and `--s3-base-url`, and `--s3-path-style true` for S3 Lite when the endpoint is an IP address or does not support virtual-hosted requests. Supported providers are `disk`, `s3`, `s3lite`, `azure`, `gcp`, `cifs`, `nfs`, `fileshare`, and the managed file share targets listed below; run `Test.Automated --help` for the full argument list. Use `--filter <text>` to run only the cases whose `SuiteId.CaseId` contains the text. The xUnit and NUnit adapters use the same shared suite and can be configured with the equivalent `BLOBJECT_TEST_*` environment variables, such as `BLOBJECT_TEST_PROVIDER`, `BLOBJECT_TEST_S3_BUCKET`, and `BLOBJECT_TEST_PREFIX`.

```bash
dotnet test src/Test.Xunit/Test.Xunit.csproj --framework net10.0 \
  -e BLOBJECT_TEST_PROVIDER=s3 \
  -e BLOBJECT_TEST_S3_ACCESS_KEY=<access-key> \
  -e BLOBJECT_TEST_S3_SECRET_KEY=<secret-key> \
  -e BLOBJECT_TEST_S3_REGION=us-east-2 \
  -e BLOBJECT_TEST_S3_BUCKET=<bucket>
```

### CIFS and NFS Test Servers

The CIFS and NFS providers are tested against ephemeral servers that the test harness starts and removes automatically. No existing file server is needed.

| Target | Server |
|---|---|
| `cifs-ephemeral` | OpenCIFS.Server, in-process on loopback |
| `cifs-samba` | Samba `smbd` in Docker |
| `nfs-ephemeral` | OpenNFS.Server, in-process on loopback |
| `nfs-ganesha` | nfs-ganesha in Docker |
| `nfs-knfsd` | Linux kernel NFS server in privileged Docker (also exercises portmapper discovery of the MOUNT port) |
| `nfs-unfs3` | unfs3 in Docker |

For each target, the harness runs the shared provider contract suites plus suites for protocol semantics, enumeration, concurrency, connection lifecycle (reconnect after server restart, dispose, cancellation, authentication failures), server-side interoperability (content verified on the server's own file system), and SMB- or NFS-specific behavior. A server-independent API surface suite validates settings, constructors, argument handling, and the public API.

```bash
# every target (Docker targets require Docker with Linux containers)
dotnet run --project src/Test.Automated/Test.Automated.csproj --framework net10.0 -- --provider fileshare

# a subset: all, cifs, nfs, inprocess, docker, or a comma-separated list of targets
dotnet run --project src/Test.Automated/Test.Automated.csproj --framework net10.0 -- --provider fileshare --fileshare-targets inprocess

# a single target
dotnet run --project src/Test.Automated/Test.Automated.csproj --framework net10.0 -- --provider nfs-knfsd
```

The xUnit and NUnit projects run the same file share suites; select targets with `BLOBJECT_TEST_FILESHARE_TARGETS` (default `all`). Docker targets are skipped with a reason when Docker is unavailable. Docker images are built from `src/Test.Shared/Docker` on first use and cached. Containers are labeled `blobject.test`, backed by anonymous volumes, and removed when the run finishes, at process exit, and on Ctrl+C; containers orphaned by a crashed run are removed at the start of the next run.

The existing `Test.*` console applications remain available for interactive provider-specific testing.

## Getting Started - AWS S3
```csharp
using Blobject;

AwsSettings settings = new AwsSettings(
	accessKey,
	secretKey,
	"us-west-1",
	bucket);

BlobClient blobs = new BlobClient(settings);
```

AWS S3 region names are normalized against the current Amazon S3 regular endpoint region list. For example, `USEast2`, `us_east_2`, and `us-east-2` are stored as `us-east-2`.

## Getting Started - AWS S3 Compatible Storage (Minio, Less3, etc)
```csharp
using Blobject.AmazonS3;

AwsSettings settings = new AwsSettings(
	endpoint,      // http://localhost:8000/
	true,          // enable or disable SSL
	accessKey,
	secretKey,
	"us-west-1",
	bucket,
	baseUrl        // i.e. http://localhost:8000/{bucket}/{key}
	);

AmazonS3BlobClient blobs = new AmazonS3BlobClient(settings);
```

## Getting Started - AWS S3 Lite (non-AWS library to reduce dependency drag)
```csharp
using Blobject.AmazonS3Lite;

// Initialize settings as above
AmazonS3LiteBlobClient blobs = new AmazonS3LiteBlobClient(settings);
```

## Getting Started - AWS S3 Anonymous Access

For accessing public buckets that don't require authentication, you can omit the access key and secret key:

```csharp
using Blobject.AmazonS3Lite;

// Anonymous access to a public bucket
AwsSettings settings = new AwsSettings(
    null,          // accessKey - null for anonymous access
    null,          // secretKey - null for anonymous access
    "us-west-1",
    "public-bucket-name"
    );

AmazonS3LiteBlobClient blobs = new AmazonS3LiteBlobClient(settings);

// Check if credentials are configured
Console.WriteLine("Has credentials: " + settings.HasCredentials);  // False

// Read operations work on public buckets
byte[] data = await blobs.GetAsync("public-file.txt");
```

**Note:** Write and delete operations require authentication. Anonymous access is read-only.

## Getting Started - Azure
```csharp
using Blobject.AzureBlob;

AzureBlobSettings settings = new AzureBlobSettings(
	accountName,
	accessKey,
	"https://[accountName].blob.core.windows.net/",
	containerName);

AzureBlobClient blobs = new AzureBlobClient(settings);
```

## Getting Started - Google Cloud

Important - you must have the JSON credentials for the service account, which includes the private key.  Creation of these credentials requires the organization policy administrator role.

```csharp
using Blobject.GoogleCloud;

GcpBlobSettings settings = new GcpBlobSettings(
	projectId,
	bucket,
	"... JSON credentials for service account ..."

GcpBlobClient blobs = new GcpBlobClient(settings);
```

## Getting Started - CIFS

`Blobject.CIFS` supports SMB 2.0.2 through SMB 3.x servers such as Windows and Samba, using [OpenCIFS](https://github.com/jchristn/OpenCIFS).

```csharp
using Blobject.CIFS;

CifsSettings settings = new CifsSettings(
	"localhost",
	username,          // or "domain\username"
	password,
	sharename);

settings.Port = 445;                // default
settings.RequireSigning = false;    // default
settings.PreferEncryption = true;   // default; SMB 3.x encryption when the server supports it
settings.MaxConnections = 4;        // default; requests on one SMB connection run one at a time

CifsBlobClient blobs = new CifsBlobClient(settings);
```

The connection is established on first use and re-established automatically if it drops. Keys use `/` as the separator and may not contain the characters SMB reserves (`< > : " | ? *` and control characters). Dispose the client, or call `DisposeAsync`, to disconnect.

## Getting Started - Disk
```csharp
using Blobject.Disk;

DiskSettings settings = new DiskSettings("blobs");

DiskBlobClient blobs = new DiskBlobClient(settings);
```

## Getting Started - NFS

`Blobject.NFS` supports NFSv3 servers such as the Linux kernel server, nfs-ganesha, and unfs3, using [OpenNFS](https://github.com/jchristn/OpenNFS).

```csharp
using Blobject.NFS;

NfsSettings settings = new NfsSettings(
	"localhost",
	0,                  // user ID, sent using AUTH_SYS
	0,                  // group ID, sent using AUTH_SYS
	"/export",          // export path
	NfsVersionEnum.V3   // only V3 is supported
	);

settings.Port = 2049;                                   // default
settings.MountPort = 0;                                 // default; 0 discovers the MOUNT port through the portmapper
settings.WriteStability = NfsWriteStabilityEnum.Unstable; // default; data is committed once each object is written

NfsBlobClient blobs = new NfsBlobClient(settings);
```

The connection is established on first use and re-established automatically if it drops. When the server does not run a portmapper, set `MountPort` explicitly. Dispose the client, or call `DisposeAsync`, to unmount and disconnect.

With both CIFS and NFS, objects are written in place, as with any file share client. Concurrent writes to the same key are not atomic, and a reader can observe an object while it is being written. Coordinate writers yourself if you need object-store-style atomic replacement.

## Getting Started (Byte Arrays for Smaller Objects)
```csharp
await blobs.WriteAsync("test", "text/plain", "This is some data");  // throws IOException
await blobs.WriteAsync("empty", "application/octet-stream", Array.Empty<byte>());
byte[] data = await blobs.GetAsync("test");                         // throws IOException
bool exists = await blobs.ExistsAsync("test");
await blobs.DeleteAsync("test");
```

## Getting Started (Streams for Larger Objects)
```csharp
// Writing a file using a stream
FileInfo fi = new FileInfo(inputFile);
long contentLength = fi.Length;

using (FileStream fs = new FileStream(inputFile, FileMode.Open))
{
    await _Blobs.WriteAsync("key", "content-type", contentLength, fs);  // throws IOException
}

// Downloading to a stream
BlobData blob = await _Blobs.GetStreamAsync(key);
// read blob.ContentLength bytes from blob.Data
```

## Accessing Files within Folders
```csharp
//
// Use a key of the form [path]/[to]/[file]/[filename].[ext]
//
await blobs.WriteAsync("subdirectory/filename.ext", "text/plain", "Hello!");
```

## Metadata and Enumeration

Enumeration is always full; the library will manage any continuation tokens (e.g. AWS S3, S3 compatible, Azure) and also recurse into subdirectories for file, CIFS, and NFS. CIFS and NFS also return each folder as an entry with `IsFolder` set and a key ending in `/`, after the folder's contents.

```csharp
// Get BLOB metadata
BlobMetadata md = await _Blobs.GetMetadataAsync("key");

// Enumerate BLOBs
await foreach (BlobMetadata blob in _Blobs.EnumerateAsync())
  Console.WriteLine(blob.Key + " " + blob.ContentLength + " folder? " + blob.IsFolder);
```

Enumeration filters are cloned internally so provider calls do not mutate caller-supplied filter instances. Object-storage providers and NFS use case-sensitive key matching; disk and CIFS use case-insensitive matching for compatibility with their typical filesystems.

## Copying BLOBs from Repository to Repository

If you have multiple storage repositories and wish to move BLOBs from one repository to another, use the ```BlobCopy``` class (refer to the ```Test.Copy``` project for a full working example).

Thanks to @phpfui for contributing code and the idea for this enhancement!

```csharp
// instantiate two BLOB clients
BlobCopy copy = new BlobCopy(from, to);
CopyStatistics stats = await copy.StartAsync();
/*
	{
	  "Success": true,
	  "Time": {
	    "Start": "2021-12-22T18:44:42.9098249Z",
	    "End": "2021-12-22T18:44:42.9379215Z",
	    "TotalMs": 28.1
	  },
	  "ContinuationTokens": 0,
	  "BlobsEnumerated": 12,
	  "BytesEnumerated": 1371041,
	  "BlobsRead": 12,
	  "BytesRead": 1371041,
	  "BlobsWritten": 12,
	  "BytesWritten": 1371041,
	  "Keys": [
	    "filename.txt",
	    ...
	  ]
	}
 */
```

`BlobCopy.Start(...)` remains available for compatibility and delegates to `StartAsync(...)`. Copy now uses `GetStreamAsync` and stream writes where the provider supports them.

## Bulk Operations

`WriteManyAsync`, `DeleteManyAsync`, and `EmptyAsync` use bounded concurrency through `BlobClientBase.MaxConcurrency`, which defaults to `4`.

```csharp
blobs.MaxConcurrency = 8;
await blobs.WriteManyAsync(writes);
```

### Deleting Multiple Objects

`DeleteManyAsync` deletes a collection of keys and returns a `DeleteManyResult` describing the outcome for each key. Providers with a native bulk-delete API use it automatically (Amazon S3 `DeleteObjects`, up to 1000 keys per request; Azure Blob batch delete, up to 256 per request). All other providers (S3 Lite, Google Cloud, disk, CIFS, NFS) fan out over `DeleteAsync` using `MaxConcurrency`. Deleting a key that does not exist is treated as a successful deletion, matching `DeleteAsync`. Null or empty keys are ignored.

```csharp
DeleteManyResult result = await blobs.DeleteManyAsync(new List<string>
{
    "logs/2023/01.txt",
    "logs/2023/02.txt",
    "logs/2023/03.txt"
});

Console.WriteLine("Deleted " + result.Deleted.Count + " of " + result.Count);
if (!result.Success)
{
    foreach (string key in result.Failed)
        Console.WriteLine("Failed: " + key);
}
```

## Version History

Refer to CHANGELOG.md for version history.
