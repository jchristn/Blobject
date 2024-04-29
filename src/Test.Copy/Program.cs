namespace Test.Copy
{
    using System;
    using System.Net;
    using Blobject.AmazonS3;
    using Blobject.AzureBlob;
    using Blobject.CIFS;
    using Blobject.Core;
    using Blobject.Disk;
    using Blobject.NFS;
    using GetSomeInput;

    class Program
    {
        static IBlobClient _From;
        static IBlobClient _To;

        static void Main(string[] args)
        {
            Console.WriteLine("Provide storage settings for the source");
            _From = InitializeClient();
            Console.WriteLine("Provide storage settings for the destination:");
            _To   = InitializeClient();

            bool runForever = true;
            while (runForever)
            {
                string cmd = Inputty.GetString("Command [? for help]:", null, false);
                switch (cmd)
                {
                    case "?":
                        Menu();
                        break;
                    case "q":
                        runForever = false;
                        break;
                    case "c":
                    case "cls":
                    case "clear":
                        Console.Clear();
                        break;
                    case "go":
                        StartCopy();
                        break;
                }
            }
        }

        static IBlobClient InitializeClient()
        {
            StorageType storageType = StorageType.Disk;
            bool runForever = true;
            while (runForever)
            {
                string str = Inputty.GetString("Storage type [aws azure disk cifs nfs]:", "disk", false);
                switch (str)
                {
                    case "aws":
                        storageType = StorageType.AwsS3;
                        runForever = false;
                        break;
                    case "azure":
                        storageType = StorageType.Azure;
                        runForever = false;
                        break;
                    case "disk":
                        storageType = StorageType.Disk;
                        runForever = false;
                        break;
                    case "cifs":
                        storageType = StorageType.CIFS;
                        runForever = false;
                        break;
                    case "nfs":
                        storageType = StorageType.NFS;
                        runForever = false;
                        break;
                    default:
                        Console.WriteLine("Unknown answer: " + storageType);
                        break;
                }
            }

            switch (storageType)
            {
                case StorageType.AwsS3:
                    Console.WriteLine("For S3-compatible storage, endpoint should be of the form http://[hostname]:[port]/");
                    string endpoint = Inputty.GetString("Endpoint   :", null, true);
                    AwsSettings aws = null;
                    if (String.IsNullOrEmpty(endpoint))
                    {
                        aws = new AwsSettings(
                           Inputty.GetString("Access key :", null, false),
                           Inputty.GetString("Secret key :", null, false),
                           Inputty.GetString("Region     :", "USWest1", false),
                           Inputty.GetString("Bucket     :", null, false)
                           );
                    }
                    else
                    {
                        aws = new AwsSettings(
                            endpoint,
                            Inputty.GetBoolean("SSL        :", true),
                            Inputty.GetString("Access key :", null, false),
                            Inputty.GetString("Secret key :", null, false),
                            Inputty.GetString("Region     :", "USWest1", false),
                            Inputty.GetString("Bucket     :", null, false),
                            Inputty.GetString("Base URL   :", "http://localhost:8000/{bucket}/{key}", false)
                            );
                    }
                    return new AmazonS3BlobClient(aws);

                case StorageType.Azure:
                    AzureBlobSettings azure = new AzureBlobSettings(
                        Inputty.GetString("Account name :", null, false),
                        Inputty.GetString("Access key   :", null, false),
                        Inputty.GetString("Endpoint URL :", null, false),
                        Inputty.GetString("Container    :", null, false));
                    return new AzureBlobClient(azure);

                case StorageType.CIFS:
                    CifsSettings cifs = new CifsSettings(
                        IPAddress.Parse(Inputty.GetString("IP Address :", null, false)),
                                        Inputty.GetString("Username   :", null, false),
                                        Inputty.GetString("Password   :", null, false),
                                        Inputty.GetString("Share      :", null, false));
                    return new CifsBlobClient(cifs);

                case StorageType.Disk:
                    DiskSettings disk = new DiskSettings(
                        Inputty.GetString("Directory :", null, false));
                    return new DiskBlobClient(disk);

                case StorageType.NFS:
                    NfsSettings nfs = new NfsSettings(
                        IPAddress.Parse(Inputty.GetString("IP Address :", null, false)),
                                        Inputty.GetInteger("User ID    :", 0, false, true),
                                        Inputty.GetInteger("Group ID   :", 0, false, true),
                                        Inputty.GetString("Share      :", null, false),
                                        (NfsVersionEnum)(Enum.Parse(typeof(NfsVersionEnum), Inputty.GetString("Version    :", "V3", false))));
                    return new NfsBlobClient(nfs);

                default:
                    throw new ArgumentException("Unknown storage type: '" + storageType + "'.");
            }
        }

        static void Menu()
        {
            Console.WriteLine("");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  ?            Help, this menu");
            Console.WriteLine("  cls          Clear the screen");
            Console.WriteLine("  q            Quit");
            Console.WriteLine("  go           Perform the copy operation");
            Console.WriteLine("");
        }

        static void StartCopy()
        {
            string prefix = Inputty.GetString("Prefix:", null, true);
            BlobCopy copy = new BlobCopy(_From, _To, prefix);
            CopyStatistics stats = copy.Start().Result;
            if (stats == null)
            {
                Console.WriteLine("(null)");
            }
            else
            {
                Console.WriteLine(stats.ToString());
            }
        }
    }
}
