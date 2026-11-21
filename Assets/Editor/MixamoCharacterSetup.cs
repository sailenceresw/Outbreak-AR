using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Mixamo characters carry their skin textures inside the FBX, but Unity doesn't wire them to the
// imported materials, so the zombies render untextured. This pulls the textures out, turns the
// materials into editable URP Lit materials and assigns the colour and normal maps by name.
public static class MixamoCharacterSetup
{
    const string Folder = "Assets/Resources/ZombieModels";

    [MenuItem("Tools/Outbreak/Set Up Mixamo Characters")]
    public static void Run()
    {
        if (!Directory.Exists(Folder)) return;

        foreach (string modelPath in Directory.GetFiles(Folder, "*.fbx"))
        {
            string path = modelPath.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(path);
            string textureDir = Folder + "/Textures/" + name;
            string materialDir = Folder + "/Materials/" + name;

            ExtractTextures(path, textureDir);
            Texture2D[] textures = LoadTextures(textureDir);
            ExtractMaterials(path, materialDir);

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { materialDir }))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                SetUp(material, textures);
            }
            Debug.Log("MixamoCharacterSetup: " + name + " has " + textures.Length + " textures");
        }
        AssetDatabase.SaveAssets();
    }

    static void ExtractTextures(string modelPath, string textureDir)
    {
        if (Directory.Exists(textureDir) && Directory.GetFiles(textureDir, "*.png").Length > 0) return;
        Directory.CreateDirectory(textureDir);
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.ExtractTextures(textureDir);
        AssetDatabase.Refresh();

        // normal maps must be imported as such or the lighting comes out wrong
        foreach (string file in Directory.GetFiles(textureDir, "*.png"))
        {
            if (!Path.GetFileName(file).ToLower().Contains("normal")) continue;
            TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
            if (textureImporter != null && textureImporter.textureType != TextureImporterType.NormalMap)
            {
                textureImporter.textureType = TextureImporterType.NormalMap;
                textureImporter.SaveAndReimport();
            }
        }
    }

    static Texture2D[] LoadTextures(string textureDir)
    {
        return AssetDatabase.FindAssets("t:Texture2D", new[] { textureDir })
            .Select(guid => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(texture => texture != null)
            .ToArray();
    }

    // embedded materials are read-only; extracting them makes editable .mat files the FBX then uses
    static void ExtractMaterials(string modelPath, string materialDir)
    {
        Directory.CreateDirectory(materialDir);
        bool extracted = false;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
        {
            if (!(asset is Material material)) continue;
            string target = materialDir + "/" + material.name + ".mat";
            if (File.Exists(target)) continue;
            string error = AssetDatabase.ExtractAsset(material, target);
            if (string.IsNullOrEmpty(error)) extracted = true;
            else Debug.LogWarning("MixamoCharacterSetup: " + error);
        }
        if (extracted)
        {
            AssetDatabase.WriteImportSettingsIfDirty(modelPath);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
        }
    }

    static void SetUp(Material material, Texture2D[] textures)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit != null) material.shader = lit;

        string materialName = material.name.ToLower();
        Texture2D diffuse = Pick(textures, materialName, "diffuse");
        Texture2D normal = Pick(textures, materialName, "normal");

        if (diffuse != null) material.SetTexture("_BaseMap", diffuse);
        material.SetColor("_BaseColor", Color.white);
        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
        }

        // Mixamo hair is drawn on flat cards whose shape lives in the diffuse texture's alpha. Rendered
        // opaque, the cards show as solid sheets, so a texture with alpha gets cut out and both faces drawn.
        // Skin and clothing without alpha stay solid and back-face culled.
        if (HasAlpha(diffuse)) UrpSurface.Cutout(material, 0.4f, doubleSided: true);
        else UrpSurface.Opaque(material);

        // damp skin
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.35f);
        EditorUtility.SetDirty(material);
    }

    // true when the source image has a real alpha channel and the importer keeps it
    static bool HasAlpha(Texture2D texture)
    {
        if (texture == null) return false;
        TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
        if (importer == null) return false;
        if (importer.alphaSource == TextureImporterAlphaSource.None) return false;
        return importer.DoesSourceTextureHaveAlpha();
    }

    // score each texture of the right kind: words it shares with the material count for it, words the
    // material lacks (e.g. "body" for the head material) count against it; ties go to the plainest name
    static Texture2D Pick(Texture2D[] textures, string materialName, string kind)
    {
        Texture2D[] ofKind = textures.Where(t => t.name.ToLower().Contains(kind)).ToArray();
        if (ofKind.Length == 0) return null;
        return ofKind
            .OrderByDescending(t => Score(t.name.ToLower(), materialName, kind))
            .ThenBy(t => t.name.Length)
            .First();
    }

    static int Score(string textureName, string materialName, string kind)
    {
        int score = 0;
        foreach (string word in textureName.Split('_', ' ', '-'))
        {
            if (word.Length <= 3 || word == kind) continue;
            score += materialName.Contains(word) ? 1 : -1;
        }
        return score;
    }
}
