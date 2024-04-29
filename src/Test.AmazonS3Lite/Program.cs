namespace Test.AmazonS3Lite
{
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8629 // Nullable value type may be null.

    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.AmazonS3Lite;
    using Blobject.Core;
    using GetSomeInput;

    class Program
    {
        static AmazonS3LiteBlobClient _Client = null;
        static AwsSettings _Settings = null;
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
                        ContainerEmpty().Wait();
                        break;
                    case "md":
                        BlobMetadata().Wait();
                        break;
                    case "enum":
                        Enumerate().Wait();
                        break;
                    case "enumpfx":
                        EnumeratePrefix().Wait();
                        break;
                    case "url":
                        GenerateUrl();
                        break;
                }
            }
        }

        static void InitializeClient()
        {
            Console.WriteLine("For S3-compatible storage, endpoint should be of the form http://[hostname]:[port]/");
            string endpoint = Inputty.GetString("Endpoint   :", null, true);

            if (String.IsNullOrEmpty(endpoint))
            {
                _Settings = new AwsSettings(
                    Inputty.GetString("Access key :", null, false),
                    Inputty.GetString("Secret key :", null, false),
                    Inputty.GetString("Region     :", "us-west-1", false),
                    Inputty.GetString("Bucket     :", null, false)
                    );
            }
            else
            {
                _Settings = new AwsSettings(
                    endpoint,
                    Inputty.GetBoolean("SSL        :", true),
                    Inputty.GetString("Access key :", null, false),
                    Inputty.GetString("Secret key :", null, false),
                    Inputty.GetString("Region     :", "us-west-1", false),
                    Inputty.GetString("Bucket     :", null, false),
                    Inputty.GetString("Base URL   :", "http://localhost:8000/{bucket}/{key}", false)
                    );
            }
            _Client = new AmazonS3LiteBlobClient(_Settings);
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
            Console.WriteLine("  get          Get a BLOB");
            Console.WriteLine("  get stream   Get a BLOB using stream");
            Console.WriteLine("  write        Write a BLOB");
            Console.WriteLine("  del          Delete a BLOB");
            Console.WriteLine("  upload       Upload a BLOB from a file");
            Console.WriteLine("  download     Download a BLOB to a file");
            Console.WriteLine("  exists       Check if a BLOB exists");
            Console.WriteLine("  empty        Empty the container (destructive)");
            Console.WriteLine("  md           Retrieve BLOB metadata");
            Console.WriteLine("  enum         Enumerate a bucket");
            Console.WriteLine("  enumpfx      Enumerate a bucket by object prefix");
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
            byte[] data = await _Client.GetAsync(Inputty.GetString("Key:", null, false));
            if (data != null && data.Length > 0)
            {
                Console.WriteLine(Encoding.UTF8.GetString(data));
            }
        }

        static async Task ReadBlobStream()
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

        static async Task DeleteBlob()
        {
            await _Client.DeleteAsync(Inputty.GetString("Key:", null, false));
        }

        static async Task BlobExists()
        {
            Console.WriteLine(await _Client.ExistsAsync(Inputty.GetString("Key:", null, false)));
        }

        static async Task ContainerEmpty()
        {
            bool accept = Inputty.GetBoolean("Delete all objects in the container?", false);
            if (!accept) return;

            await _Client.EmptyAsync();
        }

        static async Task UploadBlob()
        {
            string filename = Inputty.GetString("Filename     :", null, false);
            string key = Inputty.GetString("Key          :", null, false);
            string contentType = Inputty.GetString("Content type :", null, true);

            FileInfo fi = new FileInfo(filename);
            long contentLength = fi.Length;

            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                await _Client.WriteAsync(key, contentType, contentLength, fs);
            }

            Console.WriteLine("Success");
        }

        static async Task DownloadBlob()
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

        static async Task BlobMetadata()
        {
            BlobMetadata md = await _Client.GetMetadataAsync(Inputty.GetString("Key:", null, false));
            Console.WriteLine("");
            Console.WriteLine(md.ToString());
        }

        static async Task Enumerate()
        {
            string prefix = Inputty.GetString("Prefix :", null, true);
            string token = Inputty.GetString("Token  :", null, true);

            EnumerationResult result = await _Client.EnumerateAsync(prefix, token);

            Console.WriteLine("");
            if (result.Blobs != null && result.Blobs.Count > 0)
            {
                foreach (BlobMetadata curr in result.Blobs)
                {
                    Console.WriteLine(
                        String.Format("{0,-27}", (curr.IsFolder ? "(dir) " : "") + curr.Key) +
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

        static async Task EnumeratePrefix()
        {
            EnumerationResult result = await _Client.EnumerateAsync(
                Inputty.GetString("Prefix :", null, true),
                Inputty.GetString("Token  :", null, true));

            if (result.Blobs != null && result.Blobs.Count > 0)
            {
                foreach (BlobMetadata curr in result.Blobs)
                {
                    Console.WriteLine(
                        String.Format("{0,-27}", (curr.IsFolder ? "(dir) " : "") + curr.Key) +
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
            Console.WriteLine(_Client.GenerateUrl(
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
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8629 // Nullable value type may be null.
}
