using UnityEngine;

/// <summary>
/// Handles animated sprite playback for characters (heroes and enemies).
/// Loads individual frame PNGs from Assets/Sprites/frames/ by convention.
/// Lightweight — uses a simple frame array and timer, no Animator overhead.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterAnimator : MonoBehaviour
{
    public enum AnimState { Idle, Run }

    private SpriteRenderer sr;
    private Sprite[] idleFrames;
    private Sprite[] runFrames;
    private AnimState currentState = AnimState.Idle;

    private int frameIndex;
    private float frameTimer;
    private float frameInterval = 0.15f;  // ~6-7 fps for pixel art

    private bool isInitialized;
    private bool facingRight = true;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Initialize with a character sprite prefix.
    /// E.g., prefix="knight_m" loads knight_m_idle_anim_f0..f3 and knight_m_run_anim_f0..f3
    /// For enemies with only idle: prefix="skelet" loads skelet_idle_anim_f0..f3 and skelet_run_anim_f0..f3
    /// </summary>
    public void Setup(string spritePrefix, int idleFrameCount = 4, int runFrameCount = 4)
    {
        idleFrames = LoadFrames(spritePrefix, "idle_anim", idleFrameCount);
        runFrames = LoadFrames(spritePrefix, "run_anim", runFrameCount);

        // Fallback: if run frames not found, use idle
        if (runFrames == null || runFrames.Length == 0)
            runFrames = idleFrames;

        if (idleFrames != null && idleFrames.Length > 0)
        {
            sr.sprite = idleFrames[0];
            isInitialized = true;
        }
    }

    /// <summary>
    /// Setup for enemy types that use a simpler naming convention (e.g., "goblin_idle_anim_f0").
    /// </summary>
    public void SetupEnemy(string spritePrefix)
    {
        // Try standard hero naming first
        idleFrames = LoadFrames(spritePrefix, "idle_anim", 4);
        runFrames = LoadFrames(spritePrefix, "run_anim", 4);

        // Some enemies use just "anim" (like zombie_anim_f0)
        if (idleFrames == null || idleFrames.Length == 0)
        {
            idleFrames = LoadFrames(spritePrefix, "anim", 4);
        }

        if (runFrames == null || runFrames.Length == 0)
            runFrames = idleFrames;

        if (idleFrames != null && idleFrames.Length > 0)
        {
            sr.sprite = idleFrames[0];
            isInitialized = true;
        }
    }

    Sprite[] LoadFrames(string prefix, string animType, int count)
    {
        var frames = new Sprite[count];
        bool anyLoaded = false;

        for (int i = 0; i < count; i++)
        {
            // Files are at Assets/Sprites/frames/prefix_animType_fN.png
            // Load as Resources — we need to move or use direct load
            string path = $"Sprites/frames/{prefix}_{animType}_f{i}";
            var sprite = Resources.Load<Sprite>(path);

            if (sprite == null)
            {
                // Try loading from the frames folder directly via the sprite cache
                sprite = SpriteCache.LoadSprite($"{prefix}_{animType}_f{i}");
            }

            if (sprite != null)
            {
                frames[i] = sprite;
                anyLoaded = true;
            }
        }

        return anyLoaded ? frames : null;
    }

    void Update()
    {
        if (!isInitialized) return;

        var frames = currentState == AnimState.Run ? runFrames : idleFrames;
        if (frames == null || frames.Length == 0) return;

        frameTimer += Time.deltaTime;
        if (frameTimer >= frameInterval)
        {
            frameTimer = 0f;
            frameIndex = (frameIndex + 1) % frames.Length;
            if (frames[frameIndex] != null)
                sr.sprite = frames[frameIndex];
        }
    }

    public void SetState(AnimState state)
    {
        if (state == currentState) return;
        currentState = state;
        frameIndex = 0;
        frameTimer = 0f;
    }

    public void SetFacing(bool right)
    {
        if (facingRight == right) return;
        facingRight = right;
        var scale = transform.localScale;
        scale.x = right ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    /// <summary>
    /// Flash white for hit feedback, then restore.
    /// </summary>
    public void FlashHit()
    {
        if (sr == null) return;
        StartCoroutine(FlashCoroutine());
    }

    System.Collections.IEnumerator FlashCoroutine()
    {
        var origColor = sr.color;
        sr.color = new Color(1f, 0.35f, 0.35f, 1f);
        yield return new WaitForSeconds(0.1f);
        sr.color = origColor;
    }

    /// <summary>
    /// Set tint color (useful for indicating status effects).
    /// </summary>
    public void SetTint(Color c)
    {
        if (sr) sr.color = c;
    }

    public void ResetTint()
    {
        if (sr) sr.color = Color.white;
    }
}
