using System;
using Newtonsoft.Json;

namespace Ditto.Sqs.Consumer
{
    /// <summary>
    /// JSON converter that treats a property as a raw JSON string
    /// </summary>
    public class JsonStringConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(string);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            => throw new NotImplementedException();

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            => writer.WriteRawValue(value as string);
    }
}