using System;
using BlobHelper;
using GetSomeInput;

namespace Test.Copy
{
    class Program
    {
        static BlobClient _From;
        static BlobClient _To;

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

        static BlobClient InitializeClient()
        {
            StorageType storageType = StorageType.Disk;
            bool runForever = true;
            while (runForever)
            {
                string str = Inputty.GetString("Storage type [aws azure disk kvp komodo]:", "disk", false);
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
                    case "komodo":
                        storageType = StorageType.Komodo;
                        runForever = false;
                        break;
                    case "kvp":
                        storageType = StorageType.Kvpbase;
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
                    return new BlobClient(aws);
                case StorageType.Azure:
                    AzureSettings azure = new AzureSettings(
                        Inputty.GetString("Account name :", null, false),
                        Inputty.GetString("Access key   :", null, false),
                        Inputty.GetString("Endpoint URL :", null, false),
                        Inputty.GetString("Container    :", null, false));
                    return new BlobClient(azure);
                case StorageType.Disk:
                    DiskSettings disk = new DiskSettings(
                        Inputty.GetString("Directory :", null, false));
                    return new BlobClient(disk);
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
