using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMeshSimplifier;

namespace Deadline4Sec.Editor
{
    // Converts raw Tripo GLB downloads (kept outside Assets in /TripoRaw) into
    // mobile-ready assets: a decimated mesh, 1024px textures, a URP material and
    // a prefab. The GLB is parsed directly so only the first mesh/material of
    // Tripo's single-node output is needed.
    public static class TripoModelConverter
    {
        private const string RawFolder = "TripoRaw";
        private const int DefaultTargetTriangles = 12000;

        [MenuItem("Deadline 4 Sec/Art/Convert Tripo GLBs In TripoRaw")]
        public static void ConvertAll() => ConvertAll(false);

        [MenuItem("Deadline 4 Sec/Art/Reconvert All Tripo GLBs (overwrite)")]
        public static void ReconvertAll() => ConvertAll(true);

        private static void ConvertAll(bool force)
        {
            if (!Directory.Exists(RawFolder))
                return;
            foreach (string glb in Directory.GetFiles(RawFolder, "*.glb"))
            {
                string name = Path.GetFileNameWithoutExtension(glb);
                string folder = name.StartsWith("Enemy_") ? "Assets/Art/3DModel/Enemies"
                    : name.StartsWith("Obstacle_") ? "Assets/Art/3DModel/Obstacles"
                    : "Assets/Art/3DModel/Props";
                string prefab = folder + "/" + name + ".prefab";
                if (File.Exists(prefab) && !force)
                    continue;
                // Obstacles appear many at once and read as simple shapes; spend fewer triangles.
                Convert(glb, folder, name.StartsWith("Obstacle_") ? 6000 : DefaultTargetTriangles);
            }
        }

        public static GameObject Convert(string glbPath, string outputFolder, int targetTriangles)
        {
            string name = Path.GetFileNameWithoutExtension(glbPath);
            Directory.CreateDirectory(outputFolder);
            byte[] data = File.ReadAllBytes(glbPath);
            if (data.Length < 20 || Encoding.ASCII.GetString(data, 0, 4) != "glTF")
                throw new InvalidDataException(glbPath + " is not a GLB file.");

            int jsonLength = BitConverter.ToInt32(data, 12);
            string json = Encoding.UTF8.GetString(data, 20, jsonLength);
            int binStart = 20 + jsonLength + 8;
            Gltf gltf = JsonUtility.FromJson<Gltf>(json);
            Primitive prim = gltf.meshes[0].primitives[0];

            EditorUtility.DisplayProgressBar("Tripo convert", name + ": reading", 0.1f);
            try
            {
                Vector3[] positions = ReadVec3(gltf, data, binStart, prim.attributes.POSITION);
                Vector3[] normals = prim.attributes.NORMAL >= 0 ? ReadVec3(gltf, data, binStart, prim.attributes.NORMAL) : null;
                Vector2[] uvs = prim.attributes.TEXCOORD_0 >= 0 ? ReadVec2(gltf, data, binStart, prim.attributes.TEXCOORD_0) : null;
                int[] indices = ReadIndices(gltf, data, binStart, prim.indices);

                // glTF is right-handed: mirror X, flip winding and V.
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i].x = -positions[i].x;
                    if (normals != null) normals[i].x = -normals[i].x;
                    if (uvs != null) uvs[i].y = 1f - uvs[i].y;
                }
                for (int i = 0; i < indices.Length; i += 3)
                    (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);

                // Decimating a texture atlas with hundreds of UV islands smears it,
                // so bake the base color into vertex colors first and decimate those.
                MaterialInfo mat = gltf.materials != null && gltf.materials.Length > 0 ? gltf.materials[0] : null;
                Color[] colors = null;
                if (mat != null && uvs != null)
                    colors = BakeVertexColors(gltf, data, binStart, mat.pbrMetallicRoughness.baseColorTexture.index, uvs);

