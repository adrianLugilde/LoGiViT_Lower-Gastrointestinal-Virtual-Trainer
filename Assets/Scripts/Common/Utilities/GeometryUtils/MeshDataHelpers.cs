using g4;
using Game.Utils.Math;
using ImgSpc.Exporters;
using SplineMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

//TODO: MOVE EACH CLASS TO A SEPARETE STATIC CLASS FILE

namespace GeometryUtils
{
    public class MeshUtils
    {
        /// <summary>
        /// Returns a mesh with reversed triangles for when the scale is negative. 
        /// </summary>
        /// <param name="mesh"></param>
        public static int[] GetReversedTriangles(Mesh mesh)
        {
            Debug.LogError("GetReversedTriangles");
            var res = mesh.triangles.ToArray();
            var triangleCount = res.Length / 3;
            for (var i = 0; i < triangleCount; i++)
            {
                var tmp = res[i * 3];
                res[i * 3] = res[i * 3 + 1];
                res[i * 3 + 1] = tmp;
            }
            return res;
        }

        /// <summary>
        /// Returns a mesh similar to the given source plus given optionnal parameters.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="source"></param>
        /// <param name="triangles"></param>
        /// <param name="vertices"></param>
        /// <param name="normals"></param>
        /// <param name="uv"></param>
        /// <param name="uv2"></param>
        /// <param name="uv3"></param>
        /// <param name="uv4"></param>
        /// <param name="uv5"></param>
        /// <param name="uv6"></param>
        /// <param name="uv7"></param>
        /// <param name="uv8"></param>
        public static void CopyMesh(Mesh target, Mesh source, bool includeBlendshapes = true,
            IEnumerable<int> triangles = null,
            IEnumerable<Vector3> vertices = null,
            IEnumerable<Vector3> normals = null,
            IEnumerable<Vector2> uv = null,
            IEnumerable<Vector2> uv2 = null,
            IEnumerable<Vector2> uv3 = null,
            IEnumerable<Vector2> uv4 = null,
            IEnumerable<Vector2> uv5 = null,
            IEnumerable<Vector2> uv6 = null,
            IEnumerable<Vector2> uv7 = null,
            IEnumerable<Vector2> uv8 = null)
        {
            target.hideFlags = source.hideFlags;
#if UNITY_2017_3_OR_NEWER
            target.indexFormat = source.indexFormat;
#endif
            target.triangles = new int[0];
            target.vertices = vertices == null ? source.vertices : vertices.ToArray();
            target.normals = normals == null ? source.normals : normals.ToArray();
            target.uv = uv == null ? source.uv : uv.ToArray();
            target.uv2 = uv2 == null ? source.uv2 : uv2.ToArray();
            target.uv3 = uv3 == null ? source.uv3 : uv3.ToArray();
            target.uv4 = uv4 == null ? source.uv4 : uv4.ToArray();
#if UNITY_2018_2_OR_NEWER
            target.uv5 = uv5 == null ? source.uv5 : uv5.ToArray();
            target.uv6 = uv6 == null ? source.uv6 : uv6.ToArray();
            target.uv7 = uv7 == null ? source.uv7 : uv7.ToArray();
            target.uv8 = uv8 == null ? source.uv8 : uv8.ToArray();
#endif
            target.triangles = triangles == null ? source.triangles : triangles.ToArray();

            target.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++)
            {
                target.SetTriangles(source.GetTriangles(i), i);
            }

            if (includeBlendshapes && source.blendShapeCount != 0) CopyBlendshapes(source, target);
            target.RecalculateBounds();
            //if(target.tangents == null || target.tangents.Length == 0) NormalSolver.RecalculateTangentsParallel(target);
            if(target.tangents == null || target.tangents.Length == 0) target.RecalculateTangents();
            target.name = source.name + "_instance";
        }

        /// <summary>
        /// Returns a mesh similar to the given source plus given optionnal parameters.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="source"></param>
        /// <param name="triangles"></param>
        /// <param name="vertices"></param>
        /// <param name="normals"></param>
        /// <param name="uv"></param>
        /// <param name="uv2"></param>
        /// <param name="uv3"></param>
        /// <param name="uv4"></param>
        /// <param name="uv5"></param>
        /// <param name="uv6"></param>
        /// <param name="uv7"></param>
        /// <param name="uv8"></param>
        public static void BuildMesh(Mesh target, Mesh source, bool includeBlendshapes = true,
            Blendshape[] blendshapes = null,
            int[] triangles = null,
            Vector3[] vertices = null,
            Vector3[] normals = null,
            IEnumerable<Vector2> uv = null,
            IEnumerable<Vector2> uv2 = null,
            IEnumerable<Vector2> uv3 = null,
            IEnumerable<Vector2> uv4 = null,
            IEnumerable<Vector2> uv5 = null,
            IEnumerable<Vector2> uv6 = null,
            IEnumerable<Vector2> uv7 = null,
            IEnumerable<Vector2> uv8 = null)
        {
            target.hideFlags = source.hideFlags;
#if UNITY_2017_3_OR_NEWER
            target.indexFormat = source.indexFormat;
#endif
            target.triangles = new int[0];
            target.vertices = vertices == null ? source.vertices : vertices;
            target.normals = normals == null ? source.normals : normals;
            target.uv = uv == null ? source.uv : uv.ToArray();
            target.uv2 = uv2 == null ? source.uv2 : uv2.ToArray();
            target.uv3 = uv3 == null ? source.uv3 : uv3.ToArray();
            target.uv4 = uv4 == null ? source.uv4 : uv4.ToArray();
#if UNITY_2018_2_OR_NEWER
            target.uv5 = uv5 == null ? source.uv5 : uv5.ToArray();
            target.uv6 = uv6 == null ? source.uv6 : uv6.ToArray();
            target.uv7 = uv7 == null ? source.uv7 : uv7.ToArray();
            target.uv8 = uv8 == null ? source.uv8 : uv8.ToArray();
#endif
            target.triangles = triangles == null ? source.triangles : triangles;

            target.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++)
            {
                target.SetTriangles(source.GetTriangles(i), i);
            }

