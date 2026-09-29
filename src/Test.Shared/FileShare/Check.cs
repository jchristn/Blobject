namespace Test.Shared.FileShare
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Assertion helpers for file share suites.
    /// </summary>
    public static class Check
    {
        /// <summary>
        /// Assert a condition is true.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <param name="name">Description.</param>
        public static void True(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Expected true for " + name + ".");
        }

        /// <summary>
        /// Assert a condition is false.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <param name="name">Description.</param>
        public static void False(bool condition, string name)
        {
            if (condition) throw new InvalidOperationException("Expected false for " + name + ".");
        }

        /// <summary>
        /// Assert equality.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="expected">Expected.</param>
        /// <param name="actual">Actual.</param>
        /// <param name="name">Description.</param>
        public static void Equal<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("Expected " + name + " to be '" + expected + "' but found '" + actual + "'.");
        }

        /// <summary>
        /// Assert a value is not null.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="name">Description.</param>
        public static void NotNull(object value, string name)
        {
            if (value == null) throw new InvalidOperationException("Expected " + name + " to be non-null.");
        }

        /// <summary>
        /// Assert a value is null.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="name">Description.</param>
        public static void Null(object value, string name)
        {
            if (value != null) throw new InvalidOperationException("Expected " + name + " to be null but found '" + value + "'.");
        }

        /// <summary>
        /// Assert byte arrays are equal.
        /// </summary>
        /// <param name="expected">Expected.</param>
        /// <param name="actual">Actual.</param>
        /// <param name="name">Description.</param>
        public static void Bytes(byte[] expected, byte[] actual, string name)
        {
            if (expected == null || actual == null) throw new InvalidOperationException("Expected " + name + " byte arrays to be non-null.");
            if (expected.Length != actual.Length)
                throw new InvalidOperationException("Expected " + name + " length " + expected.Length + " but found " + actual.Length + ".");

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                    throw new InvalidOperationException("Expected " + name + " byte " + i + " to be " + expected[i] + " but found " + actual[i] + ".");
            }
        }

        /// <summary>
        /// Assert two key sets contain the same values, ignoring order.
        /// </summary>
        /// <param name="expected">Expected.</param>
        /// <param name="actual">Actual.</param>
        /// <param name="name">Description.</param>
        public static void Keys(IEnumerable<string> expected, IEnumerable<string> actual, string name)
        {
            List<string> e = expected.OrderBy(k => k, StringComparer.Ordinal).ToList();
            List<string> a = actual.OrderBy(k => k, StringComparer.Ordinal).ToList();

            if (!e.SequenceEqual(a, StringComparer.Ordinal))
            {
                List<string> missing = e.Except(a, StringComparer.Ordinal).Take(10).ToList();
                List<string> extra = a.Except(e, StringComparer.Ordinal).Take(10).ToList();
                throw new InvalidOperationException(
                    "Expected " + name + " to contain " + e.Count + " keys but found " + a.Count + "."
                    + (missing.Count > 0 ? " Missing: " + String.Join(", ", missing) + "." : "")
                    + (extra.Count > 0 ? " Unexpected: " + String.Join(", ", extra) + "." : ""));
            }
        }

        /// <summary>
        /// Assert a timestamp is within a window around now.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="window">Allowed distance from now.</param>
        /// <param name="name">Description.</param>
        public static void RecentUtc(DateTime? value, TimeSpan window, string name)
        {
            if (!value.HasValue) throw new InvalidOperationException("Expected " + name + " to have a value.");
            DateTime utc = value.Value.Kind == DateTimeKind.Local ? value.Value.ToUniversalTime() : value.Value;
            TimeSpan distance = (utc - DateTime.UtcNow).Duration();
            if (distance > window)
                throw new InvalidOperationException("Expected " + name + " (" + utc.ToString("o") + ") to be within " + window + " of now (" + DateTime.UtcNow.ToString("o") + ").");
        }

        /// <summary>
        /// Assert an action throws an exception of type T (or a subclass).
        /// </summary>
        /// <typeparam name="T">Exception type.</typeparam>
        /// <param name="action">Action.</param>
        /// <param name="name">Description.</param>
        /// <returns>The exception.</returns>
        public static async Task<T> ThrowsAsync<T>(Func<Task> action, string name) where T : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (T e)
            {
                return e;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Expected " + name + " to throw " + typeof(T).Name + " but it threw " + e.GetType().Name + ": " + e.Message, e);
            }

            throw new InvalidOperationException("Expected " + name + " to throw " + typeof(T).Name + " but it completed.");
        }

        /// <summary>
        /// Assert an action throws an exception of type T (or a subclass).
        /// </summary>
        /// <typeparam name="T">Exception type.</typeparam>
        /// <param name="action">Action.</param>
        /// <param name="name">Description.</param>
        /// <returns>The exception.</returns>
        public static T Throws<T>(Action action, string name) where T : Exception
        {
            try
            {
                action();
            }
            catch (T e)
            {
                return e;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Expected " + name + " to throw " + typeof(T).Name + " but it threw " + e.GetType().Name + ": " + e.Message, e);
            }

            throw new InvalidOperationException("Expected " + name + " to throw " + typeof(T).Name + " but it completed.");
        }
    }
}
