namespace Blobject.Core
{
    using System;

    /// <summary>
    /// Result for a single key within a delete-many operation.
    /// </summary>
    public class DeleteResult
    {
        #region Public-Members

        /// <summary>
        /// Object key.
        /// </summary>
        public string Key { get; set; } = null;

        /// <summary>
        /// Boolean indicating whether or not the object was successfully deleted.
        /// A missing object is treated as a successful deletion.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Error detail when the object could not be deleted, or null on success.
        /// </summary>
        public string Error { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DeleteResult()
        {
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="key">Object key.</param>
        /// <param name="success">Boolean indicating whether or not the object was successfully deleted.</param>
        /// <param name="error">Error detail when the object could not be deleted, or null on success.</param>
        public DeleteResult(string key, bool success, string error = null)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            Key = key;
            Success = success;
            Error = error;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
