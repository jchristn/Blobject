namespace Blobject.NFS
{
    /*
     * See https://www.dummies.com/article/technology/computers/operating-systems/linux/how-to-share-files-with-nfs-on-linux-systems-255851/
     * https://github.com/SonnyX/NFS-Client
     * https://github.com/nekoni/nekodrive
     * https://code.google.com/archive/p/nekodrive/wikis/UseNFSDotNetLibrary.wiki
     * https://ubuntu.com/server/docs/network-file-system-nfs
     * https://www.hanewin.net/nfs-e.htm
     * https://serverfault.com/questions/240897/how-to-properly-set-permissions-for-nfs-folder-permission-denied-on-mounting-en
     * https://temasre.medium.com/connecting-to-nfs-client-v4-using-net-core-and-c-bc1f4af814c9
     * 
     */

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;
    using NFSLibrary;
    using NFSLibrary.Protocols.Commons;

    /// <inheritdoc />
    public class NfsBlobClient : IBlobClient, IDisposable
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        /// <summary>
        /// Method to invoke to send log messages.
        /// </summary>
        public Action<string> Logger { get; set; } = null;

        /// <summary>
        /// Buffer size when working with streams.
        /// </summary>
        public int StreamBufferSize
        {
            get
            {
                return _StreamBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(StreamBufferSize));
                _StreamBufferSize = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Header = "[NfsBlobClient] ";
        private NfsSettings _NfsSettings = null;
        private int _StreamBufferSize = 4096;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsBlobClient"/> class.
        /// </summary>
        /// <param name="nfsSettings">Settings for <see cref="NfsBlobClient"/>.</param>
        public NfsBlobClient(NfsSettings nfsSettings)
        {
            _NfsSettings = nfsSettings;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            Log("disposing");

            if (!_Disposed)
            {
                _NfsSettings = null;
                _Disposed = true;
            }

            Log("disposed");
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            NFSLibrary.NFSClient nfs = InitializeClient();
            MountShare(nfs);

            byte[] ret = null;

            Stream stream = new MemoryStream();
            nfs.Read(key, ref stream);

            if (stream != null)
            {
                stream.Seek(0, SeekOrigin.Begin);
                ret = ((MemoryStream)stream).ToArray();
                stream.Close();
                stream.Dispose();
                stream = null;
            }

            UnmountShare(nfs);
            DisconnectClient(nfs);
            return ret;
        }

        /// <inheritdoc />
        public async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            BlobMetadata md = await GetMetadataAsync(key, token).ConfigureAwait(false);

            NFSLibrary.NFSClient nfs = InitializeClient();
            MountShare(nfs);

            BlobData ret = null;

            Stream stream = new MemoryStream();
            nfs.Read(key, ref stream);

            if (stream != null)
            {
                stream.Seek(0, SeekOrigin.Begin);

                ret = new BlobData
                {
                    Data = stream,
                    ContentLength = md.ContentLength
                };
            }

            UnmountShare(nfs);
            DisconnectClient(nfs);
            return ret;
        }

        /// <inheritdoc />
        public async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            NFSLibrary.NFSClient nfs = InitializeClient();
            MountShare(nfs);

            BlobMetadata md = null;
            List<string> files = nfs.GetItemList(key);
            if (files != null && files.Count > 0)
            {
                NFSAttributes attrib = nfs.GetItemAttributes(key);

                md = new BlobMetadata
                {
                    Key = key,
                    ContentLength = attrib.Size,
                    ContentType = "application/octet-stream",
                    CreatedUtc = attrib.CreateDateTime,
                    LastAccessUtc = attrib.LastAccessedDateTime,
                    LastUpdateUtc = attrib.ModifiedDateTime
                };
            }

            UnmountShare(nfs);
            DisconnectClient(nfs);

            if (md == null) throw new KeyNotFoundException("The requested object was not found.");
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
            NFSLibrary.NFSClient nfs = InitializeClient();
            MountShare(nfs);

            using (MemoryStream stream = new MemoryStream())
            {
                await stream.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
                stream.Seek(0, SeekOrigin.Begin);
                nfs.Write(key, stream);
            }

            UnmountShare(nfs);
            DisconnectClient(nfs);
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            NFSLibrary.NFSClient nfs = InitializeClient();
            MountShare(nfs);

            nfs.Write(key, stream);
            
            UnmountShare(nfs);
            DisconnectClient(nfs);
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
            NFSLibrary.NFSClient nfs = InitializeClient();
            MountShare(nfs);
            nfs.DeleteFile(key);
            UnmountShare(nfs);
            DisconnectClient(nfs);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            NFSLibrary.NFSClient nfs = InitializeClient();
            MountShare(nfs);

            bool exists = false;
            List<string> files = nfs.GetItemList(key);
            if (files != null && files.Count > 0) exists = true;

            UnmountShare(nfs);
            DisconnectClient(nfs);

            return exists;
        }

        /// <inheritdoc />
        public string GenerateUrl(string key, CancellationToken token = default)
        {
            string url = "/" + _NfsSettings.Ip.ToString() + "/" + _NfsSettings.Share + "/" + key;
            url = url.Replace("\\", "/").Replace("//", "/");
            return url;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult> EnumerateAsync(string prefix = null, string continuationToken = null, CancellationToken token = default)
        {
            if (prefix == null) prefix = "";
            Log("enumerating using prefix " + prefix);

            NFSLibrary.NFSClient nfs = InitializeClient();
            MountShare(nfs);

            EnumerationResult ret = new EnumerationResult();

            if (prefix.Contains("\\")) prefix = prefix.Replace("\\", "/");
            while (prefix.StartsWith("/")) prefix = prefix.Substring(1);

            if (String.IsNullOrEmpty(prefix))
            {
                #region Top-Level-No-Prefix

                foreach (string item in nfs.GetItemList("."))
                {
                    NFSAttributes attrib = nfs.GetItemAttributes(item);
                    if (attrib == null) continue;
                    BlobMetadata md = new BlobMetadata
                    {
                        Key = item,
                        ContentType = "application/octet-stream",
                        ContentLength = attrib.Size,
                        CreatedUtc = attrib.CreateDateTime,
                        LastAccessUtc = attrib.LastAccessedDateTime,
                        LastUpdateUtc = attrib.ModifiedDateTime
                    };

                    if (attrib.NFSType == NFSItemTypes.NFDIR) md.IsFolder = true;
                    ret.Blobs.Add(md);
                }

                #endregion
            }
            else
            {
                if (prefix.Contains("/"))
                {
                    #region Directory-Prefix

                    string[] pathParts = prefix.Split('/');
                    string baseDirectory = "";
                    string filePrefix = "";

                    if (prefix.EndsWith("/"))
                    {
                        #region Directory-Only

                        baseDirectory = "/" + string.Join('/', pathParts) + "/";

                        #endregion
                    }
                    else
                    {
                        #region Directory-and-File

                        for (int i = 0; (i < pathParts.Length - 1); i++)
                        {
                            baseDirectory += "/" + pathParts[i];
                        }

                        filePrefix = pathParts[pathParts.Length - 1];
                        baseDirectory += "/";

                        #endregion
                    }

                    foreach (string item in nfs.GetItemList(baseDirectory)) // not concerned about leading/trailing slashes
                    {
                        if (!String.IsNullOrEmpty(filePrefix) && !item.StartsWith(filePrefix)) continue;

                        NFSAttributes attrib = nfs.GetItemAttributes(baseDirectory + item);
                        if (attrib == null) continue;
                        BlobMetadata md = new BlobMetadata
                        {
                            Key = item,
                            ContentType = "application/octet-stream",
                            ContentLength = attrib.Size,
                            CreatedUtc = attrib.CreateDateTime,
                            LastAccessUtc = attrib.LastAccessedDateTime,
                            LastUpdateUtc = attrib.ModifiedDateTime
                        };

                        if (attrib.NFSType == NFSItemTypes.NFDIR) md.IsFolder = true;
                        ret.Blobs.Add(md);
                    }

                    #endregion
                }
                else
                {
                    #region File-Prefix

                    foreach (string item in nfs.GetItemList("."))
                    {
                        if (!item.StartsWith(prefix)) continue;

                        NFSAttributes attrib = nfs.GetItemAttributes(item);
                        if (attrib == null) continue;
                        BlobMetadata md = new BlobMetadata
                        {
                            Key = item,
                            ContentType = "application/octet-stream",
                            ContentLength = attrib.Size,
                            CreatedUtc = attrib.CreateDateTime,
                            LastAccessUtc = attrib.LastAccessedDateTime,
                            LastUpdateUtc = attrib.ModifiedDateTime
                        };

                        if (attrib.NFSType == NFSItemTypes.NFDIR) md.IsFolder = true;
                        ret.Blobs.Add(md);
                    }

                    #endregion
                }
            }

            UnmountShare(nfs);
            Log("enumeration complete with " + ret.Blobs.Count + " BLOBs");
            return ret;
        }

        /// <inheritdoc />
        public async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult er = new EmptyResult();

            string continuationToken = null;

            while (true)
            {
                EnumerationResult result = await EnumerateAsync(null, continuationToken, token).ConfigureAwait(false);
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

        private NFSClient InitializeClient()
        {
            NFSClient client = null;

            switch (_NfsSettings.Version)
            {
                case NfsVersionEnum.V2:
                    client = new NFSClient(NFSClient.NFSVersion.v2);
                    break;
                case NfsVersionEnum.V3:
                    client = new NFSClient(NFSClient.NFSVersion.v3);
                    break;
                case NfsVersionEnum.V4:
                    client = new NFSClient(NFSClient.NFSVersion.v4);
                    break;
                default:
                    throw new ArgumentException("Unknown NFS version '" + _NfsSettings.Version.ToString() + "'.");
            }

            client.Connect(_NfsSettings.Ip);
            return client;
        }

        private void DisconnectClient(NFSClient client)
        {
            client.Disconnect();
        }

        private void MountShare(NFSClient client)
        {
            List<string> devices = client.GetExportedDevices();
            if (devices != null && devices.Count > 0 && devices.Contains(_NfsSettings.Share))
            {
                client.MountDevice(_NfsSettings.Share);
                return;
            }

            throw new ArgumentException("The specified share '" + _NfsSettings.Share + "' was not found.");
        }

        private void UnmountShare(NFSClient client)
        {
            client.UnMountDevice();
        }

        private void Log(string msg)
        {
            if (!String.IsNullOrEmpty(msg))
                Logger?.Invoke(_Header + msg);
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}