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
    //
    // See https://docs.aws.amazon.com/general/latest/gr/rande.html
    //

    [JsonConverter(typeof(StringEnumConverter))]
    public enum AwsRegion
    {
        [EnumMember(Value = "APNortheast1")]
        APNortheast1,
        [EnumMember(Value = "APNortheast2")]
        APNortheast2,
        [EnumMember(Value = "APNortheast3")]
        APNortheast3,
        [EnumMember(Value = "APSoutheast1")]
        APSoutheast1,
        [EnumMember(Value = "APSoutheast2")]
        APSoutheast2,
        [EnumMember(Value = "APSouth1")]
        APSouth1,
        [EnumMember(Value = "CACentral1")]
        CACentral1,
        [EnumMember(Value = "CNNorth1")]
        CNNorth1,
        [EnumMember(Value = "EUCentral1")]
        EUCentral1,
        [EnumMember(Value = "EUNorth1")]
        EUNorth1,
        [EnumMember(Value = "EUWest1")]
        EUWest1,
        [EnumMember(Value = "EUWest2")]
        EUWest2,
        [EnumMember(Value = "EUWest3")]
        EUWest3,
        [EnumMember(Value = "SAEast1")]
        SAEast1,
        [EnumMember(Value = "USEast1")]
        USEast1,
        [EnumMember(Value = "USEast2")]
        USEast2,
        [EnumMember(Value = "USGovCloudEast1")]
        USGovCloudEast1,
        [EnumMember(Value = "USGovCloudWest1")]
        USGovCloudWest1,
        [EnumMember(Value = "USWest1")]
        USWest1,
        [EnumMember(Value = "USWest2")]
        USWest2
    }
}
