using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The NZ PT-9M pistol ships with HDRP materials (the body) and built-in Standard materials (decals, red
// dot, sight glass). Neither shader exists in URP, so the gun drew as a flat black shape with the decal
// quads showing as white squares. This moves every PT-9M material to URP Lit and carries over the pack's
// own albedo, normal and mask maps.
// Run from Tools > Outbreak > Upgrade Pistol Materials, or with -executeMethod. Safe to run repeatedly.
public static class PistolMaterialUpgrade
{
    const string MaterialFolder = "Assets/NZ PT-9M Pistol/Materials";

    // texture property names in the order to try them: HDRP first, then built-in Standard, then URP
    static readonly string[] AlbedoNames = { "_BaseColorMap", "_MainTex", "_BaseMap" };
    static readonly string[] NormalNames = { "_NormalMap", "_BumpMap" };
    static readonly string[] MaskNames = { "_MaskMap", "_MetallicGlossMap" };

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
            Texture mask = First(saved, MaskNames);

            if (!UrpSurface.UseLit(material)) return;
            SetUp(material, albedo, normal, mask);
            EditorUtility.SetDirty(material);
            upgraded++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("PistolMaterialUpgrade: upgraded " + upgraded + " materials");
    }

    static void SetUp(Material material, Texture albedo, Texture normal, Texture mask)
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

        if (mask != null)
        {
            // HDRP's mask map is R metallic, G occlusion, B detail mask, A smoothness. URP Lit reads
            // metallic from R and smoothness from A of _MetallicGlossMap, and occlusion from G, so the
            // same texture drives both slots
            Texture linearMask = ImportAsLinear(mask);
            material.SetTexture("_MetallicGlossMap", linearMask);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Smoothness", 1f);
            material.SetTexture("_OcclusionMap", linearMask);
            material.SetFloat("_OcclusionStrength", 1f);
            material.EnableKeyword("_OCCLUSIONMAP");
        }
        else
        {
            // no mask in the pack for this part: plain worn steel
            material.SetFloat("_Metallic", name.Contains("Decal") ? 0f : 0.75f);
            material.SetFloat("_Smoothness", 0.5f);
        }
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

    // mask data is not colour: read as sRGB it comes out too shiny and too metallic
    static Texture ImportAsLinear(Texture texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture>(path);
    }
}
