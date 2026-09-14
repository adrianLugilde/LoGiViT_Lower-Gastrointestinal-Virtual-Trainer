using LargeIntestine;
using Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class LI_BlenshapeConfiguration : JsonData
{
    /**** JsonData Implementation ****/
    public override string DataPath => Application.streamingAssetsPath;
    public override string FileExtension => FileManager.LibcFileExtension;
    public override JsonSerializerSettings JsonSettings => null;
    
    public List<BlendshapesWeights> renderersBlendshapeWeigths;

    public LI_BlenshapeConfiguration()
    {
        FileID = 108217697723507;
    }

    public LI_BlenshapeConfiguration(string filePath) 
    {
        ReadFromFile(filePath);
    }

    public int Build(List<SkinnedMeshRenderer> renderers)
    {

        renderersBlendshapeWeigths = new List<BlendshapesWeights>();
        foreach (var renderer in renderers)
        {
            renderersBlendshapeWeigths.Add(new BlendshapesWeights().SetValues(renderer));
        }
        return BasicMessages.None;
    }

    public new void Save(string destPath = null)
    {
        base.Save(null, null, destPath);
    }
}
