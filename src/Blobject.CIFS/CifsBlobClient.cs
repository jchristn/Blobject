﻿namespace Blobject.CIFS
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;
    using EzSmb;

    /// <inheritdoc />
    public class CifsBlobClient : IBlobClient, IDisposable
    {
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

        private string _Header = "[CifsBlobClient] ";
        private CifsSettings _CifsSettings = null;
        private int _StreamBufferSize = 4096;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="CifsBlobClient"/> class.
        /// </summary>
        /// <param name="cifsSettings">Settings for <see cref="CifsBlobClient"/>.</param>
        public CifsBlobClient(CifsSettings cifsSettings)
        {
            if (cifsSettings == null) throw new ArgumentNullException(nameof(cifsSettings));

            _CifsSettings = cifsSettings;
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
                _CifsSettings = null;
                _Disposed = true;
            }

            Log("disposing");
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

        /// <summary>
        /// List shares available on the server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of share names.</returns>
        public async Task<List<string>> ListShares(CancellationToken token = default)
        {
            Node shares = await EzSmb.Node.GetNode(_CifsSettings.Ip.ToString(), _CifsSettings.Username, _CifsSettings.Password);
            Node[] nodes = await shares.GetList();
            List<string> ret = new List<string>();
            foreach (Node node in nodes) ret.Add(node.Name);
            return ret;
        }

        /// <inheritdoc />
        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            string path = BuildFilePath(key);

            Node file = await Node.GetNode(path, _CifsSettings.Username, _CifsSettings.Password).ConfigureAwait(false);

            using (MemoryStream stream = await file.Read().ConfigureAwait(false))
            {
                byte[] data = Common.ReadStreamFully(stream);
                return data;
            }
        }

        /// <inheritdoc />
        public async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            string path = BuildFilePath(key);

            Node file = await Node.GetNode(path, _CifsSettings.Username, _CifsSettings.Password).ConfigureAwait(false);

            using (MemoryStream stream = await file.Read().ConfigureAwait(false))
            {
                BlobData ret = new BlobData();
                ret.ContentLength = (file.Size != null ? file.Size.Value : 0);
                await stream.CopyToAsync(ret.Data).ConfigureAwait(false);
                ret.Data.Seek(0, SeekOrigin.Begin);
                return ret;
            }
        }

        /// <inheritdoc />
        public async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            string path = BuildFilePath(key);

            Node file = await Node.GetNode(path, _CifsSettings.Username, _CifsSettings.Password).ConfigureAwait(false);

            if (file != null)
            {
                BlobMetadata ret = new BlobMetadata
                {
                    Key = key,
                    IsFolder = (file.Type == NodeType.Folder),
                    ContentType = "application/octet-stream",
                    ContentLength = (file.Size != null ? file.Size.Value : 0),
                    CreatedUtc = file.Created,
                    LastAccessUtc = file.LastAccessed,
                    LastUpdateUtc = file.LastAccessed
                };

                return ret;
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
            string path = BuildSharePath();
            Node file = await Node.GetNode(path, _CifsSettings.Username, _CifsSettings.Password).ConfigureAwait(false);

            if (key.EndsWith("/"))
            {
                await file.CreateFolder(key);
            }
            else
            {
                using (MemoryStream stream = new MemoryStream(data))
                {
                    await file.Write(stream, key).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            string path = BuildSharePath();
            Node file = await Node.GetNode(path, _CifsSettings.Username, _CifsSettings.Password).ConfigureAwait(false);

            if (key.EndsWith("/"))
            {
                await file.CreateFolder(key);
            }
            else
            {
                await file.Write(stream, key).ConfigureAwait(false);
            }
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
            string path = BuildFilePath(key);
            Node file = await Node.GetNode(path, _CifsSettings.Username, _CifsSettings.Password).ConfigureAwait(false);
            if (file != null) await file.Delete().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            string path = BuildFilePath(key);
            Node file = await Node.GetNode(path, _CifsSettings.Username, _CifsSettings.Password).ConfigureAwait(false);
            return (file != null);
        }

        /// <inheritdoc />
        public string GenerateUrl(string key, CancellationToken token = default)
        {
            return "\\\\" + _CifsSettings.Ip.ToString() + "\\" + _CifsSettings.Share + "\\" + key;
        }

        /// <inheritdoc />
        public IEnumerable<BlobMetadata> Enumerate(EnumerationFilter filter = null)
        {
            if (filter == null) filter = new EnumerationFilter();
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            string sharePath = BuildSharePath();
            string baseDirectory = "";

            if (filter.Prefix.Contains("/")) filter.Prefix = filter.Prefix.Replace("/", "\\");
            while (filter.Prefix.StartsWith("\\")) filter.Prefix = filter.Prefix.Substring(1);
            if (!String.IsNullOrEmpty(filter.Prefix) && !filter.Prefix.EndsWith("*")) filter.Prefix += "*";
             
            if (filter.Prefix.Contains("\\"))
            {
                #region Nested

                if (filter.Prefix.EndsWith("\\"))
                {
                    #region Path-Only

                    baseDirectory += filter.Prefix;
                    filter.Prefix = "*";

                    #endregion
                }
                else
                {
                    #region Subdirectory-and-Prefix

                    string[] parts = filter.Prefix.Split('\\');
                    for (int i = 0; i < (parts.Length - 1); i++)
                    {
                        baseDirectory += "\\" + parts[i];
                    }

                    filter.Prefix = parts[parts.Length - 1];

                    #endregion
                }

                #endregion
            }
            else
            {
                #region Root

                // do nothing

                #endregion
            }

            baseDirectory = baseDirectory.Replace("\\\\", "\\");
            Log("retrieving item list in path " + (!String.IsNullOrEmpty(baseDirectory) ? baseDirectory : "(empty)") + " prefix " + filter.Prefix);

            Node root = Node.GetNode(sharePath + "\\" + baseDirectory, _CifsSettings.Username, _CifsSettings.Password).Result;
            Node[] nodes = root.GetList(filter.Prefix).Result;
            if (nodes != null && nodes.Length > 0)
            {
                foreach (Node node in nodes)
                {
                    if (node.Type == NodeType.Folder)
                    {
                        EnumerationFilter ef = new EnumerationFilter
                        {
                            MinimumSize = filter.MinimumSize,
                            MaximumSize = filter.MaximumSize,
                            Prefix = filter.Prefix,
                            Suffix = filter.Suffix
                        };

                        IEnumerable<BlobMetadata> blobs = EnumerateSubdirectory(
                            ef, 
                            sharePath,
                            baseDirectory + "\\" + node.Name + "\\", 
                            filter.Prefix);

                        if (blobs != null)
                        {
                            foreach (BlobMetadata blob in blobs)
                            {
                                yield return blob;
                            }
                        }

                        // return the directory after returning the files to support empty operations

                        string key = (baseDirectory + "/" + node.Name).Replace("\\", "/").Replace("//", "/");
                        while (key.StartsWith("/")) key = key.Substring(1);

                        BlobMetadata dir = new BlobMetadata
                        {
                            Key = key,
                            IsFolder = true,
                            ContentType = "application/octet-stream",
                            ContentLength = (node.Size != null ? node.Size.Value : 0),
                            CreatedUtc = node.Created,
                            LastAccessUtc = node.LastAccessed,
                            LastUpdateUtc = node.Updated
                        };

                        yield return dir;
                    }
                    else
                    {
                        if (node.Size < filter.MinimumSize || node.Size > filter.MaximumSize) continue;
                        if (!String.IsNullOrEmpty(filter.Prefix) && !node.Name.ToLower().StartsWith(filter.Prefix)) continue;
                        if (!String.IsNullOrEmpty(filter.Suffix) && !node.Name.ToLower().EndsWith(filter.Suffix)) continue;

                        string key = (baseDirectory + "/" + node.Name).Replace("\\", "/").Replace("//", "/");
                        while (key.StartsWith("/")) key = key.Substring(1);

                        BlobMetadata md = new BlobMetadata
                        {
                            Key = key,
                            IsFolder = false,
                            ContentType = "application/octet-stream",
                            ContentLength = (node.Size != null ? node.Size.Value : 0),
                            CreatedUtc = node.Created,
                            LastAccessUtc = node.LastAccessed,
                            LastUpdateUtc = node.LastAccessed
                        };
                        
                        yield return md;
                    } 
                }
            }

            yield break;
        }

        /// <inheritdoc />
        public async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult er = new EmptyResult();

            foreach (BlobMetadata md in Enumerate())
            {
                await DeleteAsync(md.Key, token).ConfigureAwait(false);
                er.Blobs.Add(md);
            }

            return er;
        }

        #endregion

        #region Private-Methods

        private string BuildSharePath()
        {
            return _CifsSettings.Ip + "\\" + _CifsSettings.Share;
        }

        private string BuildFilePath(string filename)
        {
            return _CifsSettings.Ip + "\\" + _CifsSettings.Share + "\\" + filename;
        }

        private IEnumerable<BlobMetadata> EnumerateSubdirectory(EnumerationFilter filter, string sharePath, string baseDirectory, string filePrefix)
        {
            baseDirectory = baseDirectory.Replace("\\\\", "\\");
            Log("retrieving item list in path " + (!String.IsNullOrEmpty(baseDirectory) ? baseDirectory : "(empty)") + " prefix " + filePrefix);

            Node root = Node.GetNode(sharePath + "\\" + baseDirectory, _CifsSettings.Username, _CifsSettings.Password).Result;
            Node[] nodes = root.GetList(filePrefix).Result;
            if (nodes != null && nodes.Length > 0)
            {
                foreach (Node node in nodes)
                {
                    if (node.Type == NodeType.Folder)
                    {
                        EnumerationFilter ef = new EnumerationFilter
                        {
                            MinimumSize = filter.MinimumSize,
                            MaximumSize = filter.MaximumSize,
                            Prefix = filter.Prefix,
                            Suffix = filter.Suffix
                        };

                        IEnumerable<BlobMetadata> blobs = EnumerateSubdirectory(
                            ef,
                            sharePath,
                            baseDirectory + "\\" + node.Name,
                            filter.Prefix);

                        if (blobs != null)
                        {
                            foreach (BlobMetadata blob in blobs)
                            {
                                yield return blob;
                            }
                        }

                        // return the directory after returning the files to support empty operations

                        string key = (baseDirectory + "/" + node.Name).Replace("\\", "/").Replace("//", "/");
                        while (key.StartsWith("/")) key = key.Substring(1);

                        BlobMetadata dir = new BlobMetadata
                        {
                            Key = key,
                            IsFolder = true,
                            ContentType = "application/octet-stream",
                            ContentLength = (node.Size != null ? node.Size.Value : 0),
                            CreatedUtc = node.Created,
                            LastAccessUtc = node.LastAccessed,
                            LastUpdateUtc = node.Updated
                        };

                        yield return dir;
                    }
                    else
                    {
                        if (node.Size < filter.MinimumSize || node.Size > filter.MaximumSize) continue;
                        if (!String.IsNullOrEmpty(filter.Prefix) && !node.Name.ToLower().StartsWith(filter.Prefix)) continue;
                        if (!String.IsNullOrEmpty(filter.Suffix) && !node.Name.ToLower().EndsWith(filter.Suffix)) continue;

                        string key = (baseDirectory + "/" + node.Name).Replace("\\", "/").Replace("//", "/");
                        while (key.StartsWith("/")) key = key.Substring(1);

                        BlobMetadata md = new BlobMetadata
                        {
                            Key = key,
                            IsFolder = false,
                            ContentType = "application/octet-stream",
                            ContentLength = (node.Size != null ? node.Size.Value : 0),
                            CreatedUtc = node.Created,
                            LastAccessUtc = node.LastAccessed,
                            LastUpdateUtc = node.LastAccessed
                        };

                        yield return md;
                    }
                }
            }

            yield break;
        }

        private void Log(string msg)
        {
            if (!String.IsNullOrEmpty(msg))
                Logger?.Invoke(_Header + msg);
        }

        #endregion
    }
}