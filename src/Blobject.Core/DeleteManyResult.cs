namespace Blobject.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Results from a delete-many operation.
    /// </summary>
    public class DeleteManyResult
    {
        #region Public-Members

        /// <summary>
        /// The number of keys included in the operation.
        /// </summary>
        public long Count
        {
            get
            {
                return _Results.Count;
            }
        }

        /// <summary>
        /// Boolean indicating whether or not every key was deleted successfully.
        /// Returns true when the operation deleted nothing.
        /// </summary>
        public bool Success
        {
            get
            {
                return _Results.All(r => r.Success);
            }
        }

        /// <summary>
        /// Per-key delete results.
        /// </summary>
        public List<DeleteResult> Results
        {
            get
            {
                return _Results;
            }
            set
            {
                if (value == null) _Results = new List<DeleteResult>();
                else _Results = value;
            }
        }

        /// <summary>
        /// Keys that were deleted successfully.
        /// </summary>
        public List<string> Deleted
        {
            get
            {
                return _Results.Where(r => r.Success).Select(r => r.Key).ToList();
            }
        }

        /// <summary>
        /// Keys that could not be deleted.
        /// </summary>
        public List<string> Failed
        {
            get
            {
                return _Results.Where(r => !r.Success).Select(r => r.Key).ToList();
            }
        }

        #endregion

        #region Private-Members

        private List<DeleteResult> _Results = new List<DeleteResult>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public DeleteManyResult()
        {
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
