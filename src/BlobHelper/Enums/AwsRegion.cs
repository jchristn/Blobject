using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace BlobHelper
{
    //
    // See https://docs.aws.amazon.com/general/latest/gr/rande.html
    //

    /// <summary>
    /// AWS region.
    /// </summary>
    public enum AwsRegion
    {
        /// <summary>
        /// APNortheast1 region.
        /// </summary>
        [EnumMember(Value = "APNortheast1")]
        APNortheast1,
        /// <summary>
        /// APNortheast2 region.
        /// </summary>
        [EnumMember(Value = "APNortheast2")]
        APNortheast2,
        /// <summary>
        /// APNortheast3 region.
        /// </summary>
        [EnumMember(Value = "APNortheast3")]
        APNortheast3,
        /// <summary>
        /// APSoutheast1 region.
        /// </summary>
        [EnumMember(Value = "APSoutheast1")]
        APSoutheast1,
        /// <summary>
        /// APSoutheast2 region.
        /// </summary>
        [EnumMember(Value = "APSoutheast2")]
        APSoutheast2,
        /// <summary>
        /// APSoutheast3 region.
        /// </summary>
        [EnumMember(Value = "APSoutheast3")]
        APSoutheast3,
        /// <summary>
        /// APSoutheast4 region.
        /// </summary>
        [EnumMember(Value = "APSoutheast4")]
        APSoutheast4,
        /// <summary>
        /// APSouth1 region.
        /// </summary>
        [EnumMember(Value = "APSouth1")]
        APSouth1,
        /// <summary>
        /// CACentral1 region.
        /// </summary>
        [EnumMember(Value = "CACentral1")]
        CACentral1,
        /// <summary>
        /// CNNorth1 region.
        /// </summary>
        [EnumMember(Value = "CNNorth1")]
        CNNorth1,
        /// <summary>
        /// CNNorthwest1 region.
        /// </summary>
        [EnumMember(Value = "CNNorthwest1")]
        CNNorthwest1,
        /// <summary>
        /// EUCentral1 region.
        /// </summary>
        [EnumMember(Value = "EUCentral1")]
        EUCentral1,
        /// <summary>
        /// EUNorth1 region.
        /// </summary>
        [EnumMember(Value = "EUNorth1")]
        EUNorth1,
        /// <summary>
        /// EUWest1 region.
        /// </summary>
        [EnumMember(Value = "EUWest1")]
        EUWest1,
        /// <summary>
        /// EUWest2 region.
        /// </summary>
        [EnumMember(Value = "EUWest2")]
        EUWest2,
        /// <summary>
        /// EUWest3 region.
        /// </summary>
        [EnumMember(Value = "EUWest3")]
        EUWest3,
        /// <summary>
        /// SAEast1 region.
        /// </summary>
        [EnumMember(Value = "SAEast1")]
        SAEast1,
        /// <summary>
        /// USEast1 region.
        /// </summary>
        [EnumMember(Value = "USEast1")]
        USEast1,
        /// <summary>
        /// USEast2 region.
        /// </summary>
        [EnumMember(Value = "USEast2")]
        USEast2,
        /// <summary>
        /// USGovCloudEast1 region.
        /// </summary>
        [EnumMember(Value = "USGovCloudEast1")]
        USGovCloudEast1,
        /// <summary>
        /// USGovCloudWest1 region.
        /// </summary>
        [EnumMember(Value = "USGovCloudWest1")]
        USGovCloudWest1,
        /// <summary>
        /// USWest1 region.
        /// </summary>
        [EnumMember(Value = "USWest1")]
        USWest1,
        /// <summary>
        /// USWest2 region.
        /// </summary>
        [EnumMember(Value = "USWest2")]
        USWest2
    }
}
