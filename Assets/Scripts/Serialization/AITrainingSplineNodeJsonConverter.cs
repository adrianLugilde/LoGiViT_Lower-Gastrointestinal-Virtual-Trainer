using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

public class AITrainingSplineNodeJsonConverter : JsonConverter<SplineMesh.SplineNode>
{
    public override void WriteJson(JsonWriter writer, SplineMesh.SplineNode value, JsonSerializer serializer)
    {
        writer.WriteStartObject();

        // Position as array
        writer.WritePropertyName("pos");
        WriteVector3AsArray(writer, RoundVector3(value.Position));

        // Direction as array
        writer.WritePropertyName("dir");
        WriteVector3AsArray(writer, RoundVector3(value.Direction));

        // Up as array
        writer.WritePropertyName("up");
        WriteVector3AsArray(writer, RoundVector3(value.Up));

        writer.WriteEndObject();
    }

    private void WriteVector3AsArray(JsonWriter writer, Vector3 vector)
    {
        writer.WriteStartArray();
        writer.WriteValue(vector.x);
        writer.WriteValue(vector.y);
        writer.WriteValue(vector.z);
        writer.WriteEndArray();
    }

    private Vector3 RoundVector3(Vector3 vector)
    {
        return new Vector3(
            float.Parse(Math.Round(vector.x, 4, MidpointRounding.AwayFromZero).ToString("0.####")),
            float.Parse(Math.Round(vector.y, 4, MidpointRounding.AwayFromZero).ToString("0.####")),
            float.Parse(Math.Round(vector.z, 4, MidpointRounding.AwayFromZero).ToString("0.####"))
        );
    }

    public override SplineMesh.SplineNode ReadJson(JsonReader reader, Type objectType, SplineMesh.SplineNode existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);

        Vector3 position = ReadVector3FromArray(obj["pos"] as JArray);
        Vector3 direction = ReadVector3FromArray(obj["dir"] as JArray);
        Vector3 up = ReadVector3FromArray(obj["up"] as JArray);
        Vector2 scale = Vector2.one;
        float roll = 0f;

        return new SplineMesh.SplineNode(position, direction, roll, up)
        {
            Scale = scale
        };
    }

    private Vector3 ReadVector3FromArray(JArray jArray)
    {
        if (jArray != null && jArray.Count >= 3)
        {
            return new Vector3(
                jArray[0].Value<float>(),
                jArray[1].Value<float>(),
                jArray[2].Value<float>()
            );
        }
        return Vector3.zero;
    }
}