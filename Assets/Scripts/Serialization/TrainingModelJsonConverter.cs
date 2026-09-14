using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using SplineMesh;
using LargeIntestine;

public class TrainingModelJsonConverter : JsonConverter<Model>
{
    public override void WriteJson(JsonWriter writer, Model value, JsonSerializer serializer)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("fileName");
        serializer.Serialize(writer, value.FileName);

        writer.WritePropertyName("fileID");
        serializer.Serialize(writer, value.FileID);

        writer.WritePropertyName("splineNodes");
        serializer.Serialize(writer, value.SplineNodes);

        writer.WritePropertyName("renderersBlendshapesDicts");
        serializer.Serialize(writer, value.RenderersBlendshapesDicts); 

        writer.WritePropertyName("segmentsStartValueInSpline");
        serializer.Serialize(writer, value.SegmentsStartValueInSpline);

        writer.WritePropertyName("description");
        serializer.Serialize(writer, value.Description);

        writer.WritePropertyName("diseases");
        serializer.Serialize(writer, value.Diseases);

        writer.WritePropertyName("generationConfigurationFile");
        serializer.Serialize(writer, value.GenerationConfigurationFile);

        writer.WriteEndObject();
    }

    public override Model ReadJson(JsonReader reader, Type objectType, Model existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);

        var splineNodes = obj["splineNodes"].ToObject<List<SplineNode>>(serializer);
        var renderersBlendshapesDicts = obj["renderersBlendshapesDicts"].ToObject<List<SerializableDictionary<string, float>>>(serializer);
        var fileName = obj["fileName"].ToObject<string>(serializer);
        var fileID = obj["fileID"].ToObject<long>(serializer);
        var description = obj["description"]?.ToObject<string>(serializer) ?? "";
        var segmentsStartValueInSpline = obj["segmentsStartValueInSpline"]?.ToObject<List<float>>(serializer) ?? new List<float>();
        var diseases = obj["diseases"]?.ToObject<List<Disease>>(serializer) ?? new List<Disease>();
        var generationConfigurationFile = obj["generationConfigurationFile"]?.ToObject<string>(serializer);

        return new Model(
            fileID,
            fileName,
            description,
            splineNodes,
            segmentsStartValueInSpline,
            renderersBlendshapesDicts,
            diseases,
            generationConfigurationFile
        );
    }
}