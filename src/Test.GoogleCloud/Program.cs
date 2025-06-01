namespace Test.GoogleCloud
{
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8629 // Nullable value type may be null.

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.GoogleCloud;
    using Blobject.Core;
    using GetSomeInput;

    class TestGoogleCloudBlob
    {
        static GcpBlobClient _Client = null;
        static GcpBlobSettings _Settings = null;
        static bool _Debug = true;

        static void Main(string[] args)
        {
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
                    case "load":
                        LoadObjects().Wait();
                        break;
                    case "buckets":
                        ListBuckets().Wait();
                        break;
                    case "get":
                        ReadBlob().Wait();
                        break;
                    case "get stream":
                        ReadBlobStream().Wait();
                        break;
                    case "write":
                        WriteBlob().Wait();
                        break;
                    case "del":
                        DeleteBlob().Wait();
                        break;
                    case "upload":
                        UploadBlob().Wait();
                        break;
                    case "download":
                        DownloadBlob().Wait();
                        break;
                    case "exists":
                        BlobExists().Wait();
                        break;
                    case "empty":
                        BucketEmpty().Wait();
                        break;
                    case "md":
                        BlobMetadata().Wait();
                        break;
                    case "enum":
                        Enumerate();
                        break;
                    case "url":
                        GenerateUrl();
                        break;
                }
            }
        }

        static void InitializeClient()
        {
            string projectId = Inputty.GetString("Project ID:", null, false);
            string bucket = Inputty.GetString("Bucket name:", null, false);

            Console.WriteLine("");
            Console.WriteLine("Service Account JSON credentials:");
            Console.WriteLine("1. Paste the JSON content directly (single line)");
            Console.WriteLine("2. Provide a path to the JSON file");
            Console.WriteLine("");

            string jsonInput = Inputty.GetString("JSON credentials or file path:", null, false);
            string jsonCredentials = jsonInput;

            // Check if input is a file path
            if (File.Exists(jsonInput))
            {
                jsonCredentials = File.ReadAllText(jsonInput);
                Console.WriteLine("Loaded credentials from file: " + jsonInput);
            }
            else if (!jsonInput.Trim().StartsWith("{"))
            {
                Console.WriteLine("Warning: Input doesn't appear to be JSON or a valid file path.");
                Console.WriteLine("Treating as JSON credentials string.");
            }

            // Optional custom endpoint
            string customEndpoint = Inputty.GetString("Custom endpoint (optional, press Enter for default):", null, true);

            if (String.IsNullOrEmpty(customEndpoint))
            {
                _Settings = new GcpBlobSettings(projectId, bucket, jsonCredentials);
            }
            else
            {
                _Settings = new GcpBlobSettings(projectId, bucket, jsonCredentials, customEndpoint);
            }

            _Client = new GcpBlobClient(_Settings);
            if (_Debug) _Client.Logger = Console.WriteLine;
        }

        static void Menu()
        {
            Console.WriteLine("");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  ?            Help, this menu");
            Console.WriteLine("  cls          Clear the screen");
            Console.WriteLine("  q            Quit");
            Console.WriteLine("  load         Load a number of small objects");
            Console.WriteLine("  buckets      List the buckets available in the project");
            Console.WriteLine("  get          Get a BLOB");
            Console.WriteLine("  get stream   Get a BLOB using stream");
            Console.WriteLine("  write        Write a BLOB");
            Console.WriteLine("  del          Delete a BLOB");
            Console.WriteLine("  upload       Upload a BLOB from a file");
            Console.WriteLine("  download     Download a BLOB to a file");
            Console.WriteLine("  exists       Check if a BLOB exists");
            Console.WriteLine("  empty        Empty the bucket (destructive)");
            Console.WriteLine("  md           Retrieve BLOB metadata");
            Console.WriteLine("  enum         Enumerate a bucket");
            Console.WriteLine("  url          Generate a URL for an object by key");
            Console.WriteLine("");
        }

        static async Task LoadObjects()
        {
            int count = Inputty.GetInteger("Count:", 2000, true, true);
            if (count < 1) return;

            for (int i = 0; i < count; i++)
            {
                Console.Write("\rLoading object " + i.ToString() + "...");
                await _Client.WriteAsync(i.ToString(), "text/plain", "Hello, world!");
            }

            Console.WriteLine("");
        }

        static async Task ListBuckets()
        {
            try
            {
                List<string> buckets = await _Client.ListBuckets();
                if (buckets != null && buckets.Count > 0)
                {
                    Console.WriteLine("");
                    Console.WriteLine("Buckets in project " + _Settings.ProjectId + ":");
                    foreach (string bucket in buckets) Console.WriteLine("| " + bucket);
                    Console.WriteLine("");
                }
                else
                {
                    Console.WriteLine("(none)");
                    Console.WriteLine("");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error listing buckets: " + e.Message);
            }
        }

        static async Task WriteBlob()
        {
            try
            {
                string key = Inputty.GetString("Key          :", null, false);
                string contentType = Inputty.GetString("Content type :", "text/plain", true);
                string data = Inputty.GetString("Data         :", null, true);

                byte[] bytes = Array.Empty<byte>();
                if (!String.IsNullOrEmpty(data)) bytes = Encoding.UTF8.GetBytes(data);
                await _Client.WriteAsync(key, contentType, bytes);

                Console.WriteLine("Success");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        static async Task ReadBlob()
        {
            try
            {
                byte[] data = await _Client.GetAsync(Inputty.GetString("Key:", null, false));
                if (data != null && data.Length > 0)
                {
                    Console.WriteLine(Encoding.UTF8.GetString(data));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        static async Task ReadBlobStream()
        {
            try
            {
                BlobData data = await _Client.GetStreamAsync(Inputty.GetString("Key:", null, false));
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
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        static async Task DeleteBlob()
        {
            try
            {
                await _Client.DeleteAsync(Inputty.GetString("Key:", null, false));
                Console.WriteLine("Success");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        static async Task BlobExists()
        {
            try
            {
                Console.WriteLine(await _Client.ExistsAsync(Inputty.GetString("Key:", null, false)));
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        static async Task BucketEmpty()
        {
            bool accept = Inputty.GetBoolean("Delete all objects in the bucket?", false);
            if (!accept) return;

            try
            {
                EmptyResult result = await _Client.EmptyAsync();
                Console.WriteLine("Deleted " + result.Blobs.Count + " objects");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        static async Task UploadBlob()
        {
            try
            {
                string filename = Inputty.GetString("Filename     :", null, false);
                string key = Inputty.GetString("Key          :", null, false);
                string contentType = Inputty.GetString("Content type :", "application/octet-stream", true);

                FileInfo fi = new FileInfo(filename);
                long contentLength = fi.Length;

                using (FileStream fs = new FileStream(filename, FileMode.Open))
                {
                    await _Client.WriteAsync(key, contentType, contentLength, fs);
                }

                Console.WriteLine("Success");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        static async Task DownloadBlob()
        {
            try
            {
                string key = Inputty.GetString("Key      :", null, false);
                string filename = Inputty.GetString("Filename :", null, false);

                BlobData blob = await _Client.GetStreamAsync(key);
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
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        static async Task BlobMetadata()
        {
            try
            {
                BlobMetadata md = await _Client.GetMetadataAsync(Inputty.GetString("Key:", null, false));
                Console.WriteLine("");
                Console.WriteLine(md.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        static void Enumerate()
        {
            try
            {
                EnumerationFilter filter = BuildEnumerationFilter();

                int count = 0;
                long bytes = 0;

                Console.WriteLine("");
                foreach (BlobMetadata curr in _Client.Enumerate(filter))
                {
                    Console.WriteLine(
                        String.Format("{0,-27}", (curr.IsFolder ? "(dir) " : "") + curr.Key) +
                        String.Format("{0,-18}", curr.ContentLength.ToString() + " bytes") +
                        String.Format("{0,-30}", curr.CreatedUtc.Value.ToString("yyyy-MM-dd HH:mm:ss")));

                    count += 1;
                    bytes += curr.ContentLength;
                }

                Console.WriteLine("");
                Console.WriteLine("Count: " + count);
                Console.WriteLine("Bytes: " + bytes);
                Console.WriteLine("");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        static void GenerateUrl()
        {
            Console.WriteLine(_Client.GenerateUrl(
                Inputty.GetString("Key:", "hello.txt", false)));
        }

        static EnumerationFilter BuildEnumerationFilter()
        {
            EnumerationFilter ret = new EnumerationFilter
            {
                MinimumSize = Inputty.GetInteger("Minimum size :", 0, true, true),
                MaximumSize = Inputty.GetInteger("Maximum size :", Int32.MaxValue, true, true),
                Prefix = Inputty.GetString("Prefix       :", null, true),
                Suffix = Inputty.GetString("Suffix       :", null, true)
            };

            return ret;
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
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8629 // Nullable value type may be null.
}