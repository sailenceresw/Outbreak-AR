using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Renders the Zombie-dex portraits from the realistic zombie models, posed with their Mixamo animations
// and tinted the way ZombieController tints each kind. Output: Resources/DexPortraits/<Type>[_Mutant].png
// Batch: Unity.exe -batchmode -quit -projectPath . -executeMethod ZombieArt.RenderPortraits
public static class ZombieArt
{
    const int Layer = 31;
    const string OutputFolder = "Assets/Resources/DexPortraits";
    const int Width = 512;
    const int Height = 640;

    struct Portrait
    {
        public ZombieType type;
        public string model;
        public string state;
        public float time;
        public Color tint;
        public Color rim;
        // turn away from facing the camera, in degrees
        public float turn;
    }

    static Portrait[] Portraits => new[]
    {
        new Portrait { type = ZombieType.Walker, model = "warzombie", state = "Idle", time = 0.4f, tint = Color.white, rim = new Color(0.9f, 0.25f, 0.2f), turn = 20f },
        new Portrait { type = ZombieType.Runner, model = "girl", state = "Z_Run_InPlace", time = 0.3f, tint = Realistic(new Color(0.82f, 0.86f, 0.8f)), rim = new Color(0.6f, 0.75f, 1f), turn = 30f },
        new Portrait { type = ZombieType.Crawler, model = "warzombie", state = "Crawl", time = 0.4f, tint = Realistic(new Color(0.78f, 0.74f, 0.72f)), rim = new Color(0.9f, 0.6f, 0.3f), turn = 35f },
        new Portrait { type = ZombieType.Spitter, model = "girl", state = "Scream", time = 0.55f, tint = Realistic(new Color(0.62f, 0.95f, 0.5f)), rim = new Color(0.5f, 1f, 0.3f), turn = 20f },
        new Portrait { type = ZombieType.Brute, model = "warzombie", state = "Attack", time = 0.3f, tint = Realistic(new Color(0.72f, 0.62f, 0.58f)), rim = new Color(1f, 0.55f, 0.2f), turn = -20f },
        new Portrait { type = ZombieType.Boss, model = "warzombie", state = "Scream", time = 0.6f, tint = Realistic(new Color(0.6f, 0.8f, 0.55f)), rim = new Color(1f, 0.15f, 0.1f), turn = 15f },
    };

    // the realistic models only take a hint of each kind's colour (as in ZombieController.Configure)
    static Color Realistic(Color tint) => Color.Lerp(Color.white, tint, 0.35f);

