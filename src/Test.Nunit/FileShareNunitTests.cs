namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Test.Shared.FileShare;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// NUnit host for the CIFS/NFS file share suites against managed ephemeral servers.
    /// Targets are selected with BLOBJECT_TEST_FILESHARE_TARGETS (default: all); Docker targets are skipped when Docker is unavailable.
    /// </summary>
    [TestFixture]
    public sealed class FileShareNunitTests
    {
        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(FileShareSuites.BuildAll(BlobProviderOptions.FromEnvironment()));
        }

        /// <summary>
        /// Stop every managed server and remove its containers.
        /// </summary>
        /// <returns>Task.</returns>
        [OneTimeTearDown]
        public async Task TearDown()
        {
            await FileShareServers.DisposeAllAsync();
        }

        /// <summary>
        /// Run a single descriptor.
        /// </summary>
        /// <param name="testCase">Test case.</param>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
