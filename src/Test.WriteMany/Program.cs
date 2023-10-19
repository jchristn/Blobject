using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlobHelper;
using GetSomeInput;

namespace Test.WriteMany
{
    class Program
    {
        static StorageType _StorageType;
        static BlobClient _Blobs;
        static AwsSettings _AwsSettings;
        static AzureSettings _AzureSettings;
        static DiskSettings _DiskSettings;

        static void Main(string[] args)
        {
            SetStorageType();
            InitializeClient();

            int count = Inputty.GetInteger("Count:", 1000, true, false);

            List<WriteRequest> writes = new List<WriteRequest>();
            for (int i = 0; i < count; i++)
            {
                string guid = Guid.NewGuid().ToString();
                writes.Add(new WriteRequest(guid, "application/octet-stream", Encoding.UTF8.GetBytes(guid)));
            }

            Console.WriteLine("Performing " + count + " write(s)");
            _Blobs.WriteMany(writes).Wait();
        }

        static void SetStorageType()
        {
            bool runForever = true;
            while (runForever)
            {
                string storageType = Inputty.GetString("Storage type [aws azure disk kvp komodo]:", "disk", false);
                switch (storageType)
                {
                    case "aws":
                        _StorageType = StorageType.AwsS3;
                        runForever = false;
                        break;
                    case "azure":
                        _StorageType = StorageType.Azure;
                        runForever = false;
                        break;
                    case "disk":
                        _StorageType = StorageType.Disk;
                        runForever = false;
                        break;
                    case "komodo":
                        _StorageType = StorageType.Komodo;
                        runForever = false;
                        break;
                    case "kvp":
                        _StorageType = StorageType.Kvpbase;
                        runForever = false;
                        break;
                    default:
                        Console.WriteLine("Unknown answer: " + storageType);
                        break;
                }
            }
        }

        static void InitializeClient()
        {
            switch (_StorageType)
            {
                case StorageType.AwsS3:
                    Console.WriteLine("For S3-compatible storage, endpoint should be of the form http://[hostname]:[port]/");
                    string endpoint = Inputty.GetString("Endpoint   :", null, true);

                    if (String.IsNullOrEmpty(endpoint))
                    {
                        _AwsSettings = new AwsSettings(
                           Inputty.GetString("Access key :", null, false),
                           Inputty.GetString("Secret key :", null, false),
                           Inputty.GetString("Region     :", "USWest1", false),
                           Inputty.GetString("Bucket     :", null, false)
                           );
                    }
                    else
                    {
                        _AwsSettings = new AwsSettings(
                            endpoint,
                            Inputty.GetBoolean("SSL        :", true),
                            Inputty.GetString("Access key :", null, false),
                            Inputty.GetString("Secret key :", null, false),
                            Inputty.GetString("Region     :", "USWest1", false),
                            Inputty.GetString("Bucket     :", null, false),
                            Inputty.GetString("Base URL   :", "http://localhost:8000/{bucket}/{key}", false)
                            );
                    }
                    _Blobs = new BlobClient(_AwsSettings);
                    break;
                case StorageType.Azure:
                    _AzureSettings = new AzureSettings(
                        Inputty.GetString("Account name :", null, false),
                        Inputty.GetString("Access key   :", null, false),
                        Inputty.GetString("Endpoint URL :", null, false),
                        Inputty.GetString("Container    :", null, false));
                    _Blobs = new BlobClient(_AzureSettings);
                    break;
                case StorageType.Disk:
                    _DiskSettings = new DiskSettings(
                        Inputty.GetString("Directory :", null, false));
                    _Blobs = new BlobClient(_DiskSettings);
                    break;
                default:
                    throw new ArgumentException("Unknown storage type: '" + _StorageType + "'.");
            }
        }
    }
}