                Mesh source = new Mesh { indexFormat = IndexFormat.UInt32, name = name + "_Source" };
                source.vertices = positions;
                if (normals != null) source.normals = normals;
                if (uvs != null) source.uv = uvs;
                if (colors != null) source.colors = colors;
                source.triangles = indices;
                source.RecalculateBounds();

                EditorUtility.DisplayProgressBar("Tripo convert", name + ": simplifying " + indices.Length / 3 + " tris", 0.35f);
                MeshSimplifier simplifier = new MeshSimplifier
                {
                    SimplificationOptions = new SimplificationOptions
                    {
                        PreserveBorderEdges = false,
                        // Tripo atlases have hundreds of tiny UV islands; preserving every
                        // seam stalls decimation far above a mobile budget.
                        PreserveUVSeamEdges = false,
                        PreserveUVFoldoverEdges = false,
                        PreserveSurfaceCurvature = false,
                        EnableSmartLink = true,
                        VertexLinkDistance = 0.0001,
                        MaxIterationCount = 400,
                        Agressiveness = 7.0,
                        ManualUVComponentCount = false,
                        UVComponentCount = 2,
                    }
                };
                simplifier.Initialize(source);
                float quality = Mathf.Clamp01(targetTriangles / (indices.Length / 3f));
                simplifier.SimplifyMesh(quality);
                Mesh mesh = simplifier.ToMesh();
                // A second pass catches what the iteration cap left behind.
                if (mesh.triangles.Length / 3 > targetTriangles * 1.3f)
                {
                    MeshSimplifier second = new MeshSimplifier { SimplificationOptions = simplifier.SimplificationOptions };
                    second.Initialize(mesh);
                    second.SimplifyMesh(Mathf.Clamp01(targetTriangles / (mesh.triangles.Length / 3f)));
                    UnityEngine.Object.DestroyImmediate(mesh);
                    mesh = second.ToMesh();
                }
                mesh.name = name;
                mesh.indexFormat = mesh.vertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                if (normals == null)
                    mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                UnityEngine.Object.DestroyImmediate(source);
                MeshUtility.Optimize(mesh);
                string meshPath = outputFolder + "/" + name + "_Mesh.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);

                Material material = new Material(Shader.Find("Deadline4Sec/VertexColorLit")) { name = name };
                AssetDatabase.DeleteAsset(outputFolder + "/" + name + ".mat");
                AssetDatabase.CreateAsset(material, outputFolder + "/" + name + ".mat");

