namespace Test.Xunit
{
    using global::Xunit;

    /// <summary>
    /// Collection that runs the telemetry tests without parallelization.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class TelemetryCollection
    {
        /// <summary>
        /// Collection name.
        /// </summary>
        public const string Name = "Telemetry";
    }
}
