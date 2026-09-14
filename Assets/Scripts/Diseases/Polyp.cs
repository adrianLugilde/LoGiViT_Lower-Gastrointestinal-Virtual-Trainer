using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class Polyp : Disease
{
    [SerializeField, JsonProperty] private JNETClassification jnetClass;
    [SerializeField, JsonProperty] private ParisClassification parisClass;
    [SerializeField, JsonProperty] private Vector2 size = Vector2.zero;

    public JNETClassification JnetClass
    {
        get => jnetClass;
        set => jnetClass = value;
    }

    public ParisClassification ParisClass
    {
        get => parisClass;
        set => parisClass = value;
    }

    public Vector2 Size
    {
        get => size;
        set => size = value;
    }

    public Polyp() : base(DiseaseType.Polyp) { }

    public Polyp(PolypProfile profile) : base(DiseaseType.Polyp)
    {
        PolypProfileId = profile.Id;
        Name = profile.displayName;
        Description = profile.description;
        Mesh = profile.mesh;
        Materials = profile.materials;
        MeshSprite = profile.meshSprite;
        Image = profile.image;
        JnetClass = profile.jnetClass;
        ParisClass = profile.parisClass;
    }

    public Polyp(Polyp otherPolyp) : base(DiseaseType.Polyp)
    {
        PolypProfileId = otherPolyp.PolypProfileId;
        Name = otherPolyp.Name;
        Description = otherPolyp.Description;
        Location = otherPolyp.Location;
        Mesh = otherPolyp.Mesh;
        SubmeshIndex = otherPolyp.SubmeshIndex;
        Materials = otherPolyp.Materials;
        MeshSprite = otherPolyp.MeshSprite;
        Image = otherPolyp.Image;
        JnetClass = otherPolyp.JnetClass;
        ParisClass = otherPolyp.ParisClass;
        Size = otherPolyp.Size;
    }

    /// <summary>
    /// Specifies the JNET classification of the polyp.
    /// </summary>
    public enum JNETClassification
    {
        Class1 = 0,
        Class2A = 1,
        Class2B = 2,
        Class3 = 3
    }

    public string GetSizeString()
    {
        return $"{size.x}x{size.y}";
    }

    /// <summary>
    /// Specifies the Paris classification of the polyp.
    /// </summary>
    public enum ParisClassification
    {
        Ip = 0,
        Isp = 1,
        Is = 2,
        Iia = 3,
        IiaC = 4,
        Iib = 5,
        Iic = 6,
        IicIia = 7,
        Unclassifiable = 8
    }

    private static readonly Dictionary<JNETClassification, string> jnetClassificationStrings = new Dictionary<JNETClassification, string>
    {
        { JNETClassification.Class1,  "1" },
        { JNETClassification.Class2A, "2A" },
        { JNETClassification.Class2B, "2B" },
        { JNETClassification.Class3,  "3" },
    };

    public static string GetJnetClassString(JNETClassification classification)
    {
        return jnetClassificationStrings[classification];
    }

    public static IEnumerable<string> GetJnetClassificationStrings()
    {
        return Enum.GetValues(typeof(JNETClassification))
                   .Cast<JNETClassification>()
                   .Select(GetJnetClassString);
    }

    public static bool TryGetJnetClassification(string classificationString, out JNETClassification classification)
    {
        classification = JNETClassification.Class1;
        foreach (var kvp in jnetClassificationStrings)
        {
            if (kvp.Value == classificationString)
            {
                classification = kvp.Key;
                return true;
            }
        }
        return false;
    }

    private static readonly Dictionary<ParisClassification, string> parisClassificationStrings = new Dictionary<ParisClassification, string>
    {
        { ParisClassification.Ip, "0-Ip" },
        { ParisClassification.Isp, "0-Isp" },
        { ParisClassification.Is, "0-Is" },
        { ParisClassification.Iia, "0-IIa" },
        { ParisClassification.IiaC, "0-IIa|c" },
        { ParisClassification.Iib, "0-IIb" },
        { ParisClassification.Iic, "0-IIc" },
        { ParisClassification.IicIia, "0-IIc|IIa" },
        { ParisClassification.Unclassifiable, "Unclassifiable" }
    };

    public static string GetParisClassString(ParisClassification classification)
    {
        return parisClassificationStrings[classification];
    }

    public static IEnumerable<string> GetParisClassificationStrings()
    {
        return Enum.GetValues(typeof(ParisClassification))
                   .Cast<ParisClassification>()
                   .Select(Polyp.GetParisClassString);
    }

    public static bool TryGetParisClassification(string classificationString, out ParisClassification classification)
    {
        classification = ParisClassification.Ip; // Default value
        foreach (var kvp in parisClassificationStrings)
        {
            if (kvp.Value == classificationString)
            {
                classification = kvp.Key;
                return true;
            }
        }
        return false;
    }
}