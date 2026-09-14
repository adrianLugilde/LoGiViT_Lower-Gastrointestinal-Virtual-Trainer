using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

public class DiseaseJsonConverter : JsonConverter<Disease>
{
    public override void WriteJson(JsonWriter writer, Disease value, JsonSerializer serializer)
    {
        var obj = new JObject
        {
            { "type",           JToken.FromObject(value.Type, serializer) },
            { "polypProfileId", value.PolypProfileId },
            { "location",       value.Location.HasValue ? JToken.FromObject(value.Location.Value, serializer) : JValue.CreateNull() },
            { "submeshIndex",   value.SubmeshIndex },
        };

        if (value is Polyp polyp)
        {
            obj.Add("size", JToken.FromObject(polyp.Size, serializer));
        }

        obj.WriteTo(writer);
    }

    public override Disease ReadJson(JsonReader reader, Type objectType, Disease existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);

        var type = obj["type"].ToObject<Disease.DiseaseType>(serializer);
        var profileId = obj["polypProfileId"]?.Value<string>();

        Disease disease;
        if (type == Disease.DiseaseType.Polyp)
        {
            var polyp = new Polyp();
            if (obj["size"] != null)
                polyp.Size = obj["size"].ToObject<Vector2>(serializer);
            disease = polyp;
        }
        else
        {
            disease = new Disease(type);
        }

        if (!string.IsNullOrEmpty(profileId))
        {
            var catalog = PolypProfileCatalog.Load();
            var profile = catalog != null ? catalog.GetById(profileId) : null;
            if (profile != null)
            {
                disease.PolypProfileId  = profile.Id;
                disease.Name            = profile.displayName;
                disease.Description     = profile.description;
                disease.Mesh            = profile.mesh;
                disease.Materials       = profile.materials;
                disease.MeshSprite      = profile.meshSprite;
                disease.Image           = profile.image;

                if (disease is Polyp polyp)
                {
                    polyp.JnetClass  = profile.jnetClass;
                    polyp.ParisClass = profile.parisClass;
                }
            }
            else
            {
                Debug.LogWarning($"[DiseaseJsonConverter] No PolypProfile found for id '{profileId}'. Disease will have no visual assets.");
                disease.PolypProfileId = profileId;
            }
        }
        else
        {
            Debug.LogWarning("[DiseaseJsonConverter] Disease entry has no polypProfileId.");
        }

        if (obj["location"] != null && obj["location"].Type != JTokenType.Null)
            disease.Location = obj["location"].ToObject<Disease.IntestineLocation>(serializer);

        if (obj["submeshIndex"] != null)
            disease.SubmeshIndex = obj["submeshIndex"].Value<int>();

        return disease;
    }
}
