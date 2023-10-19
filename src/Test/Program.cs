using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlobHelper;
using GetSomeInput;

namespace Test
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
                    case "empty":
                        ContainerEmpty();
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
            Console.WriteLine("  empty        Empty the container (destructive)");
            Console.WriteLine("  md           Retrieve BLOB metadata");
            Console.WriteLine("  enum         Enumerate a bucket");
            Console.WriteLine("  enumpfx      Enumerate a bucket by object prefix");
            Console.WriteLine("  url          Generate a URL for an object by key");
            Console.WriteLine("");
        }

        static void WriteBlob()
        {
            try
            {
                string key =         Inputty.GetString("Key          :", null, false);
                string contentType = Inputty.GetString("Content type :", "text/plain", true);
                string data =        Inputty.GetString("Data         :", null, true);

                byte[] bytes = Array.Empty<byte>();
                if (!String.IsNullOrEmpty(data)) bytes = Encoding.UTF8.GetBytes(data);
                _Blobs.Write(key, contentType, bytes).Wait();

                Console.WriteLine("Success");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        static void ReadBlob()
        {
            byte[] data = _Blobs.Get(Inputty.GetString("Key:", null, false)).Result;
            if (data != null && data.Length > 0)
            {
                Console.WriteLine(Encoding.UTF8.GetString(data));
            }
        }

        static void ReadBlobStream()
        {
            BlobData data = _Blobs.GetStream(Inputty.GetString("Key:", null, false)).Result;
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
            _Blobs.Delete(Inputty.GetString("Key:", null, false)).Wait();
        }

        static void BlobExists()
        {
            Console.WriteLine(_Blobs.Exists(Inputty.GetString("Key:", null, false)).Result);
        }

        static void ContainerEmpty()
        {
            bool accept = Inputty.GetBoolean("Delete all objects in the container?", false);
            if (!accept) return;

            _Blobs.Empty().Wait();
        }

        static void UploadBlob()
        {
            string filename =    Inputty.GetString("Filename     :", null, false);
            string key =         Inputty.GetString("Key          :", null, false);
            string contentType = Inputty.GetString("Content type :", null, true);

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
            string key =      Inputty.GetString("Key      :", null, false);
            string filename = Inputty.GetString("Filename :", null, false);

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
            BlobMetadata md = _Blobs.GetMetadata(Inputty.GetString("Key:", null, false)).Result;
            Console.WriteLine("");
            Console.WriteLine(md.ToString());
        }

        static void Enumerate()
        {
            string prefix = Inputty.GetString("Prefix :", null, true);
            string token = Inputty.GetString("Token  :", null, true);

            EnumerationResult result = _Blobs.Enumerate(prefix, token).Result;

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
            Console.WriteLine("Count: " + result.Count);
            Console.WriteLine("Bytes: " + result.Bytes);
            Console.WriteLine("");
        }

        static void EnumeratePrefix()
        { 
            EnumerationResult result = _Blobs.Enumerate(
                Inputty.GetString("Prefix :", null, true),
                Inputty.GetString("Token  :", null, true)).Result;

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
            Console.WriteLine("Count: " + result.Count);
            Console.WriteLine("Bytes: " + result.Bytes);
            Console.WriteLine("");
        } 

        static void GenerateUrl()
        {
            Console.WriteLine(_Blobs.GenerateUrl(
                Inputty.GetString("Key:", "hello.txt", false)));
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
