# BlobHelper

BLOB storage wrapper for Microsoft Azure, Amazon S3, Kvpbase, and local filesystem written in C#.

As of v1.1.0, BlobHelper is now targeted to both .NET Core 2.0 and .NET Framework 4.6.2.

[nuget]:     https://www.nuget.org/packages/BlobHelper/
[nuget-img]: https://badge.fury.io/nu/Object.svg

## Help, Feedback, Contribute

If you have any issues or feedback, please file an issue here in Github. We'd love to have you help by contributing code for new features, optimization to the existing codebase, ideas for future releases, or fixes!

## Overview

This project was built to provide a simple interface over external storage to help support projects that need to work with potentially multiple storage providers.  It is by no means a comprehensive interface, rather, it supports core methods for creation, retrieval, and deletion.

## New in v1.2.0

- Breaking change, add ContentType to Write method
- Added missing AWS regions

## Example Project

Refer to the ```TestNetFramework``` and ```TestNetCore``` projects for exercising the library.

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

## Getting Started - IO
```
bool success = blobs.Write("test", "text/plain", This is some data").Result;
byte[] data = blobs.Get("test"); // throws IOException
bool exists = blobs.Exists("test").Result;
bool success = blobs.Delete("test").Result;
```

## Version History

New capabilities and fixes starting in v1.0.0 will be shown here.

v1.1.x
- Breaking change; async methods
- Retarget to .NET Core 2.0 and .NET Framework 4.6.2

v1.0.x
- Serialize enums as strings
- Added ```Exists``` method
- Improve S3 client resource utilization
- Support for Azure, AWS S3, Kvpbase, and disk
- Initial release
