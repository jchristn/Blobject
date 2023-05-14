using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Komodo.Sdk;
using Komodo.Sdk.Classes;

namespace BlobHelper
{
    /// <inheritdoc />
    public class KomodoBlobClient : IBlobClient
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly KomodoSettings _KomodoSettings = null;
        private readonly KomodoSdk _Komodo = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="KomodoBlobClient"/> class.
        /// </summary>
        /// <param name="komodoSettings">Settings for <see cref="KomodoBlobClient"/>.</param>
        public KomodoBlobClient(KomodoSettings komodoSettings)
        {
            _KomodoSettings = komodoSettings;
            _Komodo = new KomodoSdk(_KomodoSettings.Endpoint, _KomodoSettings.ApiKey);
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            DocumentData data = await _Komodo.GetSourceDocument(_KomodoSettings.IndexGUID, key, token).ConfigureAwait(false);
            return data.Data;
        }

        /// <inheritdoc />
        public async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            BlobData ret = new BlobData();
            DocumentData data = await _Komodo.GetSourceDocument(_KomodoSettings.IndexGUID, key, token).ConfigureAwait(false);
            ret.ContentLength = data.ContentLength;
            ret.Data = data.DataStream;
            return ret;
        }

        /// <inheritdoc />
        public async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            DocumentMetadata dm = await _Komodo.GetDocumentMetadata(_KomodoSettings.IndexGUID, key, token).ConfigureAwait(false);
            BlobMetadata md = new BlobMetadata();
            md.ContentLength = dm.SourceRecord.ContentLength;
            md.ContentType = dm.SourceRecord.ContentType;
            md.CreatedUtc = dm.SourceRecord.Created;
            md.ETag = dm.SourceRecord.Md5;
            md.Key = dm.SourceRecord.GUID;
            md.LastAccessUtc = null;
            md.LastUpdateUtc = null;
            return md;
        }

        /// <inheritdoc />
        public Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            await _Komodo.AddDocument(_KomodoSettings.IndexGUID, key, key, null, key, DocType.Unknown, data, null, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            byte[] data = Common.StreamToBytes(stream);
            await WriteAsync(key, contentType, data, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default)
        {
            foreach (WriteRequest obj in objects)
            {
                if (obj.Data != null)
                {
                    await WriteAsync(obj.Key, obj.ContentType, obj.Data, token).ConfigureAwait(false);
                }
                else
                {
                    await WriteAsync(obj.Key, obj.ContentType, obj.ContentLength, obj.DataStream, token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string key, CancellationToken token = default)
        {
            await _Komodo.DeleteDocument(_KomodoSettings.IndexGUID, key, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            try
            {
                DocumentMetadata md = await _Komodo.GetDocumentMetadata(_KomodoSettings.IndexGUID, key, token).ConfigureAwait(false);
                return true;
            }
            catch (KomodoException)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public string GenerateUrl(string key, CancellationToken token = default)
        {
            if (!_KomodoSettings.Endpoint.EndsWith("/")) _KomodoSettings.Endpoint += "/";

            string ret =
                _KomodoSettings.Endpoint +
                _KomodoSettings.IndexGUID + "/" +
                key;

            return ret;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult> EnumerateAsync(string prefix = null, string continuationToken = null, CancellationToken token = default)
        {
            int startIndex = 0;
            int count = 1000;
            if (!String.IsNullOrEmpty(continuationToken))
            {
                if (!ContinuationTokenHelper.ParseContinuationToken(continuationToken, out startIndex, out count))
                {
                    throw new ArgumentException("Unable to parse continuation token.");
                }
            }

            EnumerationQuery eq = new EnumerationQuery();
            eq.StartIndex = startIndex;
            eq.MaxResults = count;

            if (!String.IsNullOrEmpty(prefix))
            {
                SearchFilter sf = new SearchFilter("GUID", SearchCondition.StartsWith, prefix);
                eq.Filters.Add(sf);
            }

            Komodo.Sdk.Classes.EnumerationResult ker = await _Komodo.Enumerate(_KomodoSettings.IndexGUID, eq, token).ConfigureAwait(false);

            EnumerationResult ret = new EnumerationResult();
            ret.NextContinuationToken = KomodoBuildContinuationToken(startIndex + count, count);

            if (ker.Matches != null && ker.Matches.Count > 0)
            {
                foreach (SourceDocument curr in ker.Matches)
                {
                    BlobMetadata md = new BlobMetadata();
                    md.ContentLength = curr.ContentLength;
                    md.ContentType = curr.ContentType;
                    md.CreatedUtc = curr.Created;
                    md.ETag = curr.Md5;
                    md.Key = curr.GUID;
                    ret.Blobs.Add(md);
                }
            }

            return ret;
        }

        /// <inheritdoc />
        public async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult er = new EmptyResult();

            string continuationToken = null;

            while (true)
            {
                EnumerationResult result = await EnumerateAsync(null, null, token).ConfigureAwait(false);
                continuationToken = result.NextContinuationToken;

                if (result.Blobs != null && result.Blobs.Count > 0)
                {
                    foreach (BlobMetadata md in result.Blobs)
                    {
                        await DeleteAsync(md.Key, token).ConfigureAwait(false);
                        er.Blobs.Add(md);
                    }
                }
                else
                {
                    break;
                }
            }

            return er;
        }

        #endregion

        #region Private-Methods

        private static string KomodoBuildContinuationToken(long start, int count)
        {
            if (start >= count) return null;
            return ContinuationTokenHelper.BuildContinuationToken(start, count);
        }

        #endregion
    }
}