namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Test.Shared.FileShare;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using global::Xunit;

    /// <summary>
    /// xUnit host for the CIFS/NFS file share suites against managed ephemeral servers.
    /// Targets are selected with BLOBJECT_TEST_FILESHARE_TARGETS (default: all); Docker targets are skipped when Docker is unavailable.
    /// </summary>
    public sealed class FileShareTheoryTests
    {
        /// <summary>
        /// Provides test cases as theory rows.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            return new TouchstoneTheoryData(FileShareSuites.BuildAll(BlobProviderOptions.FromEnvironment()));
        }

        /// <summary>
        /// Run a single descriptor.
        /// </summary>
        /// <param name="testCase">Test case.</param>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
