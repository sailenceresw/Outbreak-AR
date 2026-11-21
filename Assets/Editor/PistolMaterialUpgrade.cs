using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// The NZ PT-9M pistol drew as a flat black shape with white squares, for two reasons:
//  - The prefab's parts pointed at ten auto-generated "PT-9M Pistol_Decal_albedo N" materials, so the whole
//    gun was painted with the near-black decal texture, under a mix of shaders URP doesn't have.
//  - The pack's real materials target HDRP (the body) and built-in Standard (decals, red dot, sight glass).
// This moves the pack's real materials to URP Lit with its albedo and normal maps plus a metallic-smoothness
// map it bakes from the pack's Metalness and Gloss maps, then points every part of each pistol prefab at
// the right one.
// Run from Tools > Outbreak > Upgrade Pistol Materials, or with -executeMethod. Safe to run repeatedly.
public static class PistolMaterialUpgrade
{
    const string MaterialFolder = "Assets/NZ PT-9M Pistol/Materials";
    const string PrefabFolder = "Assets/NZ PT-9M Pistol/Prefabs";
    // the pack's maps are 4096 px; half that is plenty for a gun held at the bottom of a phone screen
    const int BakedMaxSize = 2048;

    // each pistol prefab and the body material for its finish
    static readonly (string prefab, string body)[] Pistols =
    {
        ("PT-9M Pistol_skin01.prefab", "PT-9M Pistol_Skin01_Body_mat"),
        ("PT-9M Pistol_skin02.prefab", "PT-9M Pistol_Skin02_Body_mat"),
    };

    // texture property names in the order to try them: HDRP first, then built-in Standard, then URP
    static readonly string[] AlbedoNames = { "_BaseColorMap", "_MainTex", "_BaseMap" };
    static readonly string[] NormalNames = { "_NormalMap", "_BumpMap" };

    [MenuItem("Tools/Outbreak/Upgrade Pistol Materials")]
    public static void Upgrade()
    {
        int upgraded = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.name.StartsWith("BackgroundSprite")) continue;

            // read the maps from the saved properties before the shader changes; the old shaders are
            // missing in URP, so the material API can't see their properties
            Dictionary<string, Texture> saved = SavedTextures(material);
            Texture albedo = First(saved, AlbedoNames);
            Texture normal = First(saved, NormalNames);
            Texture metallicSmoothness = BakedMetallicSmoothness(albedo);

