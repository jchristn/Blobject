using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks; 
using KvpbaseSDK; 
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Amazon.S3;
using Amazon.S3.Model;
using Komodo.Sdk;
using Komodo.Sdk.Classes;

namespace BlobHelper
{
    /// <summary>
    /// BLOB storage client.
    /// </summary>
    public class Blobs
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private StorageType _StorageType = StorageType.Disk;
        private AwsSettings _AwsSettings = null;
        private AzureSettings _AzureSettings = null;
        private DiskSettings _DiskSettings = null;
        private KvpbaseSettings _KvpbaseSettings = null;
        private KomodoSettings _KomodoSettings = null;

        private AmazonS3Config _S3Config = null;
        private IAmazonS3 _S3Client = null;
        private Amazon.Runtime.BasicAWSCredentials _S3Credentials = null;
        private Amazon.RegionEndpoint _S3Region = null;

        private StorageCredentials _AzureCredentials = null;
        private CloudStorageAccount _AzureAccount = null;
        private CloudBlobClient _AzureBlobClient = null;
        private CloudBlobContainer _AzureContainer = null;

        private KvpbaseClient _Kvpbase = null;

        private KomodoSdk _Komodo = null;

        private ConcurrentDictionary<string, BlobContinuationToken> _AzureContinuationTokens = new ConcurrentDictionary<string, BlobContinuationToken>();

        #endregion

        #region Constructors-and-Factories
        
        private Blobs()
        {
            throw new NotImplementedException("Use a StorageType specific constructor.");
        }

        /// <summary>
        /// Instantiate the object for Azure BLOB strorage.
        /// </summary>
        /// <param name="config">Storage configuration.</param>
        public Blobs(AzureSettings config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _AzureSettings = config;
            _StorageType = StorageType.Azure;
            InitializeClients();
        }

        /// <summary>
        /// Instantiate the object for AWS S3 strorage.
        /// </summary>
        /// <param name="config">Storage configuration.</param>
        public Blobs(AwsSettings config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _AwsSettings = config;
            _StorageType = StorageType.AwsS3;
            InitializeClients();
        }

        /// <summary>
        /// Instantiate the object for disk strorage.
        /// </summary>
        /// <param name="config">Storage configuration.</param>
        public Blobs(DiskSettings config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _DiskSettings = config;
            _StorageType = StorageType.Disk;
            InitializeClients();
        }

        /// <summary>
        /// Instantiate the object for Kvpbase strorage.
        /// </summary>
        /// <param name="config">Storage configuration.</param>
        public Blobs(KvpbaseSettings config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _KvpbaseSettings = config;
            _StorageType = StorageType.Kvpbase;
            InitializeClients();
        }

