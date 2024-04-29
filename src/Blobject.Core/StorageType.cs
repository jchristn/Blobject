namespace Blobject.Core
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Storage type.
    /// </summary>
    public enum StorageType
    {
        /// <summary>
        /// Amazon Simple Storage Service.
        /// </summary>
        [EnumMember(Value = "AwsS3")]
        AwsS3,
        /// <summary>
        /// Microsoft Azure BLOB Storage Service.
        /// </summary>
        [EnumMember(Value = "Azure")]
        Azure,
        /// <summary>
        /// Local filesystem/disk storage.
        /// </summary>
        [EnumMember(Value = "Disk")]
        Disk,
        /// <summary>
        /// CIFS/SMB, e.g. Windows file share or SAMBA.
        /// </summary>
        [EnumMember(Value = "SMB")]
        CIFS,
        /// <summary>
        /// NFS, e.g. Linux export.
        /// </summary>
        [EnumMember(Value = "NFS")]
        NFS
    }
}
