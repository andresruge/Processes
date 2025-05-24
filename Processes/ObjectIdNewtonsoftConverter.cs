using Newtonsoft.Json;
using MongoDB.Bson;
using System;

public class ObjectIdNewtonsoftConverter : JsonConverter<ObjectId>
{
    public override ObjectId ReadJson(JsonReader reader, Type objectType, ObjectId existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            if (ObjectId.TryParse((string)reader.Value, out var objectId))
            {
                return objectId;
            }
            throw new JsonSerializationException($"Cannot convert invalid string '{reader.Value}' to ObjectId.");
        }
        // Handle null value explicitly if needed, though ObjectId is a struct
        if (reader.TokenType == JsonToken.Null)
        {
             // Depending on whether ObjectId is nullable in your context, you might return default(ObjectId) or throw
             throw new JsonSerializationException("Cannot convert null to ObjectId.");
        }
        throw new JsonSerializationException($"Unexpected token type {reader.TokenType} when reading ObjectId.");
    }

    public override void WriteJson(JsonWriter writer, ObjectId value, JsonSerializer serializer)
    {
        writer.WriteValue(value.ToString());
    }
}