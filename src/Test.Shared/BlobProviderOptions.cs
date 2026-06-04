namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using Blobject.NFS;

    /// <summary>
    /// Provider options for contract tests.
    /// </summary>
    public class BlobProviderOptions
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = "disk";

        /// <summary>
        /// Remove test data after each test case.
        /// </summary>
        public bool Cleanup { get; set; } = true;

        /// <summary>
        /// Maximum concurrency to apply to the client.
        /// </summary>
        public int MaxConcurrency { get; set; } = 4;

        /// <summary>
        /// Include expensive stress/pagination tests.
        /// </summary>
        public bool IncludeStress { get; set; } = false;

        /// <summary>
        /// Optional prefix for non-disk providers.
        /// </summary>
        public string Prefix { get; set; } = "blobject-tests";

        /// <summary>
        /// Optional disk directory.  If null, a temporary directory is used.
        /// </summary>
        public string DiskDirectory { get; set; } = null;

        public string S3AccessKey { get; set; } = null;
        public string S3SecretKey { get; set; } = null;
        public string S3Region { get; set; } = null;
        public string S3Bucket { get; set; } = null;
        public string S3Endpoint { get; set; } = null;
        public bool S3Ssl { get; set; } = true;
        public string S3BaseUrl { get; set; } = null;

        public string AzureAccountName { get; set; } = null;
        public string AzureAccessKey { get; set; } = null;
        public string AzureEndpoint { get; set; } = null;
        public string AzureContainer { get; set; } = null;

        public string GcpProjectId { get; set; } = null;
        public string GcpBucket { get; set; } = null;
        public string GcpJsonCredentials { get; set; } = null;
        public string GcpCustomEndpoint { get; set; } = null;

        public string CifsHostname { get; set; } = null;
        public string CifsUsername { get; set; } = null;
        public string CifsPassword { get; set; } = null;
        public string CifsShare { get; set; } = null;

        public string NfsHostname { get; set; } = null;
        public int NfsUserId { get; set; } = 0;
        public int NfsGroupId { get; set; } = 0;
        public string NfsShare { get; set; } = null;
        public NfsVersionEnum NfsVersion { get; set; } = NfsVersionEnum.V3;

        /// <summary>
        /// True when provider matching should be case-insensitive.
        /// </summary>
        public bool IsCaseInsensitiveProvider
        {
            get
            {
                return String.Equals(Provider, "disk", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(Provider, "cifs", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Load options from environment variables.
        /// </summary>
        /// <returns>Options.</returns>
        public static BlobProviderOptions FromEnvironment()
        {
            BlobProviderOptions options = new BlobProviderOptions();
            ApplyEnvironment(options);
            return options;
        }

        /// <summary>
        /// Load options from command-line arguments and environment variables.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <param name="resultsPath">Touchstone results path.</param>
        /// <returns>Options.</returns>
        public static BlobProviderOptions FromArgs(string[] args, out string resultsPath)
        {
            BlobProviderOptions options = FromEnvironment();
            resultsPath = null;

            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (String.IsNullOrEmpty(arg) || !arg.StartsWith("--")) continue;

                string name = arg.Substring(2);
                string value = "true";
                int equalsIndex = name.IndexOf('=');

                if (equalsIndex >= 0)
                {
                    value = name.Substring(equalsIndex + 1);
                    name = name.Substring(0, equalsIndex);
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    value = args[i + 1];
                    i++;
                }

                values[name] = value;
            }

            if (values.TryGetValue("results", out string results)) resultsPath = results;
            ApplyDictionary(options, values);
            return options;
        }

        private static void ApplyEnvironment(BlobProviderOptions options)
        {
            SetIfPresent(value => options.Provider = value, "BLOBJECT_TEST_PROVIDER");
            SetIfPresent(value => options.Cleanup = ParseBool(value, options.Cleanup), "BLOBJECT_TEST_CLEANUP");
            SetIfPresent(value => options.MaxConcurrency = ParseInt(value, options.MaxConcurrency), "BLOBJECT_TEST_MAX_CONCURRENCY");
            SetIfPresent(value => options.IncludeStress = ParseBool(value, options.IncludeStress), "BLOBJECT_TEST_INCLUDE_STRESS");
            SetIfPresent(value => options.Prefix = value, "BLOBJECT_TEST_PREFIX");
            SetIfPresent(value => options.DiskDirectory = value, "BLOBJECT_TEST_DISK_DIRECTORY");

            SetIfPresent(value => options.S3AccessKey = value, "BLOBJECT_TEST_S3_ACCESS_KEY");
            SetIfPresent(value => options.S3SecretKey = value, "BLOBJECT_TEST_S3_SECRET_KEY");
            SetIfPresent(value => options.S3Region = value, "BLOBJECT_TEST_S3_REGION");
            SetIfPresent(value => options.S3Bucket = value, "BLOBJECT_TEST_S3_BUCKET");
            SetIfPresent(value => options.S3Endpoint = value, "BLOBJECT_TEST_S3_ENDPOINT");
            SetIfPresent(value => options.S3Ssl = ParseBool(value, options.S3Ssl), "BLOBJECT_TEST_S3_SSL");
            SetIfPresent(value => options.S3BaseUrl = value, "BLOBJECT_TEST_S3_BASE_URL");

            SetIfPresent(value => options.AzureAccountName = value, "BLOBJECT_TEST_AZURE_ACCOUNT_NAME");
            SetIfPresent(value => options.AzureAccessKey = value, "BLOBJECT_TEST_AZURE_ACCESS_KEY");
            SetIfPresent(value => options.AzureEndpoint = value, "BLOBJECT_TEST_AZURE_ENDPOINT");
            SetIfPresent(value => options.AzureContainer = value, "BLOBJECT_TEST_AZURE_CONTAINER");

            SetIfPresent(value => options.GcpProjectId = value, "BLOBJECT_TEST_GCP_PROJECT_ID");
            SetIfPresent(value => options.GcpBucket = value, "BLOBJECT_TEST_GCP_BUCKET");
            SetIfPresent(value => options.GcpJsonCredentials = value, "BLOBJECT_TEST_GCP_JSON_CREDENTIALS");
            SetIfPresent(value => options.GcpCustomEndpoint = value, "BLOBJECT_TEST_GCP_CUSTOM_ENDPOINT");

            SetIfPresent(value => options.CifsHostname = value, "BLOBJECT_TEST_CIFS_HOSTNAME");
            SetIfPresent(value => options.CifsUsername = value, "BLOBJECT_TEST_CIFS_USERNAME");
            SetIfPresent(value => options.CifsPassword = value, "BLOBJECT_TEST_CIFS_PASSWORD");
            SetIfPresent(value => options.CifsShare = value, "BLOBJECT_TEST_CIFS_SHARE");

            SetIfPresent(value => options.NfsHostname = value, "BLOBJECT_TEST_NFS_HOSTNAME");
            SetIfPresent(value => options.NfsUserId = ParseInt(value, options.NfsUserId), "BLOBJECT_TEST_NFS_USER_ID");
            SetIfPresent(value => options.NfsGroupId = ParseInt(value, options.NfsGroupId), "BLOBJECT_TEST_NFS_GROUP_ID");
            SetIfPresent(value => options.NfsShare = value, "BLOBJECT_TEST_NFS_SHARE");
            SetIfPresent(value => options.NfsVersion = ParseNfsVersion(value, options.NfsVersion), "BLOBJECT_TEST_NFS_VERSION");
        }

        private static void ApplyDictionary(BlobProviderOptions options, Dictionary<string, string> values)
        {
            Apply(values, "provider", value => options.Provider = value);
            Apply(values, "cleanup", value => options.Cleanup = ParseBool(value, options.Cleanup));
            Apply(values, "max-concurrency", value => options.MaxConcurrency = ParseInt(value, options.MaxConcurrency));
            Apply(values, "include-stress", value => options.IncludeStress = ParseBool(value, options.IncludeStress));
            Apply(values, "prefix", value => options.Prefix = value);
            Apply(values, "disk-directory", value => options.DiskDirectory = value);

            Apply(values, "s3-access-key", value => options.S3AccessKey = value);
            Apply(values, "s3-secret-key", value => options.S3SecretKey = value);
            Apply(values, "s3-region", value => options.S3Region = value);
            Apply(values, "s3-bucket", value => options.S3Bucket = value);
            Apply(values, "s3-endpoint", value => options.S3Endpoint = value);
            Apply(values, "s3-ssl", value => options.S3Ssl = ParseBool(value, options.S3Ssl));
            Apply(values, "s3-base-url", value => options.S3BaseUrl = value);

            Apply(values, "azure-account-name", value => options.AzureAccountName = value);
            Apply(values, "azure-access-key", value => options.AzureAccessKey = value);
            Apply(values, "azure-endpoint", value => options.AzureEndpoint = value);
            Apply(values, "azure-container", value => options.AzureContainer = value);

            Apply(values, "gcp-project-id", value => options.GcpProjectId = value);
            Apply(values, "gcp-bucket", value => options.GcpBucket = value);
            Apply(values, "gcp-json-credentials", value => options.GcpJsonCredentials = value);
            Apply(values, "gcp-custom-endpoint", value => options.GcpCustomEndpoint = value);

            Apply(values, "cifs-hostname", value => options.CifsHostname = value);
            Apply(values, "cifs-username", value => options.CifsUsername = value);
            Apply(values, "cifs-password", value => options.CifsPassword = value);
            Apply(values, "cifs-share", value => options.CifsShare = value);

            Apply(values, "nfs-hostname", value => options.NfsHostname = value);
            Apply(values, "nfs-user-id", value => options.NfsUserId = ParseInt(value, options.NfsUserId));
            Apply(values, "nfs-group-id", value => options.NfsGroupId = ParseInt(value, options.NfsGroupId));
            Apply(values, "nfs-share", value => options.NfsShare = value);
            Apply(values, "nfs-version", value => options.NfsVersion = ParseNfsVersion(value, options.NfsVersion));
        }

        private static void Apply(Dictionary<string, string> values, string key, Action<string> setter)
        {
            if (values.TryGetValue(key, out string value)) setter(value);
        }

        private static void SetIfPresent(Action<string> setter, string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (!String.IsNullOrEmpty(value)) setter(value);
        }

        private static bool ParseBool(string value, bool defaultValue)
        {
            if (String.IsNullOrEmpty(value)) return defaultValue;
            if (Boolean.TryParse(value, out bool ret)) return ret;
            if (String.Equals(value, "1", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(value, "0", StringComparison.OrdinalIgnoreCase)) return false;
            if (String.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(value, "no", StringComparison.OrdinalIgnoreCase)) return false;
            return defaultValue;
        }

        private static int ParseInt(string value, int defaultValue)
        {
            if (Int32.TryParse(value, out int ret)) return ret;
            return defaultValue;
        }

        private static NfsVersionEnum ParseNfsVersion(string value, NfsVersionEnum defaultValue)
        {
            if (String.IsNullOrEmpty(value)) return defaultValue;
            if (Enum.TryParse(value, true, out NfsVersionEnum ret)) return ret;
            return defaultValue;
        }
    }
}
