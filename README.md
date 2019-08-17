# BlobHelper

BLOB storage wrapper for Microsoft Azure, Amazon S3, Kvpbase, and local filesystem written in C#.

As of v1.1.0, BlobHelper is now targeted to both .NET Core 2.0 and .NET Framework 4.6.2.

[nuget]:     https://www.nuget.org/packages/BlobHelper/
[nuget-img]: https://badge.fury.io/nu/Object.svg

## Help, Feedback, Contribute

If you have any issues or feedback, please file an issue here in Github. We'd love to have you help by contributing code for new features, optimization to the existing codebase, ideas for future releases, or fixes!

## Overview

This project was built to provide a simple interface over external storage to help support projects that need to work with potentially multiple storage providers.  It is by no means a comprehensive interface, rather, it supports core methods for creation, retrieval, and deletion.

## New in v1.3.x

- Better support for creating S3 folders using objects with keys ending in '/' and no data
- New constructor to better support S3-compatible storage, update to test app
- Fix to allow non-SSL connections to S3
- Added enumeration capabilities to list contents of a bucket or container
- Added metadata capabilities to retrieve metadata for a given BLOB
- Stream support for object read and write
- Reworked test client exercising read, write, upload, download, metadata, exists, and enumeration
- Added continuation token support for enumeration, supply ```null``` to begin enumeration, and if more records exist, ```nextContinuationToken``` will be populated with the value that should be sent in on a subsequent enumeration call to continue enumerating

## Example Project

Refer to the ```Test``` project for exercising the library.

## Getting Started - AWS
```
using BlobHelper;

AwsSettings settings = new AwsSettings(
	accessKey, 
	secretKey, 
	AwsRegion.USWest1,
	bucket);

Blobs blobs = new Blobs(settings); 
```

## Getting Started - Azure
```
using BlobHelper;

AzureSettings settings = new AzureSettings(
	accountName, 
	accessKey, 
	"https://[accountName].blob.core.windows.net/", 
	containerName);

Blobs blobs = new Blobs(settings); 
```

## Getting Started - Kvpbase
```
using BlobHelper;

KvpbaseSettings settings = new KvpbaseSettings(
	"http://localhost:8000/", 
	userGuid, 
	containerName, 
	apiKey);

Blobs blobs = new Blobs(settings); 
```

## Getting Started - Disk
```
using BlobHelper;

DiskSettings settings = new DiskSettings("blobs"); 

Blobs blobs = new Blobs(settings);
```

## Getting Started (Byte Arrays for Smaller Objects)
```
bool success = blobs.Write("test", "text/plain", This is some data").Result;
byte[] data = blobs.Get("test"); // throws IOException
bool exists = blobs.Exists("test").Result;
bool success = blobs.Delete("test").Result;
```

## Getting Started (Streams for Larger Objects)
```
// Writing a file using a stream
FileInfo fi = new FileInfo(inputFile);
long contentLength = fi.Length;

using (FileStream fs = new FileStream(inputFile, FileMode.Open))
{
    if (_Blobs.Write("key", "content-type", contentLength, fs)) Console.WriteLine("Success");
    else Console.WriteLine("Failed");
}

// Downloading to a stream
long contentLength = 0;
Stream stream = null; 
if (_Blobs.Get(key, out contentLength, out stream))
{
	// use the stream...
}
else Console.WriteLine("Failed");
```

## Metadata and Enumeration
```
// Get BLOB metadata
BlobMetadata md = null;
if (_Blobs.GetMetadata("key", out md)) Console.WriteLine(md.ToString());
else Console.WriteLine("Failed");

// Enumerate BLOBs
List<BlobMetadata> blobs = null;
string nextToken = null; 
if (_Blobs.Enumerate(null, out nextToken, out blobs))
{
	foreach (BlobMetadata md in blobs) Console.WriteLine(md.ToString());
}
else Console.WriteLine("Failed"); 
```

## Version History

New capabilities and fixes starting in v1.0.0 will be shown here.

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
