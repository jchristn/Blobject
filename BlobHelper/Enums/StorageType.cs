using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks; 
 
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;

namespace BlobHelper
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum StorageType
    {
        [EnumMember(Value = "AwsS3")]
        AwsS3,
        [EnumMember(Value = "Azure")]
        Azure,
        [EnumMember(Value = "Disk")]
        Disk,
        [EnumMember(Value = "Kvpbase")]
        Kvpbase 
    }
}
