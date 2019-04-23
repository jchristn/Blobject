using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
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
        /// Delete a BLOB by its ID.
        /// </summary>
        /// <param name="id">ID of the BLOB.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Delete(string id)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            bool success = false;

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    success = await S3Delete(id);
                    break;
                case StorageType.Azure:
                    success = await AzureDelete(id);
                    break;
                case StorageType.Disk:
                    success = await DiskDelete(id);
                    break;
                case StorageType.Kvpbase:
                    success = await KvpbaseDelete(id);
                    break;
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString()); 
            }

            return success;
        }

        /// <summary>
        /// Retrieve a BLOB by its ID.
        /// </summary>
        /// <param name="id">ID of the BLOB.</param>
        /// <param name="data">Byte array containing BLOB data.</param>
        /// <returns>Byte data of the BLOB.</returns>
        public async Task<byte[]> Get(string id)
        { 
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return await S3Get(id);
                case StorageType.Azure:
                    return await AzureGet(id);
                case StorageType.Disk:
                    return await DiskGet(id);
                case StorageType.Kvpbase:
                    return await KvpbaseGet(id);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }
        }

        /// <summary>
        /// Write a BLOB.
        /// </summary>
        /// <param name="id">ID of the BLOB.</param>
        /// <param name="base64">True of the supplied data is a string containing Base64-encoded data.</param>
        /// <param name="data">BLOB data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Write(
            string id,
            string contentType,
            byte[] data)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            if (_StorageType != StorageType.Disk && String.IsNullOrEmpty(contentType)) throw new ArgumentNullException(nameof(contentType));

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return await S3Write(id, contentType, data);
                case StorageType.Azure:
                    return await AzureWrite(id, contentType, data);
                case StorageType.Disk:
                    return await DiskWrite(id, data);
                case StorageType.Kvpbase:
                    return _Kvpbase.WriteObject(_KvpbaseSettings.Container, id, contentType, data);
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            } 
        }

        public async Task<bool> Exists(string id)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return await S3Exists(id);
                case StorageType.Azure:
                    return await AzureExists(id);
                case StorageType.Disk:
                    return await DiskExists(id);
                case StorageType.Kvpbase:
                    return await KvpbaseExists(id);
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
        private async Task<bool> KvpbaseDelete(string id)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return _Kvpbase.DeleteObject(_KvpbaseSettings.Container, id);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<byte[]> KvpbaseGet(string id)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            byte[] data = null; 
            if (_Kvpbase.ReadObject(_KvpbaseSettings.Container, id, out data)) return data;
            else throw new IOException("Unable to read object.");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> KvpbaseExists(string id)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return _Kvpbase.ObjectExists(_KvpbaseSettings.Container, id); 
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> KvpbaseWrite(string id, string contentType, byte[] data)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return _Kvpbase.WriteObject(_KvpbaseSettings.Container, id, contentType, data);
        }

        #endregion

        #region Private-Disk-Methods

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> DiskDelete(string id)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            File.Delete(DiskGenerateUrl(id));
            return true;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<byte[]> DiskGet(string id)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                return File.ReadAllBytes(DiskGenerateUrl(id));
            }
            catch (Exception)
            {
                throw new IOException("Unable to read object.");
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> DiskExists(string id)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return File.Exists(DiskGenerateUrl(id));
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> DiskWrite(string id, byte[] data)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        { 
            File.WriteAllBytes(DiskGenerateUrl(id), data);
            return true;
        }

        private string DiskGenerateUrl(string id)
        {
            return _DiskSettings.Directory + "/" + id;
        }

        #endregion

        #region Private-S3-Methods

        private async Task<bool> S3Delete(string id)
        { 
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
                  
            DeleteObjectRequest request = new DeleteObjectRequest
            {
                BucketName = _AwsSettings.Bucket,
                Key = id
            };
            
            DeleteObjectResponse response = await _S3Client.DeleteObjectAsync(request);
            int statusCode = (int)response.HttpStatusCode;

            if (response != null) return true;
            else return false;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<byte[]> S3Get(string id)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = _AwsSettings.Bucket,
                    Key = id,
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
         
        private async Task<bool> S3Exists(string id)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            try
            {
                GetObjectMetadataRequest request = new GetObjectMetadataRequest
                {
                    BucketName = _AwsSettings.Bucket,
                    Key = id
                };

                GetObjectMetadataResponse response = await _S3Client.GetObjectMetadataAsync(request);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> S3Write(string id, string contentType, byte[] data)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
                  
            Stream s = new MemoryStream(data);

            PutObjectRequest request = new PutObjectRequest
            {
                BucketName = _AwsSettings.Bucket,
                Key = id,
                InputStream = s,
                ContentType = contentType,
            };

            PutObjectResponse response = await _S3Client.PutObjectAsync(request);
            int statusCode = (int)response.HttpStatusCode;

            if (response != null) return true;
            else return false;
        }

        private string S3GenerateUrl(string id)
        {
            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest();
            request.BucketName = _AwsSettings.Bucket;
            request.Key = id;
            request.Protocol = Protocol.HTTPS;
            request.Expires = DateTime.Now.AddYears(100);
            return _S3Client.GetPreSignedURL(request);
        }

        #endregion

        #region Private-Azure-Methods

        private async Task<bool> AzureDelete(string id)
        {
            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(id);
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

        private async Task<byte[]> AzureGet(string id)
        {
            byte[] data = null;

            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(id);
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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> AzureExists(string id)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                return _AzureBlobClient.GetContainerReference(_AzureSettings.Container).GetBlockBlobReference(id).ExistsAsync().Result;
            }
            catch (Exception)
            {
                return false;
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<bool> AzureWrite(string id, string contentType, byte[] data)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(id);
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

        private string AzureGenerateUrl(string id)
        {
            return "https://" +
                _AzureSettings.AccountName +
                ".blob.core.windows.net/" +
                _AzureSettings.Container +
                "/" +
                id;
        }

        #endregion

        #endregion
    }
}
