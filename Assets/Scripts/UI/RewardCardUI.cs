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

    static readonly Color RestColor    = new Color(0.62f, 0.38f, 0.16f);
    static readonly Color UpgradeColor = new Color(0.18f, 0.36f, 0.66f);
    static readonly Color WeaponColor  = new Color(0.48f, 0.20f, 0.68f);
    static readonly Color GoldColor    = new Color(0.64f, 0.50f, 0.14f);

    public void Setup(RewardData r, System.Action<RewardData, bool> toggleCb)
    {
        reward = r;
        onToggled = toggleCb;
        isSelected = false;

        if (rewardNameLabel) rewardNameLabel.text = r.rewardName;
        if (descriptionLabel) descriptionLabel.text = r.description;

        string badge = "[UPGRADE]";
        Color bgCol = UpgradeColor;
        if (r.rewardType == RewardType.Rest)
        {
            badge = "[REST]";
            bgCol = RestColor;
        }
        else if (r.rewardType == RewardType.WeaponBoost)
        {
            badge = string.IsNullOrEmpty(r.weaponStyle) ? "[WEAPON BOOST]" : $"[{r.weaponStyle}]";
            bgCol = WeaponColor;
        }
        else if (r.rewardType == RewardType.GoldBonus)
        {
            badge = "[GOLD BOUNTY]";
            bgCol = GoldColor;
        }

        if (typeLabel) typeLabel.text = badge;
        if (cardBackground) DungeonUITheme.ApplyCard(cardBackground, bgCol);
        if (selectionBorder)
        {
            DungeonUITheme.ApplyPanel(selectionBorder, new Color(1f, 0.85f, 0.18f, 0.95f));
            selectionBorder.gameObject.SetActive(false);
        }

        var btn = GetComponent<Button>();
        if (btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnTap);
        }
    }

    void OnTap()
    {
        isSelected = !isSelected;
        if (selectionBorder) selectionBorder.gameObject.SetActive(isSelected);
        onToggled?.Invoke(reward, isSelected);
    }
}
