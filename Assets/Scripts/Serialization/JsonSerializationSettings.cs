using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

public static class JsonSerializationSettings
{
    private static readonly JsonSerializerSettings modelJsonSettings;
    private static readonly JsonSerializerSettings aITrainingModelJsonSettings;
    private static readonly JsonSerializerSettings diseaseJsonSettings;
    private static readonly JsonSerializerSettings splinePresetJsonSettings;

    static JsonSerializationSettings()
    {
        modelJsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                IgnoreSerializableAttribute = true,
            },
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter> {
                    //new TrainingModelJsonConverter(),
                    //new TrainingModelBlenshapesJsonConverter(),
                    new SplineNodeJsonConverter(),
                    new Vector4JsonConverter(),
                    new Vector3JsonConverter(),
                    new Vector2JsonConverter(),
                    new DiseaseJsonConverter(),
                },
        };

        aITrainingModelJsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                IgnoreSerializableAttribute = true,
            },
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new AITrainingModelJsonConverter(),
                new AITrainingSplineNodeJsonConverter(),
                new AITrainingModelBlenshapesJsonConverter(),
                new Vector4JsonConverter(),
                new Vector3JsonConverter(),
                new Vector2JsonConverter(),
                new DiseaseJsonConverter(),
            },
        };

        diseaseJsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                IgnoreSerializableAttribute = true,
            },
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new DiseaseJsonConverter(),
                /*new Vector4JsonConverter(),
                new Vector3JsonConverter(),
                new Vector2JsonConverter(),*/
            },
        };

        splinePresetJsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                IgnoreSerializableAttribute = true,
            },
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new SplineNodeJsonConverter(),
                /*new Vector4JsonConverter(),
                new Vector3JsonConverter(),
                new Vector2JsonConverter(),*/
            },
        };
    }

    public static JsonSerializerSettings ModelJsonSettings => modelJsonSettings;
    public static JsonSerializerSettings AITrainingModelJsonSettings => aITrainingModelJsonSettings;
    public static JsonSerializerSettings DiseaseJsonSettings => diseaseJsonSettings;
    public static JsonSerializerSettings SplinePresetJsonSettings => splinePresetJsonSettings;
}