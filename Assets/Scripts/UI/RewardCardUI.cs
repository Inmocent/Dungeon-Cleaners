using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>A single reward card in the Reward Screen. Toggle selection on tap.</summary>
public class RewardCardUI : MonoBehaviour
{
    public TextMeshProUGUI rewardNameLabel;
    public TextMeshProUGUI descriptionLabel;
    public TextMeshProUGUI typeLabel;
    public Image cardBackground;
    public Image selectionBorder;

    private RewardData reward;
    private System.Action<RewardData, bool> onToggled;
    private bool isSelected = false;

    static readonly Color RestColor    = new Color(0.8f, 0.5f, 0.2f);
    static readonly Color UpgradeColor = new Color(0.2f, 0.4f, 0.8f);
    static readonly Color SelectedBorderColor = new Color(1f, 0.85f, 0.1f);

    public void Setup(RewardData r, System.Action<RewardData, bool> toggleCb)
    {
        reward = r;
        onToggled = toggleCb;
        isSelected = false;

        if (rewardNameLabel) rewardNameLabel.text = r.rewardName;
        if (descriptionLabel) descriptionLabel.text = r.description;
        if (typeLabel) typeLabel.text = r.rewardType == RewardType.Rest ? "\ud83d\udd25 REST" : "\u2694\ufe0f UPGRADE";
        if (cardBackground) cardBackground.color = r.rewardType == RewardType.Rest ? RestColor : UpgradeColor;
        if (selectionBorder) selectionBorder.gameObject.SetActive(false);

        var btn = GetComponent<Button>();
        if (btn) btn.onClick.AddListener(OnTap);
    }

    void OnTap()
    {
        isSelected = !isSelected;
        if (selectionBorder) selectionBorder.gameObject.SetActive(isSelected);
        onToggled?.Invoke(reward, isSelected);
    }
}