namespace Test.Script
{
    using System.IO;
    using System.Text;
    using Blobject.AmazonS3;
    using Blobject.AmazonS3Lite;
    using Blobject.AzureBlob;
    using Blobject.CIFS;
    using Blobject.Core;
    using Blobject.Disk;
    using Blobject.NFS;
    using GetSomeInput;
    using SerializationHelper;

    public static class TestScript
    {
#pragma warning disable IDE0059 // Unnecessary assignment of a value
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        static string _StorageType = null;
        static BlobClientBase _Blobs = null;
        static Blobject.AmazonS3.AwsSettings _AmazonS3Settings = null;
        static Blobject.AmazonS3Lite.AwsSettings _AmazonS3LiteSettings = null;
        static AzureBlobSettings _AzureSettings = null;
        static CifsSettings _CifsSettings = null;
        static DiskSettings _DiskSettings = null;
        static NfsSettings _NfsSettings = null;
        static bool _Logging = true;

        private static List<string> _ValidStorageTypes = new List<string>
        {
            "aws",
            "awslite",
            "azure",
            "cifs",
            "disk",
            "nfs"
        };

        private static List<string> _Keys = new List<string>
        {
            "root.txt",
            "dir1/",
            "dir1/1.txt",
            "dir1/dir2/",
            "dir1/dir2/2.txt",
            "dir1/dir2/dir3/",
            "dir1/dir2/dir3/3.txt"
        };

        private static Serializer _Serializer = new Serializer();

        public static async Task Main(string[] args)
        {
            InitializeClient();

            await CreateDirectoryStructure();
            await Enumerate("Post create enumeration");
            await ReadObjectMetadata();
            await ReadObjectContents();
            await CheckObjectExistence();
            await BuildObjectUrls();
            await Enumerate("Prefix-based enumeration", "ro", null);
            await Enumerate("Prefix-based enumeration", "3", null);
            await Enumerate("Suffix-based enumeration", null, ".txt");
            await Enumerate("Size-based enumeration (0-10 bytes)", null, null, 0, 10);
            await Enumerate("Size-based enumeration (10+ bytes)", null, null, 10, null);

            /*
             * Prefix-based enumeration
             * Suffix-based enumeration
             */

            await DeleteNonEmptyDirectories();
            await Enumerate("Post non-empty directory delete enumeration");
            await DeleteFiles();
            await DeleteEmptyDirectories();
            await Enumerate("Post directory delete enumeration");
            await CheckObjectExistence();
        }

        static void InitializeClient()
        {
            while (true)
            {
                _StorageType = Inputty.GetString("Storage type [aws awslite azure disk cifs nfs]:", "disk", false);

                if (_ValidStorageTypes.Contains(_StorageType)) break;
            }

            string endpoint = null;

            switch (_StorageType)
            {
                case "aws":
                    Console.WriteLine("For S3-compatible storage, endpoint should be of the form http://[hostname]:[port]/");
                    endpoint = Inputty.GetString("Endpoint   :", null, true);

                    if (String.IsNullOrEmpty(endpoint))
                    {
                        _AmazonS3Settings = new Blobject.AmazonS3.AwsSettings(
                           Inputty.GetString("Access key :", null, false),
                           Inputty.GetString("Secret key :", null, false),
                           Inputty.GetString("Region     :", "USWest1", false),
                           Inputty.GetString("Bucket     :", null, false)
                           );
                    }
                    else
                    {
                        _AmazonS3Settings = new Blobject.AmazonS3.AwsSettings(
                            endpoint,
                            Inputty.GetBoolean("SSL        :", true),
                            Inputty.GetString("Access key :", null, false),
                            Inputty.GetString("Secret key :", null, false),
                            Inputty.GetString("Region     :", "USWest1", false),
                            Inputty.GetString("Bucket     :", null, false),
                            Inputty.GetString("Base URL   :", "http://localhost:8000/{bucket}/{key}", false)
                            );
                    }
                    _Blobs = new AmazonS3BlobClient(_AmazonS3Settings);
                    break;

                case "awslite":
                    Console.WriteLine("For S3-compatible storage, endpoint should be of the form http://[hostname]:[port]/");
                    endpoint = Inputty.GetString("Endpoint   :", null, true);

                    if (String.IsNullOrEmpty(endpoint))
                    {
                        _AmazonS3LiteSettings = new Blobject.AmazonS3Lite.AwsSettings(
                           Inputty.GetString("Access key :", null, false),
                           Inputty.GetString("Secret key :", null, false),
                           Inputty.GetString("Region     :", "USWest1", false),
                           Inputty.GetString("Bucket     :", null, false)
                           );
                    }
                    else
                    {
                        _AmazonS3LiteSettings = new Blobject.AmazonS3Lite.AwsSettings(
                            endpoint,
                            Inputty.GetBoolean("SSL        :", true),
                            Inputty.GetString("Access key :", null, false),
                            Inputty.GetString("Secret key :", null, false),
                            Inputty.GetString("Region     :", "USWest1", false),
                            Inputty.GetString("Bucket     :", null, false),
                            Inputty.GetString("Base URL   :", "http://localhost:8000/{bucket}/{key}", false)
                            );
                    }
                    _Blobs = new AmazonS3LiteBlobClient(_AmazonS3LiteSettings);
                    break;

                case "azure":
                    _AzureSettings = new AzureBlobSettings(
                        Inputty.GetString("Account name :", null, false),
                        Inputty.GetString("Access key   :", null, false),
                        Inputty.GetString("Endpoint URL :", null, false),
                        Inputty.GetString("Container    :", null, false));
                    _Blobs = new AzureBlobClient(_AzureSettings);
                    break;

                case "cifs":
                    _CifsSettings = new CifsSettings(
                        Inputty.GetString("Hostname   :", "localhost", false),
                        Inputty.GetString("Username   :", null, false),
                        Inputty.GetString("Password   :", null, false),
                        Inputty.GetString("Share      :", null, false));
                    _Blobs = new CifsBlobClient(_CifsSettings);
                    break;

                case "disk":
                    _DiskSettings = new DiskSettings(
                        Inputty.GetString("Directory :", null, false));
                    _Blobs = new DiskBlobClient(_DiskSettings);
                    break;

                case "nfs":
                    _NfsSettings = new NfsSettings(
                        Inputty.GetString("Hostname   :", "localhost", false),
                        Inputty.GetInteger("User ID    :", 0, false, true),
                        Inputty.GetInteger("Group ID   :", 0, false, true),
                        Inputty.GetString("Share      :", null, false),
                        (NfsVersionEnum)(Enum.Parse(typeof(NfsVersionEnum), Inputty.GetString("Version    :", "V3", false))));
                    _Blobs = new NfsBlobClient(_NfsSettings);
                    break;

                default:
                    throw new ArgumentException("Unknown storage type: '" + _StorageType + "'.");
            }

            if (_Logging) _Blobs.Logger = Console.WriteLine;
        }

