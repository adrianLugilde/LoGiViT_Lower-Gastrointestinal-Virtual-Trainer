using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public struct BlendVertex
{
    public Vector3 position;
    public Vector3 normal;
    public Vector3 tangent;

    public BlendVertex(Vector3 position, Vector3 normal, Vector3 tangent)
    {
        this.position = position;
        this.normal = normal;
        this.tangent = tangent;
    }
}
