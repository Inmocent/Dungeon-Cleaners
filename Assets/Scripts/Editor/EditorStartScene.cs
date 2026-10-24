using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Forces Play Mode in the Unity Editor to always boot from Scene 0 (MainMenu),
/// preventing unintended test runs from sub-scenes like Combat or Rewards.
/// Can be toggled via Tools -> Dungeon Cleaners menu.
/// </summary>
[InitializeOnLoad]
public static class EditorStartScene
{
    private const string PREF_KEY = "DungeonCleaners_ForcePlayFromMainMenu";
    private const string MAIN_MENU_SCENE_PATH = "Assets/Scenes/MainMenu.unity";

    static EditorStartScene()
    {
        ApplyStartSceneSetting();
    }

    [MenuItem("Tools/Dungeon Cleaners/Toggle Force Start from MainMenu")]
    public static void ToggleStartFromMainMenu()
    {
        bool current = EditorPrefs.GetBool(PREF_KEY, true);
        EditorPrefs.SetBool(PREF_KEY, !current);
        ApplyStartSceneSetting();
        Debug.Log($"[EditorStartScene] Force start from MainMenu is now: {!current}");
    }

    [MenuItem("Tools/Dungeon Cleaners/Toggle Force Start from MainMenu", true)]
    public static bool ToggleStartFromMainMenuValidate()
    {
        Menu.SetChecked("Tools/Dungeon Cleaners/Toggle Force Start from MainMenu", EditorPrefs.GetBool(PREF_KEY, true));
        return true;
    }

    public static void ApplyStartSceneSetting()
    {
        bool forceMainMenu = EditorPrefs.GetBool(PREF_KEY, true);
        if (forceMainMenu)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MAIN_MENU_SCENE_PATH);
            if (sceneAsset != null)
            {
                EditorSceneManager.playModeStartScene = sceneAsset;
            }
            else
            {
                Debug.LogWarning($"[EditorStartScene] Could not find scene at {MAIN_MENU_SCENE_PATH}");
            }
        }
        else
        {
            EditorSceneManager.playModeStartScene = null;
        }
    }

    [MenuItem("Tools/Dungeon Cleaners/Apply Mobile Settings")]
    public static void ApplyMobilePlayerSettings()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        Debug.Log("[EditorStartScene] Applied Mobile PlayerSettings: LandscapeLeft orientation enforced.");
    }
}
