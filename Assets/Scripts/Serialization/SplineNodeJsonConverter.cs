using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

public class SplineNodeJsonConverter : JsonConverter<SplineMesh.SplineNode>
{
    public override void WriteJson(JsonWriter writer, SplineMesh.SplineNode value, JsonSerializer serializer)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("position");
        serializer.Serialize(writer, value.Position);

        writer.WritePropertyName("direction");
        serializer.Serialize(writer, value.Direction);

        writer.WritePropertyName("up");
        serializer.Serialize(writer, value.Up);

        writer.WritePropertyName("scale");
        serializer.Serialize(writer, new Vector2Wrapper(value.Scale));

        writer.WritePropertyName("roll");
        writer.WriteValue(value.Roll);

        writer.WriteEndObject();
    }

    public override SplineMesh.SplineNode ReadJson(JsonReader reader, Type objectType, SplineMesh.SplineNode existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);

        Vector3 position = obj["position"].ToObject<Vector3>(serializer);
        Vector3 direction = obj["direction"].ToObject<Vector3>(serializer);
        Vector3 up = obj["up"].ToObject<Vector3>(serializer);
        Vector2 scale = obj["scale"].ToObject<Vector2Wrapper>(serializer).ToVector2();
        float roll = obj["roll"].Value<float>();

        return new SplineMesh.SplineNode(position, direction, roll, up)
        {
            Scale = scale
        };
    }
}