    [MenuItem("Tools/Outbreak/Render Dex Portraits")]
    public static void RenderPortraits()
    {
        Directory.CreateDirectory(OutputFolder);
        FullTextures();
        GameObject[] models = Resources.LoadAll<GameObject>("ZombieModels");
        RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>("ZombieMixamo");

        foreach (Portrait portrait in Portraits)
        {
            GameObject model = System.Array.Find(models, m => m.name.ToLower().Contains(portrait.model));
            if (model == null)
            {
                Debug.LogWarning("ZombieArt: no model for " + portrait.type);
                continue;
            }
            Render(model, controller, portrait, false, OutputFolder + "/" + portrait.type + ".png");
            Render(model, controller, portrait, true, OutputFolder + "/" + portrait.type + "_Mutant.png");
        }

        foreach (string path in Directory.GetFiles(OutputFolder, "*.png"))
        {
            string assetPath = path.Replace('\\', '/');
            AssetDatabase.ImportAsset(assetPath);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        Debug.Log("ZombieArt: portraits rendered to " + OutputFolder);
    }

    static void Render(GameObject model, RuntimeAnimatorController controller, Portrait portrait, bool mutant, string pngPath)
    {
        GameObject zombie = Object.Instantiate(model);
        zombie.transform.position = new Vector3(0f, -500f, 0f);
        zombie.transform.rotation = Quaternion.Euler(0f, 180f + portrait.turn, 0f);
        foreach (Transform t in zombie.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;

        // pose it with the state's Mixamo clip, sampled the way the Animation window does in edit mode
        Animator animator = zombie.GetComponentInChildren<Animator>();
        AnimationClip clip = ClipFor(controller, portrait.state);
        bool sampling = animator != null && clip != null;
        if (sampling)
        {
            animator.applyRootMotion = false;
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, portrait.time * clip.length);
            AnimationMode.EndSampling();
            // the clip may carry the body away from the origin; keep it where it was placed
            zombie.transform.position = new Vector3(0f, -500f, 0f);
        }
        else
        {
            Debug.LogWarning("ZombieArt: no pose for " + portrait.type);
        }

        Color tint = mutant ? Color.Lerp(portrait.tint, ZombieDex.MutantColor, 0.45f) : portrait.tint;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        Renderer[] renderers = zombie.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            r.SetPropertyBlock(block);
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

        GameObject cameraObject = new GameObject("DexCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.cullingMask = 1 << Layer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.fieldOfView = 28f;
        camera.aspect = Width / (float)Height;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 50f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

        // frame the whole body, seen from slightly below for menace
        float fit = Mathf.Max(bounds.size.y, bounds.size.x / camera.aspect, bounds.size.z / camera.aspect) * 1.08f;
        float distance = fit * 0.5f / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Vector3 target = bounds.center;
        camera.transform.position = target + new Vector3(0f, -bounds.size.y * 0.08f, -distance);
        camera.transform.LookAt(target);

        Light key = NewLight(Quaternion.Euler(25f, 35f, 0f), new Color(1f, 0.92f, 0.82f), 1.5f);
        Light fill = NewLight(Quaternion.Euler(10f, -50f, 0f), new Color(0.55f, 0.65f, 0.8f), 0.45f);
        Light rim = NewLight(Quaternion.Euler(15f, 160f, 0f), mutant ? ZombieDex.MutantColor : portrait.rim, 2.2f);
        RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.14f);

        Texture2D picture = WeaponArt.Supersample(camera, Width, Height);
        File.WriteAllBytes(pngPath, picture.EncodeToPNG());

        if (sampling) AnimationMode.StopAnimationMode();
        Object.DestroyImmediate(picture);
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(key.gameObject);
        Object.DestroyImmediate(fill.gameObject);
        Object.DestroyImmediate(rim.gameObject);
        Object.DestroyImmediate(zombie);
    }

    // In batch mode there's no baked environment: specular surfaces reflect an uninitialised cubemap and
    // render as rainbow glitter. Use flat ambient light, no environment reflections, and full-resolution textures.
    public static void FullTextures()
    {
        Texture.streamingTextureForceLoadAll = true;
        QualitySettings.streamingMipmapsActive = false;
        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = null;
        RenderSettings.reflectionIntensity = 0f;
    }

    // every generated picture in one go: gun pictures, dex portraits and the home key art
    public static void RenderEverything()
    {
        FullTextures();
        WeaponArt.RenderIcons();
        RenderPortraits();
        RenderKeyArt();
    }

    // ---------- Home screen key art ----------

    struct Extra
    {
        public string model;
        public string state;
        public float time;
        public Vector3 position;
        public float turn;
    }

    // a horde shambling toward the viewer, backlit red; transparent background so it sits over the camera feed
    [MenuItem("Tools/Outbreak/Render Home Key Art")]
    public static void RenderKeyArt()
    {
        const int width = 720, height = 1100;
        string output = "Assets/Resources/Art/HomeHorde.png";
        FullTextures();
        Directory.CreateDirectory("Assets/Resources/Art");
        GameObject[] models = Resources.LoadAll<GameObject>("ZombieModels");
        RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>("ZombieMixamo");

        Extra[] horde =
        {
            new Extra { model = "warzombie", state = "Z_Walk_InPlace", time = 0.1f, position = new Vector3(0f, 0f, 0f), turn = 0f },
            new Extra { model = "girl", state = "Z_Walk_InPlace", time = 0.6f, position = new Vector3(-0.95f, 0f, 1.4f), turn = 12f },
            new Extra { model = "warzombie", state = "Scream", time = 0.5f, position = new Vector3(1.05f, 0f, 1.7f), turn = -15f },
            new Extra { model = "girl", state = "Z_Run_InPlace", time = 0.3f, position = new Vector3(0.45f, 0f, 3.6f), turn = -5f },
            new Extra { model = "warzombie", state = "Z_Walk_InPlace", time = 0.85f, position = new Vector3(-1.7f, 0f, 3.9f), turn = 20f },
            new Extra { model = "warzombie", state = "Idle", time = 0.2f, position = new Vector3(1.9f, 0f, 4.8f), turn = -25f },
            new Extra { model = "girl", state = "Z_Walk_InPlace", time = 0.35f, position = new Vector3(-0.4f, 0f, 6f), turn = 8f },
        };

        Vector3 origin = new Vector3(0f, -500f, 0f);
        AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        System.Collections.Generic.List<GameObject> spawned = new System.Collections.Generic.List<GameObject>();
        foreach (Extra extra in horde)
        {
            GameObject model = System.Array.Find(models, m => m.name.ToLower().Contains(extra.model));
            if (model == null) continue;
            GameObject zombie = Object.Instantiate(model);
            zombie.transform.position = origin + extra.position;
            zombie.transform.rotation = Quaternion.Euler(0f, 180f + extra.turn, 0f);
            foreach (Transform t in zombie.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            Animator animator = zombie.GetComponentInChildren<Animator>();
            AnimationClip clip = ClipFor(controller, extra.state);
            if (animator != null && clip != null)
            {
                animator.applyRootMotion = false;
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, extra.time * clip.length);
            }
            zombie.transform.position = origin + extra.position;

            // further back is darker, as if fading into the night
            float depth = Mathf.InverseLerp(0f, 6f, extra.position.z);
            Color shade = Color.Lerp(new Color(0.85f, 0.82f, 0.8f), new Color(0.25f, 0.22f, 0.24f), depth);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer r in zombie.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", shade);
                block.SetColor("_Color", shade);
                r.SetPropertyBlock(block);
            }
            spawned.Add(zombie);
        }
        AnimationMode.EndSampling();

        GameObject cameraObject = new GameObject("KeyArtCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.cullingMask = 1 << Layer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.fieldOfView = 36f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 60f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        // at head height, far enough back to take in the whole horde
        camera.transform.position = origin + new Vector3(0f, 1.3f, -5.4f);
        camera.transform.rotation = Quaternion.Euler(1f, 0f, 0f);

        Light key = NewLight(Quaternion.Euler(20f, 30f, 0f), new Color(0.8f, 0.84f, 0.92f), 1.5f);
        Light rimRed = NewLight(Quaternion.Euler(12f, 200f, 0f), new Color(1f, 0.12f, 0.08f), 3f);
        Light rimGreen = NewLight(Quaternion.Euler(25f, 150f, 0f), new Color(0.35f, 1f, 0.25f), 0.45f);
        RenderSettings.ambientLight = new Color(0.2f, 0.19f, 0.2f);

        Texture2D picture = WeaponArt.Supersample(camera, width, height);
        // fade the bottom into transparency so the art melts into the menu
        Color[] pixels = picture.GetPixels();
        for (int y = 0; y < height; y++)
        {
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, height * 0.22f, y));
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                pixels[i].a *= fade;
            }
        }
        picture.SetPixels(pixels);
        picture.Apply();
        File.WriteAllBytes(output, picture.EncodeToPNG());
        Object.DestroyImmediate(picture);

        AnimationMode.StopAnimationMode();
        foreach (GameObject zombie in spawned) Object.DestroyImmediate(zombie);
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(key.gameObject);
        Object.DestroyImmediate(rimRed.gameObject);
        Object.DestroyImmediate(rimGreen.gameObject);

        AssetDatabase.ImportAsset(output);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(output);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
        Debug.Log("ZombieArt: key art rendered to " + output);
    }

    // the clip the game's animator plays for a state
    static AnimationClip ClipFor(RuntimeAnimatorController controller, string state)
    {
        UnityEditor.Animations.AnimatorController animatorController = controller as UnityEditor.Animations.AnimatorController;
        if (animatorController == null) return null;
        foreach (UnityEditor.Animations.ChildAnimatorState child in animatorController.layers[0].stateMachine.states)
        {
            if (child.state.name == state) return child.state.motion as AnimationClip;
        }
        return null;
    }

    static Light NewLight(Quaternion rotation, Color color, float intensity)
    {
        Light light = new GameObject("DexLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = rotation;
        light.color = color;
        light.intensity = intensity;
        return light;
    }
}
