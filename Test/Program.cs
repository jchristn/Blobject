using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlobHelper;

namespace TestNetCore
{
    class Program
    {
        static StorageType _StorageType;
        static Blobs _Blobs;
        static AwsSettings _AwsSettings;
        static AzureSettings _AzureSettings;
        static DiskSettings _DiskSettings;
        static KomodoSettings _KomodoSettings;
        static KvpbaseSettings _KvpbaseSettings;

        static void Main(string[] args)
        {
            SetStorageType();
            InitializeClient();

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
                    case "get":
                        ReadBlob();
                        break;
                    case "get stream":
                        ReadBlobStream();
                        break;
                    case "write":
                        WriteBlob();
                        break;
                    case "del":
                        DeleteBlob();
                        break;
                    case "upload":
                        UploadBlob();
                        break;
                    case "download":
                        DownloadBlob();
                        break;
                    case "exists":
                        BlobExists();
                        break;
                    case "md":
                        BlobMetadata();
                        break;
                    case "enum":
                        Enumerate();
                        break;
                    case "enumpfx":
                        EnumeratePrefix();
                        break;
                    case "url":
                        GenerateUrl();
                        break;
                }
            }
        }

        static void SetStorageType()
        {
            bool runForever = true;
            while (runForever)
            {
                string storageType = InputString("Storage type [aws azure disk kvp komodo]:", "disk", false);
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
                    string endpoint = InputString("Endpoint   :", null, true);

                    if (String.IsNullOrEmpty(endpoint))
                    {
                        _AwsSettings = new AwsSettings( 
                           InputString("Access key :", null, false),
                           InputString("Secret key :", null, false),
                           InputString("Region     :", "USWest1", false),
                           InputString("Bucket     :", null, false) 
                           ); 
                    }
                    else
                    {
                        _AwsSettings = new AwsSettings(
                            endpoint,
                            InputBoolean("SSL        :", true),
                            InputString("Access key :", null, false),
                            InputString("Secret key :", null, false),
                            InputString("Region     :", "USWest1", false),
                            InputString("Bucket     :", null, false),
                            InputString("Base URL   :", "http://localhost:8000/{bucket}/{key}", false)
                            );
                    }
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
                case StorageType.Komodo:
                    _KomodoSettings = new KomodoSettings(
                        InputString("Endpoint URL :", "http://localhost:9090/", false),
                        InputString("Index GUID   :", "default", false),
                        InputString("API key      :", "default", false));
                    _Blobs = new Blobs(_KomodoSettings);
                    break;
                case StorageType.Kvpbase:
                    _KvpbaseSettings = new KvpbaseSettings(
                        InputString("Endpoint URL :", "http://localhost:8000/", false),
                        InputString("User GUID    :", "default", false),
                        InputString("Container    :", "default", true),
                        InputString("API key      :", "default", false));
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
            Console.WriteLine("  get          Get a BLOB");
            Console.WriteLine("  get stream   Get a BLOB using stream");
            Console.WriteLine("  write        Write a BLOB");
            Console.WriteLine("  del          Delete a BLOB");
            Console.WriteLine("  upload       Upload a BLOB from a file");
            Console.WriteLine("  download     Download a BLOB from a file");
            Console.WriteLine("  exists       Check if a BLOB exists");
            Console.WriteLine("  md           Retrieve BLOB metadata");
            Console.WriteLine("  enum         Enumerate a bucket");
            Console.WriteLine("  enumpfx      Enumerate a bucket by object prefix");
            Console.WriteLine("  url          Generate a URL for an object by key");
            Console.WriteLine("");
        }

        static void WriteBlob()
        {
            string key = InputString("Key:", null, false);
            string contentType = InputString("Content type:", "text/plain", true);
            string data = InputString("Data:", null, true);

            byte[] bytes = new byte[0];
            if (!String.IsNullOrEmpty(data)) bytes = Encoding.UTF8.GetBytes(data);
            _Blobs.Write(key, contentType, bytes).Wait();

            Console.WriteLine("Success");
        }

        static void ReadBlob()
        {
            byte[] data = _Blobs.Get(InputString("Key:", null, false)).Result;
            if (data != null && data.Length > 0)
            {
                Console.WriteLine(Encoding.UTF8.GetString(data));
            }
        }

        static void ReadBlobStream()
        {
            BlobData data = _Blobs.GetStream(InputString("Key:", null, false)).Result;
            if (data != null)
            {
                Console.WriteLine("Length: " + data.ContentLength);
                if (data.Data != null && data.Data.CanRead && data.ContentLength > 0)
                {
                    byte[] bytes = ReadToEnd(data.Data);
                    Console.WriteLine(Encoding.UTF8.GetString(bytes));
                }
            }
        }

        static void DeleteBlob()
        {
            _Blobs.Delete(InputString("Key:", null, false)).Wait();
        }

        static void BlobExists()
        {
            Console.WriteLine(_Blobs.Exists(InputString("Key:", null, false)).Result);
        }

        static void UploadBlob()
        {
            string filename = InputString("Filename:", null, false);
            string key = InputString("Key:", null, false);
            string contentType = InputString("Content type:", null, true);

            FileInfo fi = new FileInfo(filename);
            long contentLength = fi.Length;

            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                _Blobs.Write(key, contentType, contentLength, fs).Wait();
            }

            Console.WriteLine("Success");
        }

        static void DownloadBlob()
        {
            string key = InputString("Key:", null, false);
            string filename = InputString("Filename:", null, false);

            BlobData blob = _Blobs.GetStream(key).Result;
            using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
            {
                int bytesRead = 0;
                long bytesRemaining = blob.ContentLength;
                byte[] buffer = new byte[65536];

                while (bytesRemaining > 0)
                {
                    bytesRead = blob.Data.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        fs.Write(buffer, 0, bytesRead);
                        bytesRemaining -= bytesRead;
                    }
                }
            }

            Console.WriteLine("Success");
        }

        static void BlobMetadata()
        {
            BlobMetadata md = _Blobs.GetMetadata(InputString("Key:", null, false)).Result;
            Console.WriteLine("");
            Console.WriteLine(md.ToString());
        }

        static void Enumerate()
        { 
            EnumerationResult result = _Blobs.Enumerate(InputString("Continuation token:", null, true)).Result;

            Console.WriteLine("");
            if (result.Blobs != null && result.Blobs.Count > 0)
            {
                foreach (BlobMetadata curr in result.Blobs)
                {
                    Console.WriteLine(
                        String.Format("{0,-27}", curr.Key) +
                        String.Format("{0,-18}", curr.ContentLength.ToString() + " bytes") +
                        String.Format("{0,-30}", curr.CreatedUtc.Value.ToString("yyyy-MM-dd HH:mm:ss")));
                }
            }
            else
            {
                Console.WriteLine("(none)");
            }

            if (!String.IsNullOrEmpty(result.NextContinuationToken))
                Console.WriteLine("Continuation token: " + result.NextContinuationToken);

            Console.WriteLine("");
        }

        static void EnumeratePrefix()
        { 
            EnumerationResult result = _Blobs.Enumerate(
                InputString("Prefix:", null, true),
                InputString("Continuation token:", null, true)).Result;

            if (result.Blobs != null && result.Blobs.Count > 0)
            {
                foreach (BlobMetadata curr in result.Blobs)
                {
                    Console.WriteLine(
                        String.Format("{0,-27}", curr.Key) +
                        String.Format("{0,-18}", curr.ContentLength.ToString() + " bytes") +
                        String.Format("{0,-30}", curr.CreatedUtc.Value.ToString("yyyy-MM-dd HH:mm:ss")));
                }
            }
            else
            {
                Console.WriteLine("(none)");
            }

            if (!String.IsNullOrEmpty(result.NextContinuationToken))
                Console.WriteLine("Continuation token: " + result.NextContinuationToken);
        } 

        static void GenerateUrl()
        {
            Console.WriteLine(_Blobs.GenerateUrl(
                InputString("Key:", "hello.txt", false)));
        }

        private static byte[] ReadToEnd(Stream stream)
        {
            long originalPosition = 0;

            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }
    }
}
