using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace GeometryUtils
{
    public struct MeshData
    {
        public Vector3 translation;
        public Quaternion rotation;
        public Vector3 scale;
        private bool keepBlenshapes;
        public List<Material> materials { get; private set; }
        public Mesh OriginalMesh { get; private set; }

        private Vertex[] vertices;
        internal Vertex[] Vertices
        {
            get
            {
                if (vertices == null) BuildData();
                return vertices;
            }
        }

        private Vertex[] originalVertices;
        public Vertex[] OriginalVertices
        {
            get
            {
                if (vertices == null) BuildData();
                return originalVertices;
            }
        }

        private Blendshape[] blendshapes;
        internal Blendshape[] Blendshapes
        {
            get
            {
                if (vertices == null) BuildData();
                return blendshapes;
            }
        }

        private Blendshape[] originalBlendshapes;
        public Blendshape[] OriginalBlendshapes
        {
            get
            {
                if (vertices == null) BuildData();
                return originalBlendshapes;
            }
        }

        private int[] triangles;
        public int[] Triangles
        {
            get
            {
                if (vertices == null) BuildData();
                return triangles;
            }
        }

        private float minX;
        public float MinX
        {
            get
            {
                if (vertices == null) BuildData();
                return minX;
            }
        }

        private float length;
        public float Length
        {
            get
            {
                if (vertices == null) BuildData();
                return length;
            }
        }

        private int vertexCount;
        public int VertexCount
        {
            get
            {
                if (vertices == null) BuildData();
                return vertexCount;
            }
        }

        private int blendshapeCount;
        public int BlendshapeCount
        {
            get
            {
                if (vertices == null) BuildData();
                return blendshapeCount;
            }
        }

        public bool _dataReady { get; private set; }

        public MeshData(Mesh mesh, Vector3 translation, Quaternion rotation, Vector3 scale, List<Material> submeshMaterials, bool keepBlenshapes)
        {
            this.keepBlenshapes = keepBlenshapes;
            var sourceMesh = new Mesh();
            MeshUtils.CopyMesh(sourceMesh, mesh, this.keepBlenshapes);
            OriginalMesh = sourceMesh;
            this.translation = translation;
            this.rotation = rotation;
            this.scale = scale;
            materials = submeshMaterials;
            vertexCount = OriginalMesh.vertexCount;
            var originalPositions = OriginalMesh.vertices;
            var originalNormals = OriginalMesh.normals;
            vertices = new Vertex[vertexCount];
            originalVertices = new Vertex[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                originalVertices[i] = new Vertex(originalPositions[i], originalNormals[i]);
            }
            blendshapeCount = OriginalMesh.blendShapeCount;
            if (keepBlenshapes)
            {
                originalBlendshapes = new Blendshape[blendshapeCount];
                blendshapes = new Blendshape[blendshapeCount];
                for (int i = 0; i < blendshapeCount; i++)
                {
                    originalBlendshapes[i] = new Blendshape(OriginalMesh, i);
                }

            }
            else
            {
                originalBlendshapes = null;
                blendshapes = null;
            }
            triangles = null;
            minX = 0;
            length = 0;
            _dataReady = false;
        }

        public MeshData(Mesh mesh, Vector3 translation, Quaternion rotation, Vector3 scale, bool keepBlenshapes)
         : this(mesh, translation, rotation, scale, null, keepBlenshapes)
        {

        }

        public void BuildData()
        {
            UpdateData(translation, rotation, scale);
            _dataReady = true;
        }

        public void InversedBuildData()
        {
            UpdateData(-translation, Quaternion.Inverse(rotation), new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z), true);
            _dataReady = true;
        }


        /// <summary>
        /// Computes only the triangles and vertices data from the original mesh given the transform parameters
        /// </summary>
        /// <param name="translation"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        public void UpdateBasicData(Vector3 translation, Quaternion rotation, Vector3 scale)
        {
            UpdateTriangles();

            NativeArray<Vertex> verticesNA = new NativeArray<Vertex>(vertexCount, Allocator.TempJob);
            verticesNA.CopyFrom(originalVertices);

            TransformVertices(verticesNA, translation, rotation, scale);

            verticesNA.CopyTo(vertices);
            verticesNA.Dispose();
        }


        /// <summary>
        /// Computes triangles, vertices, bounds along x and blendshapes (if keepBlendshapes = true) data from the original mesh given the transform parameters
        /// </summary>
        /// <param name="translation"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        public void UpdateData(Vector3 translation, Quaternion rotation, Vector3 scale, bool inverseTransformVerticesJob = false)
        {
            NativeArray<Vertex> verticesNA = new NativeArray<Vertex>(vertexCount, Allocator.TempJob);
            //vertices = new Vertex[vertexCount]; //why do i need to do this??!?!
            verticesNA.CopyFrom(originalVertices);

            UpdateTriangles();

            if (inverseTransformVerticesJob)
            {
                InverseTransformVertices(verticesNA, translation, rotation, scale);

            }
            else
            {
                TransformVertices(verticesNA, translation, rotation, scale);
            }


            UpdateBoundsAlongX(verticesNA);

            verticesNA.CopyTo(vertices);

            if (keepBlenshapes) UpdateBlendshapes(translation, rotation, scale);

            verticesNA.Dispose();
        }

        private void TransformVertices(NativeArray<Vertex> verticesNA, Vector3 translation, Quaternion rotation, Vector3 scale)
        {
            TransformVerticesJob updateVerticesJob = new TransformVerticesJob
            {
                vertices = verticesNA,
                translation = translation,
                rotation = rotation,
                scale = scale,
            };

            JobHandle updateVerticesJobHandle = updateVerticesJob.Schedule(vertexCount, 1024);
            updateVerticesJobHandle.Complete();
        }

        private void InverseTransformVertices(NativeArray<Vertex> verticesNA, Vector3 translation, Quaternion rotation, Vector3 scale)
        {
            InverseTransformVerticesJob updateVerticesJob = new InverseTransformVerticesJob
            {
                vertices = verticesNA,
                translation = translation,
                rotation = rotation,
                scale = scale,
            };

            JobHandle updateVerticesJobHandle = updateVerticesJob.Schedule(vertexCount, 1024);
            updateVerticesJobHandle.Complete();
        }


        private void UpdateTriangles()
        {
            bool reversed = scale.x < 0;
            if (scale.y < 0) reversed = !reversed;
            if (scale.z < 0) reversed = !reversed;
            triangles = reversed ? MeshUtils.GetReversedTriangles(OriginalMesh) : OriginalMesh.triangles;
        }

        private void UpdateBoundsAlongX(NativeArray<Vertex> verticesNA)
        {
            NativeArray<float> minXNA = new NativeArray<float>(1, Allocator.TempJob);
            NativeArray<float> maxXNA = new NativeArray<float>(1, Allocator.TempJob);
            FindBoundsAlongX findBoundsAlongXJob = new FindBoundsAlongX
            {
                vertices = verticesNA,
                minX = minXNA,
                maxX = maxXNA,
            };

            JobHandle findBoundsAlongXJobHandle = findBoundsAlongXJob.Schedule();
            findBoundsAlongXJobHandle.Complete();

            minX = minXNA[0];
            length = Mathf.Abs(maxXNA[0] - minX);
            minXNA.Dispose();
            maxXNA.Dispose();
        }

        private void UpdateBlendshapes(Vector3 translation, Quaternion rotation, Vector3 scale)
        {
            NativeArray<Vector3> deltaPositionsNA = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
            NativeArray<Vector3> deltaNormalsNA = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
            NativeArray<Vector3> deltaTangentsNA = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
            originalBlendshapes.CopyTo(blendshapes, 0);
            for (int i = 0; i < blendshapeCount; i++)
            {
                var blendshape = blendshapes[i];
                for (int j = 0; j < blendshape.frames.Length; j++)
                {
                    var frame = blendshape.frames[j];
                    deltaPositionsNA.CopyFrom(frame.deltaVertices);
                    deltaNormalsNA.CopyFrom(frame.deltaNormals);
                    deltaTangentsNA.CopyFrom(frame.deltaTangents);

                    TransformBlendshapeVerticesJob updateBlendshapeVerticesJob = new TransformBlendshapeVerticesJob
                    {
                        deltaPositions = deltaPositionsNA,
                        deltaNormals = deltaNormalsNA,
                        deltaTangents = deltaTangentsNA,
                        translation = translation,
                        rotation = rotation,
                        scale = scale,
                    };

                    JobHandle updateBlendshapeVerticesJobHandle = updateBlendshapeVerticesJob.Schedule(vertexCount, 1);
                    updateBlendshapeVerticesJobHandle.Complete();

                    deltaPositionsNA.CopyTo(frame.deltaVertices);
                    deltaNormalsNA.CopyTo(frame.deltaNormals);
                    deltaTangentsNA.CopyTo(frame.deltaTangents);
                    blendshape.frames[j] = frame;
                }
                blendshapes[i] = blendshape;
            }
            deltaPositionsNA.Dispose();
            deltaNormalsNA.Dispose();
            deltaTangentsNA.Dispose();
        }

        public IEnumerable<int> GetVerticesAtY0(float delta = 0.0000000001f)
        {
            HashSet<int> verticesAtY0 = new HashSet<int>();
            var vertices = OriginalMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                var distance = (Vector3.zero - new Vector3(0f, vertices[i].y, 0f)).sqrMagnitude;
                if (distance <= delta) verticesAtY0.Add(i);
            }
            return verticesAtY0;
        }

        public IEnumerable<int> GetTrianglesAtY0(List<int> verticesAtY0)
        {
            var triangles = OriginalMesh.triangles;
            var trianglesAtY0 = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                bool triangleAtY0 = false;
                for (int t = 0; t < 3; t++)
                {
                    if (verticesAtY0.Contains(triangles[i + t]))
                    {
                        triangleAtY0 = true;
                    }
                    if (triangleAtY0) break;
                }
                if (triangleAtY0)
                {
                    trianglesAtY0.Add(triangles[i]);
                    trianglesAtY0.Add(triangles[i + 1]);
                    trianglesAtY0.Add(triangles[i + 2]);
                }
            }
            Debug.Log("Triangles -> " + triangles.Length + " || TrianglesY0 -> " + trianglesAtY0.Count);
            return trianglesAtY0;
        }

        public float GetOriginalMeshHeigth()
        {
            OriginalMesh.RecalculateBounds();
            return OriginalMesh.bounds.max.y;
        }


        public Dictionary<int, HashSet<int>> GetEdgeVerticesAtY0(List<int> trianglesAtY0)
        {
            return MeshUtils.GenerateAdjacencyDict(trianglesAtY0.ToArray());
        }

        public Mesh GetTransformedMesh()
        {
            var res = new Mesh();
            MeshUtils.BuildMesh(res, OriginalMesh, keepBlenshapes,
                blendshapes,
                triangles,
                VertexUtils.GetPositionsArray(vertices),
                VertexUtils.GetNormalsArray(vertices));
            return res;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct TransformVerticesJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vertex> vertices;
            [ReadOnly] public Vector3 translation;
            [ReadOnly] public Quaternion rotation;
            [ReadOnly] public Vector3 scale;

            public void Execute(int index)
            {
                var vertex = vertices[index];

                if (rotation != Quaternion.identity)
                {
                    vertex.position = rotation * vertex.position;
                    vertex.normal = rotation * vertex.normal;
                }
                if (scale != Vector3.one)
                {
                    vertex.position = Vector3.Scale(vertex.position, scale);
                    vertex.normal = Vector3.Scale(vertex.normal, scale);

                }
                if (translation != Vector3.zero)
                {
                    vertex.position += translation;
                }

                vertices[index] = vertex;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct InverseTransformVerticesJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vertex> vertices;
            [ReadOnly] public Vector3 translation;
            [ReadOnly] public Quaternion rotation;
            [ReadOnly] public Vector3 scale;

            public void Execute(int index)
            {
                var vertex = vertices[index];
                if (translation != Vector3.zero)
                {
                    vertex.position += translation;
                }
                if (rotation != Quaternion.identity)
                {
                    vertex.position = rotation * vertex.position;
                    vertex.normal = rotation * vertex.normal;
                }
                if (scale != Vector3.one)
                {
                    vertex.position = Vector3.Scale(vertex.position, scale);
                    vertex.normal = Vector3.Scale(vertex.normal, scale);

                }
                vertices[index] = vertex;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct TransformBlendshapeVerticesJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> deltaPositions;
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> deltaNormals;
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> deltaTangents;
            [ReadOnly] public Vector3 translation;
            [ReadOnly] public Quaternion rotation;
            [ReadOnly] public Vector3 scale;

            public void Execute(int index)
            {
                var position = deltaPositions[index];
                var normal = deltaNormals[index];
                if (rotation != Quaternion.identity)
                {
                    position = rotation * position;
                    normal = rotation * normal;
                }
                if (scale != Vector3.one)
                {
                    position = Vector3.Scale(position, scale);
                    normal = Vector3.Scale(normal, scale);

                }
                /*if (translation != Vector3.zero)
                {
                    position += translation;
                }*/
                deltaPositions[index] = position;
                deltaNormals[index] = normal;
            }
        }

        [BurstCompile]
        private struct FindBoundsAlongX : IJob
        {
            [ReadOnly] public NativeArray<Vertex> vertices;
            public NativeArray<float> minX;
            public NativeArray<float> maxX;

            public void Execute()
            {
                float _maxX = float.MinValue;
                float _minX = float.MaxValue;
                var positionsLength = vertices.Length;
                for (int i = 0; i < positionsLength; i++)
                {
                    var position = vertices[i].position;
                    _maxX = Mathf.Max(_maxX, position.x);
                    _minX = Mathf.Min(_minX, position.x);
                    /*if (position.x > _maxX) _maxX = position.x;
                    if (position.x < _minX) _minX = position.x;*/
                }
                minX[0] = _minX;
                maxX[0] = _maxX;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            var other = (MeshData)obj;
            return OriginalMesh == other.OriginalMesh
                && translation == other.translation
                && rotation == other.rotation
                && scale == other.scale;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(MeshData sm1, MeshData sm2)
        {
            return sm1.Equals(sm2);
        }
        public static bool operator !=(MeshData sm1, MeshData sm2)
        {
            return sm1.Equals(sm2);
        }
    }
}