namespace Blobject.Core
{
    /// <summary>
    /// Process-wide Blobject telemetry settings.
    /// Blobject emits metrics through the <see cref="System.Diagnostics.Metrics.Meter"/> and spans through the
    /// <see cref="System.Diagnostics.ActivitySource"/> named <see cref="BlobjectTelemetryNames.MeterName"/> ("Blobject").
    /// Nothing is exported by the library; a host collects the data by subscribing to those names, for example
    /// OpenTelemetry's AddMeter("Blobject") and AddSource("Blobject"), or Radiant's settings.Sources.AddMeter("Blobject")
    /// and settings.Sources.AddActivitySource("Blobject").  When nothing subscribes, instrumentation costs a few checks per operation.
    /// Instrumentation is best-effort: a failure while recording telemetry never affects the storage operation.
    /// Thread safety: properties may be read and written from any thread; changes apply to operations that start afterward.
    /// </summary>
    public static class BlobjectTelemetry
    {
        #region Public-Members

        /// <summary>
        /// Master switch for Blobject metrics and spans.  Default is true.
        /// When false, operations run without creating spans or recording metrics.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                return _Enabled;
            }
            set
            {
                _Enabled = value;
            }
        }

        /// <summary>
        /// Record object keys on spans as the blobject.key attribute.  Default is false.
        /// Keys can contain user data such as file names, so they are omitted unless explicitly enabled.
        /// Keys are never recorded on metrics, regardless of this setting.
        /// </summary>
        public static bool RecordKeys
        {
            get
            {
                return _RecordKeys;
            }
            set
            {
                _RecordKeys = value;
            }
        }

        /// <summary>
        /// Version of Blobject.Core, as reported by the blobject.build.info metric.
        /// </summary>
        public static string Version
        {
            get
            {
                return BlobjectInstrumentation.Version;
            }
        }

        #endregion

        #region Private-Members

        private static volatile bool _Enabled = true;
        private static volatile bool _RecordKeys = false;

        #endregion
    }
}