        /// <summary>
        /// Instantiate the object for a Komodo index.
        /// </summary>
        /// <param name="config">Storage configuration.</param>
        public Blobs(KomodoSettings config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _KomodoSettings = config;
            _StorageType = StorageType.Komodo;
            InitializeClients();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Delete a BLOB by its key.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns>True if successful.</returns>
        public Task Delete(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                switch (_StorageType)
                {
                    case StorageType.AwsS3:
                        return S3Delete(key, token);
                    case StorageType.Azure:
                        return AzureDelete(key, token);
                    case StorageType.Disk:
                        return DiskDelete(key, token);
                    case StorageType.Komodo:
                        return KomodoDelete(key, token);
                    case StorageType.Kvpbase:
                        return KvpbaseDelete(key, token);
                    default:
                        throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
                }
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieve a BLOB.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param> 
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns>Byte data of the BLOB.</returns>
        public Task<byte[]> Get(string key, CancellationToken token = default)
        { 
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                switch (_StorageType)
                {
                    case StorageType.AwsS3:
                        return S3Get(key, token);
                    case StorageType.Azure:
                        return AzureGet(key, token);
                    case StorageType.Disk:
                        return DiskGet(key, token);
                    case StorageType.Komodo:
                        return KomodoGet(key, token);
                    case StorageType.Kvpbase:
                        return KvpbaseGet(key, token);
                    default:
                        throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
                }
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieve a BLOB.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param> 
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns>BLOB data.</returns>
        public Task<BlobData> GetStream(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                switch (_StorageType)
                {
                    case StorageType.AwsS3:
                        return S3GetStream(key, token);
                    case StorageType.Azure:
                        return AzureGetStream(key, token);
                    case StorageType.Disk:
                        return DiskGetStream(key, token);
                    case StorageType.Komodo:
                        return KomodoGetStream(key, token);
                    case StorageType.Kvpbase:
                        return KvpbaseGetStream(key, token);
                    default:
                        throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
                }
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Write a BLOB using a string.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="contentType">Content-type of the object.</param>
        /// <param name="data">BLOB data.</param>
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns></returns>
        public Task Write(string key, string contentType, string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Write(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <summary>
        /// Write a BLOB using a byte array.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="contentType">Content-type of the object.</param>
        /// <param name="data">BLOB data.</param> 
        /// <param name="token">Cancellation token to cancel the request.</param>
        public Task Write(
            string key,
            string contentType,
            byte[] data, 
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                switch (_StorageType)
                {
                    case StorageType.AwsS3:
                        return S3Write(key, contentType, data, token);
                    case StorageType.Azure:
                        return AzureWrite(key, contentType, data, token);
                    case StorageType.Disk:
                        return DiskWrite(key, data, token);
                    case StorageType.Komodo:
                        return KomodoWrite(key, contentType, data, token);
                    case StorageType.Kvpbase:
                        return KvpbaseWrite(key, contentType, data, token);
                    default:
                        throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
                }
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Write a BLOB using a stream.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="contentType">Content type.</param>
        /// <param name="contentLength">Content length.</param>
        /// <param name="stream">Stream containing the data.</param> 
        /// <param name="token">Cancellation token to cancel the request.</param>
        public Task Write(
            string key,
            string contentType,
            long contentLength,
            Stream stream, 
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            if (stream.CanSeek && stream.Length == stream.Position) stream.Seek(0, SeekOrigin.Begin);

            try
            {
                switch (_StorageType)
                {
                    case StorageType.AwsS3:
                        return S3Write(key, contentType, contentLength, stream, token);
                    case StorageType.Azure:
                        return AzureWrite(key, contentType, contentLength, stream, token);
                    case StorageType.Disk:
                        return DiskWrite(key, contentLength, stream, token);
                    case StorageType.Komodo:
                        return KomodoWrite(key, contentType, contentLength, stream, token);
                    case StorageType.Kvpbase:
                        return KvpbaseWrite(key, contentType, contentLength, stream, token);
                    default:
                        throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
                }
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a BLOB exists.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns>True if exists.</returns>
        public Task<bool> Exists(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                switch (_StorageType)
                {
                    case StorageType.AwsS3:
                        return S3Exists(key, token);
                    case StorageType.Azure:
                        return AzureExists(key, token);
                    case StorageType.Disk:
                        return DiskExists(key, token);
                    case StorageType.Komodo:
                        return KomodoExists(key, token);
                    case StorageType.Kvpbase:
                        return KvpbaseExists(key, token);
                    default:
                        throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
                }
            }
            catch (TaskCanceledException)
            {
                return Task.FromResult(false);
            }
            catch (OperationCanceledException)
            {
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Generate a URL for a given object key.
        /// </summary>
        /// <param name="key">Object key.</param>
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns>URL.</returns>
        public string GenerateUrl(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return S3GenerateUrl(_AwsSettings.Bucket, key);
                case StorageType.Azure:
                    return AzureGenerateUrl(key);
                case StorageType.Disk:
                    return DiskGenerateUrl(key);
                case StorageType.Komodo:
                    return KomodoGenerateUrl(key);
                case StorageType.Kvpbase:
                    return KvpbaseGenerateUrl(key);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }
        }

        /// <summary>
        /// Retrieve BLOB metadata.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param> 
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns>BLOB metadata.</returns>
        public Task<BlobMetadata> GetMetadata(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                switch (_StorageType)
                {
                    case StorageType.AwsS3:
                        return S3GetMetadata(key, token);
                    case StorageType.Azure:
                        return AzureGetMetadata(key, token);
                    case StorageType.Disk:
                        return DiskGetMetadata(key, token);
                    case StorageType.Komodo:
                        return KomodoGetMetadata(key, token);
                    case StorageType.Kvpbase:
                        return KvpbaseGetMetadata(key, token);
                    default:
                        throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
                }
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
         
        /// <summary>
        /// Enumerate BLOBs.
        /// </summary>
        /// <param name="prefix">Key prefix that must match.</param>
        /// <param name="continuationToken">Continuation token to use if issuing a subsequent enumeration request.</param>
        /// <param name="token">Cancellation token to cancel the request.</param> 
        /// <returns>Enumeration result.</returns>
        public Task<EnumerationResult> Enumerate(string prefix = null, string continuationToken = null, CancellationToken token = default)
        {
            try
            {
                switch (_StorageType)
                {
                    case StorageType.AwsS3:
                        return S3Enumerate(prefix, continuationToken, token);
                    case StorageType.Azure:
                        return AzureEnumerate(prefix, continuationToken, token);
                    case StorageType.Disk:
                        return DiskEnumerate(prefix, continuationToken, token);
                    case StorageType.Komodo:
                        return KomodoEnumerate(prefix, continuationToken, token);
                    case StorageType.Kvpbase:
                        return KvpbaseEnumerate(prefix, continuationToken, token);
                    default:
                        throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
                }
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        #endregion

        #region Private-Methods

        private void InitializeClients()
        {
            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    _S3Credentials = new Amazon.Runtime.BasicAWSCredentials(_AwsSettings.AccessKey, _AwsSettings.SecretKey);

                    if (String.IsNullOrEmpty(_AwsSettings.Endpoint))
                    {
                        _S3Region = _AwsSettings.GetAwsRegionEndpoint();
                        _S3Config = new AmazonS3Config
                        {
                            RegionEndpoint = _S3Region,
                            UseHttp = !_AwsSettings.Ssl,
                        };

                        // _S3Client = new AmazonS3Client(_S3Credentials, _S3Region);
                        _S3Client = new AmazonS3Client(_S3Credentials, _S3Config);
                    }
                    else
                    {
                        _S3Config = new AmazonS3Config
                        { 
                            ServiceURL = _AwsSettings.Endpoint,
                            ForcePathStyle = true,
                            UseHttp = !_AwsSettings.Ssl
                        };
                         
                        _S3Client = new AmazonS3Client(_S3Credentials, _S3Config);
                    }
                    break;
                case StorageType.Azure:
                    _AzureCredentials = new StorageCredentials(_AzureSettings.AccountName, _AzureSettings.AccessKey);
                    _AzureAccount = new CloudStorageAccount(_AzureCredentials, true);
                    _AzureBlobClient = new CloudBlobClient(new Uri(_AzureSettings.Endpoint), _AzureCredentials);
                    _AzureContainer = _AzureBlobClient.GetContainerReference(_AzureSettings.Container);
                    break;
                case StorageType.Disk: 
                    if (!Directory.Exists(_DiskSettings.Directory)) Directory.CreateDirectory(_DiskSettings.Directory);
                    break;
                case StorageType.Kvpbase:
                    _Kvpbase = new KvpbaseClient(_KvpbaseSettings.UserGuid, _KvpbaseSettings.ApiKey, _KvpbaseSettings.Endpoint);
                    break;
                case StorageType.Komodo:
                    _Komodo = new KomodoSdk(_KomodoSettings.Endpoint, _KomodoSettings.ApiKey);
                    break;
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }    
        }

        #region Delete

        private async Task KvpbaseDelete(string key, CancellationToken token)
        { 
            await _Kvpbase.DeleteObject(_KvpbaseSettings.Container, key, token).ConfigureAwait(false); 
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task DiskDelete(string key, CancellationToken token)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            string filename = DiskGenerateUrl(key);
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
            else if (Directory.Exists(filename))
            {
                Directory.Delete(filename);
            }
            else
            {
                throw new FileNotFoundException("Could not find file '" + key + "'.");
            }
        }

        private async Task S3Delete(string key, CancellationToken token)
        { 
            DeleteObjectRequest request = new DeleteObjectRequest
            {
                BucketName = _AwsSettings.Bucket,
                Key = key
            };

            DeleteObjectResponse response = await _S3Client.DeleteObjectAsync(request, token).ConfigureAwait(false); 
        }

        private async Task AzureDelete(string key, CancellationToken token)
        { 
            CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(key);
            OperationContext ctx = new OperationContext();
            await blockBlob.DeleteAsync(DeleteSnapshotsOption.None, null, null, ctx, token).ConfigureAwait(false); 
        }
         
        private async Task KomodoDelete(string key, CancellationToken token)
        {
            await _Komodo.DeleteDocument(_KomodoSettings.IndexGUID, key, token).ConfigureAwait(false);
        }

        #endregion

        #region Get

        private async Task<byte[]> KvpbaseGet(string key, CancellationToken token)
        { 
            KvpbaseObject kvpObject = await _Kvpbase.ReadObject(_KvpbaseSettings.Container, key, token).ConfigureAwait(false);
            return Common.StreamToBytes(kvpObject.Data); 
        }
         
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<byte[]> DiskGet(string key, CancellationToken token)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            string filename = DiskGenerateUrl(key);
            if (Directory.Exists(filename))
            {
                return new byte[0];
            }
            else if (File.Exists(filename))
            {
                return File.ReadAllBytes(filename);
            }
            else
            {
                throw new FileNotFoundException("Could not find file '" + key + "'.");
            }
        }

        private async Task<byte[]> S3Get(string key, CancellationToken token)
        { 
            GetObjectRequest request = new GetObjectRequest
            {
                BucketName = _AwsSettings.Bucket,
                Key = key,
            };

            using (GetObjectResponse response = await _S3Client.GetObjectAsync(request, token).ConfigureAwait(false))
            using (Stream responseStream = response.ResponseStream)
            using (StreamReader reader = new StreamReader(responseStream))
            {
                if (response.ContentLength > 0)
                {
                    // first copy the stream
                    byte[] data = new byte[response.ContentLength];

                    Stream bodyStream = response.ResponseStream;
                    data = Common.StreamToBytes(bodyStream);

                    int statusCode = (int)response.HttpStatusCode;
                    return data;
                }
                else
                {
                    throw new IOException("Unable to read object.");
                }
            } 
        }

        private async Task<byte[]> AzureGet(string key, CancellationToken token)
        {
            byte[] data = null;
             
            CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(key);
            OperationContext ctx = new OperationContext();

            MemoryStream stream = new MemoryStream();
            await blockBlob.DownloadToStreamAsync(stream).ConfigureAwait(false);
            stream.Seek(0, SeekOrigin.Begin);
            data = Common.StreamToBytes(stream);
            return data; 
        }

        private async Task<byte[]> KomodoGet(string key, CancellationToken token)
        {
            DocumentData data = await _Komodo.GetSourceDocument(_KomodoSettings.IndexGUID, key, token).ConfigureAwait(false);
            return data.Data;
        }

        #endregion

        #region Get-Stream

        private async Task<BlobData> KvpbaseGetStream(string key, CancellationToken token)
        { 
            KvpbaseObject kvpObj = await _Kvpbase.ReadObject(_KvpbaseSettings.Container, key, token).ConfigureAwait(false);
            return new BlobData(kvpObj.ContentLength, kvpObj.Data); 
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<BlobData> DiskGetStream(string key, CancellationToken token)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        { 
            string filename = DiskGenerateUrl(key);
            if (File.Exists(filename))
            {
                long contentLength = new FileInfo(filename).Length;
                FileStream stream = new FileStream(filename, FileMode.Open);
                return new BlobData(contentLength, stream);
            }
            else if (Directory.Exists(filename))
            {
                return new BlobData(0, new MemoryStream());
            }
            else
            {
                throw new FileNotFoundException("Could not find file '" + key + "'.");
            }
        }

        private async Task<BlobData> S3GetStream(string key, CancellationToken token)
        { 
            GetObjectRequest request = new GetObjectRequest
            {
                BucketName = _AwsSettings.Bucket,
                Key = key,
            };

            GetObjectResponse response = await _S3Client.GetObjectAsync(request, token).ConfigureAwait(false);
            BlobData ret = new BlobData();

            if (response.ContentLength > 0)
            {
                ret.ContentLength = response.ContentLength;
                ret.Data = response.ResponseStream;
            }
            else
            {
                ret.ContentLength = 0;
                ret.Data = new MemoryStream(new byte[0]);
            }

            return ret; 
        }
         
        private async Task<BlobData> AzureGetStream(string key, CancellationToken token)
        {  
            CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(key);
            blockBlob.FetchAttributesAsync().Wait();

            BlobData ret = new BlobData();
            ret.ContentLength = blockBlob.Properties.Length;

            MemoryStream stream = new MemoryStream();
            await blockBlob.DownloadToStreamAsync(stream).ConfigureAwait(false);

            ret.Data = stream;
            stream.Seek(0, SeekOrigin.Begin);
            return ret; 
        }

        private async Task<BlobData> KomodoGetStream(string key, CancellationToken token)
        {
            BlobData ret = new BlobData();
            DocumentData data = await _Komodo.GetSourceDocument(_KomodoSettings.IndexGUID, key, token).ConfigureAwait(false);
            ret.ContentLength = data.ContentLength;
            ret.Data = data.DataStream;
            return ret;
        }

        #endregion

        #region Exists

        private async Task<bool> KvpbaseExists(string key, CancellationToken token)
        {
            return await _Kvpbase.ObjectExists(_KvpbaseSettings.Container, key, token).ConfigureAwait(false);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> DiskExists(string key, CancellationToken token)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            string filename = DiskGenerateUrl(key);
            if (File.Exists(filename))
            {
                return true;
            }
            else if (Directory.Exists(filename))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task<bool> S3Exists(string key, CancellationToken token)
        { 
            GetObjectMetadataRequest request = new GetObjectMetadataRequest
            {
                BucketName = _AwsSettings.Bucket,
                Key = key
            };

            try
            {
                GetObjectMetadataResponse response = await _S3Client.GetObjectMetadataAsync(request, token).ConfigureAwait(false);
                return true;
            }
            catch (Amazon.S3.AmazonS3Exception ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return false;

                //status wasn't not found, so throw the exception
                throw;
            }
        }

        private async Task<bool> AzureExists(string key, CancellationToken token)
        { 
            return await _AzureBlobClient.GetContainerReference(_AzureSettings.Container).GetBlockBlobReference(key).ExistsAsync().ConfigureAwait(false); 
        }

        private async Task<bool> KomodoExists(string key, CancellationToken token)
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

        #endregion

        #region Write

        private async Task KvpbaseWrite(string key, string contentType, byte[] data, CancellationToken token)
        {
            long contentLength = 0;
            MemoryStream stream = new MemoryStream(new byte[0]);

            if (data != null && data.Length > 0)
            {
                contentLength = data.Length;
                stream = new MemoryStream(data);
                stream.Seek(0, SeekOrigin.Begin);
            }

            await KvpbaseWrite(key, contentType, contentLength, stream, token).ConfigureAwait(false);
        }

        private async Task KvpbaseWrite(string key, string contentType, long contentLength, Stream stream, CancellationToken token)
        { 
            await _Kvpbase.WriteObject(_KvpbaseSettings.Container, key, contentLength, stream, contentType, token).ConfigureAwait(false);  
        }

        private async Task DiskWrite(string key, byte[] data, CancellationToken token)
        {
            long contentLength = 0;
            MemoryStream stream = new MemoryStream(new byte[0]);

            if (data != null && data.Length > 0)
            {
                contentLength = data.Length;
                stream = new MemoryStream(data);
                stream.Seek(0, SeekOrigin.Begin);
            }

            await DiskWrite(key, contentLength, stream, token);
        }

        private async Task DiskWrite(string key, long contentLength, Stream stream, CancellationToken token)
        {
            string filename = DiskGenerateUrl(key);

            if (
                (key.EndsWith("\\") || key.EndsWith("/"))
                &&
                contentLength == 0
               )
            {
                Directory.CreateDirectory(filename);
            }
            else
            {
                int bytesRead = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[65536];
                
                using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
                {
                    while (bytesRemaining > 0)
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                        if (bytesRead > 0)
                        {
                            await fs.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                            bytesRemaining -= bytesRead;
                        }
                    }
                }
            }
        }

        private async Task S3Write(string key, string contentType, byte[] data, CancellationToken token)
        {
            long contentLength = 0;
            MemoryStream stream = new MemoryStream(new byte[0]);

            if (data != null && data.Length > 0)
            {
                contentLength = data.Length;
                stream = new MemoryStream(data);
                stream.Seek(0, SeekOrigin.Begin);
            }

            await S3Write(key, contentType, contentLength, stream, token).ConfigureAwait(false);
        }

        private async Task S3Write(string key, string contentType, long contentLength, Stream stream, CancellationToken token)
        { 
            PutObjectRequest request = new PutObjectRequest();

            if (stream == null || contentLength < 1)
            {
                request.BucketName = _AwsSettings.Bucket;
                request.Key = key;
                request.ContentType = contentType;
                request.UseChunkEncoding = false;
                request.InputStream = new MemoryStream(new byte[0]);
            }
            else
            {
                request.BucketName = _AwsSettings.Bucket;
                request.Key = key;
                request.ContentType = contentType;
                request.UseChunkEncoding = false;
                request.InputStream = stream;
            }

            PutObjectResponse response = await _S3Client.PutObjectAsync(request, token).ConfigureAwait(false); 
        }
         
        private async Task AzureWrite(string key, string contentType, byte[] data, CancellationToken token)
        {
            long contentLength = 0;
            MemoryStream stream = new MemoryStream(new byte[0]);

            if (data != null && data.Length > 0)
            {
                contentLength = data.Length;
                stream = new MemoryStream(data);
                stream.Seek(0, SeekOrigin.Begin);
            }

            await AzureWrite(key, contentType, contentLength, stream, token).ConfigureAwait(false);
        }

        private async Task AzureWrite(string key, string contentType, long contentLength, Stream stream, CancellationToken token)
        { 
            CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(key);
            blockBlob.Properties.ContentType = contentType;
            OperationContext ctx = new OperationContext();
            await blockBlob.UploadFromStreamAsync(stream, contentLength).ConfigureAwait(false);  
        }

        private async Task KomodoWrite(string key, string contentType, byte[] data, CancellationToken token)
        {
            await _Komodo.AddDocument(_KomodoSettings.IndexGUID, key, key, null, key, DocType.Unknown, data, null, token).ConfigureAwait(false);
        }

        private async Task KomodoWrite(string key, string contentType, long contentLength, Stream stream, CancellationToken token)
        {
            byte[] data = Common.StreamToBytes(stream);
            await KomodoWrite(key, contentType, data, token).ConfigureAwait(false);
        }

        #endregion

        #region Get-Metadata

        private async Task<BlobMetadata> KvpbaseGetMetadata(string key, CancellationToken token)
        { 
            ObjectMetadata objMd = await _Kvpbase.ReadObjectMetadata(_KvpbaseSettings.Container, key);
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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<BlobMetadata> DiskGetMetadata(string key, CancellationToken token)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        { 
            string filename = DiskGenerateUrl(key);

            if (File.Exists(filename))
            {
                FileInfo fi = new FileInfo(filename);
                BlobMetadata md = new BlobMetadata();
                md.Key = key;
                md.ContentLength = fi.Length;
                md.CreatedUtc = fi.CreationTimeUtc;
                md.LastAccessUtc = fi.LastAccessTimeUtc;
                md.LastUpdateUtc = fi.LastWriteTimeUtc;
                return md;
            }
            else if (Directory.Exists(filename))
            {
                DirectoryInfo di = new DirectoryInfo(filename);
                BlobMetadata md = new BlobMetadata();
                md.Key = key;
                md.ContentLength = 0;
                md.CreatedUtc = di.CreationTimeUtc;
                md.LastAccessUtc = di.LastAccessTimeUtc;
                md.LastUpdateUtc = di.LastWriteTimeUtc;
                return md;
            }
            else
            {
                throw new FileNotFoundException("Could not find file '" + key + "'.");
            }
        }
         
        private async Task<BlobMetadata> S3GetMetadata(string key, CancellationToken token)
        { 
            GetObjectMetadataRequest request = new GetObjectMetadataRequest();
            request.BucketName = _AwsSettings.Bucket;
            request.Key = key;

            GetObjectMetadataResponse response = await _S3Client.GetObjectMetadataAsync(request);

            if (response.ContentLength > 0)
            {
                BlobMetadata md = new BlobMetadata();
                md.Key = key;
                md.ContentLength = response.ContentLength;
                md.ContentType = response.Headers.ContentType;
                md.ETag = response.ETag;
                md.CreatedUtc = response.LastModified;

                if (!String.IsNullOrEmpty(md.ETag))
                {
                    while (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
                }

                return md;
            }
            else
            {
                throw new KeyNotFoundException("The requested object was not found.");
            }
        }
         
        private async Task<BlobMetadata> AzureGetMetadata(string key, CancellationToken token)
        {  
            CloudBlobContainer container = _AzureBlobClient.GetContainerReference(_AzureSettings.Container);
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(key);
            await blockBlob.FetchAttributesAsync();

            BlobMetadata md = new BlobMetadata();
            md.Key = key;
            md.ContentLength = blockBlob.Properties.Length;
            md.ContentType = blockBlob.Properties.ContentType;
            md.ETag = blockBlob.Properties.ETag;
            md.CreatedUtc = blockBlob.Properties.Created.Value.UtcDateTime;
            md.LastUpdateUtc = blockBlob.Properties.LastModified.Value.UtcDateTime;

            if (!String.IsNullOrEmpty(md.ETag))
            {
                while (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
            }

            return md; 
        }

        private async Task<BlobMetadata> KomodoGetMetadata(string key, CancellationToken token)
        {
            DocumentMetadata dm = await _Komodo.GetDocumentMetadata(_KomodoSettings.IndexGUID, key);
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

        #endregion

        #region Enumeration

        private async Task<EnumerationResult> KvpbaseEnumerate(string prefix, string continuationToken, CancellationToken token)
        {
            int startIndex = 0;
            int count = 1000;
            if (!String.IsNullOrEmpty(continuationToken))
            {
                if (!KvpbaseParseContinuationToken(continuationToken, out startIndex, out count))
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

            ret.NextContinuationToken = KvpbaseBuildContinuationToken(startIndex + count, count);

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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<EnumerationResult> DiskEnumerate(string prefix, string continuationToken, CancellationToken token)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            int startIndex = 0;
            int count = 1000;

            if (!String.IsNullOrEmpty(continuationToken))
            {
                if (!DiskParseContinuationToken(continuationToken, out startIndex, out count))
                {
                    throw new ArgumentException("Unable to parse continuation token.");
                }
            }

            long maxIndex = startIndex + count;

            long currCount = 0;
            IEnumerable<string> files = null;

            if (!String.IsNullOrEmpty(prefix))
            {
                if (Directory.Exists(_DiskSettings.Directory + prefix))
                {
                    string tempPrefix = prefix;
                    tempPrefix = tempPrefix.Replace("\\", "/");
                    if (!tempPrefix.EndsWith("/")) tempPrefix += "/";
                    files = Directory.EnumerateFiles(_DiskSettings.Directory, tempPrefix + "*", SearchOption.AllDirectories);
                }
                else
                {
                    files = Directory.EnumerateFiles(_DiskSettings.Directory, prefix + "*", SearchOption.AllDirectories);
                }
            }
            else
            {
                files = Directory.EnumerateFiles(_DiskSettings.Directory, "*", SearchOption.AllDirectories);
            }

            files = files.Skip(startIndex).Take(count);

            EnumerationResult ret = new EnumerationResult();
            if (files.Count() < 1) return ret;

            ret.NextContinuationToken = DiskBuildContinuationToken(startIndex + count, count);

            foreach (string file in files)
            {
                FileInfo fi = new FileInfo(file);

                string filename = file;
                if (filename.StartsWith(_DiskSettings.Directory)) filename = file.Substring(_DiskSettings.Directory.Length);
                if (!String.IsNullOrEmpty(filename)) filename = filename.Replace("\\", "/");

                BlobMetadata md = new BlobMetadata();
                md.Key = filename;

                md.ContentLength = fi.Length;
                md.CreatedUtc = fi.CreationTimeUtc;
                ret.Blobs.Add(md);

                currCount++;
                continue;
            }

            return ret;
        }

        private async Task<EnumerationResult> S3Enumerate(string prefix, string continuationToken, CancellationToken token)
        { 
            ListObjectsRequest req = new ListObjectsRequest();
            req.BucketName = _AwsSettings.Bucket;
            if (!String.IsNullOrEmpty(prefix)) req.Prefix = prefix;

            if (!String.IsNullOrEmpty(continuationToken)) req.Marker = continuationToken;

            ListObjectsResponse resp = await _S3Client.ListObjectsAsync(req, token).ConfigureAwait(false);
            EnumerationResult ret = new EnumerationResult();

            if (resp.S3Objects != null && resp.S3Objects.Count > 0)
            {
                foreach (S3Object curr in resp.S3Objects)
                {
                    BlobMetadata md = new BlobMetadata();
                    md.Key = curr.Key;
                    md.ContentLength = curr.Size;
                    md.ETag = curr.ETag;
                    md.CreatedUtc = curr.LastModified;

                    if (!String.IsNullOrEmpty(md.ETag))
                    {
                        while (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
                    }

                    ret.Blobs.Add(md);
                }
            }

            if (!String.IsNullOrEmpty(resp.NextMarker)) ret.NextContinuationToken = resp.NextMarker;

            return ret;
        }

        private async Task<EnumerationResult> AzureEnumerate(string prefix, string continuationToken, CancellationToken token)
        { 
            BlobContinuationToken bct = null;
            if (!String.IsNullOrEmpty(continuationToken))
            {
                if (!AzureGetContinuationToken(continuationToken, out bct))
                {
                    throw new IOException("Unable to find continuation token.");
                }
            }

            BlobResultSegment segment = null;
            EnumerationResult ret = new EnumerationResult();

            if (!String.IsNullOrEmpty(prefix))
            {
                segment = await _AzureContainer.ListBlobsSegmentedAsync(prefix, bct).ConfigureAwait(false);
            }
            else
            {
                segment = await _AzureContainer.ListBlobsSegmentedAsync(bct).ConfigureAwait(false);
            }

            if (segment == null || segment.Results == null || segment.Results.Count() < 1) return ret;

            foreach (IListBlobItem item in segment.Results)
            {
                if (item.GetType() == typeof(CloudBlockBlob))
                {
                    CloudBlockBlob blob = (CloudBlockBlob)item;
                    BlobMetadata md = new BlobMetadata();
                    md.Key = blob.Name;
                    md.ETag = blob.Properties.ETag;
                    md.ContentType = blob.Properties.ContentType;
                    md.ContentLength = blob.Properties.Length;
                    md.CreatedUtc = blob.Properties.Created.Value.DateTime;

                    if (!String.IsNullOrEmpty(md.ETag))
                    {
                        while (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
                    }

                    ret.Blobs.Add(md);
                }
            }

            if (segment.ContinuationToken != null)
            {
                ret.NextContinuationToken = Guid.NewGuid().ToString();
                AzureStoreContinuationToken(ret.NextContinuationToken, segment.ContinuationToken);
            }

            if (!String.IsNullOrEmpty(continuationToken)) AzureRemoveContinuationToken(continuationToken);

            return ret;
        }

        private async Task<EnumerationResult> KomodoEnumerate(string prefix, string continuationToken, CancellationToken token)
        {
            int startIndex = 0;
            int count = 1000;
            if (!String.IsNullOrEmpty(continuationToken))
            {
                if (!KomodoParseContinuationToken(continuationToken, out startIndex, out count))
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

        #endregion

        #region Continuation-Tokens

        private bool KvpbaseParseContinuationToken(string continuationToken, out int start, out int count)
        {
            start = -1;
            count = -1;
            if (String.IsNullOrEmpty(continuationToken)) return false;
            byte[] encoded = Convert.FromBase64String(continuationToken);
            string encodedStr = Encoding.UTF8.GetString(encoded);
            string[] parts = encodedStr.Split(' ');
            if (parts.Length != 2) return false;

            if (!Int32.TryParse(parts[0], out start)) return false;
            if (!Int32.TryParse(parts[1], out count)) return false;
            return true;
        }

        private string KvpbaseBuildContinuationToken(long start, int count)
        {
            string ret = start.ToString() + " " + count.ToString();
            byte[] retBytes = Encoding.UTF8.GetBytes(ret);
            return Convert.ToBase64String(retBytes);
        }

        private bool KomodoParseContinuationToken(string continuationToken, out int start, out int count)
        {
            return KvpbaseParseContinuationToken(continuationToken, out start, out count);
        }

        private string KomodoBuildContinuationToken(long start, int count)
        {
            if (start >= count) return null;
            return KvpbaseBuildContinuationToken(start, count);
        }

        private bool DiskParseContinuationToken(string continuationToken, out int start, out int count)
        {
            start = -1;
            count = -1;
            if (String.IsNullOrEmpty(continuationToken)) return false;
            byte[] encoded = Convert.FromBase64String(continuationToken);
            string encodedStr = Encoding.UTF8.GetString(encoded);
            string[] parts = encodedStr.Split(' ');
            if (parts.Length != 2) return false;

            if (!Int32.TryParse(parts[0], out start)) return false;
            if (!Int32.TryParse(parts[1], out count)) return false;
            return true;
        }

        private string DiskBuildContinuationToken(int start, int count)
        {
            if (start >= count) return null;
            string ret = start.ToString() + " " + count.ToString();
            byte[] retBytes = Encoding.UTF8.GetBytes(ret);
            return Convert.ToBase64String(retBytes);
        }

        private void AzureStoreContinuationToken(string guid, BlobContinuationToken token)
        {
            _AzureContinuationTokens.TryAdd(guid, token);
        }

        private bool AzureGetContinuationToken(string guid, out BlobContinuationToken token)
        {
            return _AzureContinuationTokens.TryGetValue(guid, out token);
        }

        private void AzureRemoveContinuationToken(string guid)
        {
            BlobContinuationToken token = null;
            _AzureContinuationTokens.TryRemove(guid, out token);
        }

        #endregion

        #region URL

        private string KvpbaseGenerateUrl(string key)
        {
            if (!_KvpbaseSettings.Endpoint.EndsWith("/")) _KvpbaseSettings.Endpoint += "/";

            string ret =
                _KvpbaseSettings.Endpoint +
                _KvpbaseSettings.UserGuid + "/" +
                _KvpbaseSettings.Container + "/" +
                key;

            return ret;
        }

        private string DiskGenerateUrl(string key)
        {
            string dir = _DiskSettings.Directory;
            dir = dir.Replace("\\", "/");
            while (dir.StartsWith("/")) dir = dir.Substring(1);
            while (dir.EndsWith("/")) dir = dir.Substring(0, dir.Length - 1);
            return dir + "/" + key;
        }

        private string S3GenerateUrl(string bucket, string key)
        {
            if (!String.IsNullOrEmpty(_AwsSettings.BaseUrl))
            {
                string url = _AwsSettings.BaseUrl;
                url = url.Replace("{bucket}", bucket);
                url = url.Replace("{key}", key);
                return url;
            }
            else
            {
                string ret = "";

                // https://[bucketname].s3.[regionname].amazonaws.com/
                if (_AwsSettings.Ssl) ret = "https://"; 
                else ret = "http://"; 

                ret += bucket + ".s3." + S3RegionToString(_AwsSettings.Region) + ".amazonaws.com/" + key;

                return ret;
            }
        }

        private string AzureGenerateUrl(string key)
        {
            return "https://" +
                _AzureSettings.AccountName +
                ".blob.core.windows.net/" +
                _AzureSettings.Container +
                "/" +
                key;
        }

        private string S3RegionToString(AwsRegion region)
        {
            switch (region)
            {
                case AwsRegion.APNortheast1:
                    return "ap-northeast-1";
                case AwsRegion.APNortheast2:
                    return "ap-northeast-2";
                case AwsRegion.APNortheast3:
                    return "ap-northeast-3";
                case AwsRegion.APSouth1:
                    return "ap-south-1";
                case AwsRegion.APSoutheast1:
                    return "ap-southeast-1";
                case AwsRegion.APSoutheast2:
                    return "ap-southeast-2";
                case AwsRegion.CACentral1:
                    return "ca-central-1";
                case AwsRegion.CNNorth1:
                    return "cn-north-1";
                case AwsRegion.CNNorthwest1:
                    return "cn-northwest-1";
                case AwsRegion.EUCentral1:
                    return "eu-central-1";
                case AwsRegion.EUNorth1:
                    return "eu-north-1";
                case AwsRegion.EUWest1:
                    return "eu-west-1";
                case AwsRegion.EUWest2:
                    return "eu-west-2";
                case AwsRegion.EUWest3:
                    return "eu-west-3";
                case AwsRegion.SAEast1:
                    return "sa-east-1";
                case AwsRegion.USEast1:
                    return "us-east-1";
                case AwsRegion.USEast2:
                    return "us-east-2";
                case AwsRegion.USGovCloudEast1:
                    return "us-gov-east-1";
                case AwsRegion.USGovCloudWest1:
                    return "us-gov-west-1";
                case AwsRegion.USWest1:
                    return "us-west-1";
                case AwsRegion.USWest2:
                    return "us-west-2";
                default:
                    throw new ArgumentException("Unknown region: " + region.ToString());
            } 
        }

        private string KomodoGenerateUrl(string key)
        {
            if (!_KomodoSettings.Endpoint.EndsWith("/")) _KomodoSettings.Endpoint += "/";

            string ret =
                _KomodoSettings.Endpoint +
                _KomodoSettings.IndexGUID + "/" +
                key;

            return ret;
        }

        #endregion

        #endregion
    }
}
