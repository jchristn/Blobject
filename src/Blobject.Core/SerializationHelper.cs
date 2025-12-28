namespace Blobject.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization; 

    /// <summary>
    /// Serialization helper.
    /// </summary>
    public static class SerializationHelper
    {
        #region Public-Members

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        /// <summary>
        /// Copy an object.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="source">Source object.</param>
        /// <returns>Copy.</returns>
        public static T CopyObject<T>(T source)
        {
            if (source == null) return default(T);

            string json = SerializeJson(source, false);
            try
            {
                return DeserializeJson<T>(json);
            }
            catch (Exception)
            {
                return default(T);
            }
        }

        /// <summary>
        /// Serialize JSON.
        /// </summary>
        /// <param name="obj">Object.</param>
        /// <param name="pretty">Pretty print.</param>
        /// <returns>JSON string.</returns>
        public static string SerializeJson(object obj, bool pretty)
        {
            if (obj == null) return null;

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

            // see https://github.com/dotnet/runtime/issues/43026
            options.Converters.Add(new ExceptionConverter<Exception>());
            options.Converters.Add(new NameValueCollectionConverter());
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new DateTimeConverter());

            if (!pretty)
            {
                options.WriteIndented = false;
                return JsonSerializer.Serialize(obj, options);
            }
            else
            {
                options.WriteIndented = true;
                return JsonSerializer.Serialize(obj, options);
            }
        }

        /// <summary>
        /// Deserialize JSON. 
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Instance.</returns>
        public static T DeserializeJson<T>(string json)
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.AllowTrailingCommas = true;
            options.ReadCommentHandling = JsonCommentHandling.Skip;
            options.NumberHandling = JsonNumberHandling.AllowReadingFromString;

            options.Converters.Add(new ExceptionConverter<Exception>());
            options.Converters.Add(new NameValueCollectionConverter());
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new DateTimeConverter());
            return JsonSerializer.Deserialize<T>(json, options);
        }

        /// <summary>
        /// Deserialize JSON. 
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="data">JSON data.</param>
        /// <returns>Instance.</returns>
        public static T DeserializeJson<T>(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            return JsonSerializer.Deserialize<T>(data);
        }

        #endregion

        #region Private-Classes

        private class ExceptionConverter<TExceptionType> : JsonConverter<TExceptionType>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(Exception).IsAssignableFrom(typeToConvert);
            }

            public override TExceptionType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException("Deserializing exceptions is not allowed");
            }

            public override void Write(Utf8JsonWriter writer, TExceptionType value, JsonSerializerOptions options)
            {
                var serializableProperties = value.GetType()
                    .GetProperties()
                    .Select(uu => new { uu.Name, Value = uu.GetValue(value) })
                    .Where(uu => uu.Name != nameof(Exception.TargetSite));

                if (options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull)
                {
                    serializableProperties = serializableProperties.Where(uu => uu.Value != null);
                }

                var propList = serializableProperties.ToList();

                if (propList.Count == 0)
                {
                    // Nothing to write
                    return;
                }

                writer.WriteStartObject();

                foreach (var prop in propList)
                {
                    writer.WritePropertyName(prop.Name);
                    JsonSerializer.Serialize(writer, prop.Value, options);
                }

                writer.WriteEndObject();
            }
        }

        private class NameValueCollectionConverter : JsonConverter<NameValueCollection>
        {
            public override NameValueCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

            public override void Write(Utf8JsonWriter writer, NameValueCollection value, JsonSerializerOptions options)
            {
                if (value != null)
                {
                    Dictionary<string, string> val = new Dictionary<string, string>();

                    for (int i = 0; i < value.AllKeys.Count(); i++)
                    {
                        string key = value.Keys[i];
                        string[] values = value.GetValues(key);
                        string formattedValue = null;

                        if (values != null && values.Length > 0)
                        {
                            int added = 0;

                            for (int j = 0; j < values.Length; j++)
                            {
                                if (!String.IsNullOrEmpty(values[j]))
                                {
                                    if (added == 0) formattedValue += values[j];
                                    else formattedValue += ", " + values[j];
                                }

                                added++;
                            }
                        }

                        val.Add(key, formattedValue);
                    }

                    System.Text.Json.JsonSerializer.Serialize(writer, val);
                }
            }
        }

        private class DateTimeConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(
                        ref Utf8JsonReader reader,
                        Type typeToConvert,
                        JsonSerializerOptions options)
            {
                string str = reader.GetString();

                DateTime val;
                if (DateTime.TryParse(str, out val)) return val;

                throw new FormatException("The JSON value '" + str + "' could not be converted to System.DateTime.");
            }

            public override void Write(
                Utf8JsonWriter writer,
                DateTime dateTimeValue,
                JsonSerializerOptions options)
            {
                writer.WriteStringValue(dateTimeValue.ToString(
                    "yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture));
            }

            private List<string> _AcceptedFormats = new List<string>
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ssK",
                "yyyy-MM-dd HH:mm:ss.ffffff",
                "yyyy-MM-ddTHH:mm:ss.ffffff",
                "yyyy-MM-ddTHH:mm:ss.fffffffK",
                "yyyy-MM-dd",
                "MM/dd/yyyy HH:mm",
                "MM/dd/yyyy hh:mm tt",
                "MM/dd/yyyy H:mm",
                "MM/dd/yyyy h:mm tt",
                "MM/dd/yyyy HH:mm:ss"
            };
        }
        #endregion
    }
}