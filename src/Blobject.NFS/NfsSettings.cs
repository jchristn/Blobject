namespace Blobject.NFS
{
    using System;
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
        /// Hostname or IPv4 address of the NFS server.
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
                _Ip = ResolveIp(value);
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
        /// ID of the user, sent using AUTH_SYS.
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
        /// ID of the group, sent using AUTH_SYS.
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
        /// Name of the share, i.e. the export path, for example /export.
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
                _Share = NormalizeShare(value);
            }
        }

        /// <summary>
        /// NFS version.  Only <see cref="NfsVersionEnum.V3"/> is supported.
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

        /// <summary>
        /// TCP port of the NFS service.  Default is 2049.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 1 || value > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
                _Port = value;
            }
        }

        /// <summary>
        /// TCP port of the MOUNT service.  Default is 0, meaning the port is discovered through the portmapper
        /// (see <see cref="PortmapperPort"/>), falling back to <see cref="Port"/> when discovery is not possible.
        /// </summary>
        public int MountPort
        {
            get
            {
                return _MountPort;
            }
            set
            {
                if (value < 0 || value > 65535) throw new ArgumentOutOfRangeException(nameof(MountPort));
                _MountPort = value;
            }
        }

        /// <summary>
        /// TCP port of the portmapper (rpcbind) service, used when <see cref="MountPort"/> is 0.  Default is 111.
        /// </summary>
        public int PortmapperPort
        {
            get
            {
                return _PortmapperPort;
            }
            set
            {
                if (value < 1 || value > 65535) throw new ArgumentOutOfRangeException(nameof(PortmapperPort));
                _PortmapperPort = value;
            }
        }

        /// <summary>
        /// Machine name sent in AUTH_SYS credentials.  Default is the local machine name.
        /// </summary>
        public string MachineName
        {
            get
            {
                return _MachineName;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(MachineName));
                _MachineName = value;
            }
        }

        /// <summary>
        /// Stability requested for writes.  Default is <see cref="NfsWriteStabilityEnum.Unstable"/>, in which case
        /// written data is committed to stable storage once each object has been written.
        /// </summary>
        public NfsWriteStabilityEnum WriteStability { get; set; } = NfsWriteStabilityEnum.Unstable;

        /// <summary>
        /// Connection timeout in milliseconds.  Default is 10000.
        /// </summary>
        public int ConnectTimeoutMs
        {
            get
            {
                return _ConnectTimeoutMs;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(ConnectTimeoutMs));
                _ConnectTimeoutMs = value;
            }
        }

        /// <summary>
        /// Response timeout in milliseconds for each RPC.  Default is 30000.
        /// </summary>
        public int ResponseTimeoutMs
        {
            get
            {
                return _ResponseTimeoutMs;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(ResponseTimeoutMs));
                _ResponseTimeoutMs = value;
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
        private int _Port = 2049;
        private int _MountPort = 0;
        private int _PortmapperPort = 111;
        private string _MachineName = Environment.MachineName;
        private int _ConnectTimeoutMs = 10000;
        private int _ResponseTimeoutMs = 30000;

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
        /// <param name="share">Share name, i.e. the export path.</param>
        /// <param name="version">NFS version.</param>
        public NfsSettings(string hostname, int userId, int groupId, string share, NfsVersionEnum version)
        {
            if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));
            if (String.IsNullOrEmpty(share)) throw new ArgumentNullException(nameof(share));
            if (userId < 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (groupId < 0) throw new ArgumentOutOfRangeException(nameof(groupId));

            _Ip = ResolveIp(hostname);
            _Hostname = hostname;
            _UserId = userId;
            _GroupId = groupId;
            _Share = NormalizeShare(share);
            _Version = version;
        }

        #endregion

        #region Private-Methods

        private static IPAddress ResolveIp(string hostname)
        {
            IPAddress ip;

            if (Common.IsIpV4Address(hostname))
            {
                ip = IPAddress.Parse(hostname);
            }
            else
            {
                try
                {
                    ip = Common.ResolveHostToIpV4Address(hostname);
                }
                catch (SocketException e)
                {
                    throw new ArgumentException("Unable to resolve hostname '" + hostname + "'", nameof(hostname), e);
                }
            }

            if (ip == null) throw new ArgumentException("Unable to resolve hostname '" + hostname + "'", nameof(hostname));
            return ip;
        }

        private static string NormalizeShare(string share)
        {
            share = share.Replace("\\", "/");
            while (share.Length > 1 && share.EndsWith("/")) share = share.Substring(0, share.Length - 1); // remove trailing slash
            return share;
        }

        #endregion
    }
}
