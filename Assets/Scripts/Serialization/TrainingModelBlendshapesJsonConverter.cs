using LargeIntestine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

//TODO SEMS UNUSED REMOVE IF SO
public class TrainingModelBlenshapesJsonConverter : JsonConverter<List<BlendshapesWeights>>
{
    public override void WriteJson(JsonWriter writer, List<BlendshapesWeights> value, JsonSerializer serializer)
    {
        writer.WriteStartArray();

        for (int i = 0; i < value.Count; i++)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(i.ToString());
            //todo UPDATE THIS FOR NEW MODELS
            //serializer.Serialize(writer, value[i].weightsPerBlendshape);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    public override List<BlendshapesWeights> ReadJson(JsonReader reader, Type objectType, List<BlendshapesWeights> existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JArray array = JArray.Load(reader);
        var result = new List<BlendshapesWeights>();

        foreach (JObject obj in array.Children<JObject>())
        {
            foreach (var property in obj.Properties())
            {
                var weights = property.Value.ToObject<List<float>>(serializer);
                //todo UPDATE THIS FOR NEW MODELS
                //result.Add(new BlendshapesWeights { weightsPerBlendshape = weights });
            }
        }

        return result;
    }
}