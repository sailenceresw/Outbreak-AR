using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Command-line Android build:
// Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod BuildAndroid.Build
public static class BuildAndroid
{
    const string OutputPath = "Builds/Android/OutbreakAR.apk";

    [MenuItem("Build/Android APK")]
    public static void Build()
    {
        // keep the zombie materials on the full Lit shader; safe to run every build
        ZombieMaterialUpgrade.Upgrade();
        // the Mixamo animation set, retargeted onto every zombie model
        ZombieAnimatorBuilder.Build();
        // the Mixamo characters' skin textures, wired to editable materials
        MixamoCharacterSetup.Run();

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log("Android build " + summary.result + ": " + summary.outputPath +
                  " (" + summary.totalErrors + " errors, " + summary.totalSize / (1024 * 1024) + " MB)");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
