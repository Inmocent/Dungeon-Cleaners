using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene names used across the project.</summary>
public static class SceneNames
{
    public const string MainMenu       = "MainMenu";
    public const string SquadBuilder   = "SquadBuilder";
    public const string Formation      = "Formation";
    public const string Combat         = "Combat";
    public const string Rewards        = "Rewards";
    public const string GameOver       = "GameOver";
}

/// <summary>Helpers for navigating between scenes.</summary>
public static class SceneNav
{
    public static void GoTo(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}