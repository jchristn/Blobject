# Change Log

## Current Version

v5.0.x

- Rename from `BlobHelper` to `Blobject`
- Added support for CIFS, NFS, and Google Cloud Storage
- Remove use of continuation tokens for disk
- Add `S3Lite` variant, not dependent on AWSSDK
- Refactor

## Previous Versions

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
