using System;
using BlobHelper;
using Newtonsoft.Json;

namespace Test.Copy
{
    class Program
    {
        static Blobs _From;
        static Blobs _To;

        static void Main(string[] args)
        {
            Console.WriteLine("Provide storage settings for the source");
            _From = InitializeClient();
            Console.WriteLine("Provide storage settings for the destination:");
            _To   = InitializeClient();

            bool runForever = true;
            while (runForever)
            {
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
                    case "go":
                        StartCopy();
                        break;
                }
            }
        }

        static Blobs InitializeClient()
        {
            StorageType storageType = StorageType.Disk;
            bool runForever = true;
            while (runForever)
            {
                string str = InputString("Storage type [aws azure disk kvp komodo]:", "disk", false);
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
                    string endpoint = InputString("Endpoint   :", null, true);
                    AwsSettings aws = null;
                    if (String.IsNullOrEmpty(endpoint))
                    {
                        aws = new AwsSettings(
                           InputString("Access key :", null, false),
                           InputString("Secret key :", null, false),
                           InputString("Region     :", "USWest1", false),
                           InputString("Bucket     :", null, false)
                           );
                    }
                    else
                    {
                        aws = new AwsSettings(
                            endpoint,
                            InputBoolean("SSL        :", true),
                            InputString("Access key :", null, false),
                            InputString("Secret key :", null, false),
                            InputString("Region     :", "USWest1", false),
                            InputString("Bucket     :", null, false),
                            InputString("Base URL   :", "http://localhost:8000/{bucket}/{key}", false)
                            );
                    }
                    return new Blobs(aws);
                case StorageType.Azure:
                    AzureSettings azure = new AzureSettings(
                        InputString("Account name :", null, false),
                        InputString("Access key   :", null, false),
                        InputString("Endpoint URL :", null, false),
                        InputString("Container    :", null, false));
                    return new Blobs(azure);
                case StorageType.Disk:
                    DiskSettings disk = new DiskSettings(
                        InputString("Directory :", null, false));
                    return new Blobs(disk);
                case StorageType.Komodo:
                    KomodoSettings komodo = new KomodoSettings(
                        InputString("Endpoint URL :", "http://localhost:9090/", false),
                        InputString("Index GUID   :", "default", false),
                        InputString("API key      :", "default", false));
                    return new Blobs(komodo);
                case StorageType.Kvpbase:
                    KvpbaseSettings kvpbase = new KvpbaseSettings(
                        InputString("Endpoint URL :", "http://localhost:8000/", false),
                        InputString("User GUID    :", "default", false),
                        InputString("Container    :", "default", true),
                        InputString("API key      :", "default", false));
                    return new Blobs(kvpbase);
                default:
                    throw new ArgumentException("Unknown storage type: '" + storageType + "'.");
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

        static bool InputBoolean(string question, bool yesDefault)
        {
            Console.Write(question);

            if (yesDefault) Console.Write(" [Y/n]? ");
            else Console.Write(" [y/N]? ");

            string userInput = Console.ReadLine();

            if (String.IsNullOrEmpty(userInput))
            {
                if (yesDefault) return true;
                return false;
            }

            userInput = userInput.ToLower();

            if (yesDefault)
            {
                if (
                    (String.Compare(userInput, "n") == 0)
                    || (String.Compare(userInput, "no") == 0)
                   )
                {
                    return false;
                }

                return true;
            }
            else
            {
                if (
                    (String.Compare(userInput, "y") == 0)
                    || (String.Compare(userInput, "yes") == 0)
                   )
                {
                    return true;
                }

                return false;
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
            string prefix = InputString("Prefix:", null, true);
            BlobCopy copy = new BlobCopy(_From, _To, prefix);
            CopyStatistics stats = copy.Start().Result;
            if (stats == null)
            {
                Console.WriteLine("(null)");
            }
            else
            {
                Console.WriteLine(SerializeJson(stats, true));
            }
        }

        static string SerializeJson(object obj, bool pretty)
        {
            if (obj == null) return null;
            string json;

            if (pretty)
            {
                json = JsonConvert.SerializeObject(
                  obj,
                  Newtonsoft.Json.Formatting.Indented,
                  new JsonSerializerSettings
                  {
                      NullValueHandling = NullValueHandling.Ignore,
                      DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                  });
            }
            else
            {
                json = JsonConvert.SerializeObject(obj,
                  new JsonSerializerSettings
                  {
                      NullValueHandling = NullValueHandling.Ignore,
                      DateTimeZoneHandling = DateTimeZoneHandling.Utc
                  });
            }

            return json;
        }

    }
}
