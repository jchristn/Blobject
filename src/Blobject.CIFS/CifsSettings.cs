namespace Blobject.CIFS
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using Blobject.Core;

    /// <summary>
    /// Settings when using CIFS/SMB for storage.
    /// </summary>
    public class CifsSettings : BlobSettings
    {
        #region Public-Members

        /// <summary>
        /// Hostname or IPv4 address of the SMB server.
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
        /// TCP port of the SMB server.  Default is 445.
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
        /// Username.  When including domain, use the form domain\username, or set <see cref="Domain"/>.
        /// </summary>
        public string Username
        {
            get
            {
                return _Username;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Username));
                _Username = value;
            }
        }

        /// <summary>
        /// Password.
        /// </summary>
        public string Password
        {
            get
            {
                return _Password;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Password));
                _Password = value;
            }
        }

        /// <summary>
        /// Domain or workgroup of the user.  Default is empty, meaning the domain is taken from a domain\username
        /// value in <see cref="Username"/>, or left to the server when not supplied.
        /// </summary>
        public string Domain
        {
            get
            {
                return _Domain;
            }
            set
            {
                _Domain = (value != null ? value : "");
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
                _Share = NormalizeShare(value);
            }
        }

        /// <summary>
        /// Boolean indicating whether or not SMB message signing is required.  Default is false.
        /// </summary>
        public bool RequireSigning { get; set; } = false;

        /// <summary>
        /// Boolean indicating whether or not SMB 3.x encryption is preferred when the server supports it.  Default is true.
        /// </summary>
        public bool PreferEncryption { get; set; } = true;

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
        /// Maximum number of SMB connections the client opens to the server.  Requests on one connection run one at a time,
        /// so additional connections allow operations to run in parallel.  Default is 4.
        /// </summary>
        public int MaxConnections
        {
            get
            {
                return _MaxConnections;
            }
            set
            {
                if (value < 1 || value > 64) throw new ArgumentOutOfRangeException(nameof(MaxConnections));
                _MaxConnections = value;
            }
        }

        #endregion

        #region Private-Members

        private IPAddress _Ip = null;
        private string _Hostname = null;
        private int _Port = 445;
        private string _Username = null;
        private string _Password = null;
        private string _Domain = "";
        private string _Share = null;
        private int _ConnectTimeoutMs = 10000;
        private int _MaxConnections = 4;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CifsSettings()
        {
            _Hostname = "localhost";
            _Ip = IPAddress.Parse("127.0.0.1");
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="hostname">Hostname or IPv4 address.</param>
        /// <param name="user">Username.  When including domain, use the form domain\username.</param>
        /// <param name="pass">Password.</param>
        /// <param name="share">Share.</param>
        public CifsSettings(string hostname, string user, string pass, string share)
        {
            if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));
            if (String.IsNullOrEmpty(user)) throw new ArgumentNullException(nameof(user));
            if (String.IsNullOrEmpty(pass)) throw new ArgumentNullException(nameof(pass));
            if (String.IsNullOrEmpty(share)) throw new ArgumentNullException(nameof(share));

            _Ip = ResolveIp(hostname);
            _Hostname = hostname;
            _Username = user;
            _Password = pass;
            _Share = NormalizeShare(share);
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="hostname">Hostname or IPv4 address.</param>
        /// <param name="port">TCP port.</param>
        /// <param name="user">Username.  When including domain, use the form domain\username.</param>
        /// <param name="pass">Password.</param>
        /// <param name="share">Share.</param>
        public CifsSettings(string hostname, int port, string user, string pass, string share) : this(hostname, user, pass, share)
        {
            Port = port;
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
            share = share.Replace("/", "\\");
            while (share.EndsWith("\\")) share = share.Substring(0, share.Length - 1); // remove trailing slash
            while (share.StartsWith("\\")) share = share.Substring(1);
            if (String.IsNullOrEmpty(share)) throw new ArgumentException("A share name is required.", nameof(share));
            return share;
        }

        #endregion
    }
}
