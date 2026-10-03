namespace Test.Shared.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// In-memory listener for spans emitted by an underlying SDK's activity source, such as S3Lite or OpenNFS.Client.
    /// Captures only spans in the supplied trace so that concurrently running tests are ignored.
    /// Create it before the collector's root span is used, and dispose to stop listening.
    /// </summary>
    public sealed class DependencySpanCollector : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Completed spans from the dependency's activity source in the trace.
        /// </summary>
        public List<Activity> Spans
        {
            get
            {
                lock (_Lock) return new List<Activity>(_Spans);
            }
        }

        #endregion

        #region Private-Members

        private readonly object _Lock = new object();
        private readonly List<Activity> _Spans = new List<Activity>();
        private readonly string _SourceName;
        private readonly ActivityTraceId _TraceId;
        private readonly ActivityListener _Listener;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start listening to the named activity source.
        /// </summary>
        /// <param name="sourceName">Activity source name.</param>
        /// <param name="traceId">Trace id to capture.</param>
        public DependencySpanCollector(string sourceName, ActivityTraceId traceId)
        {
            if (String.IsNullOrEmpty(sourceName)) throw new ArgumentNullException(nameof(sourceName));

            _SourceName = sourceName;
            _TraceId = traceId;
            _Listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == _SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = OnActivityStopped
            };

            ActivitySource.AddActivityListener(_Listener);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Stop listening.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Listener.Dispose();
        }

        #endregion

        #region Private-Methods

        private void OnActivityStopped(Activity activity)
        {
            if (activity.TraceId != _TraceId) return;
            if (activity.Source.Name != _SourceName) return;

            lock (_Lock) _Spans.Add(activity);
        }

        #endregion
    }
}
