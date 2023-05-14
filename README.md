# BlobHelper

BlobHelper is a common, consistent storage interface for Microsoft Azure, Amazon S3, S3 compatible storage (i.e. Minio, Less3), Kvpbase, and local filesystem written in C#.

[![NuGet Version](https://img.shields.io/nuget/v/BlobHelper.svg?style=flat)](https://www.nuget.org/packages/BlobHelper/) [![NuGet](https://img.shields.io/nuget/dt/BlobHelper.svg)](https://www.nuget.org/packages/BlobHelper) 

## Help, Feedback, Contribute

If you have any issues or feedback, please file an issue here in Github. We'd love to have you help by contributing code for new features, optimization to the existing codebase, ideas for future releases, or fixes!

## Overview

This project was built to provide a simple interface over external storage to help support projects that need to work with potentially multiple storage providers.  It is by no means a comprehensive interface, rather, it supports core methods for creation, retrieval, deletion, metadata, and enumeration.

## Contributors

- @phpfui for adding the original code for BLOB copy functionality
- @Revazashvili for fixes related to byte array instantiation, Azure, and refactoring

## New in v4.1.x

- Refactor recommendation by @Revazashvili to interface and implementation
- Minor class name change; ```Blobs``` becomes ```BlobClient```

## Example Project

Refer to the ```Test``` project for exercising the library.

## Getting Started - AWS S3
```csharp
using BlobHelper;

AwsSettings settings = new AwsSettings(
	accessKey, 
	secretKey, 
	AwsRegion.USWest1,
	bucket);

BlobClient blobs = new BlobClient(settings); 
```

## Getting Started - AWS S3 Compatible Storage (Minio, Less3, etc)
```csharp
using BlobHelper;

AwsSettings settings = new AwsSettings(
	endpoint,      // http://localhost:8000/
	true,          // enable or disable SSL
	accessKey, 
	secretKey, 
	AwsRegion.USWest1,
	bucket,
	baseUrl        // i.e. http://localhost:8000/{bucket}/{key}
	);

BlobClient blobs = new BlobClient(settings); 
```

## Getting Started - Azure
```csharp
using BlobHelper;

AzureSettings settings = new AzureSettings(
	accountName, 
	accessKey, 
	"https://[accountName].blob.core.windows.net/", 
	containerName);

BlobClient blobs = new BlobClient(settings); 
```

## Getting Started - Komodo
```csharp
using BlobHelper;

KomodoSettings settings = new KomodoSettings(
	"http://localhost:9090/", 
	indexGuid, 
	apiKey);

BlobClient blobs = new BlobClient(settings); 
```

## Getting Started - Kvpbase
```csharp
using BlobHelper;

KvpbaseSettings settings = new KvpbaseSettings(
	"http://localhost:8000/", 
	userGuid, 
	containerName, 
	apiKey);

BlobClient blobs = new BlobClient(settings); 
```

## Getting Started - Disk
```csharp
using BlobHelper;

DiskSettings settings = new DiskSettings("blobs"); 

BlobClient blobs = new BlobClient(settings);
```

## Getting Started (Byte Arrays for Smaller Objects)
```csharp
await blobs.Write("test", "text/plain", "This is some data");  // throws IOException
byte[] data = await blobs.Get("test");                         // throws IOException
bool exists = await blobs.Exists("test");
await blobs.Delete("test");
```

## Getting Started (Streams for Larger Objects)
```csharp
// Writing a file using a stream
FileInfo fi = new FileInfo(inputFile);
long contentLength = fi.Length;

using (FileStream fs = new FileStream(inputFile, FileMode.Open))
{
    await _Blobs.Write("key", "content-type", contentLength, fs);  // throws IOException
}

// Downloading to a stream
BlobData blob = await _Blobs.GetStream(key);
// read blob.ContentLength bytes from blob.Data
```

## Metadata and Enumeration
```csharp
// Get BLOB metadata
BlobMetadata md = await _Blobs.GetMetadata("key");

// Enumerate BLOBs
EnumerationResult result = await _Blobs.Enumerate();
// list of BlobMetadata contained in result.Blobs
// continuation token in result.NextContinuationToken
```

## Copying BLOBs from Repository to Repository

If you have multiple storage repositories and wish to move BLOBs from one repository to another, use the ```BlobCopy``` class (refer to the ```Test.Copy``` project for a full working example).

Thanks to @phpfui for contributing code and the idea for this enhancement!

```csharp
Blobs from    = new Blobs(new DiskSettings("./disk1/")); // assume some files are here
Blobs to      = new Blobs(new DiskSettings("./disk2/"));
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
