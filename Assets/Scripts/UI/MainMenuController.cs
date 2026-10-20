using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Drives the Main Menu scene.</summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    public Button startRunButton;
    public Button squadButton;
    public Button settingsButton;

    [Header("Settings Panel")]
    public GameObject settingsPanel;

    void Start()
    {
        settingsPanel.SetActive(false);

        startRunButton.onClick.AddListener(OnStartRun);
        squadButton.onClick.AddListener(OnSquad);
        settingsButton.onClick.AddListener(OnSettings);
    }

    void OnStartRun()
    {
        if (GameRunData.Instance != null)
            GameRunData.Instance.StartNewRun();
        SceneNav.GoTo(SceneNames.SquadBuilder);
    }

    void OnSquad()
    {
        SceneNav.GoTo(SceneNames.SquadBuilder);
    }

    void OnSettings()
    {
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }
}