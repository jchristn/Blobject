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
                IPHostEntry host = Dns.GetHostEntry(value);
                if (host.AddressList.Length < 1) throw new ArgumentException("Unable to resolve hostname '" + value + "'");
                _Ip = host.AddressList[0];
                _Hostname = value;
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
        private string _Hostname = null;
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
            IPHostEntry host = Dns.GetHostEntry("localhost");
            _Ip = host.AddressList[0];
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

            Version = version;

            IPHostEntry host = Dns.GetHostEntry(_Hostname);
            
            foreach (var candidate in host.AddressList)
            {
                byte[] bytes = candidate.GetAddressBytes();

                switch (candidate.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        if (bytes.Length == 4) _Ip = candidate;
                        break;
                }
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
