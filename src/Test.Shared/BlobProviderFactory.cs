namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.AzureBlob;
    using Blobject.CIFS;
    using Blobject.Core;
    using Blobject.Disk;
    using Blobject.GoogleCloud;
    using Blobject.NFS;

    /// <summary>
    /// Builds provider clients for contract tests.
    /// </summary>
    public static class BlobProviderFactory
    {
        /// <summary>
        /// Create a provider context.
        /// </summary>
        /// <param name="options">Options.</param>
        /// <param name="caseId">Case ID.</param>
        /// <returns>Provider context.</returns>
        public static BlobProviderContext Create(BlobProviderOptions options, string caseId)
        {
            if (options == null) options = BlobProviderOptions.FromEnvironment();
            string provider = NormalizeProvider(options.Provider);

            if (provider == "disk") return CreateDisk(options);

            BlobClientBase inner = CreateRemote(provider, options);
            inner.MaxConcurrency = options.MaxConcurrency;

            string prefix = BuildPrefix(options, caseId);
            PrefixingBlobClient scoped = new PrefixingBlobClient(inner, prefix);
            scoped.MaxConcurrency = options.MaxConcurrency;

            return new BlobProviderContext
            {
                Client = scoped,
                Options = options,
                CleanupAsync = async token =>
                {
                    if (!options.Cleanup) return;
                    await scoped.EmptyAsync(token).ConfigureAwait(false);
                }
            };
        }

        private static BlobProviderContext CreateDisk(BlobProviderOptions options)
        {
            string directory = options.DiskDirectory;
            bool temporary = String.IsNullOrEmpty(directory);

            if (temporary)
            {
                directory = Path.Combine(Path.GetTempPath(), "blobject-tests", Guid.NewGuid().ToString("N"));
            }

            Directory.CreateDirectory(directory);

            DiskBlobClient client = new DiskBlobClient(new DiskSettings(directory));
            client.MaxConcurrency = options.MaxConcurrency;

            return new BlobProviderContext
            {
                Client = client,
                Options = options,
                CleanupAsync = token =>
                {
                    if (options.Cleanup && temporary) TryDeleteDirectory(directory);
                    else if (options.Cleanup) return client.EmptyAsync(token);
                    return Task.CompletedTask;
                }
            };
        }

        private static BlobClientBase CreateRemote(string provider, BlobProviderOptions options)
        {
            switch (provider)
            {
                case "s3":
                case "amazon-s3":
                case "aws":
                    return CreateS3(options);

                case "s3lite":
                case "amazon-s3lite":
                case "aws-lite":
                case "awslite":
                    return CreateS3Lite(options);

                case "azure":
                case "azureblob":
                    return new AzureBlobClient(new AzureBlobSettings(
                        Required(options.AzureAccountName, "azure-account-name"),
                        Required(options.AzureAccessKey, "azure-access-key"),
                        Required(options.AzureEndpoint, "azure-endpoint"),
                        Required(options.AzureContainer, "azure-container")));

                case "gcp":
                case "google":
                case "googlecloud":
                    if (String.IsNullOrEmpty(options.GcpCustomEndpoint))
                    {
                        return new GcpBlobClient(new GcpBlobSettings(
                            Required(options.GcpProjectId, "gcp-project-id"),
                            Required(options.GcpBucket, "gcp-bucket"),
                            Required(options.GcpJsonCredentials, "gcp-json-credentials")));
                    }

                    return new GcpBlobClient(new GcpBlobSettings(
                        Required(options.GcpProjectId, "gcp-project-id"),
                        Required(options.GcpBucket, "gcp-bucket"),
                        Required(options.GcpJsonCredentials, "gcp-json-credentials"),
                        options.GcpCustomEndpoint));

                case "cifs":
                case "smb":
                    return new CifsBlobClient(new CifsSettings(
                        Required(options.CifsHostname, "cifs-hostname"),
                        Required(options.CifsUsername, "cifs-username"),
                        Required(options.CifsPassword, "cifs-password"),
                        Required(options.CifsShare, "cifs-share")));

                case "nfs":
                    return new NfsBlobClient(new NfsSettings(
                        Required(options.NfsHostname, "nfs-hostname"),
                        options.NfsUserId,
                        options.NfsGroupId,
                        Required(options.NfsShare, "nfs-share"),
                        options.NfsVersion));

                default:
                    throw new ArgumentException("Unknown provider '" + provider + "'.");
            }
        }

        private static BlobClientBase CreateS3(BlobProviderOptions options)
        {
            string region = Required(options.S3Region, "s3-region");
            string bucket = Required(options.S3Bucket, "s3-bucket");

            if (String.IsNullOrEmpty(options.S3Endpoint))
            {
                return new Blobject.AmazonS3.AmazonS3BlobClient(new Blobject.AmazonS3.AwsSettings(
                    options.S3AccessKey,
                    options.S3SecretKey,
                    region,
                    bucket));
            }

            return new Blobject.AmazonS3.AmazonS3BlobClient(new Blobject.AmazonS3.AwsSettings(
                options.S3Endpoint,
                options.S3Ssl,
                options.S3AccessKey,
                options.S3SecretKey,
                region,
                bucket,
                Required(options.S3BaseUrl, "s3-base-url")));
        }

        private static BlobClientBase CreateS3Lite(BlobProviderOptions options)
        {
            string region = Required(options.S3Region, "s3-region");
            string bucket = Required(options.S3Bucket, "s3-bucket");

            if (String.IsNullOrEmpty(options.S3Endpoint))
            {
                return new Blobject.AmazonS3Lite.AmazonS3LiteBlobClient(new Blobject.AmazonS3Lite.AwsSettings(
                    options.S3AccessKey,
                    options.S3SecretKey,
                    region,
                    bucket));
            }

            return new Blobject.AmazonS3Lite.AmazonS3LiteBlobClient(new Blobject.AmazonS3Lite.AwsSettings(
                options.S3Endpoint,
                options.S3Ssl,
                options.S3AccessKey,
                options.S3SecretKey,
                region,
                bucket,
                Required(options.S3BaseUrl, "s3-base-url")));
        }

        private static string NormalizeProvider(string provider)
        {
            if (String.IsNullOrEmpty(provider)) return "disk";
            return provider.Trim().ToLowerInvariant();
        }

        private static string BuildPrefix(BlobProviderOptions options, string caseId)
        {
            string prefix = String.IsNullOrEmpty(options.Prefix) ? "blobject-tests" : options.Prefix;
            prefix = prefix.Replace("\\", "/");
            while (prefix.EndsWith("/")) prefix = prefix.Substring(0, prefix.Length - 1);
            return prefix + "/" + caseId + "-" + Guid.NewGuid().ToString("N");
        }

        private static string Required(string value, string name)
        {
            if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(name);
            return value;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch
            {
            }
        }
    }
}