            if (!UrpSurface.UseLit(material)) return;
            SetUp(material, albedo, normal, metallicSmoothness);
            EditorUtility.SetDirty(material);
            upgraded++;
        }

        AssetDatabase.SaveAssets();
        int rewired = RewirePrefabs();
        Debug.Log("PistolMaterialUpgrade: upgraded " + upgraded + " materials, re-assigned " + rewired + " pistol parts");
    }

    // point every part of each pistol prefab at the pack's real material for that part
    static int RewirePrefabs()
    {
        Material decal = LoadMaterial("PT-9M Pistol_Decal_mat");
        Material dot = LoadMaterial("PT-9M Pistol_RedDot_mat");
        Material glass = LoadMaterial("PT-9M Pistol_RedDotGlass_mat");
        int rewired = 0;

        foreach ((string prefab, string bodyName) in Pistols)
        {
            string path = PrefabFolder + "/" + prefab;
            Material body = LoadMaterial(bodyName);
            if (body == null || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = 0;
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material wanted = MaterialForPart(renderer.name, body, decal, dot, glass);
                    if (wanted == null) continue;

                    Material[] materials = renderer.sharedMaterials;
                    bool differs = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == wanted) continue;
                        materials[i] = wanted;
                        differs = true;
                    }
                    if (!differs) continue;
                    renderer.sharedMaterials = materials;
                    changed++;
                }
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                rewired += changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return rewired;
    }

    // parts are named in the model: "Decal.044", "RedDot" (the dot), "RedDotGlass.001", and the rest are
    // metal and polymer, including "RedDot.001", which is the sight's housing
    static Material MaterialForPart(string part, Material body, Material decal, Material dot, Material glass)
    {
        if (part.StartsWith("Decal")) return decal;
        if (part.StartsWith("RedDotGlass")) return glass;
        if (part == "RedDot") return dot;
        return body;
    }

    static Material LoadMaterial(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat");
    }

    static void SetUp(Material material, Texture albedo, Texture normal, Texture metallicSmoothness)
    {
        string name = material.name;

        if (albedo != null) material.SetTexture("_BaseMap", albedo);
        material.SetColor("_BaseColor", Color.white);

        if (normal != null)
        {
            material.SetTexture("_BumpMap", ImportAsNormalMap(normal));
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
        }

        if (name.Contains("RedDotGlass"))
        {
            // faintly tinted, glossy lens in front of the dot
            UrpSurface.Transparent(material);
            material.SetColor("_BaseColor", new Color(0.55f, 0.15f, 0.1f, 0.18f));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.95f);
            return;
        }

        if (name.Contains("RedDot"))
        {
            // bright enough for bloom to catch it, like a real reflex sight
            UrpSurface.Opaque(material);
            material.SetColor("_BaseColor", new Color(1f, 0.12f, 0.08f));
            material.SetColor("_EmissionColor", new Color(1f, 0.08f, 0.04f) * 4f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.6f);
            return;
        }

        if (name.Contains("Decal"))
        {
            // stencilled markings: keep the lettering, cut away the rest of the quad
            UrpSurface.Cutout(material, 0.5f, doubleSided: false);
        }
        else
        {
            UrpSurface.Opaque(material);
        }

        if (metallicSmoothness != null)
        {
            // R metallic, G occlusion, A smoothness: URP Lit's own layout, so one texture fills both slots
            material.SetTexture("_MetallicGlossMap", metallicSmoothness);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Smoothness", 1f);
            material.SetTexture("_OcclusionMap", metallicSmoothness);
            material.SetFloat("_OcclusionStrength", 1f);
            material.EnableKeyword("_OCCLUSIONMAP");
        }
        else
        {
            // no maps in the pack for this part: plain worn steel
            material.SetFloat("_Metallic", name.Contains("Decal") ? 0f : 0.75f);
            material.SetFloat("_Smoothness", 0.5f);
        }
    }

    // The pack's HDRP mask map keeps gloss in B and leaves A at full white, so URP (which reads smoothness
    // from A) would turn the gun into a mirror. This bakes "<skin>_MetallicSmoothness.png" next to the
    // albedo from the pack's own Metalness (R), mask occlusion (G) and Gloss (A) maps, in URP's layout.
    static Texture BakedMetallicSmoothness(Texture albedo)
    {
        if (albedo == null) return null;
        string albedoPath = AssetDatabase.GetAssetPath(albedo);
        if (!albedoPath.Contains("_Albedo")) return null;

        string target = albedoPath.Replace("_Albedo", "_MetallicSmoothness");
        if (!File.Exists(target) && !Bake(albedoPath, target)) return null;
        return AssetDatabase.LoadAssetAtPath<Texture>(target);
    }

    static bool Bake(string albedoPath, string target)
    {
        string metalPath = albedoPath.Replace("_Albedo", "_Metalness");
        string glossPath = albedoPath.Replace("_Albedo", "_Gloss");
        string maskPath = albedoPath.Replace("_Albedo", "_Mask");
        if (!File.Exists(metalPath) || !File.Exists(glossPath))
        {
            Debug.LogWarning("PistolMaterialUpgrade: no Metalness/Gloss maps next to " + albedoPath);
            return false;
        }

        // read the PNG files directly: the imported textures aren't CPU-readable
        Texture2D metal = ReadImage(metalPath);
        Texture2D gloss = ReadImage(glossPath);
        Texture2D mask = File.Exists(maskPath) ? ReadImage(maskPath) : null;
        try
        {
            int width = metal.width, height = metal.height;
            if (gloss.width != width || gloss.height != height || (mask != null && (mask.width != width || mask.height != height)))
            {
                Debug.LogWarning("PistolMaterialUpgrade: map sizes differ for " + albedoPath);
                return false;
            }

            Color32[] metalPixels = metal.GetPixels32();
            Color32[] glossPixels = gloss.GetPixels32();
            Color32[] maskPixels = mask != null ? mask.GetPixels32() : null;

            // box-filter down to the target size; the maps are square powers of two
            int step = 1;
            while (width / step > BakedMaxSize) step *= 2;
            int outWidth = width / step, outHeight = height / step, samples = step * step;
            Color32[] baked = new Color32[outWidth * outHeight];

            for (int y = 0; y < outHeight; y++)
            {
                for (int x = 0; x < outWidth; x++)
                {
                    int metallic = 0, occlusion = 0, smoothness = 0;
                    for (int sy = 0; sy < step; sy++)
                    {
                        int row = (y * step + sy) * width + x * step;
                        for (int sx = 0; sx < step; sx++)
                        {
                            metallic += metalPixels[row + sx].r;
                            occlusion += maskPixels != null ? maskPixels[row + sx].g : 255;
                            smoothness += glossPixels[row + sx].r;
                        }
                    }
                    baked[y * outWidth + x] = new Color32(
                        (byte)(metallic / samples), (byte)(occlusion / samples), 0, (byte)(smoothness / samples));
                }
            }

            Texture2D output = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false, true);
            output.SetPixels32(baked);
            File.WriteAllBytes(target, output.EncodeToPNG());
            Object.DestroyImmediate(output);
        }
        finally
        {
            Object.DestroyImmediate(metal);
            Object.DestroyImmediate(gloss);
            if (mask != null) Object.DestroyImmediate(mask);
        }

        // data, not colour: imported as linear, or metal and gloss come out wrong
        AssetDatabase.ImportAsset(target);
        if (AssetImporter.GetAtPath(target) is TextureImporter importer)
        {
            importer.sRGBTexture = false;
            importer.maxTextureSize = BakedMaxSize;
            importer.SaveAndReimport();
        }
        return true;
    }

    static Texture2D ReadImage(string path)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        texture.LoadImage(File.ReadAllBytes(path));
        return texture;
    }

    // every texture the material has saved, by property name, whatever shader it was made for
    static Dictionary<string, Texture> SavedTextures(Material material)
    {
        Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
        SerializedObject serialized = new SerializedObject(material);
        SerializedProperty entries = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
        if (entries == null) return textures;

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            string property = entry.FindPropertyRelative("first").stringValue;
            Texture texture = entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            if (texture != null) textures[property] = texture;
        }
        return textures;
    }

    static Texture First(Dictionary<string, Texture> textures, string[] names)
    {
        foreach (string name in names)
        {
            if (textures.TryGetValue(name, out Texture texture)) return texture;
        }
        return null;
    }

    // normal maps must be imported as such or the lighting comes out wrong
    static Texture ImportAsNormalMap(Texture texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture>(path);
    }
}
