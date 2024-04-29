namespace Blobject.NFS
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Blobject.Core;

    /// <summary>
    /// Settings when using NFS for storage.
    /// </summary>
    public class NfsSettings : BlobSettings
    {
        #region Public-Members

        /// <summary>
        /// IP address of the server.
        /// </summary>
        public IPAddress Ip
        {
            get
            {
                return _Ip;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Ip));
                _Ip = value;
            }
        }

        /// <summary>
        /// ID of the user.
        /// </summary>
        public int UserId
        {
            get
            {
                return _UserId;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(UserId));
                _UserId = value;
            }
        }

        /// <summary>
        /// ID of the group.
        /// </summary>
        public int GroupId
        {
            get
            {
                return _GroupId;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(GroupId));
                _GroupId = value;
            }
        }

        /// <summary>
        /// Name of the share.
        /// </summary>
        public string Share
        {
            get
            {
                return _Share;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Share));
                _Share = value;
            }
        }

        /// <summary>
        /// NFS version.
        /// </summary>
        public NfsVersionEnum Version { get; set; } = NfsVersionEnum.V3;

        #endregion

        #region Private-Members

        private IPAddress _Ip = null;
        private int _UserId = 0;
        private int _GroupId = 0;
        private string _Share = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the object.
        /// </summary>
        public NfsSettings()
        {

        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="ip">IP address.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="groupId">Group ID.</param>
        /// <param name="share">Share name.</param>
        /// <param name="version">NFS version.</param>
        public NfsSettings(IPAddress ip, int userId, int groupId, string share, NfsVersionEnum version)
        {
            if (ip == null) throw new ArgumentNullException(nameof(ip));
            if (String.IsNullOrEmpty(share)) throw new ArgumentNullException(nameof(share));

            _Ip = ip;
            _UserId = userId;
            _GroupId = groupId;
            _Share = share;

            Version = version;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
