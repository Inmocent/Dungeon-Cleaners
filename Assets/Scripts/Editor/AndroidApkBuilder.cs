#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Automated Android APK build pipeline for mobile device testing.
/// Validates Android Build Support, InputSystemUIInputModule, CanvasScaler settings,
/// configures PlayerSettings (ARM64, IL2CPP, API 24+, Landscape), and outputs Builds/Android/DungeonCleaners_Test.apk.
/// </summary>
[InitializeOnLoad]
public static class AndroidApkBuilder
{
    private const string OutputRelativePath = "Builds/Android/DungeonCleaners_Test.apk";
    private const string AppIdentifier = "com.dungeoncleaners.game";
    private const string TriggerFile = "Temp/trigger_build_apk.txt";
    private const string StatusFile = "Temp/apk_build_result.txt";

    static AndroidApkBuilder()
    {
        EditorApplication.delayCall += CheckAndTriggerBuild;
    }

    private static void CheckAndTriggerBuild()
    {
        if (File.Exists(TriggerFile))
        {
            try
            {
                File.Delete(TriggerFile);
            }
            catch { }
            Debug.Log("[AndroidApkBuilder] Trigger detected! Starting automated Android APK Build...");
            BuildAndroidApk();
        }
    }

    private static readonly string[] DefaultScenePaths =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/SquadBuilder.unity",
        "Assets/Scenes/Formation.unity",
        "Assets/Scenes/Combat.unity",
        "Assets/Scenes/Rewards.unity",
        "Assets/Scenes/GameOver.unity"
    };

    [MenuItem("Tools/Dungeon Cleaners/Validate Mobile & Input Setup")]
    public static void ValidateMobileAndInputSetup()
    {
        string activePath = EditorSceneManager.GetActiveScene().path;
        string[] scenes = CollectBuildScenes();
        var uiActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");

        int fixedCount = 0;
        foreach (string scenePath in scenes)
        {
            if (!File.Exists(scenePath)) continue;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool dirty = false;

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null && (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                    scaler.referenceResolution != new Vector2(1920, 1080)))
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.matchWidthOrHeight = 0.5f;
                    EditorUtility.SetDirty(scaler);
                    dirty = true;
                }
            }

            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<EventSystem>();
                dirty = true;
            }

            var uiModule = es.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null)
            {
                uiModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
                dirty = true;
            }

            if (uiModule.actionsAsset == null)
            {
                if (uiActions != null)
                {
                    uiModule.actionsAsset = uiActions;
                    uiModule.point = InputActionReference.Create(uiActions.FindAction("UI/Point"));
                    uiModule.leftClick = InputActionReference.Create(uiActions.FindAction("UI/Click"));
                    uiModule.middleClick = InputActionReference.Create(uiActions.FindAction("UI/MiddleClick"));
                    uiModule.rightClick = InputActionReference.Create(uiActions.FindAction("UI/RightClick"));
                    uiModule.scrollWheel = InputActionReference.Create(uiActions.FindAction("UI/ScrollWheel"));
                    uiModule.move = InputActionReference.Create(uiActions.FindAction("UI/Navigate"));
                    uiModule.submit = InputActionReference.Create(uiActions.FindAction("UI/Submit"));
                    uiModule.cancel = InputActionReference.Create(uiActions.FindAction("UI/Cancel"));
                }
                else
                {
                    uiModule.AssignDefaultActions();
                }
                EditorUtility.SetDirty(uiModule);
                dirty = true;
            }

            if (dirty)
            {
                EditorSceneManager.SaveScene(scene);
                fixedCount++;
            }
        }

        if (!string.IsNullOrEmpty(activePath) && File.Exists(activePath))
        {
            EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);
        }

        Debug.Log($"[AndroidApkBuilder] Mobile & Touch Input validation complete. Updated {fixedCount} scene(s).");
    }

    [MenuItem("Tools/Dungeon Cleaners/Build Android APK")]
    public static void BuildAndroidApk()
    {
        // 1. Verify Android Build Support module is installed
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError("[AndroidApkBuilder] Android Build Support module is NOT installed in this Unity Editor! " +
                           "Open Unity Hub -> Installs -> Add Modules -> Android Build Support (SDK, NDK & OpenJDK), then retry.");
            return;
        }

        // 2. Switch active build target to Android if needed
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log("[AndroidApkBuilder] Switching active build target to Android...");
            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            if (!switched)
            {
                Debug.LogError("[AndroidApkBuilder] Failed to switch active build target to Android.");
                return;
            }
        }

        // 3. Validate touch input & CanvasScaler across scenes before building
        ValidateMobileAndInputSetup();

        // 4. Configure Android PlayerSettings (ARM64 requires IL2CPP in Unity)
        PlayerSettings.companyName = "DungeonCleaners";
        PlayerSettings.productName = "Dungeon Cleaners";
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppIdentifier);

        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

        // ARM64 requires IL2CPP; Mono2x only supports 32-bit ARMv7 in Unity
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        // Landscape orientation matching GameRunData & 1920x1080 CanvasScaler
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        // Ensure standalone .apk output (not .aab)
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        // 5. Collect enabled scenes (or populate if EditorBuildSettings is empty)
        string[] scenes = CollectBuildScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError("[AndroidApkBuilder] No valid scenes found to build!");
            return;
        }

        // 6. Prepare output folder & run BuildPipeline.BuildPlayer
        string fullApkPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), OutputRelativePath));
        string outputDir = Path.GetDirectoryName(fullApkPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = fullApkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.CompressWithLz4
        };

        FixPackageCacheEditorAsmdefs();

        Debug.Log($"[AndroidApkBuilder] Building Android APK ({scenes.Length} scenes) -> {fullApkPath}");
        BuildReport report = BuildPipeline.BuildPlayer(options);

        string statusFilePath = Path.Combine(Directory.GetCurrentDirectory(), StatusFile);
        if (report.summary.result == BuildResult.Succeeded)
        {
            float sizeMb = report.summary.totalSize / (1024f * 1024f);
            File.WriteAllText(statusFilePath, $"SUCCESS|{fullApkPath}|{sizeMb:F1} MB");
            Debug.Log($"<color=green><b>[AndroidApkBuilder] SUCCESS!</b></color> APK built ({sizeMb:F1} MB) at:\n{fullApkPath}");
            EditorUtility.RevealInFinder(fullApkPath);
        }
        else
        {
            var errList = new List<string>();
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception || msg.type == LogType.Assert)
                    {
                        errList.Add($"[{step.name}] {msg.content}");
                        Debug.LogError($"[Build Step: {step.name}] {msg.content}");
                    }
                }
            }
            File.WriteAllText(statusFilePath, $"FAILED|{report.summary.result}|Errors: {report.summary.totalErrors}\n" + string.Join("\n", errList));
            Debug.LogError($"[AndroidApkBuilder] Build FAILED with status: {report.summary.result} (Errors: {report.summary.totalErrors})");
        }
    }

    private static string[] CollectBuildScenes()
    {
        var enabled = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path))
            {
                enabled.Add(s.path);
            }
        }

        if (enabled.Count > 0) return enabled.ToArray();

        // Fallback: register standard project scenes or active scene
        var fallback = new List<EditorBuildSettingsScene>();
        foreach (var path in DefaultScenePaths)
        {
            if (File.Exists(path))
            {
                enabled.Add(path);
                fallback.Add(new EditorBuildSettingsScene(path, true));
            }
        }

        if (enabled.Count == 0)
        {
            string active = EditorSceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(active) && File.Exists(active))
            {
                enabled.Add(active);
                fallback.Add(new EditorBuildSettingsScene(active, true));
            }
        }

        if (fallback.Count > 0)
        {
            EditorBuildSettings.scenes = fallback.ToArray();
        }

        return enabled.ToArray();
    }

    private static void FixPackageCacheEditorAsmdefs()
    {
        string packageCache = Path.Combine(Directory.GetCurrentDirectory(), "Library", "PackageCache");
        if (!Directory.Exists(packageCache)) return;

        foreach (string asmdefPath in Directory.GetFiles(packageCache, "*.asmdef", SearchOption.AllDirectories))
        {
            if (asmdefPath.Contains("com.scenarioinc.scenario") && (asmdefPath.Contains("RestSharp.Unity") || asmdefPath.Contains("Needle.Deeplink")))
            {
                try
                {
                    string content = File.ReadAllText(asmdefPath);
                    if (content.Contains("\"includePlatforms\": []"))
                    {
                        content = content.Replace("\"includePlatforms\": []", "\"includePlatforms\": [\n        \"Editor\"\n    ]");
                        File.WriteAllText(asmdefPath, content);
                        Debug.Log($"[AndroidApkBuilder] Patched Editor asmdef for player build: {Path.GetFileName(asmdefPath)}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AndroidApkBuilder] Could not patch asmdef {asmdefPath}: {ex.Message}");
                }
            }
        }
    }
}
#endif
