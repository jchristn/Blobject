namespace Blobject.NFS
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// NFS version.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NfsVersionEnum
    {
        /// <summary>
        /// V2.  Not supported.
        /// </summary>
        [EnumMember(Value = "V2")]
        V2,
        /// <summary>
        /// V3.  Supported.
        /// </summary>
        [EnumMember(Value = "V3")]
        V3,
        /// <summary>
        /// V4.  Not supported.
        /// </summary>
        [EnumMember(Value = "V4")]
        V4
    }
}
