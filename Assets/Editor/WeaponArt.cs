using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Renders gun models to transparent PNGs: candidate previews, and the shop pictures in Resources/WeaponIcons.
// Batch: Unity.exe -batchmode -quit -projectPath . -executeMethod WeaponArt.RenderCandidates
public static class WeaponArt
{
    const int Layer = 31;
    const string ModelFolder = "Assets/Resources/Weapons";
    const string MaterialFolder = "Assets/Weapons/Materials";
    const string IconFolder = "Assets/Resources/WeaponIcons";
    const string PistolPrefab = "Assets/NZ PT-9M Pistol/Prefabs/PT-9M Pistol_skin01.prefab";

    // the pack's flat colours by material name, swapped for metal, polymer and wood
    struct Finish
    {
        public string name;
        public Color color;
        public float metallic;
        public float smoothness;
        public Finish(string name, string hex, float metallic, float smoothness)
        {
            this.name = name;
            ColorUtility.TryParseHtmlString("#" + hex, out color);
            this.metallic = metallic;
            this.smoothness = smoothness;
        }
    }

    static readonly (string source, Finish finish)[] Finishes =
    {
        ("Metal", new Finish("Gun_Gunmetal", "3A3C40", 0.9f, 0.58f)),
        ("DarkMetal", new Finish("Gun_DarkGunmetal", "232529", 0.85f, 0.5f)),
        ("LightMetal", new Finish("Gun_Steel", "7A7D82", 0.95f, 0.68f)),
        ("Black", new Finish("Gun_Polymer", "1C1D20", 0f, 0.32f)),
        ("Black2", new Finish("Gun_Polymer", "1C1D20", 0f, 0.32f)),
        ("Main", new Finish("Gun_Polymer", "1C1D20", 0f, 0.32f)),
        ("MainDark", new Finish("Gun_PolymerDark", "121315", 0f, 0.28f)),
        ("MainLight", new Finish("Gun_PolymerLight", "2C2E32", 0.2f, 0.38f)),
        ("Grey", new Finish("Gun_PolymerLight", "2C2E32", 0.2f, 0.38f)),
        ("Wood", new Finish("Gun_Wood", "6E4A30", 0f, 0.42f)),
        ("DarkWood", new Finish("Gun_DarkWood", "4A3022", 0f, 0.38f)),
    };

    // swap every gun model's materials for the metal, polymer and wood finishes; safe to run repeatedly
    [MenuItem("Tools/Outbreak/Set Up Weapon Materials")]
    public static void SetupMaterials()
    {
        Directory.CreateDirectory(MaterialFolder);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
            bool changed = false;
            foreach ((string source, Finish finish) in Finishes)
            {
                string materialPath = MaterialFolder + "/" + finish.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(lit);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.SetColor("_BaseColor", finish.color);
                material.SetFloat("_Metallic", finish.metallic);
                material.SetFloat("_Smoothness", finish.smoothness);
                EditorUtility.SetDirty(material);

                AssetImporter.SourceAssetIdentifier id = new AssetImporter.SourceAssetIdentifier(typeof(Material), source);
                Object existing;
                if (!importer.GetExternalObjectMap().TryGetValue(id, out existing) || existing != material)
                {
                    importer.AddRemap(id, material);
                    changed = true;
                }
            }
            if (changed) importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("WeaponArt: weapon materials set up");
    }

    // the shop's gun pictures, one per WeaponId, muzzle to the right
    [MenuItem("Tools/Outbreak/Render Weapon Icons")]
    public static void RenderIcons()
    {
        SetupMaterials();
        Directory.CreateDirectory(IconFolder);
        foreach (WeaponId id in System.Enum.GetValues(typeof(WeaponId)))
        {
            GameObject model = id == WeaponId.Pistol
                ? AssetDatabase.LoadAssetAtPath<GameObject>(PistolPrefab)
                : AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "/" + id + ".fbx");
            if (model == null)
            {
                Debug.LogWarning("WeaponArt: no model for " + id);
                continue;
            }
            Quaternion turn = id == WeaponId.Pistol ? PistolIconTurn : Quaternion.identity;
            string path = IconFolder + "/" + id + ".png";
            RenderSide(model, path, 512, 256, turn);
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        Debug.Log("WeaponArt: icons rendered to " + IconFolder);
    }

    // turns the PT-9M so it lies like the pack's guns (muzzle along -X, up +Z)
    static readonly Quaternion PistolIconTurn = Quaternion.identity;

    // each gun as it's held, seen through a phone-shaped camera, for checking poses without a device
    [MenuItem("Tools/Outbreak/Render First-Person Previews")]
    public static void RenderFirstPerson()
    {
        SetupMaterials();
        string outDir = System.Environment.GetEnvironmentVariable("WEAPON_PREVIEW_DIR");
        if (string.IsNullOrEmpty(outDir)) outDir = "Builds/WeaponPreviews";
        Directory.CreateDirectory(outDir);

        foreach (WeaponId id in System.Enum.GetValues(typeof(WeaponId)))
        {
            GameObject cameraObject = new GameObject("FPCamera");
            cameraObject.transform.position = new Vector3(0f, -500f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.cullingMask = 1 << Layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.42f, 0.38f);
            camera.nearClipPlane = 0.01f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            // the scene's gun: the PT-9M at the pose it has in Level1
            GameObject gun = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PistolPrefab));
            gun.transform.SetParent(cameraObject.transform, false);
            gun.transform.localPosition = new Vector3(0.08f, -0.1f, 0.48f);
            gun.transform.localRotation = new Quaternion(0f, -0.036294863f, 0f, 0.99934113f);

