using UnityEditor;

// Everything in Resources/Icons imports as a UI sprite, small enough to stay light on the phone.
public class IconImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Icons/")) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 256;
    }
}
