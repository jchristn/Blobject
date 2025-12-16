namespace Blobject.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Enumeration filter.
    /// </summary>
    public class EnumerationFilter
    {
        #region Public-Members

        /// <summary>
        /// Minimum size.
        /// </summary>
        public long MinimumSize
        {
            get
            {
                return _MinimumSize;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(MinimumSize));
                _MinimumSize = value;
            }
        }

        /// <summary>
        /// Maximum size.
        /// </summary>
        public long MaximumSize
        {
            get
            {
                return _MaximumSize;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(MaximumSize));
                _MaximumSize = value;
            }
        }

        /// <summary>
        /// Prefix.
        /// </summary>
        public string Prefix
        {
            get
            {
                return _Prefix;
            }
            set
            {
                if (!String.IsNullOrEmpty(value)) _Prefix = value.ToLower();
                else _Prefix = "";
            }
        }

        /// <summary>
        /// Suffix.
        /// </summary>
        public string Suffix
        {
            get
            {
                return _Suffix;
            }
            set
            {
                if (!String.IsNullOrEmpty(value)) _Suffix = value.ToLower();
                else _Suffix = "";
            }
        }

        #endregion

        #region Private-Members

        private long _MinimumSize = 0;
        private long _MaximumSize = Int64.MaxValue;
        private string _Prefix = "";
        private string _Suffix = "";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public EnumerationFilter()
        {
        }

        #endregion

        #region Public-Methods
         
        #endregion

        #region Private-Methods

        #endregion
    }
}
