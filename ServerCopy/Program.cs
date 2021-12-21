namespace ImageStorageCopier
{
    using System;
    using System.Collections.Generic;

    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Copy stored objects between servers");
            Console.WriteLine(String.Empty);
            var helperFrom = new Server();
            helperFrom.GetInfo("Source Server");
            if (!helperFrom.valid())
            {
                Console.WriteLine(String.Empty);
                Console.WriteLine("No Source Server specified, exiting...");
                return;
            }
            Console.WriteLine(String.Empty);
            var helperTo = new Server();
            helperTo.GetInfo("Destination Server");
            if (!helperTo.valid())
            {
                Console.WriteLine(String.Empty);
                Console.WriteLine("No Destination Server specified, exiting...");
                return;
            }

            Console.WriteLine(String.Empty);
            Console.WriteLine("Confirm Copy Object Storage");
            Console.WriteLine(String.Empty);
            Console.WriteLine(helperFrom + " => " + helperTo);

            string input = String.Empty;
            while (input != "Yes" && input != "Exit")
            {
                Console.WriteLine(String.Empty);
                Console.WriteLine("Enter 'Yes' to continue or 'Exit' to quit");
                input = Console.ReadLine();
            }
            if (input == "Exit")
            {
                Console.WriteLine(String.Empty);
                Console.WriteLine("Exiting");

                return;
            }

            var fromBlobs = helperFrom.GetBlobs();
            var toBlobs = helperTo.GetBlobs();

            var from = fromBlobs.Enumerate().Result;

            if (from.Blobs != null && from.Blobs.Count > 0)
            {
                foreach (var curr in from.Blobs)
                {
                    toBlobs.Write(curr.Key, curr.ContentType, fromBlobs.Get(curr.Key).Result).Wait();

                    Console.WriteLine(curr.Key);
                }
                Console.WriteLine(from.Count + " items copied from " + helperFrom + " to " + helperTo);
            }
            else
            {
                Console.WriteLine("Server " + helperFrom + " appears to be empty.");
            }

            // is this needed? When would I get a continuation token?
            if (!String.IsNullOrEmpty(from.NextContinuationToken))
            {
                Console.WriteLine("Continuation token: " + from.NextContinuationToken);
            }
        }
    }

    internal class Server
    {
        private Dictionary<string, string[]> allowedSettings = new Dictionary<string, string[]>()
        {
            ["Aws"] = new string[] { "AccessKey", "SecretKey", "Region", "Bucket" },
            ["Azure"] = new string[] { "AccountName", "AccessKey", "Endpoint", "Container" },
            ["Disk"] = new string[] { "Directory" },
            ["Komodo"] = new string[] { "Endpoint", "IndexGUID", "ApiKey" },
            ["Kvpbase"] = new string[] { "Endpoint", "UserGuid", "Container", "ApiKey" }
        };

        private Dictionary<string, string> fieldMap = new Dictionary<string, string>();
        private string settingType = String.Empty;

        public BlobHelper.Blobs? GetBlobs()
        {
            switch (settingType)
            {
                case "Aws":
                    return new BlobHelper.Blobs(new BlobHelper.AwsSettings(fieldMap["AccessKey"], fieldMap["SecretKey"], fieldMap["Region"], fieldMap["Bucket"]));

                case "Disk":
                    return new BlobHelper.Blobs(new BlobHelper.DiskSettings(fieldMap["Directory"]));

                case "Azure":
                    return new BlobHelper.Blobs(new BlobHelper.AzureSettings(fieldMap["AccountName"], fieldMap["AccessKey"], fieldMap["Endpoint"], fieldMap["Container"]));

                case "Komodo":
                    return new BlobHelper.Blobs(new BlobHelper.KomodoSettings(fieldMap["Endpoint"], fieldMap["IndexGUID"], fieldMap["ApiKey"]));

                case "Kvpbase":
                    return new BlobHelper.Blobs(new BlobHelper.KvpbaseSettings(fieldMap["Endpoint"], fieldMap["UserGuid"], fieldMap["Container"], fieldMap["ApiKey"]));
            }

            return null;
        }

        public void GetInfo(string serverType)
        {
            string input = String.Empty;
            string types = String.Empty;
            string comma = String.Empty;
            foreach (var kvp in allowedSettings)
            {
                types += comma + kvp.Key;
                comma = ", ";
            }

            settingType = String.Empty;
            do
            {
                Console.WriteLine(String.Empty);
                Console.WriteLine(serverType + ": Enter a server type (" + types + ") or return to exit.");
                input = Console.ReadLine();
            }
            while (input.Length > 0 && !allowedSettings.ContainsKey(input));

            if (input.Length == 0)
            {
                return;
            }

            settingType = input;
            fieldMap = new Dictionary<string, string>();

            foreach (var fieldName in allowedSettings[settingType])
            {
                Console.WriteLine(String.Empty);
                Console.WriteLine(serverType + ": Enter " + settingType + " " + fieldName + ", or return to exit.");
                input = Console.ReadLine();
                if (input.Length == 0)
                {
                    settingType = String.Empty;
                    return;
                }
                fieldMap[fieldName] = input;
            }
        }

        public override string ToString()
        {
            var retVal = "Invalid";
            if (this.valid())
            {
                retVal = this.settingType + ": ";
                foreach (var kvp in this.fieldMap)
                {
                    retVal += kvp.Key + "=" + kvp.Value + ' ';
                }
            }

            return retVal;
        }

        public bool valid()
        {
            return settingType.Length > 0 && fieldMap.Count > 0;
        }
    }
}