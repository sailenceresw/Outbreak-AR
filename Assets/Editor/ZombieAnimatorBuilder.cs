using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds Resources/ZombieMixamo.controller from the Mixamo clips. State names match the ones
// ZombieController already plays, so the new animations drop in without code changes; new states
// (Scream, Bite, Crawl, HitReact) are used where they exist.
public static class ZombieAnimatorBuilder
{
    const string ClipFolder = "Assets/Mixamo/Animations/";
    const string OutputPath = "Assets/Resources/ZombieMixamo.controller";

    static readonly string[,] States =
    {
        { "Idle", "zombie idle" },
        { "Z_Walk_InPlace", "zombie walk" },
        { "Z_Walk1_InPlace 0", "zombie walk" },
        { "Z_Run_InPlace", "zombie run" },
        { "Attack", "zombie attack" },
        { "Bite", "zombie biting" },
        { "DieBack", "zombie death" },
        { "DieForward", "zombie dying" },
        { "Scream", "zombie scream" },
        { "Crawl", "running crawl" },
        { "HitReact", "zombie reaction hit" },
    };

    [MenuItem("Tools/Outbreak/Build Zombie Animator")]
    public static void Build()
    {
        if (!Directory.Exists(ClipFolder)) return;

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputPath) != null) AssetDatabase.DeleteAsset(OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        int added = 0;
        for (int i = 0; i < States.GetLength(0); i++)
        {
            AnimationClip clip = LoadClip(States[i, 1]);
            if (clip == null)
            {
                Debug.LogWarning("ZombieAnimatorBuilder: missing clip " + States[i, 1]);
                continue;
            }
            AnimatorState state = machine.AddState(States[i, 0]);
            state.motion = clip;
            if (States[i, 0] == "Idle") machine.defaultState = state;
            added++;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("ZombieAnimatorBuilder: built " + OutputPath + " with " + added + " states");
    }

    static AnimationClip LoadClip(string fileName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ClipFolder + fileName + ".fbx"))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) return clip;
        }
        return null;
    }
}
