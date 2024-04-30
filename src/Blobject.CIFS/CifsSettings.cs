namespace Blobject.CIFS
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using Blobject.Core;

    /// <summary>
    /// Settings when using CIFS for storage.
    /// </summary>
    public class CifsSettings : BlobSettings
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
        /// Username.  When including domain, use the form \\domain\username.
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

        #endregion

        #region Private-Members

        private IPAddress _Ip = null;
        private string _Hostname = null;
        private string _Username = null;
        private string _Password = null;
        private string _Share = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the object.
        /// </summary>
        public CifsSettings()
        {
            IPHostEntry host = Dns.GetHostEntry("localhost");
            _Ip = host.AddressList[0];
        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="hostname">Hostname of the server.</param>
        /// <param name="user">Username.  When including domain, use the form \\domain\username.</param>
        /// <param name="pass">Password.</param>
        /// <param name="share">Share name.</param>
        public CifsSettings(string hostname, string user, string pass, string share)
        {
            if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));
            if (String.IsNullOrEmpty(user)) throw new ArgumentNullException(nameof(user));
            if (String.IsNullOrEmpty(pass)) throw new ArgumentNullException(nameof(pass));
            if (String.IsNullOrEmpty(share)) throw new ArgumentNullException(nameof(share));

            _Hostname = hostname;
            _Username = user;
            _Password = pass;
            _Share = share;

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
