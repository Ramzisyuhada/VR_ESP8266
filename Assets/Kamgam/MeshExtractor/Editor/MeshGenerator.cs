using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Unity.Collections;
using System.Linq;

namespace Kamgam.MeshExtractor
{
    public class MeshGenerator
    {
        public class BlendShapeFrame
        {
            public string Name;
            public float Weight;
            public Vector3[] DeltaVertices;
            public Vector3[] DeltaNormals;
            public Vector3[] DeltaTangents;

            public BlendShapeFrame(string name, float weight, int vertexCount)
            {
                Name = name;
                Weight = weight;
                DeltaVertices = new Vector3[vertexCount];
                DeltaNormals = new Vector3[vertexCount];
                DeltaTangents = new Vector3[vertexCount];
            }

            public static List<List<BlendShapeFrame>> CreateFromMesh(Mesh mesh)
            {
                var result = new List<List<BlendShapeFrame>>();

                if (mesh.blendShapeCount == 0)
                    return result;

                for (int shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                {
                    var frames = CreateFromMesh(mesh, shapeIndex);
                    if (frames.Count > 0)
                    {
                        result.Add(frames);
                    }
                }

                return result;
            }

            public static List<BlendShapeFrame> CreateFromMesh(Mesh mesh, int shapeIndex)
            {
                var result = new List<BlendShapeFrame>();

                if (mesh.blendShapeCount <= shapeIndex)
                    return result;

                int frameCount = mesh.GetBlendShapeFrameCount(shapeIndex);
                if (frameCount == 0)
                    return result;

                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    var frame = CreateFromMesh(mesh, shapeIndex, frameIndex);
                    if (frame != null)
                        result.Add(frame);
                }

                return result;
            }

            public static BlendShapeFrame CreateFromMesh(Mesh mesh, int shapeIndex, int frameIndex)
            {
                if (mesh.blendShapeCount <= shapeIndex)
                    return null;

                int frameCount = mesh.GetBlendShapeFrameCount(shapeIndex);

                if (frameCount <= frameIndex)
                    return null;

                string name = mesh.GetBlendShapeName(shapeIndex);
                var weight = mesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);

                var frame = new BlendShapeFrame(name, weight, mesh.vertexCount);
                mesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, frame.DeltaVertices, frame.DeltaNormals, frame.DeltaTangents);

                return frame;
            }

            /// <summary>
            /// Removes all vertices that are not in the list of remaining vertex INDICES.
            /// </summary>
            /// <param name="remainingVertexIndices"></param>
            public void TrimVertices(List<int> remainingVertexIndices)
            {
                Vector3[] newDeltaVertices = new Vector3[remainingVertexIndices.Count];
                copyVerticesByIndex(DeltaVertices, newDeltaVertices, remainingVertexIndices);
                DeltaVertices = newDeltaVertices;

                Vector3[] newDeltaNormals = new Vector3[remainingVertexIndices.Count];
                copyVerticesByIndex(DeltaNormals, newDeltaNormals, remainingVertexIndices);
                DeltaNormals = newDeltaNormals;

                Vector3[] newDeltaTangents = new Vector3[remainingVertexIndices.Count];
                copyVerticesByIndex(DeltaTangents, newDeltaTangents, remainingVertexIndices);
                DeltaTangents = newDeltaTangents;
            }