            //TODO DARLE UNA VUELTA A ESTE IF
            if (includeBlendshapes)
            {
                target.ClearBlendShapes();
                if (blendshapes != null)
                {
                    for (int i = 0; i < blendshapes.Length; i++)
                    {
                        var blendshape = blendshapes[i];
                        for(int j = 0; j < blendshape.frames.Length; j++)
                        {
                            var frame = blendshape.frames[j];
                            //var blendshapeVertices = BlenshapeVertexUtils.GetBlendshapeVertices(frame.vertices);
                            /*target.AddBlendShapeFrame(blendshape.name, frame.frameWeight,
                                BlenshapeVertexUtils.GetPositionsArray(frame.vertices),
                                BlenshapeVertexUtils.GetNormalsArray(frame.vertices),
                                BlenshapeVertexUtils.GetTangentsArray(frame.vertices));*/
                            /*target.AddBlendShapeFrame(blendshape.name, frame.frameWeight,
                                blendshapeVertices.positions,
                                blendshapeVertices.normals,
                                blendshapeVertices.tangents);*/
                            target.AddBlendShapeFrame(blendshape.name, frame.frameWeight,
                                frame.deltaVertices,
                                frame.deltaNormals,
                                frame.deltaTangents);
                        }
                    }
                } else if (source.blendShapeCount != 0)
                {
                    CopyBlendshapes(source, target);
                }
            }
            target.RecalculateBounds();
            //NormalSolver.RecalculateTangentsParallel(target);
            //target.RecalculateTangents();
            //target.RecalculateNormals();
            target.name = source.name + "_instance";
        }

        public static void SimpleMeshCopy(Mesh target, Mesh source, bool includeBlendshapes = true,
            Blendshape[] blendshapes = null,
            int[] triangles = null,
            Vector3[] vertices = null,
            Vector3[] normals = null,
            IEnumerable<Vector2> uv = null,
            IEnumerable<Vector2> uv2 = null,
            IEnumerable<Vector2> uv3 = null,
            IEnumerable<Vector2> uv4 = null,
            IEnumerable<Vector2> uv5 = null,
            IEnumerable<Vector2> uv6 = null,
            IEnumerable<Vector2> uv7 = null,
            IEnumerable<Vector2> uv8 = null)
        {
            target.hideFlags = source.hideFlags;
            target.indexFormat = source.indexFormat;
            target.triangles = new int[0];
            target.vertices = vertices == null ? source.vertices : vertices;
            target.normals = normals == null ? source.normals : normals;
            target.uv = uv == null ? source.uv : uv.ToArray();
            target.uv2 = uv2 == null ? source.uv2 : uv2.ToArray();
            target.uv3 = uv3 == null ? source.uv3 : uv3.ToArray();
            target.uv4 = uv4 == null ? source.uv4 : uv4.ToArray();
            target.uv5 = uv5 == null ? source.uv5 : uv5.ToArray();
            target.uv6 = uv6 == null ? source.uv6 : uv6.ToArray();
            target.uv7 = uv7 == null ? source.uv7 : uv7.ToArray();
            target.uv8 = uv8 == null ? source.uv8 : uv8.ToArray();
            target.triangles = triangles == null ? source.triangles : triangles;

            target.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++)
            {
                target.SetTriangles(source.GetTriangles(i), i);
            }

            //TODO DARLE UNA VUELTA A ESTE IF
            if (includeBlendshapes)
            {
                target.ClearBlendShapes();
                if (blendshapes != null)
                {
                    var blendshapesCount = blendshapes.Length;
                    for (int i = 0; i < blendshapesCount; i++)
                    {
                        var blendshape = blendshapes[i];
                        var framesCount = blendshape.frames.Length;
                        for (int j = 0; j < framesCount; j++)
                        {
                            var frame = blendshape.frames[j];
                            target.AddBlendShapeFrame(blendshape.name, 
                                frame.frameWeight,
                                frame.deltaVertices,
                                frame.deltaNormals,
                                frame.deltaTangents);
                        }
                    }
                }
                else if (source.blendShapeCount != 0)
                {
                    CopyBlendshapes(source, target);
                }
            }

            target.name = source.name + "_instance";
        }

        public static void BuildMesh2(Mesh target, Mesh source, bool includeBlendshapes = true,
            OldBlendshape[] blendshapes = null,
            IEnumerable<int> triangles = null,
            IEnumerable<Vector3> vertices = null,
            IEnumerable<Vector3> normals = null,
            IEnumerable<Vector2> uv = null,
            IEnumerable<Vector2> uv2 = null,
            IEnumerable<Vector2> uv3 = null,
            IEnumerable<Vector2> uv4 = null,
            IEnumerable<Vector2> uv5 = null,
            IEnumerable<Vector2> uv6 = null,
            IEnumerable<Vector2> uv7 = null,
            IEnumerable<Vector2> uv8 = null)
        {
            target.hideFlags = source.hideFlags;
#if UNITY_2017_3_OR_NEWER
            target.indexFormat = source.indexFormat;
#endif
            target.triangles = new int[0];
            target.vertices = vertices == null ? source.vertices : vertices.ToArray();
            target.normals = normals == null ? source.normals : normals.ToArray();
            target.uv = uv == null ? source.uv : uv.ToArray();
            target.uv2 = uv2 == null ? source.uv2 : uv2.ToArray();
            target.uv3 = uv3 == null ? source.uv3 : uv3.ToArray();
            target.uv4 = uv4 == null ? source.uv4 : uv4.ToArray();
#if UNITY_2018_2_OR_NEWER
            target.uv5 = uv5 == null ? source.uv5 : uv5.ToArray();
            target.uv6 = uv6 == null ? source.uv6 : uv6.ToArray();
            target.uv7 = uv7 == null ? source.uv7 : uv7.ToArray();
            target.uv8 = uv8 == null ? source.uv8 : uv8.ToArray();
#endif
            target.triangles = triangles == null ? source.triangles : triangles.ToArray();

            target.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++)
            {
                target.SetTriangles(source.GetTriangles(i), i);
            }

            if (includeBlendshapes)
            {
                target.ClearBlendShapes();
                if (blendshapes != null)
                {
                    for (int i = 0; i < blendshapes.Length; i++)
                    {
                        var blendshape = blendshapes[i];
                        for (int j = 0; j < blendshape.frames.Length; j++)
                        {
                            var frame = blendshape.frames[j];
                            target.AddBlendShapeFrame(blendshape.name, frame.frameWeight,
                                frame.deltaVertices,
                                frame.deltaNormals,
                                frame.deltaTangents);
                        }
                    }
                }
                else if (source.blendShapeCount != 0)
                {
                    CopyBlendshapes(source, target);
                }
            }
            target.RecalculateBounds();
            target.RecalculateTangents();
            //target.RecalculateNormals();
            target.name = source.name + "_instance";
        }


        public static void CopyBlendshapes(Mesh source, Mesh target, int startIdx = -1, int endIdx = -1)
       {
           Vector3[] deltaVertices = new Vector3[source.vertexCount];
           Vector3[] deltaNormals = new Vector3[source.vertexCount];
           Vector3[] deltaTangents = new Vector3[source.vertexCount];

           var blendShapeCount = endIdx == -1 ? source.blendShapeCount : endIdx;
           var blendShapeIdx = startIdx == -1 ? 0 : startIdx;

           for (int shapeIndex = blendShapeIdx; shapeIndex < blendShapeCount; shapeIndex++)
           {
               string blendShapeName = source.GetBlendShapeName(shapeIndex);

               if (target.GetBlendShapeIndex(blendShapeName) < 0)
               {
                   int frameCount = source.GetBlendShapeFrameCount(shapeIndex);
                   for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                   {
                       float frameWeight = source.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                       source.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                       target.AddBlendShapeFrame(blendShapeName, frameWeight, deltaVertices, deltaNormals, deltaTangents);
                   }
               }
           }
       }

       /// <summary>
       /// Exports the mesh of the given object to a destination path. 
       /// </summary>
       /// <param name="mesh"></param>
       /// <param name="exportMethod"></param>
       /// <param name="destPath"></param>
       public static void ExportMesh(ImgSpcExporter meshExporter, string exportMethod, string destPath)
       {
           Debug.LogWarning("Exporting mesh to " + destPath);
           ExportMethod method = ExportMethodRegistry.GetMethod(exportMethod);
           meshExporter.DestinationFilename = destPath;
           meshExporter.ExportMethod = exportMethod;
           meshExporter.Export();
       }

       /// <summary>
       /// Given the triangles of a mesh, returns a dictionary with the adjacent vertices indices for each vertex index
       /// </summary>
       /// <param name="mesh"></param>
       /// <param name="exportMethod"></param>
       /// <param name="destPath"></param>
       public static Dictionary<int, HashSet<int>> GenerateAdjacencyDict(int[] triangles)
       {
           var adjacencySetsDict = new Dictionary<int, HashSet<int>>();
           int itemCounter = 0;
           for (int i = 0; i < triangles.Length; i += 3)
           {
               for (int t = 0; t < 3; t++)
               {
                   int currentVertex = triangles[i + t];
                   int vertex1 = triangles[i + ((t + 1) % 3)];
                   int vertex2 = triangles[i + ((t + 2) % 3)];

                   if (!adjacencySetsDict.TryGetValue(currentVertex, out HashSet<int> value))
                   {
                       adjacencySetsDict[currentVertex] = new HashSet<int>();
                   }
                   adjacencySetsDict[currentVertex].Add(vertex1);
                   adjacencySetsDict[currentVertex].Add(vertex2);
                   itemCounter += 2;
               }
           }
           return adjacencySetsDict;
       }

       public static Dictionary<int, HashSet<int>> GetAdjacentVerticesDict(int[] triangles)
       {
           var adjacencySetsDict = new Dictionary<int, HashSet<int>>();
           for (int i = 0; i < triangles.Length; i += 3)
           {
               var v1 = triangles[i];
               var v2 = triangles[i + 1];
               var v3 = triangles[i + 2];
               if (!adjacencySetsDict.TryGetValue(v1, out HashSet<int> value))
               {
                   adjacencySetsDict[v1] = new HashSet<int>();
               }
               adjacencySetsDict[v1].Add(v2);
               adjacencySetsDict[v1].Add(v3);

               if (!adjacencySetsDict.TryGetValue(v2, out HashSet<int> value2))
               {
                   adjacencySetsDict[v2] = new HashSet<int>();
               }
               adjacencySetsDict[v2].Add(v1);
               adjacencySetsDict[v2].Add(v3);

               if (!adjacencySetsDict.TryGetValue(v3, out HashSet<int> value3))
               {
                   adjacencySetsDict[v3] = new HashSet<int>();
               }
               adjacencySetsDict[v3].Add(v1);
               adjacencySetsDict[v3].Add(v2);
           }

           return adjacencySetsDict;
       }

       /// <summary>
       /// From a collection of vectices, return the subcollection of those vertices inside the given bounds
       /// </summary>
       /// <param name="bounds"></param>
       /// <param name="vertices"></param>
       public static IEnumerable<Vector3> GetVerticesInsideBounds(IEnumerable<Vector3> vertices, Bounds bounds)
       {
           var res = new List<Vector3>();
           foreach (var vertex in vertices)
           {
               if (bounds.Contains(vertex)) res.Add(vertex);
           }
           return res;
       }

       /// <summary>
       /// Given the triangles of the original mesh, the vertices of the cropped mesh and the vertices removed from the original mesh, returns the vertices of that cropped mesh that were left disjoined
       /// </summary>
       /// <param name="bounds"></param>
       /// <param name="vertices"></param>
       public static IEnumerable<Vector3> GetDisjoinedVertices(Mesh sourceMesh, IEnumerable<Vector3> verticesKept, IEnumerable<Vector3> removedVertices)
       {
           List<Vector3> res = new List<Vector3>();
           var adjacencyDict = GetAdjacentVerticesDict(sourceMesh.triangles);
           foreach (var vertexKept in verticesKept)
           {
               bool disjoined = false;
               var vertexKeptIdx = Array.IndexOf(sourceMesh.vertices, vertexKept);
               foreach (var removedVertex in removedVertices)
               {
                   var removedVertexIdx = Array.IndexOf(sourceMesh.vertices, removedVertex);
                   if (adjacencyDict[vertexKeptIdx].Contains(removedVertexIdx))
                   {
                       disjoined = true;
                   }
                   if (disjoined) break;
               }
               if (disjoined) res.Add(vertexKept);
           }
           return res;
       }

        public static Mesh GenerateCroppedMesh3(Mesh originalMesh, IEnumerable<Vector3> verticesToRemove)
        {
            var res = new Mesh();
            var originalVertices = originalMesh.vertices;
            var originalNormals = originalMesh.normals;
            var originalUvs = originalMesh.uv;
            var originalTriangles = originalMesh.triangles;
            var verticesToRemoveList = verticesToRemove.ToList();
            var meshSizeAfterRemoval = originalVertices.Length - verticesToRemoveList.Count;
            Vector3[] finalVertices = new Vector3[meshSizeAfterRemoval];
            Vector3[] finalNormals = new Vector3[meshSizeAfterRemoval];
            Vector2[] finalUvs = new Vector2[meshSizeAfterRemoval];
            Dictionary<int, int> indexesDict = new Dictionary<int, int>();
            var currentIndex = 0;
            for (int i = 0; i < originalVertices.Length; i++)
            {
                //if current vertex is not to be removed
                if (verticesToRemoveList.IndexOf(originalVertices[i]) == -1)
                {
                    //add current vertex to final vertices
                    finalVertices[currentIndex] = originalVertices[i];
                    finalNormals[currentIndex] = originalNormals[i];
                    finalUvs[currentIndex] = originalUvs[i];
                    indexesDict[i] = currentIndex;//give to each vertex its new index 
                    currentIndex++;
                }
                else
                {
                    indexesDict[i] = -1;
                }
            }

            //MeshVerticesUtils.DrawVerticesAsSpheres(finalVertices.ToList().GetRange(0,7500), 0.005f, "verticeswithcrop");
            MeshVerticesUtils.DrawVerticesAsSpheres(finalVertices, 0.005f, "verticeswithcrop");

            var finalTriangles = new List<int>();
            var triangles = originalMesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                bool removedTriangle = false;
                for(int j = 0; j < 3; j++)
                {
                    if (removedTriangle) break;
                    if (indexesDict[triangles[i + j]] == -1) removedTriangle = true;
                }
                if(!removedTriangle)
                {
                    finalTriangles.Add(indexesDict[triangles[i]]);
                    finalTriangles.Add(indexesDict[triangles[i+1]]);
                    finalTriangles.Add(indexesDict[triangles[i+2]]);
                }
            }

            /*var finalTriangles = new List<int>();
            var submeshes = new List<List<int>>();
            for (int i = 0; i < originalMesh.subMeshCount; i++)
            {
                var subMeshTriangles = originalMesh.GetTriangles(i);
                var newSubMeshTriangles = new List<int>();
                for (int j = 0; j < subMeshTriangles.Length; j += 3)
                {
                    bool removedTriangle = false;
                    for (int t = 0; t < 3; t++)
                    {
                        if (removedTriangle) break;
                        if (indexesDict[subMeshTriangles[j + t]] == -1) removedTriangle = true;
                    }
                    if (!removedTriangle)
                    {
                        newSubMeshTriangles.Add(indexesDict[subMeshTriangles[j]]);
                        newSubMeshTriangles.Add(indexesDict[subMeshTriangles[j + 1]]);
                        newSubMeshTriangles.Add(indexesDict[subMeshTriangles[j + 2]]);
                    }
                }
                submeshes.Add(newSubMeshTriangles);
                finalTriangles.AddRange(newSubMeshTriangles);
            }*/

            res.vertices = finalVertices;
            res.normals = finalNormals;
            res.uv = finalUvs;
            res.triangles = finalTriangles.ToArray();
            //res.subMeshCount = originalMesh.subMeshCount;
            /*for (int i = 0; i < submeshes.Count; i++)
            {
                res.SetTriangles(submeshes[i].ToArray(), i);
            }*/
            Debug.LogError("intestinecroppedmesh submeshcount " + res.subMeshCount);
            res.RecalculateNormals();
            res.RecalculateTangents();
            res.RecalculateBounds();

            return res;
        }

        //TODO ACTUALIZAR ESTE M�TODO PARA QUE HAGA TODO EN UN FOR PARA LOS SUBMESHES COMO EL DEL DISEASE PLACER
        /// <summary>
        /// Returns a mesh with the indicated vertices removed from the geometry (including the associated normals, uvs, triangles...)
        /// </summary>
        /// <param name="originalMesh"></param>
        /// <param name="verticesToRemove"></param>
        public static Mesh GenerateCroppedMesh2(Mesh originalMesh, IEnumerable<Vector3> verticesToRemove)
       {
           var res = new Mesh();
           res.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
           var originalVertices = originalMesh.vertices;
           var originalNormals = originalMesh.normals;
           var originalUvs = originalMesh.uv;
           var originalTriangles = originalMesh.triangles;
           var verticesToRemoveList = verticesToRemove.ToList();
           var meshSizeAfterRemoval = originalVertices.Length - verticesToRemoveList.Count;
           Vector3[] finalVertices = new Vector3[meshSizeAfterRemoval];
           Vector3[] finalNormals = new Vector3[meshSizeAfterRemoval];
           Vector2[] finalUvs = new Vector2[meshSizeAfterRemoval];
           Dictionary<int, int> indexesDict = new Dictionary<int, int>();
           var currentIndex = 0;
           for (int i = 0; i < originalVertices.Length; i++)
           {
               if (verticesToRemoveList.IndexOf(originalVertices[i]) == -1)
               {
                   finalVertices[currentIndex] = originalVertices[i];
                   finalNormals[currentIndex] = originalNormals[i];
                   finalUvs[currentIndex] = originalUvs[i];
                   indexesDict[i] = currentIndex;
                   currentIndex++;
               }
               else
               {
                   indexesDict[i] = -1;
               }
           }


           var finalTriangles = new List<int>();
           var submeshes = new List<List<int>>();
           for (int i = 0; i < originalMesh.subMeshCount; i++)
           {
               var subMeshTriangles = originalMesh.GetTriangles(i);
               var newSubMeshTriangles = new List<int>();
               for (int j = 0; j < subMeshTriangles.Length; j += 3)
               {
                   bool removedTriangle = false;
                   for (int t = 0; t < 3; t++)
                   {
                       if (removedTriangle) break;
                       if (indexesDict[subMeshTriangles[j + t]] == -1) removedTriangle = true;
                   }
                   if (!removedTriangle)
                   {
                       newSubMeshTriangles.Add(indexesDict[subMeshTriangles[j]]);
                       newSubMeshTriangles.Add(indexesDict[subMeshTriangles[j + 1]]);
                       newSubMeshTriangles.Add(indexesDict[subMeshTriangles[j + 2]]);
                   }
               }
               submeshes.Add(newSubMeshTriangles);
               finalTriangles.AddRange(newSubMeshTriangles);
           }

           res.vertices = finalVertices;
           res.normals = finalNormals;
           res.uv = finalUvs;
           res.triangles = finalTriangles.ToArray();
           res.subMeshCount = originalMesh.subMeshCount;

           for (int i = 0; i < submeshes.Count; i++)
           {
               res.SetTriangles(submeshes[i].ToArray(), i);
           }
           Debug.LogError("intestinecroppedmesh submeshcount " + res.subMeshCount);
           res.RecalculateNormals();
           res.RecalculateTangents();
           res.RecalculateBounds();

           return res;
       }

       public static Mesh GenerateCroppedMesh(Mesh originalMesh, IEnumerable<Vector3> verticesToRemove)
       {
           var res = new Mesh();
           var originalVertices = originalMesh.vertices;
           var originalNormals = originalMesh.normals;
           var originalUvs = originalMesh.uv;
           var originalTriangles = originalMesh.triangles;
           var verticesToRemoveList = verticesToRemove.ToList();
           var meshSizeAfterRemoval = originalVertices.Length - verticesToRemoveList.Count;
           Vector3[] finalVertices = new Vector3[meshSizeAfterRemoval];
           Vector3[] finalNormals = new Vector3[meshSizeAfterRemoval];
           Vector2[] finalUvs = new Vector2[meshSizeAfterRemoval];
           Dictionary<int, int> indexesDict = new Dictionary<int, int>();
           var currentIndex = 0;
           for (int i = 0; i < originalVertices.Length; i++)
           {
               if (verticesToRemoveList.IndexOf(originalVertices[i]) == -1)
               {
                   finalVertices[currentIndex] = originalVertices[i];
                   finalNormals[currentIndex] = originalNormals[i];
                   finalUvs[currentIndex] = originalUvs[i];
                   indexesDict[i] = currentIndex;
                   currentIndex++;
               }
               else
               {
                   indexesDict[i] = -1;
               }
           }

           var finalTriangles = new List<int>();
           for (int i = 0; i < originalTriangles.Length; i += 3)
           {
               bool removedTriangle = false;
               for (int t = 0; t < 3; t++)
               {
                   if (removedTriangle) break;
                   if (indexesDict[originalTriangles[i + t]] == -1) removedTriangle = true;
               }
               if (!removedTriangle)
               {
                   finalTriangles.Add(indexesDict[originalTriangles[i]]);
                   finalTriangles.Add(indexesDict[originalTriangles[i + 1]]);
                   finalTriangles.Add(indexesDict[originalTriangles[i + 2]]);
               }
           }

           res.vertices = finalVertices;
           res.normals = finalNormals;
           res.uv = finalUvs;
           res.triangles = finalTriangles.ToArray();
           res.RecalculateNormals();
           res.RecalculateTangents();
           res.RecalculateBounds();

           return res;
       }

       /// <summary>
       /// Returns the vertices to crop from a mesh when another is being projected by raycasting over it
       /// </summary>
       /// <param name="sourceMesh"></param>
       /// <param name="projectedMeshBounds"></param>
       /// <param name="hitTrianglesIdxs"></param>
       /// <param name="boundsExpansion"></param>
       public static List<Vector3> GetVerticesToCrop(Mesh sourceMesh, Bounds projectedMeshBounds, IEnumerable<int> hitTrianglesIdxs, float boundsExpansion = 0.05f)
       {
           var triangles = sourceMesh.triangles;
           var vertices = sourceMesh.vertices;
           var verticesOfHitTriangles = new HashSet<Vector3>();
           Dictionary<Vector3, HashSet<int>> trianglesOfVertices = new Dictionary<Vector3, HashSet<int>>();
           foreach (var hitTriangleIdx in hitTrianglesIdxs)
           {
               for (int i = 0; i < 3; i++)
               {
                   var vertex = vertices[triangles[hitTriangleIdx * 3 + i]];
                   if (trianglesOfVertices.TryGetValue(vertex, out var value))
                   {
                       value.Add(hitTriangleIdx);
                   }
                   else
                   {
                       trianglesOfVertices[vertex] = new HashSet<int> { hitTriangleIdx };
                   }
                   verticesOfHitTriangles.Add(vertex);
               }
           }
           var expandedBounds = new Bounds();
           expandedBounds.SetMinMax(projectedMeshBounds.min, projectedMeshBounds.max);
           expandedBounds.Expand(boundsExpansion);
           //Get the vertices inside the bounds of the projected mesh 
           var verticesInsideBounds = GetVerticesInsideBounds(vertices, expandedBounds);
           //Get the vertices inside bounds that are shared with the vertices of the triangles hit
           var verticesOfHitTrianglesInsideBounds = verticesInsideBounds.Intersect(verticesOfHitTriangles);
           //Get the highly connected vertices of the triangles hit, the ones to be croped
           var highlyConnectedVerts = trianglesOfVertices.Where(x => x.Value.Count >= 4).Select(x => x.Key);
           highlyConnectedVerts = verticesInsideBounds.Union(highlyConnectedVerts);

           return highlyConnectedVerts.ToList();
       }

        public static Mesh CreateMeshFromTriangles(List<Triangle2D> triangles)
        {
            List<Vector3> vertices = new List<Vector3>(triangles.Count * 3);
            List<int> indices = new List<int>(triangles.Count * 3);

            for (int i = 0; i < triangles.Count; ++i)
            {
                vertices.Add(triangles[i].p0);
                vertices.Add(triangles[i].p1);
                vertices.Add(triangles[i].p2);
                indices.Add(i * 3 + 2); // Changes order
                indices.Add(i * 3 + 1);
                indices.Add(i * 3);
            }

            Mesh mesh = new Mesh();
            mesh.subMeshCount = 1;
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            return mesh;
        }

        public static void GenerateMeshBounds(Mesh sourceMesh, Transform transform)
        {
            var vertices = sourceMesh.vertices;
            if (vertices.Length <= 0) return;

            var min = transform.TransformPoint(vertices[0]);
            var max = min;

            for (var i = 1; i < vertices.Length; i++)
            {
                var V = transform.TransformPoint(vertices[i]);

                for (var n = 0; n < 3; n++)
                {
                    max[n] = Mathf.Max(V[n], max[n]);
                    min[n] = Mathf.Min(V[n], min[n]);
                }
            }
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            sourceMesh.bounds = bounds;
        }

        /// <summary>
        /// Creates a new GameObject with the given mesh.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="goName"></param>+
        /// <param name="useSkinnedMeshRender"></param>
        public static GameObject CreateGOFromMesh(Mesh mesh, string goName = "GOFromMesh", bool useSkinnedMeshRender = true)
        {
            var go = new GameObject();
            go.name = goName;
            var material = new Material(Shader.Find("Diffuse"));
            var tempTriangles = mesh.triangles;
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.triangles = tempTriangles;
            if (useSkinnedMeshRender)
            {
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.sharedMaterial = material;
            }
            else
            {
                go.AddComponent<MeshRenderer>().sharedMaterial = material; ;
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
            }
            Debug.Log("Created GO from mesh with name " + go.name);
            return go;
        }

        public static Mesh CombineMeshesOld(List<Mesh> meshesToCombine, bool createSubmeshes = true)
        {
            var combinedVertices = new List<Vector3>();
            var combinedNormals = new List<Vector3>();
            var combinedTangents = new List<Vector4>();
            var combinedUv = new List<Vector2>();
            var combinedTriangles = new List<int>();
            var submeshTrianglesList = new List<List<int>>();
            var vertexOffsetMap = new Dictionary<Mesh, int>();
            var vertexOffset = 0;
            var combinedSubmeshesCounter = 0;
            foreach (var mesh in meshesToCombine)
            {
                combinedVertices.AddRange(mesh.vertices);
                combinedNormals.AddRange(mesh.normals);
                combinedTangents.AddRange(mesh.tangents);
                combinedUv.AddRange(mesh.uv);
                if (createSubmeshes)
                {
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        var submeshTriangles = mesh.GetTriangles(s);
                        if (combinedSubmeshesCounter != 0)
                        {
                            var offsetedTriangles = mesh.GetTriangles(s).Select(triIdx => triIdx + vertexOffset).ToList();
                            submeshTrianglesList.Add(offsetedTriangles);
                            combinedTriangles.AddRange(offsetedTriangles);
                        }
                        else
                        {
                            submeshTrianglesList.Add(submeshTriangles.ToList());
                            combinedTriangles.AddRange(submeshTriangles);
                        }
                        combinedSubmeshesCounter++;
                    }
                }
                else
                {
                    combinedTriangles.AddRange(mesh.triangles.Select(triIdx => triIdx + vertexOffset));
                }

                vertexOffset += mesh.vertexCount;
            }

            var res = new Mesh();
            res.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            res.vertices = combinedVertices.ToArray();
            res.normals = combinedNormals.ToArray();
            res.tangents = combinedTangents.ToArray();
            res.uv = combinedUv.ToArray();
            res.triangles = combinedTriangles.ToArray();
            if (createSubmeshes)
            {
                res.subMeshCount = submeshTrianglesList.Count;
                for (int s = 0; s < submeshTrianglesList.Count; s++)
                {
                    res.SetTriangles(submeshTrianglesList[s], s);
                }
            }
            res.RecalculateBounds();

            return res;
        }

        /// <summary>
        /// Combines (not merges) the given mesh into a single one, either with or without submeshes.
        /// </summary>
        /// <param name="meshesToCombine"></param>
        /// <param name="createSubmeshes"></param>
        public static Mesh CombineMeshes(List<Mesh> meshesToCombine, bool createSubmeshes = true)
        {
            // First pass: cache all mesh data (each property access = native→managed copy) and compute total sizes.
            int meshCount = meshesToCombine.Count;
            var cachedVerts    = new Vector3[meshCount][];
            var cachedNormals  = new Vector3[meshCount][];
            var cachedTangents = new Vector4[meshCount][];
            var cachedUvs      = new Vector2[meshCount][];
            var cachedSubTris  = new int[meshCount][][];

            int totalVertexCount  = 0;
            int totalSubmeshCount = 0;
            int totalTriCount     = 0;

            for (int i = 0; i < meshCount; i++)
            {
                var mesh = meshesToCombine[i];
                if(mesh.tangents == null || mesh.tangents.Length == 0)
                {
                    mesh.RecalculateTangents();
                }
                cachedVerts[i]    = mesh.vertices;
                cachedNormals[i]  = mesh.normals;
                cachedTangents[i] = mesh.tangents;
                cachedUvs[i]      = mesh.uv;

                int sc = mesh.subMeshCount;
                cachedSubTris[i] = new int[sc][];
                for (int s = 0; s < sc; s++)
                {
                    cachedSubTris[i][s] = mesh.GetTriangles(s);
                    totalTriCount += cachedSubTris[i][s].Length;
                }

                totalVertexCount  += cachedVerts[i].Length;
                totalSubmeshCount += sc;
            }

            // Pre-allocate output arrays — no List growth or GC pressure.
            var combinedVertices  = new Vector3[totalVertexCount];
            var combinedNormals   = new Vector3[totalVertexCount];
            var combinedTangents  = new Vector4[totalVertexCount];
            var combinedUv        = new Vector2[totalVertexCount];

            int[][] submeshArrays = createSubmeshes ? new int[totalSubmeshCount][] : null;
            int[]   combinedTris  = createSubmeshes ? null : new int[totalTriCount];

            int vertexWritePos = 0;
            int submeshIdx     = 0;
            int triWritePos    = 0;

            for (int i = 0; i < meshCount; i++)
            {
                var verts    = cachedVerts[i];
                var normals  = cachedNormals[i];
                var tangents = cachedTangents[i];
                var uvs      = cachedUvs[i];
                int vCount   = verts.Length;

                Array.Copy(verts,    0, combinedVertices,  vertexWritePos, vCount);
                Array.Copy(normals,  0, combinedNormals,   vertexWritePos, vCount);
                Array.Copy(tangents, 0, combinedTangents,  vertexWritePos, vCount);
                Array.Copy(uvs,      0, combinedUv,        vertexWritePos, vCount);

                var subTris = cachedSubTris[i];
                for (int s = 0; s < subTris.Length; s++)
                {
                    var tris      = subTris[s];
                    var offsetted = new int[tris.Length];
                    for (int t = 0; t < tris.Length; t++)
                        offsetted[t] = tris[t] + vertexWritePos;

                    if (createSubmeshes)
                    {
                        submeshArrays[submeshIdx++] = offsetted;
                    }
                    else
                    {
                        Array.Copy(offsetted, 0, combinedTris, triWritePos, offsetted.Length);
                        triWritePos += offsetted.Length;
                    }
                }

                vertexWritePos += vCount;
            }

            var res = new Mesh();
            res.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            res.vertices  = combinedVertices;
            res.normals   = combinedNormals;
            res.tangents  = combinedTangents;
            res.uv        = combinedUv;

            if (createSubmeshes)
            {
                res.subMeshCount = submeshArrays.Length;
                for (int s = 0; s < submeshArrays.Length; s++)
                    res.SetTriangles(submeshArrays[s], s);
            }
            else
            {
                res.triangles = combinedTris;
            }

            res.RecalculateBounds();
            return res;
        }

        public static Mesh SimplifyMeshVertexCount(Mesh sourceMesh, int targetVertexCount)
        {
            var reducer = SetUpSimplifyMeshReducer(sourceMesh);
            reducer.ReduceToVertexCount(targetVertexCount);
            return G4Utils.DMeshToUnityMesh(reducer.Mesh, false);
        }

        public static Mesh SimplifyMeshTriangleCount(Mesh sourceMesh, int targetTriangleCount)
        {
            /*var reducer = SetUpSimplifyMeshReducer(sourceMesh);
            reducer.ReduceToTriangleCount(targetTriangleCount);
            return G4Utils.DMeshToUnityMesh(reducer.Mesh, false);*/

            var newVertices = new List<Vector3d>();
            var newNormals = new List<Vector3d>();
            var vertLength = sourceMesh.vertices.Length;
            for (int i = 0; i < vertLength; i++)
            {
                newVertices.Add(sourceMesh.vertices[i]);
                newNormals.Add(sourceMesh.normals[i]);
            }
            DMesh3 dmesh3 = DMesh3Builder.Build(newVertices, sourceMesh.triangles, newNormals);
            Debug.Log(dmesh3.CheckValidity());
            dmesh3.CompactInPlace();

            var reducer = new Reducer(dmesh3);
            reducer.ReduceToTriangleCount(targetTriangleCount);
            return G4Utils.DMeshToUnityMesh(reducer.Mesh, false);
        }

        public static Mesh SimplifyMeshToEdgeLength(Mesh sourceMesh, double targetMinEdgeLength)
        {
            var reducer = SetUpSimplifyMeshReducer(sourceMesh);
            reducer.ReduceToEdgeLength(targetMinEdgeLength);
            return G4Utils.DMeshToUnityMesh(reducer.Mesh, false);
        }

        private static Reducer SetUpSimplifyMeshReducer(Mesh sourceMesh)
        {
            var newVertices = new List<Vector3d>();
            var newNormals = new List<Vector3d>();
            var vertLength = sourceMesh.vertices.Length;
            for (int i = 0; i < vertLength; i++)
            {
                newVertices.Add(sourceMesh.vertices[i]);
                newNormals.Add(sourceMesh.normals[i]);
            }
            DMesh3 dmesh3 = DMesh3Builder.Build(newVertices, sourceMesh.triangles, newNormals);
            Debug.Log(dmesh3.CheckValidity());
            dmesh3.CompactInPlace();
            return new Reducer(dmesh3);
        }

        /// <summary>
        /// Given a Mesh returns a list of the submeshes as new meshes
        /// </summary>
        /// <param name="meshData"></param>
        /// <returns></returns>
        public static List<Mesh> GetSubmeshesAsMeshes(Mesh originalMesh, List<int> excludedSubmeshes)
        {
            var result = new List<Mesh>();
            var submeshesCount = originalMesh.subMeshCount;
            var originalVertices = originalMesh.vertices;
            var originalNormals = originalMesh.normals;
            var originalUVs = originalMesh.uv;
            for(int i = 0; i < submeshesCount; i ++)
            {
                if (!excludedSubmeshes.Contains(i)) result.Add(ExtractSubmesh(originalVertices, originalNormals, originalUVs, originalMesh.GetTriangles(i)));
            }
            return result;
        }

        private static Mesh ExtractSubmesh(Vector3[] originalVertices, Vector3[] originalNormals, Vector2[] originalUVs, int[] submeshTriangles)
        {
            var newMesh = new Mesh();
            var newVertices = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newUvs = new List<Vector2>();
            var newTriangles = new List<int>();
            var idxCounter = 0;
            for (int i = 0; i < submeshTriangles.Length; i+=3)
            {
                for(int  j = 0; j < 3; j++)
                {
                    var idx = submeshTriangles[i + j];
                    newVertices.Add(originalVertices[idx]);
                    newNormals.Add(originalNormals[idx]);
                    newUvs.Add(originalUVs[idx]);
                    newTriangles.Add(idxCounter++);
                }
            }
            newMesh.vertices = newVertices.ToArray();
            newMesh.normals = newNormals.ToArray();
            newMesh.uv = newUvs.ToArray();
            newMesh.triangles = newTriangles.ToArray();
            newMesh.RecalculateBounds();
            newMesh.RecalculateNormals();
            newMesh.RecalculateTangents();
            return newMesh;
        } 
    }

    public class MeshVerticesUtils
    {
        public static List<Vector3> GetNeighborVertices(Mesh sourceMesh, IEnumerable<Vector3> lookUpVertices/*, out HashSet<int> neighborVerticesIndexes*/)
        {
            var res = new HashSet<Vector3>();
            var edgeList = MeshEdgesUtils.GetEdgeList(sourceMesh);
            var sharedEdgesWithLookUpVertices = edgeList.Where(x => lookUpVertices.Contains(x.v1.position) || lookUpVertices.Contains(x.v2.position)).ToList();
            //neighborVerticesIndexes = new HashSet<int>();
            foreach (var sharedEdge in sharedEdgesWithLookUpVertices)
            {
                if(!lookUpVertices.Contains(sharedEdge.v1.position))
                {
                    res.Add(sharedEdge.v1.position);
                    //neighborVerticesIndexes.Add(sharedEdge.v1.index);
                }
                if (!lookUpVertices.Contains(sharedEdge.v2.position))
                {
                    res.Add(sharedEdge.v2.position);
                    //neighborVerticesIndexes.Add(sharedEdge.v2.index);
                }
            }

            return res.ToList();
        }

        public static void DrawVerticesAsSpheres(IEnumerable<Vector3> vertices, float scale = 0.005f, string holderName = "Holder", GameObject holder = null)
        {
            if(holder == null) holder = GameObject.Find("holderName");
           if (holder == null)
            {
                holder = new GameObject(holderName);
            }
            for(int i = 0; i< vertices.Count(); i++)
            {
                DrawSphereInPosition(vertices.ElementAt(i), holder, scale, i);
            }
            foreach (var vertex in vertices)
            {
                
                /*var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.localScale = new Vector3(scale, scale, scale);
                sphere.transform.position = vertex;
                sphere.transform.parent = holder.transform;*/
            }
        }

        public static void DrawVerticesAsSpheres(IEnumerable<Vector2> vertices, float scale = 0.005f, string holderName = "Holder")
        {
            var holder = new GameObject();
            holder.name = holderName;
            foreach (var vertex in vertices)
            {
                DrawSphereInPosition(vertex, holder, scale);
                /*var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.localScale = new Vector3(scale, scale, scale);
                sphere.transform.position = vertex;
                sphere.transform.parent = holder.transform;*/
            }
        }

        public static GameObject DrawSphereInPosition(Vector3 position, GameObject holder = null, float scale = 0.005f, int nameIdx = -1)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = new Vector3(scale, scale, scale);
            sphere.transform.position = position;
            if(holder != null) sphere.transform.parent = holder.transform;
            if (nameIdx != -1) sphere.name = sphere.name + "_" + nameIdx.ToString();
            return sphere;
        }


        public static GameObject DrawSphereInPosition2(Vector3 position, GameObject holder, float scale = 0.005f, string name = "Sphere")
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = new Vector3(scale, scale, scale);
            sphere.transform.position = position;
            sphere.transform.parent = holder.transform;
            sphere.name = name;
            return sphere;
        }

        public static Vector3 RotateVertex(Vector3 vertex, Quaternion rotation)
        {
            return rotation * vertex;
        }

        public static List<Vector3> FindUniqueVertices(Vector3[] aVertices, float delta = 0.0001f)
        {

            var list = new List<Vector3>();
            for (int i = 0; i < aVertices.Length; i++)
            {
                bool duplicated = false;
                foreach (var item in list)
                {
                    if ((item - aVertices[i]).sqrMagnitude < delta)
                    {
                        duplicated = true;
                        break;
                    }
                }
                if (!duplicated)
                {
                    list.Add(aVertices[i]);
                }
            }
            return list;
        }
    }

    public class MeshEdgesUtils
    {
        public static List<Vertex> FindSharedVertices(Vector3[] aVertices, float delta = 0.0001f)
        {
            var list = new List<Vertex>();
            for (int i = 0; i < aVertices.Length; i++)
            {
                //Vertex v = null;
                Vertex v = new Vertex();
                bool shared = false;
                foreach (var item in list)
                {
                    if ((item.position - aVertices[i]).sqrMagnitude < delta)
                    {
                        v = item;
                        shared = true;
                        break;
                    }
                }
                /*if (v == null)
                {
                    v = new Vertex(aVertices[i]);
                }*/
                if(!shared)
                {
                    v = new Vertex(aVertices[i]);
                }
                list.Add(v);
            }
            Debug.Log(aVertices.ToList().Equals(list));
            return list;
        }

        private static List<Edge> CreateEdgeList(List<Vertex> triangles)
        {
            var res = new HashSet<Edge>();
            int count = triangles.Count / 3;
            for (int i = 0; i < count; i++)
            {
                Vertex v1 = triangles[i * 3];
                Vertex v2 = triangles[i * 3 + 1];
                Vertex v3 = triangles[i * 3 + 2];
                Edge e;
                e = new Edge(v1, v2);
                res.Add(e);
                e = new Edge(v2, v3);
                res.Add(e);
                e = new Edge(v3, v1);
                res.Add(e);
            }
            return res.ToList();
        }

        public static List<Edge> GetEdgeList(Mesh sourceMesh)
        {
            var vertexList = sourceMesh.vertices.Select((v,i) => new Vertex(v,i)).ToList();
            var tris = sourceMesh.triangles;
            var triangles = new List<Vertex>(tris.Length);
            foreach (var t in tris)
                triangles.Add(vertexList[t]);
            return CreateEdgeList(triangles);
        }
    }

    public class CurveSamplesUtils
    {
        public static Vector3[] GetLocationsArray(List<SplineMesh.CurveSample> samples)
        {
            var res = new Vector3[samples.Count];
            var verticesLength = samples.Count;
            for(int i = 0; i < verticesLength; i++)
            {
                res[i] = samples[i].location;
            }
            return res;
        }

        public static Quaternion[] GetRotationsArray(List<SplineMesh.CurveSample> samples)
        {
            var res = new Quaternion[samples.Count];
            var verticesLength = samples.Count;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = samples[i].Rotation;
            }
            return res;
        }

        public static Vector2[] GetScalesArray(List<SplineMesh.CurveSample> samples)
        {
            var res = new Vector2[samples.Count];
            var verticesLength = samples.Count;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = samples[i].scale;
            }
            return res;
        }

        public static float[] GetRollsArray(List<SplineMesh.CurveSample> samples)
        {
            var res = new float[samples.Count];
            var verticesLength = samples.Count;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = samples[i].roll;
            }
            return res;
        }

        public static float3[] GetLocationsArray2(List<SplineMesh.CurveSample> samples)
        {
            var res = new float3[samples.Count];
            var verticesLength = samples.Count;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = samples[i].location;
            }
            return res;
        }

        public static quaternion[] GetRotationsArray2(List<SplineMesh.CurveSample> samples)
        {
            var res = new quaternion[samples.Count];
            var verticesLength = samples.Count;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = samples[i].Rotation;
            }
            return res;
        }

        public static float2[] GetScalesArray2(List<SplineMesh.CurveSample> samples)
        {
            var res = new float2[samples.Count];
            var verticesLength = samples.Count;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = samples[i].scale;
            }
            return res;
        }

        public static float[] GetRollsArray2(List<SplineMesh.CurveSample> samples)
        {
            var res = new float[samples.Count];
            var verticesLength = samples.Count;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = samples[i].roll;
            }
            return res;
        }

        public static CurveSamples GetCurveSamplesArrays(List<SplineMesh.CurveSample> samples)
        {
            var samplesCount = samples.Count;
            var res = new CurveSamples(samplesCount);
            for (int i = 0; i < samplesCount; i++)
            {
                res.locations[i] = samples[i].location;
                res.rotations[i] = samples[i].Rotation;
                res.scales[i] = samples[i].scale;
                res.rolls[i] = samples[i].roll;
            }
            return res;
        }

    }

    public struct CurveSamples
    {
        public Vector3[] locations;
        public Quaternion[] rotations;
        public Vector2[] scales;
        public float[] rolls;

        public CurveSamples(int size)
        {
            locations = new Vector3[size];
            rotations = new Quaternion[size];
            scales = new Vector2[size];
            rolls = new float[size];
        }
    }

    public class VertexUtils
    {
        public static Vector3[] GetPositionsArray(Vertex[] vertices)
        {
            var res = new Vector3[vertices.Length];
            var verticesLength = vertices.Length;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = vertices[i].position;
            }
            return res;
        }

        public static Vector3[] GetNormalsArray(Vertex[] vertices)
        {
            var res = new Vector3[vertices.Length];
            var verticesLength = vertices.Length;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = vertices[i].normal;
            }
            return res;
        }

        public static Vector4[] GetTangentsArray(Vertex[] vertices)
        {
            var res = new Vector4[vertices.Length];
            var verticesLength = vertices.Length;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = vertices[i].tangent;
            }
            return res;
        }
    }

    public class BlenshapeVertexUtils
    {
        public static Vector3[] GetPositionsArray(BlendshapeVertex[] vertices)
        {
            var res = new Vector3[vertices.Length];
            var verticesLength = vertices.Length;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = vertices[i].position;
            }
            return res;
        }

        public static Vector3[] GetNormalsArray(BlendshapeVertex[] vertices)
        {
            var res = new Vector3[vertices.Length];
            var verticesLength = vertices.Length;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = vertices[i].normal;
            }
            return res;
        }

        public static Vector3[] GetTangentsArray(BlendshapeVertex[] vertices)
        {
            var res = new Vector3[vertices.Length];
            var verticesLength = vertices.Length;
            for (int i = 0; i < verticesLength; i++)
            {
                res[i] = vertices[i].tangent;
            }
            return res;
        }

        public static BlendshapeVertices GetBlendshapeVertices(BlendshapeVertex[] vertices)
        {
            var verticesLength = vertices.Length;
            var res = new BlendshapeVertices(verticesLength);
            for(int i = 0; i < verticesLength; i++)
            {
                res.positions[i] = vertices[i].position;
                res.normals[i] = vertices[i].normal;
                res.tangents[i] = vertices[i].tangent;
            }
            return res;
        }


    }

    /*public class SplineMeshUtils
    {
        public static void BuildMesh(CubicBezierCurve curve, Mesh targetMesh,
            Mesh sourceMesh,
            IEnumerable<int> triangles = null,
            IEnumerable<Vector3> vertices = null,
            IEnumerable<Vector3> normals = null,
            IEnumerable<Blendshape> blendshapes = null,
            IEnumerable<Vector2> uv = null,
            IEnumerable<Vector2> uv2 = null,
            IEnumerable<Vector2> uv3 = null,
            IEnumerable<Vector2> uv4 = null,
            IEnumerable<Vector2> uv5 = null,
            IEnumerable<Vector2> uv6 = null,
            IEnumerable<Vector2> uv7 = null,
            IEnumerable<Vector2> uv8 = null)
        {
            targetMesh.hideFlags = sourceMesh.hideFlags;
#if UNITY_2017_3_OR_NEWER
            targetMesh.indexFormat = sourceMesh.indexFormat;
#endif
            targetMesh.triangles = new int[0];
            targetMesh.vertices = vertices == null ? sourceMesh.vertices : vertices.ToArray();
            targetMesh.normals = normals == null ? sourceMesh.normals : normals.ToArray();
            targetMesh.uv = uv == null ? sourceMesh.uv : uv.ToArray();
            targetMesh.uv2 = uv2 == null ? sourceMesh.uv2 : uv2.ToArray();
            targetMesh.uv3 = uv3 == null ? sourceMesh.uv3 : uv3.ToArray();
            targetMesh.uv4 = uv4 == null ? sourceMesh.uv4 : uv4.ToArray();
#if UNITY_2018_2_OR_NEWER
            targetMesh.uv5 = uv5 == null ? sourceMesh.uv5 : uv5.ToArray();
            targetMesh.uv6 = uv6 == null ? sourceMesh.uv6 : uv6.ToArray();
            targetMesh.uv7 = uv7 == null ? sourceMesh.uv7 : uv7.ToArray();
            targetMesh.uv8 = uv8 == null ? sourceMesh.uv8 : uv8.ToArray();
#endif
            targetMesh.triangles = triangles == null ? sourceMesh.triangles : triangles.ToArray();

            targetMesh.ClearBlendShapes();

            if (blendshapes != null)
            {
                foreach (var blendshape in blendshapes)
                {
                    foreach (var frame in blendshape.frames)
                    {
                        targetMesh.AddBlendShapeFrame(blendshape.name, frame.frameWeight, frame.deltaVertices, frame.deltaNormals, frame.deltaTangents);
                    }
                }
            }
            targetMesh.RecalculateBounds();
            //targetMesh.RecalculateNormals();
            targetMesh.RecalculateTangents();
        }
    }*/

    public class Vector2ClockwiseComparer : IComparer<Vector2>
    {
        private Vector2 m_Origin;

        #region Properties

        /// <summary>
        ///     Gets or sets the origin.
        /// </summary>
        /// <value>The origin.</value>
        public Vector2 origin { get { return m_Origin; } set { m_Origin = value; } }

        #endregion

        /// <summary>
        ///     Initializes a new instance of the ClockwiseComparer class.
        /// </summary>
        /// <param name="origin">Origin.</param>
        public Vector2ClockwiseComparer(Vector2 origin)
        {
            m_Origin = origin;
        }

        #region IComparer Methods

        /// <summary>
        ///     Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
        /// </summary>
        /// <param name="first">First.</param>
        /// <param name="second">Second.</param>
        public int Compare(Vector2 pointA, Vector2 pointB)
        {

            return IsClockwise(pointA, pointB, m_Origin);
        }

        #endregion

        /// <summary>
        ///     Returns 1 if first comes before second in clockwise order.
        ///     Returns -1 if second comes before first.
        ///     Returns 0 if the points are identical.
        /// </summary>
        /// <param name="first">First.</param>
        /// <param name="second">Second.</param>
        /// <param name="origin">Origin.</param>
        public static int IsClockwise(Vector2 first, Vector2 second, Vector2 origin)
        {
            if (first == second)
                return 0;

            Vector2 firstOffset = first - origin;
            Vector2 secondOffset = second - origin;

            float angle1 = Mathf.Atan2(firstOffset.x, firstOffset.y);
            float angle2 = Mathf.Atan2(secondOffset.x, secondOffset.y);

            if (angle1 < angle2)
                return 1;

            if (angle1 > angle2)
                return -1;

            // Check to see which point is closest
            return (firstOffset.sqrMagnitude < secondOffset.sqrMagnitude) ? 1 : -1;
        }

    }
}