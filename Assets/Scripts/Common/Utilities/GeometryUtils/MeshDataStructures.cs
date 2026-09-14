using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace GeometryUtils
{
    [Serializable]
    public struct Vertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;
        public Vector4 tangent;
        public int index; //TODO SI REALMENTE NO USO ESTO, QUITARLO


        public Vertex(Vector3 position, Vector3 normal, Vector2 uv, Vector4 tangent, int index)
        {
            this.position = position;
            this.normal = normal;
            this.uv = uv;
            this.tangent = tangent;
            this.index = index;
        }

        public Vertex(Vector3 position, Vector3 normal, Vector3 uv, Vector4 tangent)
            : this(position, normal, uv, tangent, 0)
        {
        }

        public Vertex(Vector3 position, Vector3 normal, Vector2 uv)
            : this(position, normal, uv, Vector4.zero, 0)
        {
        }

        public Vertex(Vector3 position, Vector3 normal)
            : this(position, normal, Vector2.zero, Vector4.zero, 0)
        {
        }

        public Vertex(Vector3 position)
            : this(position, Vector3.zero, Vector2.zero, Vector4.zero, 0)
        {
        }

        public Vertex(Vector3 position, int index)
            : this(position, Vector3.zero, Vector2.zero, Vector4.zero, index)
        {
        }
    }

    [Serializable]
    public struct Edge
    {
        public Vertex v1;
        public Vertex v2;

        public Edge(Vertex v1, Vertex v2)
        {
            // ensure the same order to guarantee equality
            if (v1.GetHashCode() > v2.GetHashCode())
            {
                this.v1 = v1; this.v2 = v2;
            }
            else
            {
                this.v1 = v2; this.v2 = v1;
            }
        }
    }

    [Serializable]
    public struct Triangle
    {

        public Vertex v1;
        public Vertex v2;
        public Vertex v3;

        public Triangle(Vertex v1, Vertex v2, Vertex v3)
        {
            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;
        }
    }

    [Serializable]
    public struct Blendshape
    {
        public string name;
        public BlendshapeFrame[] frames;

        public Blendshape(Mesh sourceMesh, int blendshapeIdx)
        {
            name = sourceMesh.GetBlendShapeName(blendshapeIdx);
            frames = new BlendshapeFrame[sourceMesh.GetBlendShapeFrameCount(blendshapeIdx)];
            for (int frameIdx = 0; frameIdx < frames.Length; frameIdx++)
            {
                frames[frameIdx] = new BlendshapeFrame(sourceMesh, blendshapeIdx, frameIdx);
            }
        }
    }

    [Serializable]
    public struct BlendshapeFrame
    {
        public float frameWeight;
        public int frameIndex;
        //public BlendshapeVertex[] vertices;
        public Vector3[] deltaVertices;
        public Vector3[] deltaNormals;
        public Vector3[] deltaTangents;

        public BlendshapeFrame(Mesh sourceMesh, int blendshapeIdx, int frameIndex)
        {
            /*var vertexCount = sourceMesh.vertexCount;
            var deltaVertices = new Vector3[vertexCount];
            var deltaNormals = new Vector3[vertexCount];
            var deltaTangents = new Vector3[vertexCount];
            frameWeight = sourceMesh.GetBlendShapeFrameWeight(blendshapeIdx, frameIndex);
            this.frameIndex = frameIndex;
            sourceMesh.GetBlendShapeFrameVertices(blendshapeIdx, frameIndex, deltaVertices, deltaNormals, deltaTangents);
            NativeArray<Vector3> positionsNA = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
            NativeArray<Vector3> normalsNA = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
            NativeArray<Vector3> tangentsNA = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
            NativeArray<BlendshapeVertex> verticesNA = new NativeArray<BlendshapeVertex>(vertexCount, Allocator.TempJob);
            positionsNA.CopyFrom(deltaVertices);
            normalsNA.CopyTo(deltaNormals);
            tangentsNA.CopyFrom(deltaTangents);

            vertices = new BlendshapeVertex[vertexCount];
            for(int i = 0; i < vertexCount; i++)
            {
                vertices[i] = new BlendshapeVertex(deltaVertices[i], deltaNormals[i], deltaTangents[i]);
            }

            BuildVerticesJob buildVerticesJob = new BuildVerticesJob
            {
                positions = positionsNA,
                normals = normalsNA,
                tangents = tangentsNA,
                vertices = verticesNA
            };

            JobHandle buildVerticesJobHandle = buildVerticesJob.Schedule(vertexCount, 1);
            buildVerticesJobHandle.Complete();
            verticesNA.CopyTo(vertices);

            positionsNA.Dispose();
            normalsNA.Dispose();
            tangentsNA.Dispose();
            verticesNA.Dispose();*/
            var vertexCount = sourceMesh.vertexCount;
            deltaVertices = new Vector3[vertexCount];
            deltaNormals = new Vector3[vertexCount];
            deltaTangents = new Vector3[vertexCount];
            frameWeight = sourceMesh.GetBlendShapeFrameWeight(blendshapeIdx, frameIndex);
            this.frameIndex = frameIndex;
            sourceMesh.GetBlendShapeFrameVertices(blendshapeIdx, frameIndex, deltaVertices, deltaNormals, deltaTangents);
            /*for(int i = 0; i < vertexCount; i++)
            {
                deltaVertices[i] = new Vector3(deltaVertices[i].x, deltaVertices[i].y, deltaVertices[i].z);
                deltaNormals[i] = new Vector3(deltaNormals[i].x, deltaNormals[i].y, deltaNormals[i].z);
                deltaTangents[i] = new Vector3(deltaTangents[i].x, deltaTangents[i].y, deltaTangents[i].z);
            }*/
        }

        public struct BuildVerticesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> positions;
            [ReadOnly] public NativeArray<Vector3> normals;
            [ReadOnly] public NativeArray<Vector3> tangents;
            [NativeDisableParallelForRestriction] public NativeArray<BlendshapeVertex> vertices;
            public void Execute(int index)
            {
                vertices[index] = new BlendshapeVertex(positions[index], normals[index], tangents[index]);
            }
        }
    }

    public struct BlendshapeVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector3 tangent;


        public BlendshapeVertex(Vector3 position, Vector3 normal,  Vector3 tangent)
        {
            this.position = position;
            this.normal = normal;
            this.tangent = tangent;
        }
    }

    public struct BlendshapeVertices
    {
        public Vector3[] positions;
        public Vector3[] normals;
        public Vector3[] tangents;

        public BlendshapeVertices(int vertexCount)
        {
            positions = new Vector3[vertexCount];
            normals = new Vector3[vertexCount];
            tangents = new Vector3[vertexCount];
        }
    }


    [Serializable]
    public struct OldBlendshape
    {
        public string name;
        public OldBlendShapeFrame[] frames;

        public OldBlendshape(Mesh sourceMesh, int blendshapeIdx)
        {
            name = sourceMesh.GetBlendShapeName(blendshapeIdx);
            frames = new OldBlendShapeFrame[sourceMesh.GetBlendShapeFrameCount(blendshapeIdx)];
            for (int frameIdx = 0; frameIdx < frames.Length; frameIdx++)
            {
                frames[frameIdx] = new OldBlendShapeFrame(sourceMesh, blendshapeIdx, frameIdx);
            }
        }

    }

    [Serializable]
    public struct OldBlendShapeFrame
    {
        public float frameWeight;
        public int frameIndex;
        public Vector3[] deltaVertices;
        public Vector3[] deltaNormals;
        public Vector3[] deltaTangents;

        public OldBlendShapeFrame(Mesh sourceMesh, int blendshapeIdx, int frameIndex)
        {
            deltaVertices = new Vector3[sourceMesh.vertexCount];
            deltaNormals = new Vector3[sourceMesh.vertexCount];
            deltaTangents = new Vector3[sourceMesh.vertexCount];
            frameWeight = sourceMesh.GetBlendShapeFrameWeight(blendshapeIdx, frameIndex);
            this.frameIndex = frameIndex;
            sourceMesh.GetBlendShapeFrameVertices(blendshapeIdx, frameIndex, deltaVertices, deltaNormals, deltaTangents);
        }
    }
}