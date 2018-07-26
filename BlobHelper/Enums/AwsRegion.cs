using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;

namespace BlobHelper
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AwsRegion
    {
        [EnumMember(Value = "APNortheast1")]
        APNortheast1,
        [EnumMember(Value = "APSoutheast1")]
        APSoutheast1,
        [EnumMember(Value = "APSoutheast2")]
        APSoutheast2,
        [EnumMember(Value = "EUWest1")]
        EUWest1,
        [EnumMember(Value = "SAEast1")]
        SAEast1,
        [EnumMember(Value = "USEast1")]
        USEast1,
        [EnumMember(Value = "USGovCloudWest1")]
        USGovCloudWest1,
        [EnumMember(Value = "USWest1")]
        USWest1,
        [EnumMember(Value = "USWest2")]
        USWest2
    }
}
