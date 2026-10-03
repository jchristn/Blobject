namespace Test.Shared.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using Blobject.Core;

    /// <summary>
    /// In-memory listener for Blobject spans and metrics.  Starts a root span and captures only spans and measurements
    /// that belong to its trace, so operations from tests running concurrently are ignored.
    /// Dispose to stop listening.
    /// </summary>
    public sealed class TelemetryCollector : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Name of the activity source used for the collector's root span.
        /// </summary>
        public const string TestSourceName = "Blobject.Tests";

        /// <summary>
        /// Trace id of the root span.
        /// </summary>
        public ActivityTraceId TraceId { get; }

        /// <summary>
        /// Span id of the root span.
        /// </summary>
        public ActivitySpanId RootSpanId { get; }

        /// <summary>
        /// Completed Blobject spans in the root span's trace.
        /// </summary>
        public List<Activity> Spans
        {
            get
            {
                lock (_Lock) return new List<Activity>(_Spans);
            }
        }

        /// <summary>
        /// Blobject measurements recorded within the root span's trace.
        /// </summary>
        public List<RecordedMeasurement> Measurements
        {
            get
            {
                lock (_Lock) return new List<RecordedMeasurement>(_Measurements);
            }
        }

        #endregion

        #region Private-Members

        private static readonly ActivitySource _TestSource = new ActivitySource(TestSourceName);

        private readonly object _Lock = new object();
        private readonly List<Activity> _Spans = new List<Activity>();
        private readonly List<RecordedMeasurement> _Measurements = new List<RecordedMeasurement>();
        private readonly ActivityListener _ActivityListener;
        private readonly MeterListener _MeterListener;
        private readonly Activity _Root;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start listening and start the root span, which becomes Activity.Current for the caller.
        /// </summary>
        public TelemetryCollector()
        {
            _ActivityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == BlobjectTelemetryNames.ActivitySourceName || source.Name == TestSourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = OnActivityStopped
            };

            ActivitySource.AddActivityListener(_ActivityListener);

            _MeterListener = new MeterListener();
            _MeterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == BlobjectTelemetryNames.MeterName) listener.EnableMeasurementEvents(instrument);
            };
            _MeterListener.SetMeasurementEventCallback<double>((i, v, t, s) => OnMeasurement(i, v, t));
            _MeterListener.SetMeasurementEventCallback<long>((i, v, t, s) => OnMeasurement(i, v, t));
            _MeterListener.SetMeasurementEventCallback<int>((i, v, t, s) => OnMeasurement(i, v, t));
            _MeterListener.Start();

            _Root = _TestSource.StartActivity("telemetry-test", ActivityKind.Internal);
            if (_Root == null) throw new InvalidOperationException("The test root span was not created.");
            TraceId = _Root.TraceId;
            RootSpanId = _Root.SpanId;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Collect observable gauges (build info, last copy success) now, within the root span's trace.
        /// </summary>
        public void CollectObservable()
        {
            Activity previous = Activity.Current;
            Activity.Current = _Root;

            try
            {
                _MeterListener.RecordObservableInstruments();
            }
            finally
            {
                Activity.Current = previous;
            }
        }

        /// <summary>
        /// Spans with the supplied display name.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <returns>Spans.</returns>
        public List<Activity> SpansNamed(string name)
        {
            return Spans.Where(a => a.DisplayName == name).ToList();
        }

        /// <summary>
        /// The single span with the supplied name.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <returns>Span.</returns>
        /// <exception cref="InvalidOperationException">Thrown when there is not exactly one such span.</exception>
        public Activity Span(string name)
        {
            List<Activity> spans = SpansNamed(name);
            if (spans.Count != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one span named '" + name + "' but found " + spans.Count + ". Spans: "
                    + String.Join(", ", Spans.Select(a => a.DisplayName)));
            }

            return spans[0];
        }

        /// <summary>
        /// Measurements of an instrument matching the supplied tags.
        /// </summary>
        /// <param name="name">Instrument name.</param>
        /// <param name="tags">Tags to match; may be null.</param>
        /// <returns>Measurements.</returns>
        public List<RecordedMeasurement> MeasurementsOf(string name, IDictionary<string, string> tags = null)
        {
            return Measurements.Where(m => m.Name == name && m.Matches(tags)).ToList();
        }

        /// <summary>
        /// Sum of the values of an instrument matching the supplied tags.
        /// </summary>
        /// <param name="name">Instrument name.</param>
        /// <param name="tags">Tags to match; may be null.</param>
        /// <returns>Sum.</returns>
        public double Sum(string name, IDictionary<string, string> tags = null)
        {
            return MeasurementsOf(name, tags).Sum(m => m.Value);
        }

        /// <summary>
        /// Describe everything captured, for failure messages.
        /// </summary>
        /// <returns>Description.</returns>
        public string Describe()
        {
            return "Spans: [" + String.Join(", ", Spans.Select(a => a.DisplayName + ":" + a.Status)) + "] Measurements: ["
                + String.Join("; ", Measurements.Select(m => m.ToString())) + "]";
        }

        /// <summary>
        /// Stop the root span and stop listening.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            _Root.Dispose();
            _MeterListener.Dispose();
            _ActivityListener.Dispose();
        }

        #endregion

        #region Private-Methods

        private void OnActivityStopped(Activity activity)
        {
            if (activity.TraceId != TraceId) return;
            if (activity.Source.Name != BlobjectTelemetryNames.ActivitySourceName) return;

            lock (_Lock) _Spans.Add(activity);
        }

        private void OnMeasurement<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            Activity current = Activity.Current;
            if (current == null || current.TraceId != TraceId) return;

            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (KeyValuePair<string, object> tag in tags) dict[tag.Key] = tag.Value != null ? tag.Value.ToString() : null;

            RecordedMeasurement measurement = new RecordedMeasurement(instrument.Name, Convert.ToDouble(value), dict);
            lock (_Lock) _Measurements.Add(measurement);
        }

        #endregion
    }
}