        static async Task CreateDirectoryStructure()
        {
            /*

        /
        |-- root.txt
        |-- dir1
            |-- 1.txt
            |-- dir2
                |-- 2.txt
                |-- dir3
                    |-- 3.txt
                
             */

            Console.WriteLine("");
            Console.WriteLine("Initializing repository");

            if (await _Blobs.ExistsAsync("root.txt"))
            {
                Console.WriteLine("| File root.txt already exists");
            }
            else
            {
                await _Blobs.WriteAsync("root.txt", "text/plain", Encoding.UTF8.GetBytes("file root.txt"));
                Console.WriteLine("| Created file root.txt");
            }

            if (await _Blobs.ExistsAsync("dir1/"))
            {
                Console.WriteLine("| Directory dir1/ already exists");
            }
            else
            {
                await _Blobs.WriteAsync("dir1/", "text/plain", Array.Empty<byte>());
                Console.WriteLine("| Created directory dir1/");
            }

            if (await _Blobs.ExistsAsync("dir1/1.txt"))
            {
                Console.WriteLine("| File dir1/1.txt already exists");
            }
            else
            {
                await _Blobs.WriteAsync("dir1/1.txt", "text/plain", Encoding.UTF8.GetBytes("file dir1/1.txt"));
                Console.WriteLine("| Created file dir1/1.txt");
            }

            if (await _Blobs.ExistsAsync("dir1/dir2/"))
            {
                Console.WriteLine("| Directory dir1/dir2/ already exists");
            }
            else
            {
                await _Blobs.WriteAsync("dir1/dir2/", "text/plain", Array.Empty<byte>());
                Console.WriteLine("| Created directory dir1/dir2/");
            }

            if (await _Blobs.ExistsAsync("dir1/dir2/2.txt"))
            {
                Console.WriteLine("| File dir1/dir2/2.txt already exists");
            }
            else
            {
                await _Blobs.WriteAsync("dir1/dir2/2.txt", "text/plain", Encoding.UTF8.GetBytes("file dir1/dir2/2.txt"));
                Console.WriteLine("| Created file dir1/dir2/2.txt");
            }

            if (await _Blobs.ExistsAsync("dir1/dir2/dir3/"))
            {
                Console.WriteLine("| Directory dir1/dir2/dir3/ already exists");
            }
            else
            {
                await _Blobs.WriteAsync("dir1/dir2/dir3/", "text/plain", Array.Empty<byte>());
                Console.WriteLine("| Created directory dir1/dir2/dir3/");
            }

            if (await _Blobs.ExistsAsync("dir1/dir2/dir3/3.txt"))
            {
                Console.WriteLine("| File dir1/dir2/dir3/3.txt already exists");
            }
            else
            {
                await _Blobs.WriteAsync("dir1/dir2/dir3/3.txt", "text/plain", Encoding.UTF8.GetBytes("file dir1/dir2/dir3/3.txt"));
                Console.WriteLine("| Created file dir1/dir2/dir3/3.txt");
            }
        }