            GameObject held = WeaponModels.Create(id, gun.transform, out Vector3 muzzle);
            if (held != null)
            {
                foreach (Renderer r in gun.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.transform.IsChildOf(held.transform)) r.enabled = false;
                }
            }
            SetLayer(cameraObject, Layer);

            // a red dot where the muzzle flash will appear
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.transform.SetParent(gun.transform, false);
            dot.transform.localPosition = muzzle;
            dot.transform.localScale = Vector3.one * 0.015f;
            dot.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.red };
            dot.layer = Layer;

            GameObject keyObject = new GameObject("Key");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.3f;
            key.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderTexture target = new RenderTexture(540, 1200, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.aspect = 540f / 1200f;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            Texture2D picture = new Texture2D(540, 1200, TextureFormat.RGBA32, false);
            picture.ReadPixels(new Rect(0, 0, 540, 1200), 0, 0);
            picture.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(outDir, "fp_" + id + ".png"), picture.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(picture);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(keyObject);
            Object.DestroyImmediate(cameraObject);
        }
        Debug.Log("WeaponArt: first-person previews rendered to " + outDir);
    }

    // batch entry: materials, icons and first-person previews in one go
    public static void RenderAll()
    {
        RenderIcons();
        RenderFirstPerson();
    }

    [MenuItem("Tools/Outbreak/Render Weapon Candidates")]
    public static void RenderCandidates()
    {
        string outDir = System.Environment.GetEnvironmentVariable("WEAPON_PREVIEW_DIR");
        if (string.IsNullOrEmpty(outDir)) outDir = "Builds/WeaponPreviews";
        Directory.CreateDirectory(outDir);

        StringBuilder report = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            string name = Path.GetFileNameWithoutExtension(path);
            Bounds bounds = RenderSide(model, Path.Combine(outDir, name + ".png"), 512, 256, Quaternion.identity);

            report.Append(name).Append("  size=").Append(bounds.size.ToString("F3")).Append("  center=").Append(bounds.center.ToString("F3")).Append("  materials:");
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    report.Append(' ').Append(m.name).Append('=').Append(ColorUtility.ToHtmlStringRGB(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color));
                }
            }
            report.AppendLine();
        }
        File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString());
        Debug.Log("WeaponArt: candidates rendered to " + outDir);
    }

    // renders the model seen from its thinnest side (a gun's flank); returns the model's bounds
    public static Bounds RenderSide(GameObject model, string pngPath, int width, int height, Quaternion extraRotation)
    {
        GameObject instance = Object.Instantiate(model);
        instance.transform.position = new Vector3(0f, -500f, 0f);
        instance.transform.rotation = extraRotation;
        SetLayer(instance, Layer);

        Bounds bounds = WorldBounds(instance);
        Vector3 size = bounds.size;

        // look along the thinnest axis; the longest runs across the picture
        Vector3 forward = size.x <= size.y && size.x <= size.z ? Vector3.right
            : size.y <= size.z ? Vector3.up : Vector3.forward;
        Vector3 up = forward == Vector3.up ? Vector3.forward : Vector3.up;

        GameObject cameraObject = new GameObject("WeaponArtCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.cullingMask = 1 << Layer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
        camera.aspect = width / (float)height;
        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;

        float extent = Mathf.Max(size.x, size.y, size.z);
        camera.transform.position = bounds.center - forward * (extent + 1f);
        camera.transform.rotation = Quaternion.LookRotation(forward, up);
        // fit the long side across the width, with a margin
        float halfWidth = extent * 0.55f;
        camera.orthographicSize = Mathf.Max(halfWidth / camera.aspect, Vector3.Dot(size, Abs(up)) * 0.6f);

        GameObject keyObject = new GameObject("WeaponArtKey");
        Light key = keyObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.6f;
        key.transform.rotation = Quaternion.LookRotation(forward + Vector3.down * 0.8f + Vector3.right * 0.3f);
        GameObject fillObject = new GameObject("WeaponArtFill");
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.6f;
        fill.transform.rotation = Quaternion.LookRotation(-forward + Vector3.down * 0.3f);

        Texture2D picture = Supersample(camera, width, height);
        File.WriteAllBytes(pngPath, picture.EncodeToPNG());

        Object.DestroyImmediate(picture);
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(keyObject);
        Object.DestroyImmediate(fillObject);
        Object.DestroyImmediate(instance);
        return bounds;
    }

    // renders at twice the size and averages down: smooth edges without the sparkles MSAA leaves on speculars
    public static Texture2D Supersample(Camera camera, int width, int height)
    {
        camera.aspect = width / (float)height;
        RenderTexture large = new RenderTexture(width * 2, height * 2, 24, RenderTextureFormat.ARGB32);
        RenderTexture small = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        large.filterMode = FilterMode.Bilinear;
        camera.targetTexture = large;
        camera.Render();
        camera.targetTexture = null;
        Graphics.Blit(large, small);

        RenderTexture.active = small;
        Texture2D picture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        picture.Apply();
        RenderTexture.active = null;
        Object.DestroyImmediate(large);
        Object.DestroyImmediate(small);
        return picture;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    static Bounds WorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.one * 0.1f);
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static void SetLayer(GameObject root, int layer)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }
}
