using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using KvpbaseSDK;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace BlobHelper
{
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
        public bool Delete(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                Debug.WriteLine("Delete invalid value or null for ID");
                return false;
            }

            bool success = false;

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    success = S3Delete(id);
                    break;
                case StorageType.Azure:
                    success = AzureDelete(id);
                    break;
                case StorageType.Disk:
                    success = DiskDelete(id);
                    break;
                case StorageType.Kvpbase:
                    success = KvpbaseDelete(id);
                    break;
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString()); 
            }

            if (!success)
            {
                Debug.WriteLine("Delete failure condition indicated from external storage");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retrieve a BLOB by its ID.
        /// </summary>
        /// <param name="id">ID of the BLOB.</param>
        /// <param name="data">Byte array containing BLOB data.</param>
        /// <returns>True if successful.</returns>
        public bool Get(string id, out byte[] data)
        {
            data = null;

            if (String.IsNullOrEmpty(id))
            {
                Debug.WriteLine("Get invalid value or null supplied for ID");
                return false;
            }

            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    return S3Get(id, out data);
                case StorageType.Azure:
                    return AzureGet(id, out data);
                case StorageType.Disk:
                    return DiskGet(id, out data);
                case StorageType.Kvpbase:
                    return KvpbaseGet(id, out data);
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
        public bool Write(
            string id,
            bool base64,
            object data,
            out string url)
        {
            url = null;

            if (String.IsNullOrEmpty(id))
            {
                Debug.WriteLine("Write no ID supplied");
                return false;
            }

            byte[] byteData;

            if (base64 && (data is string))
            {
                byteData = Common.Base64ToBytes((string)data);
            }
            else if (data is byte[])
            {
                byteData = (byte[])data;
            }
            else if (data is string)
            {
                byteData = Encoding.UTF8.GetBytes((string)data);
            }
            else
            {
                byteData = Common.ObjectToBytes(data);
            }

            bool success = false; 
            
            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    success = S3Write(id, byteData);
                    url = S3GenerateUrl(id);
                    break;
                case StorageType.Azure:
                    success = AzureWrite(id, byteData);
                    url = AzureGenerateUrl(id);
                    break;
                case StorageType.Disk:
                    success = DiskWrite(id, byteData);
                    url = DiskGenerateUrl(id);
                    break;
                case StorageType.Kvpbase:
                    success = _Kvpbase.WriteObject(_KvpbaseSettings.UserGuid, _KvpbaseSettings.Container, id, "application/octet-stream", byteData);
                    break;
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }

            if (success)
            {
                Debug.WriteLine("Write successfully stored BLOB data at " + url);
                return true;
            }
            else
            {
                return false;
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
                    _S3Client = new AmazonS3Client(_S3Credentials, _S3Region);
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
                    _Kvpbase = new KvpbaseClient(_KvpbaseSettings.ApiKey, _KvpbaseSettings.Endpoint);
                    break;
                default:
                    throw new ArgumentException("Unknown storage type: " + _StorageType.ToString());
            }    
        }

        #region Private-Kvpbase-Methods

        private bool KvpbaseDelete(string id)
        {
            if (_Kvpbase.DeleteObject(_KvpbaseSettings.UserGuid, _KvpbaseSettings.Container, id))
            {
                Debug.WriteLine("KvpbaseDelete success response from kvpbase for ID " + id);
                return true;
            }
            else
            {
                Debug.WriteLine("KvpbaseDelete failure response from kvpbase for ID " + id);
                return false;
            }
        }

        private bool KvpbaseGet(string id, out byte[] data)
        {
            data = null;

            if (_Kvpbase.ReadObject(_KvpbaseSettings.UserGuid, _KvpbaseSettings.Container, id, out data))
            {
                Debug.WriteLine("KvpbaseGet retrieved " + data.Length + " bytes for ID " + id);
                return true;
            }
            else
            {
                Debug.WriteLine("KvpbaseGet failure response from kvpbase for ID " + id);
                return false;
            }
        }

        private bool KvpbaseWrite(string id, string contentType, byte[] data, out string writtenUrl)
        {
            writtenUrl = null;

            if (_Kvpbase.WriteObject(_KvpbaseSettings.UserGuid, _KvpbaseSettings.Container, id, contentType, data))
            {
                Debug.WriteLine("KvpbaseWrite success response from kvpbase for ID " + id + ": " + writtenUrl);
                return true;
            }
            else
            {
                Debug.WriteLine("KvpbaseWrite failure response from kvpbase for ID " + id);
                return false;
            }
        }

        #endregion

        #region Private-Disk-Methods

        private bool DiskDelete(string id)
        {
            try
            {
                File.Delete(DiskGenerateUrl(id));
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine("DiskDelete unable to delete " + id + ": " + e.Message);
                return false;
            }
        }

        private bool DiskGet(string id, out byte[] data)
        {
            data = null;

            try
            {
                data = File.ReadAllBytes(DiskGenerateUrl(id));
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine("DiskGet unable to read " + id + ": " + e.Message);
                return false;
            }
        }

        private bool DiskWrite(string id, byte[] data)
        {
            try
            {
                File.WriteAllBytes(DiskGenerateUrl(id), data);
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine("DiskWrite unable to write " + id + ": " + e.Message);
                return false;
            }
        }

        private string DiskGenerateUrl(string id)
        {
            return _DiskSettings.Directory + "/" + id;
        }

        #endregion

        #region Private-S3-Methods

        private bool S3Delete(string id)
        {
            try
            {
                #region Check-for-Null-Values

                if (String.IsNullOrEmpty(id)) return false;

                #endregion

                #region Process
                 
                using (_S3Client = new AmazonS3Client(_S3Credentials, _S3Region))
                {
                    DeleteObjectRequest request = new DeleteObjectRequest
                    {
                        BucketName = _AwsSettings.Bucket,
                        Key = id
                    };

                    DeleteObjectResponse response = _S3Client.DeleteObject(request);
                    int statusCode = (int)response.HttpStatusCode;

                    if (response != null)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool S3Get(string id, out byte[] data)
        {
            data = null;

            try
            {
                #region Process
                 
                using (_S3Client = new AmazonS3Client(_S3Credentials, _S3Region))
                {
                    GetObjectRequest request = new GetObjectRequest
                    {
                        BucketName = _AwsSettings.Bucket,
                        Key = id
                    };

                    using (GetObjectResponse response = _S3Client.GetObject(request))
                    using (Stream responseStream = response.ResponseStream)
                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        if (response.ContentLength > 0)
                        {
                            // first copy the stream
                            data = new byte[response.ContentLength];
                            byte[] temp = new byte[2];

                            Stream bodyStream = response.ResponseStream;
                            data = Common.StreamToBytes(bodyStream);

                            int statusCode = (int)response.HttpStatusCode;
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

                #endregion
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool S3Write(string id, byte[] data)
        {
            try
            {
                #region Process
                 
                using (_S3Client = new AmazonS3Client(_S3Credentials, _S3Region))
                {
                    Stream s = new MemoryStream(data);
                    PutObjectRequest request = new PutObjectRequest
                    {
                        BucketName = _AwsSettings.Bucket,
                        Key = id,
                        InputStream = s,
                        ContentType = "application/octet-stream",
                    };

                    PutObjectResponse response = _S3Client.PutObject(request);
                    int statusCode = (int)response.HttpStatusCode;

                    if (response != null)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception)
            {
                return false;
            }
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

        private bool AzureDelete(string id)
        {
            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(id);
                OperationContext ctx = new OperationContext();
                blockBlob.Delete(DeleteSnapshotsOption.None, null, null, ctx);
                int statusCode = ctx.LastResult.HttpStatusCode;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool AzureGet(string id, out byte[] data)
        {
            data = null;

            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(id);
                OperationContext ctx = new OperationContext();
                Stream blobStream = new MemoryStream();
                blockBlob.DownloadToStream(blobStream, null, null, ctx);
                blobStream.Position = 0;
                data = Common.StreamToBytes(blobStream);
                blobStream.Close();

                int statusCode = ctx.LastResult.HttpStatusCode;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool AzureWrite(string id, byte[] data)
        {
            try
            {
                CloudBlockBlob blockBlob = _AzureContainer.GetBlockBlobReference(id);
                OperationContext ctx = new OperationContext();
                MemoryStream stream = new MemoryStream(data);
                blockBlob.UploadFromStream(stream, null, null, ctx);
                stream.Close();

                int statusCode = ctx.LastResult.HttpStatusCode;
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
