using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Drives the Main Menu scene.</summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    public Button startRunButton;
    public Button squadButton;
    public Button formationButton;
    public Button settingsButton;

    [Header("Settings Panel")]
    public GameObject settingsPanel;

    void Start()
    {
        Time.timeScale = 1f;

        if (settingsPanel) settingsPanel.SetActive(false);

        if (startRunButton) startRunButton.onClick.AddListener(OnStartRun);
        if (squadButton) squadButton.onClick.AddListener(OnSquad);
        if (formationButton) formationButton.onClick.AddListener(OnFormation);
        if (settingsButton) settingsButton.onClick.AddListener(OnSettings);
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

    void OnFormation()
    {
        if (GameRunData.Instance != null)
            GameRunData.Instance.EnsureSquadAndFormation();
        SceneNav.GoTo(SceneNames.Formation);
    }

    void OnSettings()
    {
        if (settingsPanel)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }
}