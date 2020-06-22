using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks; 
using KvpbaseSDK;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Amazon.S3;
using Amazon.S3.Model; 

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

        private StorageType _StorageType;
        private AwsSettings _AwsSettings;
        private AzureSettings _AzureSettings;
        private DiskSettings _DiskSettings;
        private KvpbaseSettings _KvpbaseSettings;

        private AmazonS3Config _S3Config;
        private IAmazonS3 _S3Client;
        private Amazon.Runtime.BasicAWSCredentials _S3Credentials;
        private Amazon.RegionEndpoint _S3Region;

        private StorageCredentials _AzureCredentials;
        private CloudStorageAccount _AzureAccount;
        private CloudBlobClient _AzureBlobClient;
        private CloudBlobContainer _AzureContainer;

        private KvpbaseClient _Kvpbase;

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

        #endregion

        #region Public-Methods

        /// <summary>
        /// Delete a BLOB by its key.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Delete(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            bool success = false;

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    success = await S3Delete(key);
                    break;
                case StorageType.Azure:
                    success = await AzureDelete(key);
                    break;
                case StorageType.Disk:
                    success = await DiskDelete(key);
                    break;
                case StorageType.Kvpbase:
                    success = await KvpbaseDelete(key);
                    break;
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString()); 
            }

            return success;
        }

        /// <summary>
        /// Retrieve a BLOB.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param> 
        /// <returns>Byte data of the BLOB.</returns>
        public async Task<byte[]> Get(string key)
        { 
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return await S3Get(key);
                case StorageType.Azure:
                    return await AzureGet(key);
                case StorageType.Disk:
                    return await DiskGet(key);
                case StorageType.Kvpbase:
                    return await KvpbaseGet(key);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }
        }

        /// <summary>
        /// Retrieve a BLOB.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param> 
        /// <returns>BLOB data.</returns>
        public async Task<BlobData> GetStream(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return await S3GetStream(key);
                case StorageType.Azure:
                    return await AzureGetStream(key);
                case StorageType.Disk:
                    return await DiskGetStream(key);
                case StorageType.Kvpbase:
                    return await KvpbaseGetStream(key);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }
        }

        /// <summary>
        /// Writea  BLOB using a string.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="contentType">Content-type of the object.</param>
        /// <param name="data">BLOB data.</param>
        /// <returns></returns>
        public async Task Write(string key, string contentType, string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            await Write(key, contentType, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Write a BLOB using a byte array.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="contentType">Content-type of the object.</param>
        /// <param name="data">BLOB data.</param> 
        public async Task Write(
            string key,
            string contentType,
            byte[] data)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key)); 

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    await S3Write(key, contentType, data);
                    return;
                case StorageType.Azure:
                    await AzureWrite(key, contentType, data);
                    return;
                case StorageType.Disk:
                    await DiskWrite(key, data);
                    return;
                case StorageType.Kvpbase:
                    await KvpbaseWrite(key, contentType, data);
                    return;
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            } 
        }

        /// <summary>
        /// Write a BLOB using a stream.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="contentType">Content type.</param>
        /// <param name="contentLength">Content length.</param>
        /// <param name="stream">Stream containing the data.</param> 
        public async Task Write(
            string key,
            string contentType,
            long contentLength,
            Stream stream)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    await S3Write(key, contentType, contentLength, stream);
                    return;
                case StorageType.Azure:
                    await AzureWrite(key, contentType, contentLength, stream);
                    return;
                case StorageType.Disk:
                    await DiskWrite(key, contentLength, stream);
                    return;
                case StorageType.Kvpbase:
                    await KvpbaseWrite(key, contentType, contentLength, stream);
                    return;
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }
        }

        /// <summary>
        /// Check if a BLOB exists.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <returns>True if exists.</returns>
        public async Task<bool> Exists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return await S3Exists(key);
                case StorageType.Azure:
                    return await AzureExists(key);
                case StorageType.Disk:
                    return await DiskExists(key);
                case StorageType.Kvpbase:
                    return await KvpbaseExists(key);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }
        }

        /// <summary>
        /// Generate a URL for a given object key.
        /// </summary>
        /// <param name="key">Object key.</param>
        /// <returns>URL.</returns>
        public string GenerateUrl(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return S3GenerateUrl(key);
                case StorageType.Azure:
                    return AzureGenerateUrl(key);
                case StorageType.Disk:
                    return DiskGenerateUrl(key);
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
        /// <returns>BLOB metadata.</returns>
        public async Task<BlobMetadata> GetMetadata(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return await S3GetMetadata(key);
                case StorageType.Azure:
                    return await AzureGetMetadata(key);
                case StorageType.Disk:
                    return await DiskGetMetadata(key);
                case StorageType.Kvpbase:
                    return await KvpbaseGetMetadata(key);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            } 
        }

        /// <summary>
        /// Enumerate BLOBs.
        /// </summary> 
        /// <returns>Enumeration result.</returns>
        public async Task<EnumerationResult> Enumerate()
        {
            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return await S3Enumerate(null, null);
                case StorageType.Azure:
                    return await AzureEnumerate(null, null);
                case StorageType.Disk:
                    return await DiskEnumerate(null, null);
                case StorageType.Kvpbase:
                    return await KvpbaseEnumerate(null, null);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }
        }

        /// <summary>
        /// Enumerate BLOBs.
        /// </summary> 
        /// <param name="continuationToken">Continuation token to use if issuing a subsequent enumeration request.</param> 
        /// <returns>Enumeration result.</returns>
        public async Task<EnumerationResult> Enumerate(string continuationToken)
        { 
            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return await S3Enumerate(null, continuationToken);
                case StorageType.Azure:
                    return await AzureEnumerate(null, continuationToken);
                case StorageType.Disk:
                    return await DiskEnumerate(null, continuationToken);
                case StorageType.Kvpbase:
                    return await KvpbaseEnumerate(null, continuationToken);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }
        }

        /// <summary>
        /// Enumerate BLOBs.
        /// </summary>
        /// <param name="prefix">Key prefix that must match.</param>
        /// <param name="continuationToken">Continuation token to use if issuing a subsequent enumeration request.</param> 
        /// <returns>Enumeration result.</returns>
        public async Task<EnumerationResult> Enumerate(string prefix, string continuationToken)
        { 
            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return await S3Enumerate(prefix, continuationToken);
                case StorageType.Azure:
                    return await AzureEnumerate(prefix, continuationToken);
                case StorageType.Disk:
                    return await DiskEnumerate(prefix, continuationToken);
                case StorageType.Kvpbase:
                    return await KvpbaseEnumerate(prefix, continuationToken);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }
        }

        #endregion

        #region Private-Methods

        private void InitializeClients()
        {
            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    _S3Region = _AwsSettings.GetAwsRegionEndpoint();
                    _S3Credentials = new Amazon.Runtime.BasicAWSCredentials(_AwsSettings.AccessKey, _AwsSettings.SecretKey);

                    if (String.IsNullOrEmpty(_AwsSettings.Endpoint))
                    { 
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
                            RegionEndpoint = _S3Region,
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
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }    
        }

        #region Delete

        private async Task<bool> KvpbaseDelete(string key)
        {
            try
            {
                await _Kvpbase.DeleteObject(_KvpbaseSettings.Container, key);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> DiskDelete(string key)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                File.Delete(DiskGenerateUrl(key));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> S3Delete(string key)
        {
            try
            {
                DeleteObjectRequest request = new DeleteObjectRequest
                {
                    BucketName = _AwsSettings.Bucket,
                    Key = key
                };

                DeleteObjectResponse response = await _S3Client.DeleteObjectAsync(request);
                int statusCode = (int)response.HttpStatusCode;

                if (response != null) return true;
                else return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> AzureDelete(string key)
        {
            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(key);
                OperationContext ctx = new OperationContext();
                await blockBlob.DeleteAsync(DeleteSnapshotsOption.None, null, null, ctx);
                int statusCode = ctx.LastResult.HttpStatusCode;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Get

        private async Task<byte[]> KvpbaseGet(string key)
        {
            try
            {
                KvpbaseObject kvpObject = await _Kvpbase.ReadObject(_KvpbaseSettings.Container, key);
                return Common.StreamToBytes(kvpObject.Data);
            }
            catch
            {
                throw new IOException("Unable to read object.");
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<byte[]> DiskGet(string key)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                return File.ReadAllBytes(DiskGenerateUrl(key));
            }
            catch (Exception)
            {
                throw new IOException("Unable to read object.");
            }
        }

        private async Task<byte[]> S3Get(string key)
        {
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = _AwsSettings.Bucket,
                    Key = key,
                };

                using (GetObjectResponse response = await _S3Client.GetObjectAsync(request))
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
            catch (Exception)
            {
                throw new IOException("Unable to read object.");
            }
        }

        private async Task<byte[]> AzureGet(string key)
        {
            byte[] data = null;

            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(key);
                OperationContext ctx = new OperationContext();

                MemoryStream stream = new MemoryStream();
                await blockBlob.DownloadToStreamAsync(stream);
                stream.Seek(0, SeekOrigin.Begin);
                data = Common.StreamToBytes(stream);
                return data;
            }
            catch (Exception)
            {
                throw new IOException("Unable to read object.");
            }
        }

        #endregion

        #region Get-Stream

        private async Task<BlobData> KvpbaseGetStream(string key)
        {
            try
            {
                KvpbaseObject kvpObj = await _Kvpbase.ReadObject(_KvpbaseSettings.Container, key);
                return new BlobData(kvpObj.ContentLength, kvpObj.Data);
            }
            catch (Exception)
            {
                throw new IOException("Unable to read object.");
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<BlobData> DiskGetStream(string key)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                string url = DiskGenerateUrl(key);
                long contentLength = new FileInfo(url).Length;
                FileStream stream = new FileStream(url, FileMode.Open);
                return new BlobData(contentLength, stream);
            }
            catch (Exception)
            {
                throw new IOException("Unable to read object.");
            }
        }

        private async Task<BlobData> S3GetStream(string key)
        {
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = _AwsSettings.Bucket,
                    Key = key,
                };

                GetObjectResponse response = await _S3Client.GetObjectAsync(request);
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
            catch (Exception)
            {
                throw new IOException("Unable to read object.");
            }
        }
         
        private async Task<BlobData> AzureGetStream(string key) 
        { 
            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(key);
                blockBlob.FetchAttributesAsync().Wait();

                BlobData ret = new BlobData();
                ret.ContentLength = blockBlob.Properties.Length;

                MemoryStream stream = new MemoryStream();
                await blockBlob.DownloadToStreamAsync(stream);

                ret.Data = stream;
                stream.Seek(0, SeekOrigin.Begin);
                return ret;
            }
            catch (Exception)
            {
                throw new IOException("Unable to read object.");
            }
        }

        #endregion

        #region Exists

        private async Task<bool> KvpbaseExists(string key)
        {
            return await _Kvpbase.ObjectExists(_KvpbaseSettings.Container, key);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> DiskExists(string key)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                return File.Exists(DiskGenerateUrl(key));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> S3Exists(string key)
        {
            try
            {
                GetObjectMetadataRequest request = new GetObjectMetadataRequest
                {
                    BucketName = _AwsSettings.Bucket,
                    Key = key
                };

                GetObjectMetadataResponse response = await _S3Client.GetObjectMetadataAsync(request);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> AzureExists(string key)
        {
            try
            {
                return await _AzureBlobClient.GetContainerReference(_AzureSettings.Container).GetBlockBlobReference(key).ExistsAsync();
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Write

        private async Task KvpbaseWrite(string key, string contentType, byte[] data)
        {
            long contentLength = 0;
            MemoryStream stream = new MemoryStream(new byte[0]);

            if (data != null && data.Length > 0)
            {
                contentLength = data.Length;
                stream = new MemoryStream(data);
                stream.Seek(0, SeekOrigin.Begin);
            }

            await KvpbaseWrite(key, contentType, contentLength, stream);
        }

        private async Task KvpbaseWrite(string key, string contentType, long contentLength, Stream stream)
        {
            try
            {
                await _Kvpbase.WriteObject(_KvpbaseSettings.Container, key, contentType, contentLength, stream); 
            }
            catch (Exception)
            {
                throw new IOException("Unable to write object.");
            }
        }

        private async Task DiskWrite(string key, byte[] data)
        {
            long contentLength = 0;
            MemoryStream stream = new MemoryStream(new byte[0]);

            if (data != null && data.Length > 0)
            {
                contentLength = data.Length;
                stream = new MemoryStream(data);
                stream.Seek(0, SeekOrigin.Begin);
            }

            await DiskWrite(key, contentLength, stream);
        }

        private async Task DiskWrite(string key, long contentLength, Stream stream)
        {
            try
            {
                int bytesRead = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[65536];
                string url = DiskGenerateUrl(key);

                using (FileStream fs = new FileStream(url, FileMode.OpenOrCreate))
                {
                    while (bytesRemaining > 0)
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            await fs.WriteAsync(buffer, 0, bytesRead);
                            bytesRemaining -= bytesRead;
                        }
                    }
                } 
            }
            catch (Exception)
            {
                throw new IOException("Unable to write object.");
            }
        }

        private async Task S3Write(string key, string contentType, byte[] data)
        {
            long contentLength = 0;
            MemoryStream stream = new MemoryStream(new byte[0]);

            if (data != null && data.Length > 0)
            {
                contentLength = data.Length;
                stream = new MemoryStream(data);
                stream.Seek(0, SeekOrigin.Begin);
            }

            await S3Write(key, contentType, contentLength, stream);
        }

        private async Task S3Write(string key, string contentType, long contentLength, Stream stream)
        {
            try
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

                PutObjectResponse response = await _S3Client.PutObjectAsync(request);
                int statusCode = (int)response.HttpStatusCode; 
            }
            catch (Exception)
            {
                throw new IOException("Unable to write object.");
            }
        }
         
        private async Task AzureWrite(string key, string contentType, byte[] data)
        {
            long contentLength = 0;
            MemoryStream stream = new MemoryStream(new byte[0]);

            if (data != null && data.Length > 0)
            {
                contentLength = data.Length;
                stream = new MemoryStream(data);
                stream.Seek(0, SeekOrigin.Begin);
            }

            await AzureWrite(key, contentType, contentLength, stream);
        }

        private async Task AzureWrite(string key, string contentType, long contentLength, Stream stream)
        {
            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(key);
                blockBlob.Properties.ContentType = contentType;
                OperationContext ctx = new OperationContext();
                await blockBlob.UploadFromStreamAsync(stream, contentLength); 
            }
            catch (Exception)
            {
                throw new IOException("Unable to write object.");
            }
        }

        #endregion

        #region Get-Metadata

        private async Task<BlobMetadata> KvpbaseGetMetadata(string key)
        {
            try
            {
                ObjectMetadata objMd = await _Kvpbase.ReadObjectMetadata(_KvpbaseSettings.Container, key);
                BlobMetadata md = new BlobMetadata();
                md.Key = objMd.ObjectKey;
                md.ContentLength = Convert.ToInt64(objMd.ContentLength);
                md.ContentType = objMd.ContentType;
                md.ETag = objMd.Md5;
                md.Created = objMd.CreatedUtc.Value;
                return md;
            }
            catch (Exception)
            {
                throw new IOException("Unable to read object.");
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<BlobMetadata> DiskGetMetadata(string key)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                string url = DiskGenerateUrl(key);

                FileInfo fi = new FileInfo(url);
                BlobMetadata md = new BlobMetadata();
                md.Key = key;
                md.ContentLength = fi.Length;
                md.Created = fi.CreationTimeUtc;

                return md;
            }
            catch (Exception)
            {
                throw new IOException("Unable to read object.");
            }
        }
         
        private async Task<BlobMetadata> S3GetMetadata(string key) 
        {
            try
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
                    md.Created = response.LastModified;

                    if (!String.IsNullOrEmpty(md.ETag))
                    {
                        while (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
                    }

                    return md;
                }
                else
                {
                    throw new IOException("Unable to read object.");
                }
            }
            catch (Exception)
            {
                throw new IOException("Unable to read object.");
            }
        }
         
        private async Task<BlobMetadata> AzureGetMetadata(string key) 
        { 
            try
            {
                CloudBlobContainer container = _AzureBlobClient.GetContainerReference(_AzureSettings.Container);
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(key);
                await blockBlob.FetchAttributesAsync();

                BlobMetadata md = new BlobMetadata();
                md.Key = key;
                md.ContentLength = blockBlob.Properties.Length;
                md.ContentType = blockBlob.Properties.ContentType;
                md.ETag = blockBlob.Properties.ETag;
                md.Created = blockBlob.Properties.Created.Value.UtcDateTime;

                if (!String.IsNullOrEmpty(md.ETag))
                {
                    while (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
                }

                return md;
            }
            catch (Exception)
            {
                throw new IOException("Unable to read object.");
            } 
        }

        #endregion

        #region Enumeration

        private async Task<EnumerationResult> KvpbaseEnumerate(string prefix, string continuationToken)
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

            try
            {
                if (String.IsNullOrEmpty(prefix))
                {
                    cmd = await _Kvpbase.EnumerateContainer(_KvpbaseSettings.Container, startIndex, count);
                }
                else
                {
                    EnumerationFilter filter = new EnumerationFilter();
                    filter.Prefix = prefix;
                    cmd = await _Kvpbase.EnumerateContainer(filter, _KvpbaseSettings.Container, startIndex, count);
                }
            }
            catch (Exception)
            {
                throw new IOException("Unable to enumerate objects.");
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
                    md.Created = curr.CreatedUtc.Value;
                    ret.Blobs.Add(md);
                }
            }

            return ret;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<EnumerationResult> DiskEnumerate(string prefix, string continuationToken)
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
                files = Directory.EnumerateDirectories(_DiskSettings.Directory, prefix + "*", SearchOption.TopDirectoryOnly);
            }
            else
            {
                files = Directory.EnumerateFiles(_DiskSettings.Directory, "*", SearchOption.TopDirectoryOnly);
            }

            files = files.Skip(startIndex).Take(count);

            EnumerationResult ret = new EnumerationResult();
            if (files.Count() < 1) return ret;

            ret.NextContinuationToken = DiskBuildContinuationToken(startIndex + count, count);

            foreach (string file in files)
            {
                string key = Path.GetFileName(file);
                FileInfo fi = new FileInfo(file);

                BlobMetadata md = new BlobMetadata();
                md.Key = key;
                md.ContentLength = fi.Length;
                md.Created = fi.CreationTimeUtc;
                ret.Blobs.Add(md);

                currCount++;
                continue;
            }

            return ret;
        }

        private async Task<EnumerationResult> S3Enumerate(string prefix, string continuationToken)
        { 
            ListObjectsRequest req = new ListObjectsRequest();
            req.BucketName = _AwsSettings.Bucket;
            if (!String.IsNullOrEmpty(prefix)) req.Prefix = prefix;

            if (!String.IsNullOrEmpty(continuationToken)) req.Marker = continuationToken;

            ListObjectsResponse resp = await _S3Client.ListObjectsAsync(req);
            EnumerationResult ret = new EnumerationResult();

            if (resp.S3Objects != null && resp.S3Objects.Count > 0)
            {
                foreach (S3Object curr in resp.S3Objects)
                {
                    BlobMetadata md = new BlobMetadata();
                    md.Key = curr.Key;
                    md.ContentLength = curr.Size;
                    md.ETag = curr.ETag;
                    md.Created = curr.LastModified;

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

        private async Task<EnumerationResult> AzureEnumerate(string prefix, string continuationToken)
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
                segment = await _AzureContainer.ListBlobsSegmentedAsync(prefix, bct);
            }
            else
            {
                segment = await _AzureContainer.ListBlobsSegmentedAsync(bct);
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
                    md.Created = blob.Properties.Created.Value.DateTime;

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
            string dir = String.Copy(_DiskSettings.Directory);
            while (dir.EndsWith("\\")) dir = dir.Substring(0, dir.Length - 1);
            while (dir.EndsWith("/")) dir = dir.Substring(0, dir.Length - 1);
            dir = dir.Replace("\\", "/");
            return dir + "/" + key;
        }

        private string S3GenerateUrl(string key)
        {
            if (!String.IsNullOrEmpty(_AwsSettings.BaseUrl)) return _AwsSettings.BaseUrl + key;
            else
            {
                string ret = "";

                // https://[bucketname].s3.[regionname].amazonaws.com/
                if (_AwsSettings.Ssl)
                {
                    ret = "https://";
                }
                else
                {
                    ret = "http://";
                }

                ret += _AwsSettings.Bucket + ".s3." + S3RegionToString(_AwsSettings.Region) + ".amazonaws.com/" + key;

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

        #endregion

        #endregion
    }
}
