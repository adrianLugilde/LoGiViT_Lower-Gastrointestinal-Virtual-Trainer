using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using SplineMesh;
using LargeIntestine;
using System.Diagnostics;

public class AITrainingModelJsonConverter : JsonConverter<Model>
{
    public override void WriteJson(JsonWriter writer, Model value, JsonSerializer serializer)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("splineNodes");
        serializer.Serialize(writer, value.SplineNodes);

        //writer.WritePropertyName("renderersBlendshapesWeights");
        //serializer.Serialize(writer, value.renderersBlendshapesWeights);

        writer.WriteEndObject();
    }

    public override Model ReadJson(JsonReader reader, Type objectType, Model existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        Debug.WriteLine("Reading Model from JSON");
        JObject obj = JObject.Load(reader);

        var splineNodes = obj["splineNodes"].ToObject<List<SplineNode>>(serializer);
        //var renderersBlendshapesWeights = obj["renderersBlendshapesWeights"].ToObject<List<BlendshapesWeights>>(serializer);

        return new Model
        {
            SplineNodes = splineNodes,
            //renderersBlendshapesWeights = renderersBlendshapesWeights
        };
    }
}