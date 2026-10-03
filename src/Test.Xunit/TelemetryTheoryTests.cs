namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared.Telemetry;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using global::Xunit;

    /// <summary>
    /// xUnit host for the telemetry suites.  Runs in its own collection with parallelization disabled, because some
    /// cases toggle process-wide telemetry settings and attach listeners.
    /// </summary>
    [Collection(TelemetryCollection.Name)]
    public sealed class TelemetryTheoryTests
    {
        /// <summary>
        /// Provides test cases as theory rows.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            return new TouchstoneTheoryData(TelemetrySuites.All);
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
