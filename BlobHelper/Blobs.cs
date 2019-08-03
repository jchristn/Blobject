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
        /// <param name="data">Byte array containing BLOB data.</param>
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
        /// <param name="contentLength">Content length.</param>
        /// <param name="stream">Stream.</param>
        /// <returns>True if successful.</returns>
        public bool Get(string key, out long contentLength, out Stream stream)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return S3Get(key, out contentLength, out stream);
                case StorageType.Azure:
                    return AzureGet(key, out contentLength, out stream);
                case StorageType.Disk:
                    return DiskGet(key, out contentLength, out stream);
                case StorageType.Kvpbase:
                    return KvpbaseGet(key, out contentLength, out stream);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }
        }

        /// <summary>
        /// Write a BLOB using a byte array.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="base64">True of the supplied data is a string containing Base64-encoded data.</param>
        /// <param name="data">BLOB data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Write(
            string key,
            string contentType,
            byte[] data)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (_StorageType != StorageType.Disk && String.IsNullOrEmpty(contentType)) throw new ArgumentNullException(nameof(contentType));

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return await S3Write(key, contentType, data);
                case StorageType.Azure:
                    return await AzureWrite(key, contentType, data);
                case StorageType.Disk:
                    return await DiskWrite(key, data);
                case StorageType.Kvpbase:
                    return await KvpbaseWrite(key, contentType, data);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            } 
        }

        /// <summary>
        /// Write a BLOB.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="contentType">Content type.</param>
        /// <param name="contentLength">Content length.</param>
        /// <param name="stream">Stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public bool Write(
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
                    return S3Write(key, contentType, contentLength, stream);
                case StorageType.Azure:
                    return AzureWrite(key, contentType, contentLength, stream);
                case StorageType.Disk:
                    return DiskWrite(key, contentLength, stream);
                case StorageType.Kvpbase:
                    return KvpbaseWrite(key, contentType, contentLength, stream);
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
        /// Retrieve BLOB metadata.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="md">Metadata.</param>
        /// <returns>True if successful.</returns>
        public bool GetMetadata(string key, out BlobMetadata md)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return S3GetMetadata(key, out md);
                case StorageType.Azure:
                    return AzureGetMetadata(key, out md);
                case StorageType.Disk:
                    return DiskGetMetadata(key, out md);
                case StorageType.Kvpbase:
                    return KvpbaseGetMetadata(key, out md);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            } 
        }

        /// <summary>
        /// Enumerate BLOBs.
        /// </summary>
        /// <param name="prefix">Key prefix that must match.</param>
        /// <param name="continuationToken">Continuation token to use for subsequent enumeration requests.</param>
        /// <param name="nextContinuationToken">Next continuation token to supply should you want to continue enumerating from the end of the current response.</param>
        /// <param name="blobs">List of BLOB metadata.</param>
        /// <returns>True if successful.</returns>
        public bool Enumerate(string continuationToken, out string nextContinuationToken, out List<BlobMetadata> blobs)
        {
            blobs = new List<BlobMetadata>();

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return S3Enumerate(continuationToken, out nextContinuationToken, out blobs);
                case StorageType.Azure:
                    return AzureEnumerate(continuationToken, out nextContinuationToken, out blobs);
                case StorageType.Disk:
                    return DiskEnumerate(continuationToken, out nextContinuationToken, out blobs);
                case StorageType.Kvpbase:
                    return KvpbaseEnumerate(continuationToken, out nextContinuationToken, out blobs);
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
                    _S3Region = _AwsSettings.GetAwsRegion();
                    _S3Credentials = new Amazon.Runtime.BasicAWSCredentials(_AwsSettings.AccessKey, _AwsSettings.SecretKey);

                    if (String.IsNullOrEmpty(_AwsSettings.Hostname))
                    {
                        _S3Config = null;
                        _S3Client = new AmazonS3Client(_S3Credentials, _S3Region);
                    }
                    else
                    {
                        _S3Config = new AmazonS3Config
                        {
                            RegionEndpoint = _S3Region,
                            ServiceURL = _AwsSettings.Hostname,
                            ForcePathStyle = true,
                            UseHttp = _AwsSettings.Ssl
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

        #region Private-Kvpbase-Methods

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> KvpbaseDelete(string key)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return _Kvpbase.DeleteObject(_KvpbaseSettings.Container, key);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<byte[]> KvpbaseGet(string key)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            byte[] data = null;
            if (_Kvpbase.ReadObject(_KvpbaseSettings.Container, key, out data)) return data;
            else throw new IOException("Unable to read object.");
        }

        private bool KvpbaseGet(string key, out long contentLength, out Stream stream)
        {
            contentLength = 0;
            stream = null;
            if (_Kvpbase.ReadObject(_KvpbaseSettings.Container, key, out contentLength, out stream)) return true;
            else throw new IOException("Unable to read object.");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> KvpbaseExists(string key)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return _Kvpbase.ObjectExists(_KvpbaseSettings.Container, key); 
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> KvpbaseWrite(string key, string contentType, byte[] data)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return _Kvpbase.WriteObject(_KvpbaseSettings.Container, key, contentType, data);
        }

        private bool KvpbaseWrite(string key, string contentType, long contentLength, Stream stream)
        {
            return _Kvpbase.WriteObject(_KvpbaseSettings.Container, key, contentType, contentLength, stream);
        }

        private bool KvpbaseGetMetadata(string key, out BlobMetadata md)
        {
            md = new BlobMetadata();
            md.Key = key;

            ObjectMetadata objMd = null;
            if (_Kvpbase.GetObjectMetadata(_KvpbaseSettings.Container, key, out objMd))
            {
                md.ContentLength = Convert.ToInt64(objMd.ContentLength);
                md.ContentType = objMd.ContentType;
                md.ETag = objMd.Md5;
                md.Created = objMd.CreatedUtc.Value;
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool KvpbaseEnumerate(string continuationToken, out string nextContinuationToken, out List<BlobMetadata> blobs)
        {
            blobs = new List<BlobMetadata>();
            nextContinuationToken = null;

            int startIndex = 0;
            int count = 1000;
            if (!String.IsNullOrEmpty(continuationToken))
            {
                if (!KvpbaseParseContinuationToken(continuationToken, out startIndex, out count))
                {
                    return false;
                }
            }

            ContainerMetadata cmd = null;
            if (!_Kvpbase.EnumerateContainer(_KvpbaseSettings.Container, startIndex, count, out cmd))
            {
                return false;
            }

            if (cmd.Objects != null && cmd.Objects.Count > 0)
            {
                foreach (ObjectMetadata curr in cmd.Objects)
                {
                    BlobMetadata md = new BlobMetadata();
                    md.Key = curr.Key;
                    md.ETag = curr.Md5;
                    md.ContentLength = Convert.ToInt64(curr.ContentLength);
                    md.ContentType = curr.ContentType;
                    md.Created = curr.CreatedUtc.Value;
                    blobs.Add(md);
                }
            }

            return true;
        }

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

        #endregion

        #region Private-Disk-Methods

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
         
        private bool DiskGet(string key, out long contentLength, out Stream stream) 
        {
            contentLength = 0;
            stream = null;

            try
            {
                string url = DiskGenerateUrl(key);
                contentLength = new FileInfo(url).Length;
                stream = new FileStream(url, FileMode.Open);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> DiskWrite(string key, byte[] data)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                File.WriteAllBytes(DiskGenerateUrl(key), data);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
         
        private bool DiskWrite(string key, long contentLength, Stream stream) 
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
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            fs.Write(buffer, 0, bytesRead);
                            bytesRemaining -= bytesRead;
                        }
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool DiskGetMetadata(string key, out BlobMetadata md)
        {
            string url = DiskGenerateUrl(key);

            FileInfo fi = new FileInfo(url);
            md = new BlobMetadata();
            md.Key = key;
            md.ContentLength = fi.Length;
            md.Created = fi.CreationTimeUtc;

            return true;
        }

        private bool DiskEnumerate(string continuationToken, out string nextContinuationToken, out List<BlobMetadata> blobs)
        {
            nextContinuationToken = null;
            blobs = new List<BlobMetadata>(); 

            int startIndex = 0;
            int count = 1000;

            if (!String.IsNullOrEmpty(continuationToken))
            {
                if (!DiskParseContinuationToken(continuationToken, out startIndex, out count))
                {
                    return false;
                }
            }

            long maxIndex = startIndex + count;

            long currCount = 0;
            IEnumerable<string> files = Directory.EnumerateFiles(_DiskSettings.Directory, "*", SearchOption.TopDirectoryOnly);
            files = files.Skip(startIndex).Take(count);

            if (files.Count() < 1) return true;

            nextContinuationToken = DiskBuildContinuationToken(startIndex + count, count);

            foreach (string file in files)
            { 
                string key = Path.GetFileName(file); 
                FileInfo fi = new FileInfo(file);

                BlobMetadata md = new BlobMetadata();
                md.Key = key;
                md.ContentLength = fi.Length;
                md.Created = fi.CreationTimeUtc;
                blobs.Add(md);

                currCount++;
                continue; 
            }

            return true;
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

        private string DiskGenerateUrl(string key)
        {
            return _DiskSettings.Directory + "/" + key;
        }

        #endregion

        #region Private-S3-Methods

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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<byte[]> S3Get(string key)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
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

        private bool S3Get(string key, out long contentLength, out Stream stream)
        {
            contentLength = 0;
            stream = null;

            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = _AwsSettings.Bucket,
                    Key = key,
                };

                GetObjectResponse response = _S3Client.GetObjectAsync(request).Result;

                if (response.ContentLength > 0)
                {
                    contentLength = response.ContentLength;
                    stream = response.ResponseStream;
                    return true;
                }
                else
                {
                    contentLength = 0;
                    stream = null;
                    return false;
                }
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

        private async Task<bool> S3Write(string key, string contentType, byte[] data)
        {
            try
            {
                Stream s = new MemoryStream(data);

                PutObjectRequest request = new PutObjectRequest
                {
                    BucketName = _AwsSettings.Bucket,
                    Key = key,
                    InputStream = s,
                    ContentType = contentType,
                    UseChunkEncoding = false
                };

                PutObjectResponse response = await _S3Client.PutObjectAsync(request);
                int statusCode = (int)response.HttpStatusCode;

                if (response != null) return true;
                else return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool S3Write(string key, string contentType, long contentLength, Stream stream)
        {
            try
            {
                PutObjectRequest request = new PutObjectRequest
                {
                    BucketName = _AwsSettings.Bucket,
                    Key = key,
                    InputStream = stream,
                    ContentType = contentType,
                    UseChunkEncoding = false
                };

                PutObjectResponse response = _S3Client.PutObjectAsync(request).Result;
                int statusCode = (int)response.HttpStatusCode;

                if (response != null) return true;
                else return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool S3GetMetadata(string key, out BlobMetadata md)
        {
            md = new BlobMetadata();
            md.Key = key;

            try
            { 
                GetObjectMetadataRequest request = new GetObjectMetadataRequest();
                request.BucketName = _AwsSettings.Bucket;
                request.Key = key;

                GetObjectMetadataResponse response = _S3Client.GetObjectMetadataAsync(request).Result;

                if (response.ContentLength > 0)
                {
                    md.ContentLength = response.ContentLength;
                    md.ContentType = response.Headers.ContentType;
                    md.ETag = response.ETag;
                    md.Created = response.LastModified;
                    
                    if (!String.IsNullOrEmpty(md.ETag))
                    {
                        while (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool S3Enumerate(string continuationToken, out string nextContinuationToken, out List<BlobMetadata> blobs)
        {
            nextContinuationToken = null;
            blobs = new List<BlobMetadata>();

            ListObjectsRequest req = new ListObjectsRequest();
            req.BucketName = _AwsSettings.Bucket;

            if (!String.IsNullOrEmpty(continuationToken)) req.Marker = continuationToken;

            ListObjectsResponse resp = _S3Client.ListObjectsAsync(req).Result;
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

                    blobs.Add(md);
                }
            }

            if (!String.IsNullOrEmpty(resp.NextMarker)) nextContinuationToken = resp.NextMarker;

            return true;
        }

        private string S3GenerateUrl(string key)
        {
            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest();
            request.BucketName = _AwsSettings.Bucket;
            request.Key = key;
            request.Protocol = Protocol.HTTPS;
            request.Expires = DateTime.Now.AddYears(100);
            return _S3Client.GetPreSignedURL(request);
        }

        #endregion

        #region Private-Azure-Methods

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

        private bool AzureGet(string key, out long contentLength, out Stream stream)
        {
            contentLength = 0;
            stream = null;

            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(key);
                blockBlob.FetchAttributesAsync().Wait();
                contentLength = blockBlob.Properties.Length;
                stream = new MemoryStream();
                blockBlob.DownloadToStreamAsync(stream).Wait();

                stream.Seek(0, SeekOrigin.Begin);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> AzureExists(string key)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                return _AzureBlobClient.GetContainerReference(_AzureSettings.Container).GetBlockBlobReference(key).ExistsAsync().Result;
            }
            catch (Exception)
            {
                return false;
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> AzureWrite(string key, string contentType, byte[] data)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(key);
                blockBlob.Properties.ContentType = contentType;
                OperationContext ctx = new OperationContext();
                blockBlob.UploadFromByteArrayAsync(data, 0, data.Length).Wait();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
         
        private bool AzureWrite(string key, string contentType, long contentLength, Stream stream)
        {
            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(key);
                blockBlob.Properties.ContentType = contentType;
                OperationContext ctx = new OperationContext();
                blockBlob.UploadFromStreamAsync(stream, contentLength).Wait();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool AzureGetMetadata(string key, out BlobMetadata md)
        {
            md = new BlobMetadata();
            md.Key = key;

            try
            {
                CloudBlobContainer container = _AzureBlobClient.GetContainerReference(_AzureSettings.Container);
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(key);
                blockBlob.FetchAttributesAsync().Wait();
                md.ContentLength = blockBlob.Properties.Length;
                md.ContentType = blockBlob.Properties.ContentType;
                md.ETag = blockBlob.Properties.ETag;
                md.Created = blockBlob.Properties.Created.Value.UtcDateTime;

                if (!String.IsNullOrEmpty(md.ETag))
                {
                    while (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        private bool AzureEnumerate(string continuationToken, out string nextContinuationToken, out List<BlobMetadata> blobs)
        {
            nextContinuationToken = null;
            blobs = new List<BlobMetadata>();

            BlobContinuationToken bct = null;
            if (!String.IsNullOrEmpty(continuationToken))
            {
                if (!AzureGetContinuationToken(continuationToken, out bct))
                {
                    return false;
                }
            }

            BlobResultSegment segment = _AzureContainer.ListBlobsSegmentedAsync(bct).Result;
            if (segment == null || segment.Results == null || segment.Results.Count() < 1) return true;

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

                    blobs.Add(md);
                } 
            }

            if (segment.ContinuationToken != null)
            {
                nextContinuationToken = Guid.NewGuid().ToString();
                AzureStoreContinuationToken(nextContinuationToken, segment.ContinuationToken);
            }

            if (!String.IsNullOrEmpty(continuationToken)) AzureRemoveContinuationToken(continuationToken);

            return true;
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

        private string AzureGenerateUrl(string key)
        {
            return "https://" +
                _AzureSettings.AccountName +
                ".blob.core.windows.net/" +
                _AzureSettings.Container +
                "/" +
                key;
        }

        #endregion

        #endregion
    }
}
