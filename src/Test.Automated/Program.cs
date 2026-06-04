using System;
using System.Linq;
using Test.Shared;
using Touchstone.Cli;

if (args.Any(arg => arg == "--help" || arg == "-h" || arg == "/?"))
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src/Test.Automated -- [options]");
    Console.WriteLine("");
    Console.WriteLine("Common:");
    Console.WriteLine("  --provider disk|s3|s3lite|azure|gcp|cifs|nfs");
    Console.WriteLine("  --results <path>");
    Console.WriteLine("  --cleanup true|false");
    Console.WriteLine("  --prefix <prefix>");
    Console.WriteLine("  --max-concurrency <n>");
    Console.WriteLine("  --include-stress true|false");
    Console.WriteLine("  --disk-directory <path>");
    Console.WriteLine("");
    Console.WriteLine("S3/S3Lite:");
    Console.WriteLine("  --s3-access-key <value> --s3-secret-key <value> --s3-region <value> --s3-bucket <value>");
    Console.WriteLine("  --s3-endpoint <url> --s3-ssl true|false --s3-base-url <template>");
    Console.WriteLine("");
    Console.WriteLine("Azure:");
    Console.WriteLine("  --azure-account-name <value> --azure-access-key <value> --azure-endpoint <url> --azure-container <value>");
    Console.WriteLine("");
    Console.WriteLine("GCP:");
    Console.WriteLine("  --gcp-project-id <value> --gcp-bucket <value> --gcp-json-credentials <json> [--gcp-custom-endpoint <url>]");
    Console.WriteLine("");
    Console.WriteLine("CIFS:");
    Console.WriteLine("  --cifs-hostname <value> --cifs-username <value> --cifs-password <value> --cifs-share <value>");
    Console.WriteLine("");
    Console.WriteLine("NFS:");
    Console.WriteLine("  --nfs-hostname <value> --nfs-user-id <n> --nfs-group-id <n> --nfs-share <value> --nfs-version V2|V3|V4");
    return 0;
}

BlobProviderOptions options = BlobProviderOptions.FromArgs(args, out string resultsPath);
BlobContractSuites.Configure(options);

return await ConsoleRunner.RunAsync(BlobContractSuites.All, resultsPath: resultsPath);
