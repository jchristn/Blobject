namespace Blobject.NFS
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using Blobject.Core;

    /// <summary>
    /// Settings when using NFS for storage.
    /// </summary>
    public class NfsSettings : BlobSettings
    {
        #region Public-Members

        /// <summary>
        /// Hostname.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Hostname));

                if (Common.IsIpV4Address(value))
                {
                    _Ip = IPAddress.Parse(value);
                }
                else
                {
                    _Ip = Common.ResolveHostToIpV4Address(value);
                }

                if (_Ip == null) throw new ArgumentException("Unable to resolve hostname '" + value + "'");
            }
        }

        /// <summary>
        /// IP address from the supplied hostname.
        /// </summary>
        public IPAddress Ip
        {
            get
            {
                return _Ip;
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
                value = value.Replace("\\", "/");
                while (value.EndsWith("/")) 
                    value = value.Substring(0, value.Length - 1); // remove trailing slash

                _Share = value;
            }
        }

        /// <summary>
        /// NFS version.
        /// </summary>
        public NfsVersionEnum Version
        {
            get
            {
                return _Version;
            }
            set
            {
                _Version = value;
            }
        }

        #endregion

        #region Private-Members

        private IPAddress _Ip = null;
        private string _Hostname = null;
        private int _UserId = 0;
        private int _GroupId = 0;
        private string _Share = null;
        private NfsVersionEnum _Version = NfsVersionEnum.V3;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the object.
        /// </summary>
        public NfsSettings()
        {
            _Hostname = "localhost";
            _Ip = IPAddress.Parse("127.0.0.1");
        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="hostname">Hostname of the server.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="groupId">Group ID.</param>
        /// <param name="share">Share name.</param>
        /// <param name="version">NFS version.</param>
        public NfsSettings(string hostname, int userId, int groupId, string share, NfsVersionEnum version)
        {
            if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));
            if (String.IsNullOrEmpty(share)) throw new ArgumentNullException(nameof(share));

            _Hostname = hostname;
            _UserId = userId;
            _GroupId = groupId;
            _Share = share;
            _Version = version;

            if (Common.IsIpV4Address(_Hostname))
            {
                _Ip = IPAddress.Parse(_Hostname);
            }
            else
            {
                _Ip = Common.ResolveHostToIpV4Address(_Hostname);
            }

            if (_Ip == null) throw new ArgumentException("Unable to resolve hostname '" + _Hostname + "'");
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
