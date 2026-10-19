using UnityEditor;
using UnityEngine;

// Import rules for everything downloaded from Mixamo: animations in Assets/Mixamo and the zombie
// characters in Resources/ZombieModels. Humanoid rigs so any clip plays on any model, looping where
// the motion repeats, root motion baked in place because the game moves zombies itself, and solid
// materials (Mixamo's often import as see-through).
public class MixamoImporter : AssetPostprocessor
{
    const string AnimationFolder = "Assets/Mixamo/";
    const string CharacterFolder = "Assets/Resources/ZombieModels/";

    bool IsMixamo => assetPath.StartsWith(AnimationFolder) || assetPath.StartsWith(CharacterFolder);

    void OnPreprocessModel()
    {
        if (!IsMixamo) return;
        ModelImporter importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = assetPath.StartsWith(AnimationFolder);
        // keep the textures packed inside the FBX available to its materials
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(AnimationFolder)) return;
        ModelImporter importer = (ModelImporter)assetImporter;
        string name = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLower();
        bool loop = name.Contains("walk") || name.Contains("run") || name.Contains("idle")
            || name.Contains("crawl") || name.Contains("attack") || name.Contains("biting");

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = loop;
            clip.loopPose = loop;
            // in place: no drifting forward or turning; keep the animation's own hip height (crawls stay low)
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clips;
    }

    void OnPostprocessMaterial(Material material)
    {
        if (!assetPath.StartsWith(CharacterFolder)) return;

        // URP Lit, fully opaque: Mixamo exports a transparency value that turns skin into glass
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit != null && material.shader != lit) material.shader = lit;
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_AlphaClip", 0f);
        material.SetFloat("_SrcBlend", 1f);
        material.SetFloat("_DstBlend", 0f);
        material.SetFloat("_ZWrite", 1f);
        material.SetOverrideTag("RenderType", "Opaque");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = -1;
        if (material.HasProperty("_BaseColor"))
        {
            Color color = material.GetColor("_BaseColor");
            color.a = 1f;
            material.SetColor("_BaseColor", color);
        }
        // damp, slightly glossy skin
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
    }
}
