using UnityEditor;
using UnityEngine;

// Moves the zombie materials from Simple Lit to the full URP Lit shader and wires up the
// normal, occlusion, metallic and emission maps that ship with the zombie pack.
// Run from Tools > Outbreak > Upgrade Zombie Materials, or with -executeMethod.
public static class ZombieMaterialUpgrade
{
    const string MaterialFolder = "Assets/Zombie/Materials";
    const string TextureFolder = "Assets/Zombie/Textures/";

    [MenuItem("Tools/Outbreak/Upgrade Zombie Materials")]
    public static void Upgrade()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("ZombieMaterialUpgrade: URP Lit shader not found");
            return;
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "Zombie.tga");
        Texture2D normal = LoadNormalMap(TextureFolder + "Zombie_nm.png");
        Texture2D occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "Zombie_ao.png");
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "Zombie_metallic.tga");
        Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "zombie_emission.tga");

        int upgraded = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!System.IO.Path.GetFileName(path).StartsWith("Zombie")) continue;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            material.shader = lit;

            // the runner/brute material had no texture at all, only a flat green tint
            if (albedo != null) material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1.2f);
                material.EnableKeyword("_NORMALMAP");
            }
            if (occlusion != null)
            {
                material.SetTexture("_OcclusionMap", occlusion);
                material.SetFloat("_OcclusionStrength", 1f);
                material.EnableKeyword("_OCCLUSIONMAP");
            }
            if (metallic != null)
            {
                material.SetTexture("_MetallicGlossMap", metallic);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            material.SetFloat("_Metallic", 0f);
            // slick, sweaty skin catches the light without looking like plastic
            material.SetFloat("_Smoothness", 0.45f);
            material.SetFloat("_GlossMapScale", 0.6f);

            if (emission != null)
            {
                // the pack's emission map lights the eyes; keep it dim so bloom only catches the glow
                material.SetTexture("_EmissionMap", emission);
                material.SetColor("_EmissionColor", new Color(0.55f, 0.5f, 0.35f));
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(material);
            upgraded++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("ZombieMaterialUpgrade: upgraded " + upgraded + " materials");
    }

    // make sure the normal map is imported as one, or it shades wrongly
    static Texture2D LoadNormalMap(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
