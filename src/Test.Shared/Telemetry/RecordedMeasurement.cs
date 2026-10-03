namespace Test.Shared.Telemetry
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A metric measurement captured by <see cref="TelemetryCollector"/>.
    /// </summary>
    public sealed class RecordedMeasurement
    {
        /// <summary>
        /// Instrument name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Measured value.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Measurement tags, with values converted to strings.
        /// </summary>
        public IReadOnlyDictionary<string, string> Tags { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="name">Instrument name.</param>
        /// <param name="value">Value.</param>
        /// <param name="tags">Tags.</param>
        public RecordedMeasurement(string name, double value, IReadOnlyDictionary<string, string> tags)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value;
            Tags = tags ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Get a tag value.
        /// </summary>
        /// <param name="key">Tag key.</param>
        /// <returns>Value, or null if absent.</returns>
        public string Tag(string key)
        {
            return Tags.TryGetValue(key, out string value) ? value : null;
        }

        /// <summary>
        /// Determine whether the measurement carries every supplied tag value.
        /// </summary>
        /// <param name="expected">Expected tag key/value pairs; a null value requires the tag to be absent.</param>
        /// <returns>True if all match.</returns>
        public bool Matches(IDictionary<string, string> expected)
        {
            if (expected == null) return true;

            foreach (KeyValuePair<string, string> pair in expected)
            {
                if (!String.Equals(Tag(pair.Key), pair.Value, StringComparison.Ordinal)) return false;
            }

            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            List<string> tags = new List<string>();
            foreach (KeyValuePair<string, string> pair in Tags) tags.Add(pair.Key + "=" + pair.Value);
            return Name + " " + Value + " {" + String.Join(", ", tags) + "}";
        }
    }
}
