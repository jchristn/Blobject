using System;
using System.Collections.Generic;
using System.Linq;
using Test.Shared;
using Test.Shared.FileShare;
using Test.Shared.Telemetry;
using Touchstone.Cli;
using Touchstone.Core;

if (args.Any(arg => arg == "--help" || arg == "-h" || arg == "/?"))
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src/Test.Automated -- [options]");
    Console.WriteLine("");
    Console.WriteLine("Common:");
    Console.WriteLine("  --provider disk|s3|s3lite|azure|gcp|cifs|nfs|fileshare|telemetry|<managed target>");
    Console.WriteLine("  --results <path>");
    Console.WriteLine("  --cleanup true|false");
    Console.WriteLine("  --prefix <prefix>");
    Console.WriteLine("  --max-concurrency <n>");
    Console.WriteLine("  --include-stress true|false");
    Console.WriteLine("  --disk-directory <path>");
    Console.WriteLine("  --filter <text>       Run only cases whose SuiteId.CaseId contains the text (case-insensitive)");
    Console.WriteLine("");
    Console.WriteLine("Telemetry:");
    Console.WriteLine("  --provider telemetry  Runs the telemetry suites (spans and metrics, in-process; no external services)");
    Console.WriteLine("");
    Console.WriteLine("Managed CIFS/NFS servers (started and removed automatically):");
    Console.WriteLine("  --provider fileshare [--fileshare-targets all|cifs|nfs|inprocess|docker|<target>[,<target>...]]");
    Console.WriteLine("      Runs the API surface suites, then the provider contract suites plus the protocol semantics,");
    Console.WriteLine("      enumeration, concurrency, lifecycle, interop, and SMB/NFS-specific suites for each target.");
    Console.WriteLine("  --provider <target>   Runs the same suites for a single target.");
    Console.WriteLine("  Targets: " + String.Join(", ", FileShareServers.AllTargets));
    Console.WriteLine("    cifs-ephemeral  OpenCIFS.Server in-process on loopback");
    Console.WriteLine("    cifs-samba      Samba smbd in Docker");
    Console.WriteLine("    nfs-ephemeral   OpenNFS.Server in-process on loopback");
    Console.WriteLine("    nfs-ganesha     nfs-ganesha in Docker");
    Console.WriteLine("    nfs-knfsd       Linux kernel nfsd in privileged Docker (exercises portmapper discovery)");
    Console.WriteLine("    nfs-unfs3       unfs3 in Docker");
    Console.WriteLine("");
    Console.WriteLine("S3/S3Lite:");
    Console.WriteLine("  --s3-access-key <value> --s3-secret-key <value> --s3-region <value> --s3-bucket <value>");
    Console.WriteLine("  --s3-endpoint <url> --s3-ssl true|false --s3-base-url <template> [--s3-path-style true|false]");
    Console.WriteLine("");
    Console.WriteLine("Azure:");
    Console.WriteLine("  --azure-account-name <value> --azure-access-key <value> --azure-endpoint <url> --azure-container <value>");
    Console.WriteLine("");
    Console.WriteLine("GCP:");
    Console.WriteLine("  --gcp-project-id <value> --gcp-bucket <value> --gcp-json-credentials <json> [--gcp-custom-endpoint <url>]");
    Console.WriteLine("");
    Console.WriteLine("CIFS (existing server):");
    Console.WriteLine("  --cifs-hostname <value> --cifs-username <value> --cifs-password <value> --cifs-share <value>");
    Console.WriteLine("  [--cifs-port <n>] [--cifs-domain <value>] [--cifs-require-signing true|false] [--cifs-prefer-encryption true|false]");
    Console.WriteLine("");
    Console.WriteLine("NFS (existing server):");
    Console.WriteLine("  --nfs-hostname <value> --nfs-user-id <n> --nfs-group-id <n> --nfs-share <value> --nfs-version V3");
    Console.WriteLine("  [--nfs-port <n>] [--nfs-mount-port <n>] [--nfs-portmapper-port <n>] [--nfs-write-stability Unstable|DataSync|FileSync]");
    return 0;
}

BlobProviderOptions options = BlobProviderOptions.FromArgs(args, out string resultsPath);
IReadOnlyList<TestSuiteDescriptor> suites;

if (String.Equals(options.Provider, "telemetry", StringComparison.OrdinalIgnoreCase))
{
    suites = TelemetrySuites.All;
}
else if (String.Equals(options.Provider, "fileshare", StringComparison.OrdinalIgnoreCase))
{
    suites = FileShareSuites.BuildAll(options);
}
else if (FileShareServers.IsManagedTarget(options.Provider))
{
    List<TestSuiteDescriptor> list = new List<TestSuiteDescriptor>(FileShareApiSuites.Build());
    list.AddRange(FileShareSuites.BuildForTarget(options, options.Provider, includeContract: true));
    suites = list;
}
else
{
    BlobContractSuites.Configure(options);
    suites = BlobContractSuites.All;
}

string filter = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--filter") filter = args[i + 1];
}

if (!String.IsNullOrEmpty(filter))
{
    suites = suites
        .Select(s => new TestSuiteDescriptor(
            s.SuiteId,
            s.DisplayName,
            s.Cases.Where(c => (c.SuiteId + "." + c.CaseId).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList(),
            s.BeforeSuiteAsync,
            s.AfterSuiteAsync))
        .Where(s => s.Cases.Count > 0)
        .ToList();
}

try
{
    await DockerCli.RemoveOrphanedContainersAsync();
    return await ConsoleRunner.RunAsync(suites, resultsPath: resultsPath);
}
finally
{
    await FileShareServers.DisposeAllAsync();
}
