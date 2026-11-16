using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Centralized Dark-Dungeon Pixel-Art UI theme asset loaded from Resources/UI/DungeonUITheme.
/// Shared by DungeonCleanersBuilder prefabs/scenes and runtime-generated modals.
/// </summary>
[CreateAssetMenu(fileName = "DungeonUITheme", menuName = "Dungeon Cleaners/UI Theme")]
public class DungeonUITheme : ScriptableObject
{
    [Header("9-Slice Pixel-Art Sprites")]
    public Sprite panelFrame;
    public Sprite cardFrame;
    public Sprite btnNormal;
    public Sprite btnHover;
    public Sprite btnPressed;
    public Sprite barBg;
    public Sprite barFill;

    [Header("Dark-Dungeon Palette")]
    public Color bgVoid            = new Color(0.05f, 0.06f, 0.09f, 1f);
    public Color panelSlate        = new Color(0.16f, 0.18f, 0.24f, 0.98f);
    public Color panelHeaderTint   = new Color(0.11f, 0.13f, 0.18f, 0.98f);
    public Color cardBaseTint      = new Color(0.20f, 0.23f, 0.31f, 0.98f);
    public Color accentGold        = new Color(0.98f, 0.82f, 0.28f, 1f);
    public Color accentCyan        = new Color(0.28f, 0.84f, 0.98f, 1f);
    public Color btnPrimaryGreen   = new Color(0.24f, 0.68f, 0.38f, 1f);
    public Color btnSecondarySteel = new Color(0.32f, 0.38f, 0.50f, 1f);
    public Color btnDangerRed      = new Color(0.78f, 0.24f, 0.26f, 1f);
    public Color btnArcaneBlue     = new Color(0.24f, 0.48f, 0.84f, 1f);
    public Color hpGreen           = new Color(0.24f, 0.84f, 0.34f, 1f);
    public Color hpLowRed          = new Color(0.94f, 0.24f, 0.24f, 1f);
    public Color energyBlue        = new Color(0.26f, 0.58f, 0.98f, 1f);
    public Color fatigueAmber      = new Color(0.98f, 0.72f, 0.20f, 1f);

    // WCAG-safe text tones (spec: replace #64748B which fails AA at 2.35:1)
    public static readonly Color MutedText = new Color(0.58f, 0.64f, 0.72f, 1f); // #94A3B8
    public static readonly Color BodyText  = new Color(0.89f, 0.91f, 0.94f, 1f); // #E2E8F0

    private static DungeonUITheme _cached;
    public static DungeonUITheme Instance
    {
        get
        {
            if (_cached == null)
            {
                _cached = Resources.Load<DungeonUITheme>("UI/DungeonUITheme");
            }
            return _cached;
        }
    }

    public static void ClearCache() => _cached = null;

    public static void ApplyPanel(Image img, Color? tint = null)
    {
        if (img == null) return;
        var t = Instance;
        if (t != null && t.panelFrame != null)
        {
            img.sprite = t.panelFrame;
            img.type = Image.Type.Sliced;
        }
        img.color = tint ?? (t != null ? t.panelSlate : new Color(0.14f, 0.16f, 0.22f, 0.98f));
    }

    public static void ApplyCard(Image img, Color? tint = null)
    {
        if (img == null) return;
        var t = Instance;
        if (t != null && t.cardFrame != null)
        {
            img.sprite = t.cardFrame;
            img.type = Image.Type.Sliced;
        }
        img.color = tint ?? (t != null ? t.cardBaseTint : new Color(0.20f, 0.23f, 0.31f, 0.98f));
    }

    public static void ApplyButton(Button btn, Color? tint = null, bool primary = true)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        ApplyButtonImage(img, tint, primary);

        var t = Instance;
        if (t != null && t.btnNormal != null && t.btnHover != null && t.btnPressed != null)
        {
            btn.transition = Selectable.Transition.SpriteSwap;
            var state = btn.spriteState;
            state.highlightedSprite = t.btnHover;
            state.selectedSprite = t.btnHover;
            state.pressedSprite = t.btnPressed;
            state.disabledSprite = t.btnPressed;
            btn.spriteState = state;
        }
    }

    public static void ApplyButtonImage(Image img, Color? tint = null, bool primary = true)
    {
        if (img == null) return;
        var t = Instance;
        if (t != null && t.btnNormal != null)
        {
            img.sprite = t.btnNormal;
            img.type = Image.Type.Sliced;
        }
        if (tint.HasValue) img.color = tint.Value;
        else if (t != null) img.color = primary ? t.btnPrimaryGreen : t.btnSecondarySteel;
    }

    public static void ApplySlider(Slider slider, Color fillColor)
    {        if (slider == null) return;
        var t = Instance;

        if (slider.targetGraphic is Image bgImg)
        {
            if (t != null && t.barBg != null)
            {
                bgImg.sprite = t.barBg;
                bgImg.type = Image.Type.Sliced;
            }
            bgImg.color = Color.white;
        }

        if (slider.fillRect != null)
        {
            var fillImg = slider.fillRect.GetComponent<Image>();
            if (fillImg != null)
            {
                if (t != null && t.barFill != null)
                {
                    fillImg.sprite = t.barFill;
                    fillImg.type = Image.Type.Sliced;
                }
                fillImg.color = fillColor;
            }
        }
    }

    private static Material _ctaMaterial;

    /// <summary>
    /// Dark outline + underlay so white CTA labels stay readable on green/blue/red buttons
    /// (white on #3DAD61 measures ~2.85:1 without it).
    /// </summary>
    public static void ApplyCtaText(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;

        if (_ctaMaterial == null && tmp.font != null)
        {
            _ctaMaterial = new Material(tmp.font.material);
            _ctaMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.024f, 0.031f, 0.047f, 1f)); // #06080C
            _ctaMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.20f);
            _ctaMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.024f, 0.031f, 0.047f, 0.85f));
            _ctaMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            _ctaMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            _ctaMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);
            _ctaMaterial.EnableKeyword("OUTLINE_ON");
            _ctaMaterial.EnableKeyword("UNDERLAY_ON");
        }

        if (_ctaMaterial != null) tmp.fontMaterial = _ctaMaterial;
        tmp.color = Color.white;
    }
}

