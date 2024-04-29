namespace Blobject.CIFS
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Blobject.Core;

    /// <summary>
    /// Settings when using CIFS for storage.
    /// </summary>
    public class CifsSettings : BlobSettings
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

        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="ip">IP address.</param>
        /// <param name="user">Username.  When including domain, use the form \\domain\username.</param>
        /// <param name="pass">Password.</param>
        /// <param name="share">Share name.</param>
        public CifsSettings(IPAddress ip, string user, string pass, string share)
        {
            if (ip == null) throw new ArgumentNullException(nameof(ip));
            if (String.IsNullOrEmpty(user)) throw new ArgumentNullException(nameof(user));
            if (String.IsNullOrEmpty(pass)) throw new ArgumentNullException(nameof(pass));
            if (String.IsNullOrEmpty(share)) throw new ArgumentNullException(nameof(share));

            _Ip = ip;
            _Username = user;
            _Password = pass;
            _Share = share; 
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
