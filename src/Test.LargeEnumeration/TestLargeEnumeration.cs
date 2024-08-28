namespace Test.LargeEnumeration
{
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
        static int _NumFiles = 5000;
        static bool _Logging = true;
        static bool _Cleanup = false;

        private static List<string> _ValidStorageTypes = new List<string>
        {
            "aws",
            "awslite",
            "azure",
            "cifs",
            "disk",
            "nfs"
        };

        private static Serializer _Serializer = new Serializer();

        public static async Task Main(string[] args)
        {
            InitializeClient();

            // await CreateFiles();
            await Enumerate("Post create enumeration");

            if (_Cleanup) await DeleteFiles();
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

        static async Task CreateFiles()
        {
            Console.WriteLine("");
            Console.WriteLine("Creating " + _NumFiles + " files");

            for (int i = 0; i < _NumFiles; i++)
            {
                if (!await _Blobs.ExistsAsync(i.ToString()))
                {
                    await _Blobs.WriteAsync(i.ToString(), "text/plain", Encoding.UTF8.GetBytes(i.ToString()));
                    Console.Write(i.ToString() + " ");
                }
            }

            Console.WriteLine("");
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
            if (minSize != null) Console.WriteLine("| Using min size : " + minSize);
            if (maxSize != null) Console.WriteLine("| Using max size : " + maxSize);

            EnumerationFilter ef = new EnumerationFilter
            {
                MinimumSize = (minSize != null ? minSize.Value : 0),
                MaximumSize = (maxSize != null ? maxSize.Value : long.MaxValue),
                Prefix = prefix,
                Suffix = suffix
            };

            IEnumerable<BlobMetadata> blobs = _Blobs.Enumerate(ef);

            int count = blobs.Count();

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

            Console.WriteLine(Environment.NewLine + count + " BLOBs enumerated");
        }

        static async Task DeleteFiles()
        {
            Console.WriteLine("");
            Console.WriteLine("Deleting files");

            for (int i = 0; i < _NumFiles; i++)
            {
                await _Blobs.DeleteAsync(i.ToString());
            }
        }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore IDE0059 // Unnecessary assignment of a value
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}