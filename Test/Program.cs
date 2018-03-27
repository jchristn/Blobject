using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlobHelper;

namespace Test
{
    class Program
    {
        static StorageType _StorageType;
        static Blobs _Blobs;
        static AwsSettings _AwsSettings;
        static AzureSettings _AzureSettings;
        static DiskSettings _DiskSettings;
        static KvpbaseSettings _KvpbaseSettings;

        static void Main(string[] args)
        {
            SetStorageType();
            InitializeClient();

            bool runForever = true;
            while (runForever)
            {
                byte[] data = null;
                string url = null;
                string cmd = InputString("Command [? for help]:", null, false);
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
                    case "get":
                        _Blobs.Get(
                            InputString("ID:", null, false),
                            out data);
                        if (data != null && data.Length > 0)
                        {
                            Console.WriteLine(Encoding.UTF8.GetString(data));
                        }
                        break;
                    case "write":
                        _Blobs.Write(
                            InputString("ID:", null, false),
                            false,
                            InputString("Data:", null, false),
                            out url);
                        if (!String.IsNullOrEmpty(url))
                        {
                            Console.WriteLine("URL: " + url);
                        }
                        break;
                    case "del":
                        _Blobs.Delete(
                            InputString("ID:", null, false));
                        break; 
                }
            }
        }

        static void SetStorageType()
        {
            bool runForever = true;
            while (runForever)
            {
                string storageType = InputString("Storage type [aws azure disk kvp]:", "disk", false);
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
                    _AwsSettings = new AwsSettings(
                        InputString("Access key :", null, false),
                        InputString("Secret key :", null, false),
                        InputString("Region     :", "USWest1", false),
                        InputString("Bucket     :", null, false));
                    _Blobs = new Blobs(_AwsSettings);
                    break;
                case StorageType.Azure:
                    _AzureSettings = new AzureSettings(
                        InputString("Account name :", null, false),
                        InputString("Access key   :", null, false),
                        InputString("Endpoint URL :", null, false),
                        InputString("Container    :", null, false));
                    _Blobs = new Blobs(_AzureSettings);
                    break;
                case StorageType.Disk:
                    _DiskSettings = new DiskSettings(
                        InputString("Directory :", null, false));
                    _Blobs = new Blobs(_DiskSettings);
                    break;
                case StorageType.Kvpbase:
                    _KvpbaseSettings = new KvpbaseSettings(
                        InputString("Endpoint URL :", null, false),
                        InputString("User GUID    :", null, false),
                        InputString("Container    :", null, true),
                        InputString("API key      :", null, false));
                    _Blobs = new Blobs(_KvpbaseSettings);
                    break;
            }
        }

        static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!String.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (!String.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        } 

        static void Menu()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  ?       help, this menu");
            Console.WriteLine("  cls     clear the screen");
            Console.WriteLine("  q       quit");
            Console.WriteLine("  get     get a BLOB");
            Console.WriteLine("  write   write a BLOB");
            Console.WriteLine("  del     delete a BLOB");
        }
    }
}
