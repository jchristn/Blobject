![](https://github.com/jchristn/BlobHelper/blob/master/assets/icon.png?raw=true)

# Blobject

Blobject (formerly BlobHelper) is a common, consistent storage interface for Microsoft Azure, Amazon S3, S3 compatible storage (i.e. Minio, Less3, View), CIFS (Windows file shares), NFS (Linux and UNIX file shares), and local filesystem written in C#.

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
| EzSmb | https://github.com/ume05rw/EzSmb | LGPL-3.0 |
| SMBLibrary | https://github.com/TalAloni/SMBLibrary | LGPL-3.0 |
| NFS-Client | https://github.com/SonnyX/NFS-Client | Unknown, public |
| Nekodrive | https://github.com/nekoni/nekodrive | Unknown, public | 
| S3Lite | https://github.com/jchristn/S3Lite | MIT |

## New in v5.0.x

- Rename from `BlobHelper` to `Blobject`
- Added support for CIFS and NFS
- Remove use of continuation tokens for disk
- Add `S3Lite` variant, not dependent on AWSSDK
- Refactor

## Example Project

Refer to the `Test` project for exercising the library.

## Getting Started - AWS S3
```csharp
using Blobject;

AwsSettings settings = new AwsSettings(
	accessKey, 
	secretKey, 
	AwsRegion.USWest1,
	bucket);

BlobClient blobs = new BlobClient(settings); 
```

## Getting Started - AWS S3 Compatible Storage (Minio, Less3, etc)
```csharp
using Blobject.AmazonS3;

AwsSettings settings = new AwsSettings(
	endpoint,      // http://localhost:8000/
	true,          // enable or disable SSL
	accessKey, 
	secretKey, 
	AwsRegion.USWest1,
	bucket,
	baseUrl        // i.e. http://localhost:8000/{bucket}/{key}
	);

AmazonS3BlobClient blobs = new AmazonS3BlobClient(settings); 
```

## Getting Started - AWS S3 Lite (non-AWS library to reduce dependency drag)
```csharp
using Blobject.AmazonS3Lite;

// Initialize settings as above
AmazonS3LiteBlobClient blobs = new AmazonS3BlobClient(settings); 
```

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

## Getting Started - CIFS
```csharp
using Blobject.CIFS;

CifsSettings settings = new CifsSettings(
	IPAddress.Parse("127.0.0.1"),
	username,
	password,
	sharename);

CifsBlobClient blobs = new CifsBlobClient(settings);
```

## Getting Started - Disk
```csharp
using Blobject.Disk;

DiskSettings settings = new DiskSettings("blobs"); 

DiskBlobClient blobs = new DiskBlobClient(settings);
```

## Getting Started - NFS
```csharp
using Blobject.NFS;

NfsSettings settings = new NfsSettings(
	IPAddress.Parse("127.0.0.1"),
	0, // user ID
	0, // group ID,
	sharename,
	NfsVersionEnum.V3 // V2, V3, or V4
	);

NfsBlobClient = new NfsBlobClient(settings);
```

## Getting Started (Byte Arrays for Smaller Objects)
```csharp
await blobs.WriteAsync("test", "text/plain", "This is some data");  // throws IOException
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
```csharp
// Get BLOB metadata
BlobMetadata md = await _Blobs.GetMetadataAsync("key");

// Enumerate BLOBs
EnumerationResult result = await _Blobs.EnumerateAsync();
// list of BlobMetadata contained in result.Blobs
// continuation token in result.NextContinuationToken
```

## Copying BLOBs from Repository to Repository

If you have multiple storage repositories and wish to move BLOBs from one repository to another, use the ```BlobCopy``` class (refer to the ```Test.Copy``` project for a full working example).

Thanks to @phpfui for contributing code and the idea for this enhancement!

```csharp
// instantiate two BLOB clients
BlobCopy copy = new BlobCopy(from, to);
CopyStatistics stats = copy.Start();
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

## Version History

Refer to CHANGELOG.md for version history.