            protected void copyVerticesByIndex(Vector3[] source, Vector3[] target, List<int> remainingVertexIndices)
            {
                Debug.Assert(remainingVertexIndices.Count == target.Length);

                int targetIndex = 0;
                for (int i = 0; i < source.Length; i++)
                {
                    if (remainingVertexIndices.Contains(i))
                    {
                        target[targetIndex] = source[i];
                        targetIndex++;
                    }
                }
            }
        }



        /// <summary>
        /// Takes the selected triangles and generates a new mesh from them.
        /// </summary>
        /// <param name="pivotGlobal"></param>
        /// <param name="selectedTriangles"></param>
        /// <param name="filePath">File path relative to the Assets directory. You don not have to include "Assets/".</param>
        /// <param name="replaceOldFiles"></param>
        /// <param name="preserveSubMeshes"></param>
        /// <param name="combineSubMeshesBasedOnMaterials"></param>
        /// <param name="combineMeshes"></param>
        /// <param name="saveAsObj"></param>
        /// <param name="extractTextures"></param>
        /// <param name="extractBoneWeights"></param>
        /// <param name="extractBoneTransforms"></param>
        /// <param name="extractBlendShapes"></param>
        /// <param name="createPrefab"></param>
        /// <param name="showProgress"></param>
        public static void GenerateMesh(
            Vector3 pivotGlobal,
            HashSet<SelectedTriangle> selectedTriangles,
            string filePath,
            bool replaceOldFiles,
            bool preserveSubMeshes = true,
            bool combineSubMeshesBasedOnMaterials = true,
            bool combineMeshes = true,
            bool saveAsObj = false,
            bool extractTextures = false,
            bool extractBoneWeights = false,
            bool extractBoneTransforms = false,
            bool extractBlendShapes = true,
            bool createPrefab = true, bool showProgress = true)
        {
            if (saveAsObj && (extractBoneWeights || extractBlendShapes))
            {
                Logger.LogMessage("Saving meshes with bone weights or blend shapes as obj is not supported. Mesh will be saved as a Unity asset instead.");
                saveAsObj = false;
            }

            // A list linking new meshes to selected triangles.
            var newMeshToTriMap = new List<(Mesh, SelectedTriangle)>();

            // Notice that the vertices stored in the SelectedTriangles are NOT used. Instead the vertices
            // are directly copied form the source mesh (or baked mesh if skinned).
            var selectedTrisPerMesh = new Dictionary<Mesh, List<SelectedTriangle>>();
            var vertices = new Dictionary<Mesh, List<Vector3>>();
            var triangles = new Dictionary<Mesh, List<int[]>>();
            var normals = new Dictionary<Mesh, List<Vector3>>();
            var uvs = new Dictionary<Mesh, List<Vector2>>();
            var uv2s = new Dictionary<Mesh, List<Vector2>>();
            var uv3s = new Dictionary<Mesh, List<Vector2>>();
            var uv4s = new Dictionary<Mesh, List<Vector2>>();
            var uv5s = new Dictionary<Mesh, List<Vector2>>();
            var uv6s = new Dictionary<Mesh, List<Vector2>>();
            var uv7s = new Dictionary<Mesh, List<Vector2>>();
            var uv8s = new Dictionary<Mesh, List<Vector2>>();
            var colors = new Dictionary<Mesh, List<Color>>();
            var tangents = new Dictionary<Mesh, List<Vector4>>();
            var materials = new Dictionary<Mesh, List<Material>>();
            var bindPoses = new Dictionary<Mesh, Matrix4x4[]>();
            var boneWeights = new Dictionary<Mesh, NativeArray<BoneWeight1>>();
            var bonesPerVertex = new Dictionary<Mesh, NativeArray<byte>>();
            var hasBones = new Dictionary<Mesh, bool>();
            var hasBlendShapes = new Dictionary<Mesh, bool>();
            var blendShapes = new Dictionary<Mesh, List<List<BlendShapeFrame>>>();

            foreach (var tri in selectedTriangles)
            {
                // If bones or blend shapes are used then we want the shared (unaltered) mesh, not the deformed vertices.
                var sourceMesh = (extractBoneWeights || extractBlendShapes) ? tri.SharedMesh : tri.Mesh;

                // Do once for each mesh used a selected triangle.
                if (!vertices.ContainsKey(sourceMesh))
                {
                    // Create new triangle list for this mesh
                    selectedTrisPerMesh.Add(sourceMesh, new List<SelectedTriangle>());

                    // Vertices
                    // Convert vertices to world space and match the desired pivot.
                    sourceMesh.GetVertices(getOrCreateMeshData(sourceMesh, vertices));
                    int numOfVertices = vertices[sourceMesh].Count;
                    for (int i = 0; i < numOfVertices; i++)
                    {
                        vertices[sourceMesh][i] = tri.Transform.TransformPoint(vertices[sourceMesh][i]);
                        if (extractBoneWeights || extractBlendShapes)
                        {
                            // Bring back from world space into the space of the root bone.
                            vertices[sourceMesh][i] = tri.Transform.InverseTransformPoint(vertices[sourceMesh][i]);
                        }
                        else
                        {
                            // Apply user pivot if exporting without bone weights.
                            vertices[sourceMesh][i] -= pivotGlobal;
                        }
                    }

                    // Triangles
                    var tris = new List<int[]>();
                    for (int i = 0; i < sourceMesh.subMeshCount; i++)
                    {
                        tris.Add(sourceMesh.GetTriangles(i));
                    }
                    triangles.Add(sourceMesh, tris);

                    // Normals
                    sourceMesh.GetNormals(getOrCreateMeshData(sourceMesh, normals));
                    // Convert normals to world space
                    int numOfNormals = normals[sourceMesh].Count;
                    for (int i = 0; i < numOfNormals; i++)
                    {
                        normals[sourceMesh][i] = tri.Transform.TransformDirection(normals[sourceMesh][i]);
                        // If bone export then transform into local space of root bone
                        if (extractBoneWeights || extractBlendShapes)
                        {
                            // Bring back from world space into the space of the root bone.
                            normals[sourceMesh][i] = tri.Transform.InverseTransformDirection(normals[sourceMesh][i]);
                        }
                    }

                    // UVs
                    sourceMesh.GetUVs(0, getOrCreateMeshData(sourceMesh, uvs));
                    sourceMesh.GetUVs(1, getOrCreateMeshData(sourceMesh, uv2s));
                    sourceMesh.GetUVs(2, getOrCreateMeshData(sourceMesh, uv3s));
                    sourceMesh.GetUVs(3, getOrCreateMeshData(sourceMesh, uv4s));
                    sourceMesh.GetUVs(4, getOrCreateMeshData(sourceMesh, uv5s));
                    sourceMesh.GetUVs(5, getOrCreateMeshData(sourceMesh, uv6s));
                    sourceMesh.GetUVs(6, getOrCreateMeshData(sourceMesh, uv7s));
                    sourceMesh.GetUVs(7, getOrCreateMeshData(sourceMesh, uv8s));

                    // Colors
                    sourceMesh.GetColors(getOrCreateMeshData(sourceMesh, colors));

                    // Tangents
                    sourceMesh.GetTangents(getOrCreateMeshData(sourceMesh, tangents));

                    // Materials
                    var meshRenderer = tri.Transform.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        meshRenderer.GetSharedMaterials(getOrCreateMeshData(sourceMesh, materials));
                    }
                    else
                    {
                        var skinnedMeshRenderer = tri.Component as SkinnedMeshRenderer;
                        if (skinnedMeshRenderer != null)
                        {
                            skinnedMeshRenderer.GetSharedMaterials(getOrCreateMeshData(sourceMesh, materials));
                        }
                        else
                        {
                            // init empty if no mesh filter or skinned renderer was found (no materials)
                            getOrCreateMeshData(sourceMesh, materials);
                        }
                    }

                    // Bone weights
                    if (extractBoneWeights)
                    {
                        var weights = sourceMesh.GetAllBoneWeights();
                        boneWeights.Add(sourceMesh, weights);
                        bonesPerVertex.Add(sourceMesh, sourceMesh.GetBonesPerVertex());
                        bindPoses.Add(sourceMesh, sourceMesh.bindposes);
                        hasBones.Add(sourceMesh, weights.Length > 0);
                    }

                    // Blend Shapes
                    if (extractBlendShapes)
                    {
                        var shapes = BlendShapeFrame.CreateFromMesh(sourceMesh);
                        if(shapes.Count > 0)
                        {
                            hasBlendShapes.Add(sourceMesh, true);
                            blendShapes.Add(sourceMesh, shapes);
                        }
                        else
                        {
                            hasBlendShapes.Add(sourceMesh, false);
                        }
                    }
                }

                // Add triangle to mesh list
                selectedTrisPerMesh[sourceMesh].Add(tri);
            }

            if (showProgress)
                EditorUtility.DisplayProgressBar("Extracting Mesh", "Gathering and sorting " + selectedTriangles.Count + " triangles " + (selectedTriangles.Count > 10000 ? "(this may take a while)" : "") + " ..", 0.2f);

            // new mesh to new (possibly merged) sub mesh materials list
            var materialsForSubMeshes = new Dictionary<Mesh, List<Material>>();

            foreach (var kv in selectedTrisPerMesh)
            {
                var mesh = kv.Key;
                var selectedTris = kv.Value;

                // gather old vertices and sort them
                var newVertexIndices = new List<int>();
                foreach (var tri in selectedTris)
                {
                    int a = tri.TriangleIndices[0];
                    if (!newVertexIndices.Contains(a))
                    {
                        newVertexIndices.Add(a);
                    }

                    int b = tri.TriangleIndices[1];
                    if (!newVertexIndices.Contains(b))
                    {
                        newVertexIndices.Add(b);
                    }

                    int c = tri.TriangleIndices[2];
                    if (!newVertexIndices.Contains(c))
                    {
                        newVertexIndices.Add(c);
                    }
                }
                newVertexIndices.Sort();
                var oldToNewVertexMap = new Dictionary<int, int>();
                for (int i = 0; i < newVertexIndices.Count; i++)
                {
                    oldToNewVertexMap.Add(newVertexIndices[i], i);
                }

                // New vertices and per vertex infos
                var newVertices = new List<Vector3>();
                var newNormals = new List<Vector3>();
                var newUVs = new List<Vector2>();
                var newUV2s = new List<Vector2>();
                var newUV3s = new List<Vector2>();
                var newUV4s = new List<Vector2>();
                var newUV5s = new List<Vector2>();
                var newUV6s = new List<Vector2>();
                var newUV7s = new List<Vector2>();
                var newUV8s = new List<Vector2>();
                var newColors = new List<Color>();
                var newTangents = new List<Vector4>();
                var newBoneWeights = new List<BoneWeight1>();
                var newBonesPerVertex = new List<byte>();
                var newBindPoses = mesh.bindposes;
                var newBlendShapeIndices = new List<int>(); // The list of indices that will remain in the mesh.

                for (int i = 0; i < newVertexIndices.Count; i++)
                {
                    int vertexIndex = newVertexIndices[i];
                    newVertices.Add(vertices[mesh][vertexIndex]);
                    newNormals.Add(normals[mesh][vertexIndex]);
                    newUVs.Add(uvs[mesh][vertexIndex]);
                    if (uv2s[mesh].Count > 0) newUV2s.Add(uv2s[mesh][vertexIndex]);
                    if (uv3s[mesh].Count > 0) newUV3s.Add(uv3s[mesh][vertexIndex]);
                    if (uv4s[mesh].Count > 0) newUV4s.Add(uv4s[mesh][vertexIndex]);
                    if (uv5s[mesh].Count > 0) newUV5s.Add(uv5s[mesh][vertexIndex]);
                    if (uv6s[mesh].Count > 0) newUV6s.Add(uv6s[mesh][vertexIndex]);
                    if (uv7s[mesh].Count > 0) newUV7s.Add(uv7s[mesh][vertexIndex]);
                    if (uv8s[mesh].Count > 0) newUV8s.Add(uv8s[mesh][vertexIndex]);
                    if (colors[mesh].Count > 0) newColors.Add(colors[mesh][vertexIndex]);
                    if (tangents[mesh].Count > 0) newTangents.Add(tangents[mesh][vertexIndex]);

                    if (extractBoneWeights && hasBones[mesh])
                    {
                        newBonesPerVertex.Add(bonesPerVertex[mesh][vertexIndex]);
                        // find the starting index of the weights in newBoneWeights
                        int boneWeightIndex = 0;
                        for (int b = 0; b < vertexIndex; b++)
                        {
                            boneWeightIndex += bonesPerVertex[mesh][b];
                        }
                        // add the number of weights specified in newBonesPerVertex
                        int numOfWeightsForThisIndex = bonesPerVertex[mesh][vertexIndex];
                        for (int w = 0; w < numOfWeightsForThisIndex; w++)
                        {
                            var weight = boneWeights[mesh][boneWeightIndex + w];
                            newBoneWeights.Add(weight);
                        }
                    }

                    if (extractBlendShapes && hasBlendShapes[mesh])
                    {
                        newBlendShapeIndices.Add(vertexIndex);
                    }
                }

                var newMesh = new Mesh();

                // new tris (and sub meshes)
                if (!preserveSubMeshes)
                {
                    var newTris = new List<int>();
                    foreach (var tri in selectedTris)
                    {
                        int a = oldToNewVertexMap[tri.TriangleIndices[0]];
                        int b = oldToNewVertexMap[tri.TriangleIndices[1]];
                        int c = oldToNewVertexMap[tri.TriangleIndices[2]];

                        newTris.Add(a);
                        newTris.Add(b);
                        newTris.Add(c);
                    }

                    newMesh.SetVertices(newVertices);
                    newMesh.SetNormals(newNormals);
                    newMesh.SetTriangles(newTris, 0);
                    newMesh.SetUVs(0, newUVs);
                    if (newUV2s.Count > 0) newMesh.SetUVs(1, newUV2s);
                    if (newUV3s.Count > 0) newMesh.SetUVs(2, newUV3s);
                    if (newUV4s.Count > 0) newMesh.SetUVs(3, newUV4s);
                    if (newUV5s.Count > 0) newMesh.SetUVs(4, newUV5s);
                    if (newUV6s.Count > 0) newMesh.SetUVs(5, newUV6s);
                    if (newUV7s.Count > 0) newMesh.SetUVs(6, newUV7s);
                    if (newUV8s.Count > 0) newMesh.SetUVs(7, newUV8s);
                    if (newColors.Count > 0) newMesh.SetColors(newColors);
                    newMesh.SetTangents(newTangents);

                    if (extractBoneWeights && hasBones[mesh])
                    {
                        var nativeBonesPerVertex = new NativeArray<byte>(newBonesPerVertex.ToArray(), Allocator.Temp);
                        var nativeWeights = new NativeArray<BoneWeight1>(newBoneWeights.ToArray(), Allocator.Temp);
                        newMesh.SetBoneWeights(nativeBonesPerVertex, nativeWeights);
                    }

                    if (extractBlendShapes && hasBlendShapes[mesh])
                    {
                        foreach (var shapes in blendShapes[mesh])
                        {
                            foreach (var frame in shapes)
                            {
                                frame.TrimVertices(newBlendShapeIndices);
                                newMesh.AddBlendShapeFrame(frame.Name, frame.Weight, frame.DeltaVertices, frame.DeltaNormals, frame.DeltaTangents);
                            }
                        }
                    }

                    newMesh.RecalculateBounds();
                }
                else
                {
                    // NOTICE: We are still only working on the tris within each mesh. Merging
                    // sub meshes across meshes is done later in the mesh combine step (see below).

                    var trisPerSubMesh = new Dictionary<int, List<SelectedTriangle>>();
                    // Merge by material only if the flag is set AND if there are materials.
                    if (combineSubMeshesBasedOnMaterials && materials[mesh] != null && materials[mesh].Count > 0)
                    {
                        // SubMeshMap: index 0 = the material for subMesh 0, 1 = the material for subMesh 1, ...
                        var subMeshToMaterialMap = new List<Material>();

                        // Group selected tris into sub meshes based on MATERIAL within one mesh.
                        foreach (var tri in selectedTris)
                        {
                            // If bones or blend shapes are used then we want the shared (unaltered) mesh, not the deformed vertices.
                            var sourceMesh = (extractBoneWeights || extractBlendShapes) ? tri.SharedMesh : tri.Mesh;

                            // Get the material matching the current tri.
                            // It may happen that no material is assigned. In that case
                            // we sort the tri into the NULL material sub mesh.
                            Material mat = null;
                            if (materials[sourceMesh] != null && materials[sourceMesh].Count > tri.SubMeshIndex && materials[sourceMesh][tri.SubMeshIndex] != null)
                            {
                                mat = materials[sourceMesh][tri.SubMeshIndex];
                            }
                            if(!subMeshToMaterialMap.Contains(mat))
                            {
                                subMeshToMaterialMap.Add(mat);
                            }
                            // Convert material to sub mesh index.
                            int index = subMeshToMaterialMap.IndexOf(mat);
                            // Insert tris per material
                            if (!trisPerSubMesh.ContainsKey(index))
                            {
                                trisPerSubMesh.Add(index, new List<SelectedTriangle>());
                            }
                            trisPerSubMesh[index].Add(tri);
                        }

                        // Save the materials per sub mesh info for merging multiple meshes.
                        materialsForSubMeshes.Add(newMesh, subMeshToMaterialMap);
                    }
                    else
                    {
                        // Group selected tris into sub meshes based on INDEX within one mesh.
                        foreach (var tri in selectedTris)
                        {
                            if (!trisPerSubMesh.ContainsKey(tri.SubMeshIndex))
                            {
                                trisPerSubMesh.Add(tri.SubMeshIndex, new List<SelectedTriangle>());
                            }
                            trisPerSubMesh[tri.SubMeshIndex].Add(tri);
                        }

                        // Save the materials per sub mesh info for merging multiple meshes.
                        materialsForSubMeshes.Add(newMesh, new List<Material>(materials[mesh]));
                    }

                    // Start mesh
                    newMesh.SetVertices(newVertices);
                    newMesh.SetNormals(newNormals);
                    newMesh.SetUVs(0, newUVs);
                    if (newUV2s.Count > 0) newMesh.SetUVs(1, newUV2s);
                    if (newUV3s.Count > 0) newMesh.SetUVs(2, newUV3s);
                    if (newUV4s.Count > 0) newMesh.SetUVs(3, newUV4s);
                    if (newUV5s.Count > 0) newMesh.SetUVs(4, newUV5s);
                    if (newUV6s.Count > 0) newMesh.SetUVs(5, newUV6s);
                    if (newUV7s.Count > 0) newMesh.SetUVs(6, newUV7s);
                    if (newUV8s.Count > 0) newMesh.SetUVs(7, newUV8s);
                    if (newColors.Count > 0) newMesh.SetColors(newColors);
                    newMesh.SetTangents(newTangents);

                    if (extractBoneWeights && hasBones[mesh])
                    {
                        var nativeBonesPerVertex = new NativeArray<byte>(newBonesPerVertex.ToArray(), Allocator.Temp);
                        var nativeWeights = new NativeArray<BoneWeight1>(newBoneWeights.ToArray(), Allocator.Temp);
                        newMesh.SetBoneWeights(nativeBonesPerVertex, nativeWeights);
                    }

                    if (extractBlendShapes && hasBlendShapes[mesh])
                    {
                        foreach (var shapes in blendShapes[mesh])
                        {
                            foreach (var frame in shapes)
                            {
                                frame.TrimVertices(newBlendShapeIndices);
                                newMesh.AddBlendShapeFrame(frame.Name, frame.Weight, frame.DeltaVertices, frame.DeltaNormals, frame.DeltaTangents);
                            }
                        }
                    }

                    // Add sub mesh tris
                    newMesh.subMeshCount = trisPerSubMesh.Count;
                    int subMeshIndex = 0;
                    foreach (var subKV in trisPerSubMesh)
                    {
                        var tris = subKV.Value;
                        var newTris = new List<int>();
                        foreach (var tri in tris)
                        {
                            int a = oldToNewVertexMap[tri.TriangleIndices[0]];
                            int b = oldToNewVertexMap[tri.TriangleIndices[1]];
                            int c = oldToNewVertexMap[tri.TriangleIndices[2]];

                            newTris.Add(a);
                            newTris.Add(b);
                            newTris.Add(c);
                        }

                        newMesh.SetTriangles(newTris, subMeshIndex);
                        subMeshIndex++;
                    }

                    // finalize mesh
                    newMesh.RecalculateTangents();
                    newMesh.RecalculateBounds();
                }

                // Bind poses (just copy all)
                if (extractBoneWeights && hasBones[mesh])
                {
                    newMesh.bindposes = mesh.bindposes;
                }

                // Add a tuple which links the new mesh to the old mesh infos via a single SelectedTriangle.
                newMeshToTriMap.Add((newMesh, selectedTris[0]));
            }

            if (showProgress)
                EditorUtility.DisplayProgressBar("Extracting Mesh", "Merging tris and materials ..", 0.7f);

            bool shouldCombine = combineMeshes && newMeshToTriMap.Count > 1;
            if (shouldCombine && extractBoneWeights)
            {
                Logger.LogMessage("Combining meshes is not supported if bone-weight export is enabled. Meshes will NOT be combined.");
                shouldCombine = false;
            }
            if (shouldCombine)
            {
                // Combine meshes of multiple objects into one.
                var combinedMesh = new Mesh();

                // Calc sub mesh number
                int maxSubMeshCount = 0;
                if (combineSubMeshesBasedOnMaterials)
                {
                    maxSubMeshCount = materialsForSubMeshes.SelectMany(kv => kv.Value).Distinct().Count();
                }
                else
                {
                    for (int m = 0; m < newMeshToTriMap.Count; m++)
                    {
                        var newMesh = newMeshToTriMap[m].Item1;
                        maxSubMeshCount = Mathf.Max(newMesh.subMeshCount, maxSubMeshCount);
                    }
                }
                combinedMesh.subMeshCount = maxSubMeshCount;

                // Find materials
                // SubMeshMap: index 0 = the material for subMesh 0, 1 = the material for subMesh 1, ...
                var subMeshToMaterialMap = new List<Material>();
                for (int m = 0; m < newMeshToTriMap.Count; m++)
                {
                    var newMesh = newMeshToTriMap[m].Item1;
                    var oldMesh = newMeshToTriMap[m].Item2.Mesh;

                    for (int s = 0; s < maxSubMeshCount; s++)
                    {
                        // Not all meshes may have that many sub meshes.
                        if (s >= newMesh.subMeshCount)
                            continue;
                        
                        // Find the material for the current sub mesh
                        Material mat = null;
                        if (materialsForSubMeshes[newMesh] != null && materialsForSubMeshes[newMesh].Count > s)
                        {
                            mat = materialsForSubMeshes[newMesh][s];
                        }

                        // Add the material to the materials for sub meshes list.
                        if (combineSubMeshesBasedOnMaterials)
                        {
                            // Ensure each material is only used once.
                            if (!subMeshToMaterialMap.Contains(mat))
                            {
                                subMeshToMaterialMap.Add(mat);
                            }
                        }
                        else
                        {
                            // Simply use the first material found.
                            // If the current material is null then try to replace it with the current one (if the current one is not null).
                            if(subMeshToMaterialMap.Count <= s)
                            {
                                subMeshToMaterialMap.Add(mat);
                            }
                            else if (subMeshToMaterialMap[s] == null && mat != null)
                            {
                                subMeshToMaterialMap[s] = mat;
                            }
                        }
                    }
                }

                // Combined vertices and per vertex infos
                var combinedVertices = new List<Vector3>();
                var combinedNormals = new List<Vector3>();
                var combinedUVs = new List<Vector2>();
                var combinedUV2s = new List<Vector2>();
                var combinedUV3s = new List<Vector2>();
                var combinedUV4s = new List<Vector2>();
                var combinedUV5s = new List<Vector2>();
                var combinedUV6s = new List<Vector2>();
                var combinedUV7s = new List<Vector2>();
                var combinedUV8s = new List<Vector2>();
                var combinedColors = new List<Color>();
                var combinedTangents = new List<Vector4>();
                var combinedMaterials = new List<Material>(subMeshToMaterialMap);

                var tmpVector2 = new List<Vector2>();
                var tmpVector3 = new List<Vector3>();
                var tmpVector4 = new List<Vector4>();
                var tmpColor = new List<Color>();
                var tmpInt = new List<int>();

                var offsets = new List<int>();

                for (int m = 0; m < newMeshToTriMap.Count; m++)
                {
                    var newMesh = newMeshToTriMap[m].Item1;
                    var oldMesh = newMeshToTriMap[m].Item2.Mesh;

                    offsets.Add(combinedVertices.Count);

                    // Vertices
                    tmpVector3.Clear();
                    newMesh.GetVertices(tmpVector3);
                    combinedVertices.AddRange(tmpVector3);

                    // Normals
                    tmpVector3.Clear();
                    newMesh.GetNormals(tmpVector3);
                    combinedNormals.AddRange(tmpVector3);

                    // UV0s
                    tmpVector2.Clear();
                    newMesh.GetUVs(0, tmpVector2);
                    combinedUVs.AddRange(tmpVector2);

                    // UV2
                    tmpVector2.Clear();
                    newMesh.GetUVs(1, tmpVector2);
                    combinedUV2s.AddRange(tmpVector2);

                    // UV3
                    tmpVector2.Clear();
                    newMesh.GetUVs(2, tmpVector2);
                    combinedUV3s.AddRange(tmpVector2);

                    // UV4
                    tmpVector2.Clear();
                    newMesh.GetUVs(3, tmpVector2);
                    combinedUV4s.AddRange(tmpVector2);

                    // UV5
                    tmpVector2.Clear();
                    newMesh.GetUVs(4, tmpVector2);
                    combinedUV5s.AddRange(tmpVector2);

                    // UV6
                    tmpVector2.Clear();
                    newMesh.GetUVs(5, tmpVector2);
                    combinedUV6s.AddRange(tmpVector2);

                    // UV7
                    tmpVector2.Clear();
                    newMesh.GetUVs(6, tmpVector2);
                    combinedUV7s.AddRange(tmpVector2);

                    // UV8
                    tmpVector2.Clear();
                    newMesh.GetUVs(7, tmpVector2);
                    combinedUV8s.AddRange(tmpVector2);

                    // Colors
                    tmpColor.Clear();
                    newMesh.GetColors(tmpColor);
                    combinedColors.AddRange(tmpColor);

                    // Tangents
                    tmpVector4.Clear();
                    newMesh.GetTangents(tmpVector4);
                    combinedTangents.AddRange(tmpVector4);
                }

                combinedMesh.SetVertices(combinedVertices);
                combinedMesh.SetNormals(combinedNormals);
                combinedMesh.SetUVs(0, combinedUVs);
                if (combinedUV2s.Count > 0) combinedMesh.SetUVs(1, combinedUV2s);
                if (combinedUV3s.Count > 0) combinedMesh.SetUVs(2, combinedUV3s);
                if (combinedUV4s.Count > 0) combinedMesh.SetUVs(3, combinedUV4s);
                if (combinedUV5s.Count > 0) combinedMesh.SetUVs(4, combinedUV5s);
                if (combinedUV6s.Count > 0) combinedMesh.SetUVs(5, combinedUV6s);
                if (combinedUV7s.Count > 0) combinedMesh.SetUVs(6, combinedUV7s);
                if (combinedUV8s.Count > 0) combinedMesh.SetUVs(7, combinedUV8s);
                if (combinedColors.Count > 0) combinedMesh.SetColors(combinedColors);
                combinedMesh.SetTangents(combinedTangents);

                // s is the final sub mesh index
                for (int s = 0; s < maxSubMeshCount; s++)
                {
                    tmpInt.Clear();

                    for (int m = 0; m < newMeshToTriMap.Count; m++)
                    {
                        int vertexOffset = offsets[m];
                        var newMesh = newMeshToTriMap[m].Item1;
                        var oldMesh = newMeshToTriMap[m].Item2.Mesh;

                        if (combineSubMeshesBasedOnMaterials)
                        {
                            // Find the material for the current sub mesh
                            Material material = subMeshToMaterialMap[s];

                            // Add all the sub meshes with that material to the current final submesh.
                            for (int i = 0; i < newMesh.subMeshCount; i++)
                            {
                                // Convert material to sub mesh index.
                                var subMeshMaterial = materialsForSubMeshes[newMesh][i];
                                if (material == subMeshMaterial)
                                {
                                    var tris = newMesh.GetTriangles(i);
                                    for (int t = 0; t < tris.Length; t++)
                                    {
                                        tris[t] += vertexOffset;
                                    }
                                    tmpInt.AddRange(tris);
                                }
                            }
                        }
                        else
                        {
                            // Not all meshes may have that many sub meshes.
                            if (s >= newMesh.subMeshCount)
                                continue;

                            // Simply append to the sub mesh at index s.
                            var tris = newMesh.GetTriangles(s);
                            for (int t = 0; t < tris.Length; t++)
                            {
                                tris[t] += vertexOffset;
                            }
                            tmpInt.AddRange(tris);
                        }
                    }
                    // Set sub mesh
                    combinedMesh.SetTriangles(tmpInt.ToArray(), s);
                }

                // Finalize mesh
                combinedMesh.RecalculateBounds();

                if (showProgress)
                    EditorUtility.DisplayProgressBar("Extracting Mesh", "Saving meshes ..", 0.9f);

                var path = deleteFileAndGetPath(replaceOldFiles, "Assets/" + filePath + ".asset");

                var combinedMeshAndMaterials = new Dictionary<Mesh, List<Material>>();
                combinedMeshAndMaterials[combinedMesh] = combinedMaterials;

                if (extractTextures)
                {
                    combinedMeshAndMaterials = TextureGenerator.GenerateTexturesAndUpdateUVs(path, combinedMeshAndMaterials, MeshExtractorSettings.GetOrCreateSettings().LogFilePaths);
                }

                Dictionary<string, string> objFiles = null;
                if (saveAsObj) 
                {
                    // It does not matter that the path ends with ".asset" because the SaveMeshAsObj() ignores the file extension anyways.
                    objFiles = ObjExporter.SaveMeshAsObj(path, "Mesh", combinedMesh, materials: combinedMeshAndMaterials[combinedMesh], MeshExtractorSettings.GetOrCreateSettings().LogFilePaths);
                }
                else
                {
                    AssetExporter.SaveMeshAsAsset(combinedMesh, null, path, MeshExtractorSettings.GetOrCreateSettings().LogFilePaths);
                    ProjectWindowUtil.ShowCreatedAsset(combinedMesh);  // Forces an update on the project view window.
                }

                if (createPrefab)
                {
                    createPrefabAsset(filePath, replaceOldFiles, saveAsObj, combinedMesh, combinedMeshAndMaterials[combinedMesh], path, objFiles, boneData: null, extractBoneTransforms, source: null);
                }

                AssetDatabase.Refresh();
            }
            else
            {
                // If meshes should not be combined then create one asset for each mesh.

                if (showProgress)
                    EditorUtility.DisplayProgressBar("Extracting Mesh", "Saving meshes ..", 0.9f);

                int index = 0;
                Dictionary<string, string> objFiles = null;
                foreach (var m in newMeshToTriMap)
                {
                    var newMesh = m.Item1;
                    var selectedTri = m.Item2;
                    var sourceMesh = (extractBoneWeights || extractBlendShapes) ? selectedTri.SharedMesh : selectedTri.Mesh;
                    string path;
                    if(newMeshToTriMap.Count > 1)
                    {
                        index++;
                        path = deleteFileAndGetPath(replaceOldFiles, "Assets/" + filePath + " part " + index + ".asset");
                    }
                    else
                    {
                        path = deleteFileAndGetPath(replaceOldFiles, "Assets/" + filePath + ".asset");
                    }

                    var meshAndMaterials = new Dictionary<Mesh, List<Material>>();
                    if (preserveSubMeshes)
                    {
                        meshAndMaterials.Add(newMesh, materialsForSubMeshes[newMesh]);
                    }
                    else
                    {
                        // Use the first material in the original mesh as the new material.
                        var material = materials[sourceMesh].FirstOrDefault(m => m != null);
                        meshAndMaterials.Add(newMesh, new List<Material>() { material });
                    }

                    if (extractTextures)
                    {
                        meshAndMaterials = TextureGenerator.GenerateTexturesAndUpdateUVs(path, meshAndMaterials, MeshExtractorSettings.GetOrCreateSettings().LogFilePaths);
                    }

                    BoneData newMeshBoneData = null;

                    if (saveAsObj)
                    {
                        // It does not matter that the path ends with ".asset" because the SaveMeshAsObj() ignores the file extension anyways.
                        objFiles = ObjExporter.SaveMeshAsObj(path, "Mesh", newMesh, meshAndMaterials[newMesh], MeshExtractorSettings.GetOrCreateSettings().LogFilePaths);
                    }
                    else
                    {
                        // Extract bone info as a BoneData asset.
                        if (extractBoneWeights && hasBones[selectedTri.SharedMesh])
                        {
                            var renderer = selectedTri.Component as SkinnedMeshRenderer;
                            if (renderer.rootBone != null)
                            {
                                newMeshBoneData = ScriptableObject.CreateInstance<BoneData>();
                                newMeshBoneData.ExtractFromRenderer(renderer);
                            }
                            else
                            {
                                Logger.LogWarning("The SkinnedMeshRenderers '" + renderer.name + "' has no root bone! Your bones will not align!\n" +
                                    "This is required for mesh bones extraction. " +
                                    "Please set the root bone to the root of the rig/armature before exporting.\n" +
                                    "Skipping bone-data export for now (your bones will probably not align wit the exported weights).");
                            }
                        }

                        AssetExporter.SaveMeshAsAsset(newMesh, newMeshBoneData, path, MeshExtractorSettings.GetOrCreateSettings().LogFilePaths);
                        ProjectWindowUtil.ShowCreatedAsset(newMesh); // Forces an update on the project view window.
                    }

                    if (createPrefab)
                    {
                        createPrefabAsset(filePath, replaceOldFiles, saveAsObj, newMesh, meshAndMaterials[newMesh], path, objFiles, newMeshBoneData, extractBoneTransforms, selectedTri.Component.gameObject);
                    }
                }

                AssetDatabase.Refresh();
            }
        }

        private static void createPrefabAsset(string filePath, bool replaceOldFiles, bool saveAsObj, Mesh combinedMesh, List<Material> combinedMaterials, string path, Dictionary<string, string> objFiles, BoneData boneData, bool extractBoneTransforms, GameObject source)
        {
            var prefabPath = path.Replace(".asset", ".prefab");
            prefabPath = deleteFileAndGetPath(replaceOldFiles, prefabPath);

            var name = System.IO.Path.GetFileNameWithoutExtension(filePath);
            var prefabGO = new GameObject(name);
            GameObject meshGO = prefabGO;
            GameObject rootBoneGO = null;
            if (boneData != null)
            {
                if (extractBoneTransforms && source != null)
                {
                    meshGO = new GameObject(name + "_Mesh");
                    meshGO.transform.SetParent(prefabGO.transform);
                    meshGO.transform.localPosition = Vector3.zero;
                    meshGO.transform.localRotation = Quaternion.identity;
                    meshGO.transform.localScale = Vector3.one;

                    var sourceRenderer = source.GetComponent<SkinnedMeshRenderer>();
                    if (sourceRenderer != null)
                    {
                        var rootBone = sourceRenderer.rootBone;
                        if(rootBone == null && sourceRenderer.bones != null && sourceRenderer.bones.Length > 0)
                        {
                            rootBone = sourceRenderer.bones[0]; // may still be null
                        }
                        if (rootBone != null)
                        {
                            rootBoneGO = new GameObject(rootBone.gameObject.name);
                            rootBoneGO.transform.SetParent(prefabGO.transform);
                            rootBoneGO.transform.localPosition = rootBone.localPosition;
                            rootBoneGO.transform.localRotation = rootBone.localRotation;
                            rootBoneGO.transform.localScale = rootBone.localScale;

                            copyTransforms(rootBone, rootBoneGO.transform, false);
                        }
                        else
                        {
                            Logger.LogWarning("No root bone found to copy. Skipping bone transform export.");
                        }
                    }
                }

                var skinnedMeshRenderer = meshGO.AddComponent<SkinnedMeshRenderer>();
                if (saveAsObj)
                {
                    // Save as obj not supported if bones are extracted
                    Logger.LogWarning("Saving a .obj is not supported if bone weights are exported.");
                }
                else
                {
                    // Mesh data
                    skinnedMeshRenderer.sharedMesh = combinedMesh;

                    // bone data & resolver
                    boneData.ResolveAndApplyTo(skinnedMeshRenderer.rootBone, skinnedMeshRenderer);
                    var boneResolver = meshGO.AddComponent<BoneDataResolver>();
                    boneResolver.BoneData = boneData;
                    if (rootBoneGO != null)
                    {
                        boneResolver.Resolve(rootBoneGO);
                    }
                    else
                    {
                        // Assign bone transforms to the renderer to avoid 
                        // 'Bones do not match bindpose' errors.
                        // The resolver will do its work anyways once the prefab is dragged into the scene.
                        boneResolver.AssignNullTransforms();
                    }

                    skinnedMeshRenderer.sharedMesh.RecalculateBounds();
                    skinnedMeshRenderer.localBounds = skinnedMeshRenderer.sharedMesh.bounds;
                }
                skinnedMeshRenderer.sharedMaterials = combinedMaterials.ToArray();
            }
            else if(combinedMesh.blendShapeCount > 0)
            {
                meshGO = new GameObject(name + "_Mesh");
                meshGO.transform.SetParent(prefabGO.transform);
                meshGO.transform.localPosition = Vector3.zero;
                meshGO.transform.localRotation = Quaternion.identity;
                meshGO.transform.localScale = Vector3.one;

                if (saveAsObj)
                {
                    // Save as obj not supported if blend shapes are extracted
                    Logger.LogWarning("Saving a .obj is not supported if blend shapes are exported.");
                }

                var skinnedMeshRenderer = meshGO.AddComponent<SkinnedMeshRenderer>();
                skinnedMeshRenderer.sharedMesh = combinedMesh;
                skinnedMeshRenderer.sharedMaterials = combinedMaterials.ToArray();
                skinnedMeshRenderer.sharedMesh.RecalculateBounds();
                skinnedMeshRenderer.localBounds = skinnedMeshRenderer.sharedMesh.bounds;

                // Copy from source
                if (source != null)
                {
                    var sourceRenderer = source.GetComponent<SkinnedMeshRenderer>();
                    skinnedMeshRenderer.rootBone = sourceRenderer.rootBone;

                    // Copy animator component if there was one
                    var animator = source.GetComponent<Animator>();
                    if(animator != null)
                    {
                        var newAnimator = meshGO.AddComponent<Animator>();
#if UNITY_EDITOR
                        EditorUtility.CopySerialized(animator, newAnimator);
#endif
                        /*
                        newAnimator.runtimeAnimatorController = animator.runtimeAnimatorController;
                        newAnimator.rootPosition = animator.rootPosition;
                        newAnimator.rootRotation = animator.rootRotation;
                        newAnimator.avatar = animator.avatar;
                        */
                    }
                }
            }
            else
            {
                var meshFilter = meshGO.AddComponent<MeshFilter>();
                if (saveAsObj)
                {
                    // Load the model and extract the mesh
                    string modelFileName = objFiles.Keys.FirstOrDefault(k => k.EndsWith(".obj"));
                    var modelPath = System.IO.Path.GetDirectoryName(path) + "/" + modelFileName;
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    if (model != null)
                    {
                        var modelMeshFilter = model.GetComponentInChildren<MeshFilter>(includeInactive: true);
                        if (modelMeshFilter != null)
                        {
                            meshFilter.sharedMesh = modelMeshFilter.sharedMesh;
                        }
                    }
                }
                else
                {
                    meshFilter.sharedMesh = combinedMesh;
                }
                var meshRenderer = meshGO.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterials = combinedMaterials.ToArray();
            }

            // Save the transform's GameObject as a prefab asset.
            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(prefabGO, prefabPath);
            GameObject.DestroyImmediate(prefabGO);
            ProjectWindowUtil.ShowCreatedAsset(prefabAsset);
        }

        private static void copyTransforms(Transform source, Transform targetParent, bool copyInitialSource, int depth = 0, int maxDepth = 50)
        {
            // Add new entry
            GameObject newEntry;
            if (copyInitialSource)
            {
                newEntry = new GameObject(source.gameObject.name);
                newEntry.transform.SetParent(targetParent);
                newEntry.transform.localPosition = source.transform.localPosition;
                newEntry.transform.localRotation = source.transform.localRotation;
                newEntry.transform.localScale = source.transform.localScale;
            }
            else
            {
                newEntry = targetParent.gameObject;
            }

            // Recurse into children
            if (source.childCount > 0)
            {
                // Depth to limit infinite recursions if data is handed in wrong.
                depth++;
                if (depth < maxDepth)
                {
                    for (int i = 0; i < source.childCount; i++)
                    {
                        copyTransforms(source.GetChild(i), newEntry.transform, true, depth, maxDepth);
                    }
                }
            }
        }


        private static string deleteFileAndGetPath(bool replaceOldMesh, string filePath)
        {
            // Create dirs if necessary
            string dirPath = System.IO.Path.GetDirectoryName(Application.dataPath + "/../" + filePath);
            if (!System.IO.Directory.Exists(dirPath))
            {
                System.IO.Directory.CreateDirectory(dirPath);
            }

            if (replaceOldMesh && System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                if (System.IO.File.Exists(filePath + ".meta"))
                {
                    System.IO.File.Delete(filePath + ".meta");
                }
                return filePath;
            }
            else
            {
                return AssetDatabase.GenerateUniqueAssetPath(filePath);
            }
        }

        static T getOrCreateMeshData<T>(Mesh mesh, Dictionary<Mesh, T> data) where T : new()
        {
            if (!data.ContainsKey(mesh))
            {
                data.Add(mesh, new T());
            }
            return data[mesh];
        }
    }
}
