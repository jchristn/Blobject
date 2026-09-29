namespace Blobject.NFS
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Stability requested for NFS writes.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NfsWriteStabilityEnum
    {
        /// <summary>
        /// The server may cache written data; data is committed to stable storage once the object has been written.
        /// </summary>
        [EnumMember(Value = "Unstable")]
        Unstable,
        /// <summary>
        /// Data is written to stable storage before each write completes; file metadata may be cached.
        /// </summary>
        [EnumMember(Value = "DataSync")]
        DataSync,
        /// <summary>
        /// Data and file metadata are written to stable storage before each write completes.
        /// </summary>
        [EnumMember(Value = "FileSync")]
        FileSync
    }
}
