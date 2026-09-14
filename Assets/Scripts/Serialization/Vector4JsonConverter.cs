using Newtonsoft.Json;
using System;
using UnityEngine;

public class Vector4JsonConverter : JsonConverter<Vector4>
{
    public override void WriteJson(JsonWriter writer, Vector4 value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x");
        writer.WriteValue(value.x);
        writer.WritePropertyName("y");
        writer.WriteValue(value.y);
        writer.WritePropertyName("z");
        writer.WriteValue(value.z);
        writer.WritePropertyName("w");
        writer.WriteValue(value.w);
        writer.WriteEndObject();
    }

    public override Vector4 ReadJson(JsonReader reader, Type objectType, Vector4 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        float x = 0, y = 0, z = 0, w = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.PropertyName)
            {
                switch (reader.Value.ToString())
                {
                    case "x":
                        reader.Read();
                        x = (float)(double)reader.Value;
                        break;
                    case "y":
                        reader.Read();
                        y = (float)(double)reader.Value;
                        break;
                    case "z":
                        reader.Read();
                        z = (float)(double)reader.Value;
                        break;
                    case "w":
                        reader.Read();
                        w = (float)(double)reader.Value;
                        break;
                }
            }
            if (reader.TokenType == JsonToken.EndObject) break;
        }

        return new Vector4(x, y, z, w);
    }
}
