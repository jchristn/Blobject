using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks; 
using Amazon.S3;
using Amazon.S3.Model;

namespace BlobHelper
{ 
    public enum StorageType
    {
        AwsS3,
        Azure,
        Disk,
        Kvpbase 
    }
}