        static async Task Enumerate(
            string msg, 
            string prefix = null, 
            string suffix = null,
            long? minSize = null,
            long? maxSize = null)
        {
            Console.WriteLine("");
            Console.WriteLine(msg);
            if (!String.IsNullOrEmpty(prefix)) Console.WriteLine("| Using prefix   : " + prefix);
            if (!String.IsNullOrEmpty(suffix)) Console.WriteLine("| Using suffix   : " + suffix);
            if (minSize != null)               Console.WriteLine("| Using min size : " + minSize);
            if (maxSize != null)               Console.WriteLine("| Using max size : " + maxSize);

            EnumerationFilter ef = new EnumerationFilter
            {
                MinimumSize =  (minSize != null ? minSize.Value : 0),
                MaximumSize = (maxSize != null ? maxSize.Value : long.MaxValue),
                Prefix = prefix,
                Suffix = suffix
            };

            IEnumerable<BlobMetadata> blobs = _Blobs.Enumerate(ef);

            if (blobs != null)
            {
                Console.WriteLine("BLOBs:");

                foreach (BlobMetadata md in blobs)
                    Console.WriteLine("| " + md.Key + (md.IsFolder ? " (folder)" : ""));
            }
            else
            {
                Console.WriteLine("No BLOBs");
            }
        }

        static async Task ReadObjectMetadata()
        {
            Console.WriteLine("");
            Console.WriteLine("Retrieving metadata");

            foreach (string key in _Keys)
            {
                BlobMetadata md = await _Blobs.GetMetadataAsync(key);
                Console.WriteLine(md.ToString());
            }
        }

        static async Task ReadObjectContents()
        {
            Console.WriteLine("");
            Console.WriteLine("Retrieving object content");

            foreach (string key in _Keys)
            {
                byte[] data = await _Blobs.GetAsync(key);
                if (data != null)
                    Console.WriteLine("| " + key + ": " + Encoding.UTF8.GetString(data).Trim());
                else
                    Console.WriteLine("| " + key + ": (null)");
            }
        }

        static async Task DeleteNonEmptyDirectories()
        {
            Console.WriteLine("");
            Console.WriteLine("Deleting non-empty directories");

            try
            {
                await _Blobs.DeleteAsync("dir1/");
                Console.WriteLine("| This message indicates a failure for the operation on dir1/");
            }
            catch (Exception e)
            {
                Console.WriteLine("| Exception (expected): " + e.Message);
            }

            try
            {
                await _Blobs.DeleteAsync("dir1/dir2/");
                Console.WriteLine("| This message indicates a failure for the operation on dir1/dir2/");
            }
            catch (Exception e)
            {
                Console.WriteLine("| Exception (expected): " + e.Message);
            }

            try
            {
                await _Blobs.DeleteAsync("dir1/dir2/dir3/");
                Console.WriteLine("| This message indicates a failure for the operation on dir1/dir2/dir3/");
            }
            catch (Exception e)
            {
                Console.WriteLine("| Exception (expected): " + e.Message);
            }
        }

        static async Task DeleteFiles()
        {
            Console.WriteLine("");
            Console.WriteLine("Deleting files");

            /*

        /
        |-- root.txt
        |-- dir1
            |-- 1.txt
            |-- dir2
                |-- 2.txt
                |-- dir3
                    |-- 3.txt
                
             */

            await _Blobs.DeleteAsync("root.txt");
            await _Blobs.DeleteAsync("dir1/1.txt");
            await _Blobs.DeleteAsync("dir1/dir2/2.txt");
            await _Blobs.DeleteAsync("dir1/dir2/dir3/3.txt");
        }

        static async Task DeleteEmptyDirectories()
        {
            Console.WriteLine("");
            Console.WriteLine("Deleting empty directories");

            await _Blobs.DeleteAsync("dir1/dir2/dir3/");
            await _Blobs.DeleteAsync("dir1/dir2/");
            await _Blobs.DeleteAsync("dir1/");
        }

        static async Task CheckObjectExistence()
        {
            Console.WriteLine("");
            Console.WriteLine("Checking existence:");

            foreach (string key in _Keys)
            {
                Console.WriteLine("| " + key + ": " + await _Blobs.ExistsAsync(key));
            }
        }

        static async Task BuildObjectUrls()
        {
            Console.WriteLine("");
            Console.WriteLine("Building object URLs:");

            foreach (string key in _Keys)
            {
                Console.WriteLine("| " + key + ": " + _Blobs.GenerateUrl(key));
            }
        }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore IDE0059 // Unnecessary assignment of a value
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}