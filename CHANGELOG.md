# Change Log

## Current Version

v2.0.1

- Breaking changes
- Fully async APIs
- Separation of ```Get``` and ```GetStream``` APIs
- ```BlobData``` object returned when using ```GetStream``` API to download objects to stream (contains content length and stream)
- ```EnumerationResult``` object returned for enumeration results including continuation token and list of ```BlobMetadata``` objects
- Internal consistency amongst APIs
- Dependency updates

## Previous Versions

v1.3.5

- Added ```string GenerateUrl(string key)``` API
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
- Added continuation token support for enumeration, supply ```null``` to begin enumeration, and if more records exist, ```nextContinuationToken``` will be populated with the value that should be sent in on a subsequent enumeration call to continue enumerating

v1.2.x

- Breaking change, add ContentType to Write method
- Added missing AWS regions

v1.1.x

- Breaking change; async methods
- Retarget to .NET Core 2.0 and .NET Framework 4.6.2

v1.0.x

- Serialize enums as strings
- Added ```Exists``` method
- Improve S3 client resource utilization
- Support for Azure, AWS S3, Kvpbase, and disk
- Initial release
