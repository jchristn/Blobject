using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KvpbaseSDK;

namespace BlobHelper
{
    /// <inheritdoc />
    public class KvpbaseBlobClient : IBlobClient
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly KvpbaseClient _Kvpbase = null;
        private readonly KvpbaseSettings _KvpbaseSettings = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="KvpbaseBlobClient"/> class.
        /// </summary>
        /// <param name="kvpbaseSettings">Settings for <see cref="KvpbaseBlobClient"/>.</param>
        public KvpbaseBlobClient(KvpbaseSettings kvpbaseSettings)
        {
            _KvpbaseSettings = kvpbaseSettings;
            _Kvpbase = new KvpbaseClient(_KvpbaseSettings.UserGuid, _KvpbaseSettings.ApiKey, _KvpbaseSettings.Endpoint);
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            KvpbaseObject kvpObject = await _Kvpbase.ReadObject(_KvpbaseSettings.Container, key, token).ConfigureAwait(false);
            return Common.StreamToBytes(kvpObject.Data);
        }

        /// <inheritdoc />
        public async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            KvpbaseObject kvpObj = await _Kvpbase.ReadObject(_KvpbaseSettings.Container, key, token).ConfigureAwait(false);
            return new BlobData(kvpObj.ContentLength, kvpObj.Data);
        }

        /// <inheritdoc />
        public async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            ObjectMetadata objMd = await _Kvpbase.ReadObjectMetadata(_KvpbaseSettings.Container, key, token).ConfigureAwait(false);
            if (objMd != null)
            {
                BlobMetadata md = new BlobMetadata();
                md.Key = objMd.ObjectKey;
                md.ContentLength = Convert.ToInt64(objMd.ContentLength);
                md.ContentType = objMd.ContentType;
                md.ETag = objMd.Md5;
                md.CreatedUtc = objMd.CreatedUtc;
                md.LastAccessUtc = objMd.LastAccessUtc;
                md.LastUpdateUtc = objMd.LastUpdateUtc;
                return md;
            }
            else
            {
                throw new KeyNotFoundException("The requested object was not found.");
            }
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
            long contentLength = 0;
            MemoryStream stream = new MemoryStream(Array.Empty<byte>());

            if (data != null && data.Length > 0)
            {
                contentLength = data.Length;
                stream = new MemoryStream(data);
                stream.Seek(0, SeekOrigin.Begin);
            }

            await WriteAsync(key, contentType, contentLength, stream, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            await _Kvpbase.WriteObject(_KvpbaseSettings.Container, key, contentLength, stream, contentType, token).ConfigureAwait(false);
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
            await _Kvpbase.DeleteObject(_KvpbaseSettings.Container, key, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            return await _Kvpbase.ObjectExists(_KvpbaseSettings.Container, key, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public string GenerateUrl(string key, CancellationToken token = default)
        {
            if (!_KvpbaseSettings.Endpoint.EndsWith("/")) _KvpbaseSettings.Endpoint += "/";

            string ret =
                _KvpbaseSettings.Endpoint +
                _KvpbaseSettings.UserGuid + "/" +
                _KvpbaseSettings.Container + "/" +
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

            ContainerMetadata cmd = null;
            EnumerationResult ret = new EnumerationResult();

            if (String.IsNullOrEmpty(prefix))
            {
                cmd = await _Kvpbase.EnumerateContainer(_KvpbaseSettings.Container, startIndex, count, token).ConfigureAwait(false);
            }
            else
            {
                EnumerationFilter filter = new EnumerationFilter();
                filter.Prefix = prefix;
                cmd = await _Kvpbase.EnumerateContainer(filter, _KvpbaseSettings.Container, startIndex, count, token).ConfigureAwait(false);
            }

            ret.NextContinuationToken = ContinuationTokenHelper.BuildContinuationToken(startIndex + count, count);

            if (cmd.Objects != null && cmd.Objects.Count > 0)
            {
                foreach (ObjectMetadata curr in cmd.Objects)
                {
                    BlobMetadata md = new BlobMetadata();
                    md.Key = curr.ObjectKey;
                    md.ETag = curr.Md5;
                    md.ContentLength = Convert.ToInt64(curr.ContentLength);
                    md.ContentType = curr.ContentType;
                    md.CreatedUtc = curr.CreatedUtc.Value;
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

        #endregion
    }
}