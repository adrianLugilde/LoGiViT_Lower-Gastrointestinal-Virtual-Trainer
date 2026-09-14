using System.Collections.Generic;
using UnityEngine;

namespace ModelEditor
{
    /// <summary>
    /// Interface for a section of a generated model.
    /// Allows different organ generators to expose their sections uniformly.
    /// </summary>
    public interface IModelSection
    {
        /// <summary>
        /// The renderer for this section (typically SkinnedMeshRenderer for blendshapes)
        /// </summary>
        Renderer Renderer { get; }
        
        /// <summary>
        /// The collider for this section
        /// </summary>
        Collider Collider { get; }
        
        /// <summary>
        /// Gets a blendshape weight by name
        /// </summary>
        /// <param name="blendshapeName">The name of the blendshape</param>
        /// <returns>The weight value (0-100), or 0 if not found</returns>
        float GetBlendShapeWeight(string blendshapeName);
        
        /// <summary>
        /// Sets blendshape weights from a list (by index order)
        /// </summary>
        void SetRendererBlendshapeWeights(List<float> weights);
        
        /// <summary>
        /// Gets all blendshape values as a dictionary
        /// </summary>
        IDictionary<string, float> GetBlendshapeValues();
    }
}