                GameObject go = new GameObject(name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                string prefabPath = outputFolder + "/" + name + ".prefab";
                AssetDatabase.DeleteAsset(prefabPath);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
                Debug.Log($"Tripo convert {name}: {indices.Length / 3} -> {mesh.triangles.Length / 3} tris, {mesh.vertexCount} verts");
                return prefab;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static Color[] BakeVertexColors(Gltf gltf, byte[] data, int binStart, int textureIndex, Vector2[] uvs)
        {
            if (textureIndex < 0 || gltf.textures == null || textureIndex >= gltf.textures.Length)
                return null;
            ImageInfo image = gltf.images[gltf.textures[textureIndex].source];
            BufferView view = gltf.bufferViews[image.bufferView];
            byte[] bytes = new byte[view.byteLength];
            Buffer.BlockCopy(data, binStart + view.byteOffset, bytes, 0, view.byteLength);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(bytes);
            Color[] colors = new Color[uvs.Length];
            for (int i = 0; i < uvs.Length; i++)
                // Vertex colors bypass sRGB conversion, so store them in linear space.
                colors[i] = texture.GetPixelBilinear(uvs[i].x, uvs[i].y).linear;
            UnityEngine.Object.DestroyImmediate(texture);
            return colors;
        }

        private static Texture2D SaveTexture(Gltf gltf, byte[] data, int binStart, int textureIndex,
            string folder, string fileName, bool normal)
        {
            if (textureIndex < 0 || gltf.textures == null || textureIndex >= gltf.textures.Length)
                return null;
            ImageInfo image = gltf.images[gltf.textures[textureIndex].source];
            BufferView view = gltf.bufferViews[image.bufferView];
            string ext = image.mimeType == "image/png" ? ".png" : ".jpg";
            string path = folder + "/" + fileName + ext;
            byte[] bytes = new byte[view.byteLength];
            Buffer.BlockCopy(data, binStart + view.byteOffset, bytes, 0, view.byteLength);
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            if (normal)
                importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Vector3[] ReadVec3(Gltf g, byte[] d, int bin, int accessorIndex)
        {
            Accessor a = g.accessors[accessorIndex];
            BufferView v = g.bufferViews[a.bufferView];
            int stride = v.byteStride > 0 ? v.byteStride : 12;
            int start = bin + v.byteOffset + a.byteOffset;
            Vector3[] result = new Vector3[a.count];
            for (int i = 0; i < a.count; i++)
            {
                int o = start + i * stride;
                result[i] = new Vector3(BitConverter.ToSingle(d, o), BitConverter.ToSingle(d, o + 4), BitConverter.ToSingle(d, o + 8));
            }
            return result;
        }

        private static Vector2[] ReadVec2(Gltf g, byte[] d, int bin, int accessorIndex)
        {
            Accessor a = g.accessors[accessorIndex];
            BufferView v = g.bufferViews[a.bufferView];
            int stride = v.byteStride > 0 ? v.byteStride : 8;
            int start = bin + v.byteOffset + a.byteOffset;
            Vector2[] result = new Vector2[a.count];
            for (int i = 0; i < a.count; i++)
            {
                int o = start + i * stride;
                result[i] = new Vector2(BitConverter.ToSingle(d, o), BitConverter.ToSingle(d, o + 4));
            }
            return result;
        }

        private static int[] ReadIndices(Gltf g, byte[] d, int bin, int accessorIndex)
        {
            Accessor a = g.accessors[accessorIndex];
            BufferView v = g.bufferViews[a.bufferView];
            int start = bin + v.byteOffset + a.byteOffset;
            int[] result = new int[a.count];
            for (int i = 0; i < a.count; i++)
            {
                switch (a.componentType)
                {
                    case 5125: result[i] = (int)BitConverter.ToUInt32(d, start + i * 4); break;
                    case 5123: result[i] = BitConverter.ToUInt16(d, start + i * 2); break;
                    default: result[i] = d[start + i]; break;
                }
            }
            return result;
        }

        // Minimal glTF JSON shapes for JsonUtility. Missing ints default to -1 via initializers.
        [Serializable] private class Gltf
        {
            public Accessor[] accessors;
            public BufferView[] bufferViews;
            public MeshInfo[] meshes;
            public MaterialInfo[] materials;
            public TextureInfo[] textures;
            public ImageInfo[] images;
        }
        [Serializable] private class Accessor { public int bufferView; public int byteOffset; public int componentType; public int count; }
        [Serializable] private class BufferView { public int byteOffset; public int byteLength; public int byteStride; }
        [Serializable] private class MeshInfo { public Primitive[] primitives; }
        [Serializable] private class Primitive { public Attributes attributes = new Attributes(); public int indices = -1; }
        [Serializable] private class Attributes { public int POSITION = -1; public int NORMAL = -1; public int TEXCOORD_0 = -1; }
        [Serializable] private class MaterialInfo { public Pbr pbrMetallicRoughness = new Pbr(); public TexRef normalTexture = new TexRef(); }
        [Serializable] private class Pbr { public TexRef baseColorTexture = new TexRef(); }
        [Serializable] private class TexRef { public int index = -1; }
        [Serializable] private class TextureInfo { public int source; }
        [Serializable] private class ImageInfo { public int bufferView; public string mimeType; }
    }
}
