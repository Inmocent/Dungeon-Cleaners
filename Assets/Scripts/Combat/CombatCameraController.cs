using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Dynamic Combat Cinematography Controller based on "How Many Dudes?".
/// Implements:
/// 1. Multi-Target Centroid Tracking via 2D Axis-Aligned Bounding Box (AABB)
/// 2. Adaptive Orthographic Scaling with Viewport Aspect & HUD Safety Margins
/// 3. Decoupled Critically Damped Springs (SmoothDamp) for Lateral Pan & Zoom
/// 4. Non-Linear Procedural Trauma-Based Screen Shake (Perlin translational & rotational Z-roll)
/// 5. Terminal Climax Framing with snap-zoom & simulation time dilation
/// 6. Strict Arena Viewport Boundary Clamping
/// </summary>
public class CombatCameraController : MonoBehaviour
{
    public static CombatCameraController Instance { get; private set; }

    [Header("Camera References")]
    [SerializeField] private Camera targetCamera;

    [Header("Adaptive Orthographic Framing")]
    [Tooltip("Minimum orthographic size for close skirmishes and climax framing")]
    public float minOrthoSize = 4.8f;

    [Tooltip("Maximum orthographic size for wide arena encounters")]
    public float maxOrthoSize = 11.5f;

    [Tooltip("Safety margin factor (0.20 - 0.35) protecting unit sprites from HUD overlap")]
    public float marginFactor = 0.28f;

    [Header("Decoupled Spring Damping")]
    [Tooltip("Smooth time for lateral positional panning")]
    public float posSmoothTime = 0.35f;

    [Tooltip("Smooth time for orthographic zoom adjustments")]
    public float sizeSmoothTime = 0.50f;

    [Tooltip("Snap-zoom duration used for final lethal knockout close-up")]
    public float climaxZoomTime = 0.20f;

    [Header("Procedural Trauma Shake")]
    [Tooltip("Linear dissipation rate for non-linear procedural screen shake (s^-1)")]
    public float traumaDecayRate = 1.5f;

    [Tooltip("Maximum translational offset displacement during peak trauma")]
    public float maxShakeTranslation = 0.55f;

    [Tooltip("Maximum rotational Z-roll angle (degrees) during peak trauma")]
    public float maxShakeRollAngle = 3.5f;

    [Header("Arena Viewport Confinement")]
    public bool enableBoundaryClamping = true;
    public Rect arenaBounds = new Rect(-20f, -20f, 40f, 40f);

    // Runtime state
    private Vector2 currentPosVelocity;
    private float currentSizeVelocity;
    private float currentTrauma = 0f;
    private float noiseSeedX;
    private float noiseSeedY;
    private float noiseSeedRot;

    private bool isMultiTargetTracking = true;
    private Vector2 overrideTargetPos;
    private float overrideOrthoSize;
    private bool isOverrideActive = false;
    private bool isTerminalClimax = false;

    // Cache of active targets to avoid allocations
    private readonly List<Transform> trackedTargets = new List<Transform>(32);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (targetCamera == null)
            targetCamera = GetComponent<Camera>() ?? Camera.main;

        noiseSeedX = Random.value * 1000f;
        noiseSeedY = Random.value * 1000f;
        noiseSeedRot = Random.value * 1000f;

        if (targetCamera != null)
        {
            overrideOrthoSize = targetCamera.orthographicSize;
            overrideTargetPos = targetCamera.transform.position;
        }
    }

    /// <summary>
    /// Register dynamic combatant transforms for multi-target AABB bounding.
    /// </summary>
    public void SetTrackedTargets(IEnumerable<Transform> targets)
    {
        trackedTargets.Clear();
        if (targets != null)
        {
            foreach (var t in targets)
            {
                if (t != null && t.gameObject.activeInHierarchy)
                    trackedTargets.Add(t);
            }
        }
    }

    /// <summary>
    /// Update arena boundaries to prevent camera frustum from showing out-of-bounds void.
    /// </summary>
    public void SetArenaBounds(Rect bounds)
    {
        arenaBounds = bounds;
        enableBoundaryClamping = true;
    }

    /// <summary>
    /// Injects trauma impulse [0.0 - 1.0] for non-linear procedural screen shake.
    /// </summary>
    public void AddTrauma(float amount)
    {
        currentTrauma = Mathf.Clamp01(currentTrauma + amount);
    }

    /// <summary>
    /// Focuses camera smoothly onto a specific coordinate with custom orthographic zoom.
    /// </summary>
    public void FocusOnPoint(Vector2 worldPos, float orthoSize = 6.0f)
    {
        isMultiTargetTracking = false;
        isOverrideActive = true;
        overrideTargetPos = worldPos;
        overrideOrthoSize = Mathf.Clamp(orthoSize, minOrthoSize, maxOrthoSize);
    }

    /// <summary>
    /// Resumes automated multi-target AABB tracking.
    /// </summary>
    public void ResumeMultiTargetTracking()
    {
        isMultiTargetTracking = true;
        isOverrideActive = false;
        isTerminalClimax = false;
    }

    /// <summary>
    /// Executes terminal climax framing on the lethal blowout:
    /// snap-zooms directly onto the casualty and dilates simulation time.
    /// </summary>
    public void TriggerTerminalClimax(Vector3 casualtyPos, System.Action onComplete = null)
    {
        StartCoroutine(TerminalClimaxRoutine(casualtyPos, onComplete));
    }

    private IEnumerator TerminalClimaxRoutine(Vector3 casualtyPos, System.Action onComplete)
    {
        isTerminalClimax = true;
        isMultiTargetTracking = false;
        isOverrideActive = true;
        overrideTargetPos = casualtyPos;
        overrideOrthoSize = minOrthoSize;

        // Add visceral tactile shake
        AddTrauma(0.65f);

        // Dilate simulation time scale for punchline impact
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0.35f;

        float elapsed = 0f;
        while (elapsed < climaxZoomTime)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Hold close-up beat in slow-mo
        yield return new WaitForSecondsRealtime(0.35f);

        // Smoothly restore time scale
        Time.timeScale = originalTimeScale;
        isTerminalClimax = false;

        onComplete?.Invoke();
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        float dt = Time.unscaledDeltaTime;

        // 1. Determine unconstrained target position & target orthographic size
        Vector2 targetPan;
        float targetSize;

        if (isOverrideActive || !isMultiTargetTracking)
        {
            targetPan = overrideTargetPos;
            targetSize = overrideOrthoSize;
        }
        else
        {
            ComputeAABB(out targetPan, out targetSize);
        }

        // 2. Viewport Arena Boundary Clamping (prevents viewing empty void)
        float aspect = targetCamera.aspect > 0.1f ? targetCamera.aspect : (16f / 9f);
        if (enableBoundaryClamping && arenaBounds.width > 2f && arenaBounds.height > 2f)
        {
            float halfHeight = targetSize;
            float halfWidth = halfHeight * aspect;

            // If arena is smaller than camera view, center on arena; otherwise clamp inside
            if (arenaBounds.width <= halfWidth * 2f)
            {
                targetPan.x = arenaBounds.center.x;
            }
            else
            {
                targetPan.x = Mathf.Clamp(targetPan.x, arenaBounds.xMin + halfWidth, arenaBounds.xMax - halfWidth);
            }

            if (arenaBounds.height <= halfHeight * 2f)
            {
                targetPan.y = arenaBounds.center.y;
            }
            else
            {
                targetPan.y = Mathf.Clamp(targetPan.y, arenaBounds.yMin + halfHeight, arenaBounds.yMax - halfHeight);
            }
        }

        // 3. Decoupled Critically Damped Springs (SmoothDamp)
        float currentSize = targetCamera.orthographicSize;
        float pSmooth = isTerminalClimax ? climaxZoomTime : posSmoothTime;
        float sSmooth = isTerminalClimax ? climaxZoomTime : sizeSmoothTime;

        Vector2 currentCamPos = targetCamera.transform.position;
        Vector2 smoothedPos = Vector2.SmoothDamp(currentCamPos, targetPan, ref currentPosVelocity, pSmooth, Mathf.Infinity, dt);
        float smoothedSize = Mathf.SmoothDamp(currentSize, targetSize, ref currentSizeVelocity, sSmooth, Mathf.Infinity, dt);

        targetCamera.orthographicSize = smoothedSize;

        // 4. Non-Linear Procedural Trauma Shake
        Vector3 shakeOffset = Vector3.zero;
        float shakeAngle = 0f;

        if (currentTrauma > 0.001f)
        {
            // Power curve: trauma^2 provides gentle vibration at low values and violent jolts at high values
            float shakePower = currentTrauma * currentTrauma;

            float timeVal = Time.unscaledTime * 22f;
            float pX = (Mathf.PerlinNoise(noiseSeedX, timeVal) - 0.5f) * 2f;
            float pY = (Mathf.PerlinNoise(noiseSeedY, timeVal) - 0.5f) * 2f;
            float pRot = (Mathf.PerlinNoise(noiseSeedRot, timeVal) - 0.5f) * 2f;

            shakeOffset = new Vector3(pX * maxShakeTranslation * shakePower, pY * maxShakeTranslation * shakePower, 0f);
            shakeAngle = pRot * maxShakeRollAngle * shakePower;

            currentTrauma = Mathf.Max(0f, currentTrauma - traumaDecayRate * dt);
        }

        // Apply final camera transform
        targetCamera.transform.position = new Vector3(smoothedPos.x + shakeOffset.x, smoothedPos.y + shakeOffset.y, -10f);
        targetCamera.transform.rotation = Quaternion.Euler(0f, 0f, shakeAngle);
    }

    /// <summary>
    /// Computes the 2D Axis-Aligned Bounding Box (AABB) encapsulating all active combatants
    /// and derives target pan coordinate and aspect-padded orthographic size.
    /// </summary>
    private void ComputeAABB(out Vector2 targetPan, out float targetSize)
    {
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        int activeCount = 0;

        for (int i = 0; i < trackedTargets.Count; i++)
        {
            var t = trackedTargets[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;

            Vector3 pos = t.position;
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.y > maxY) maxY = pos.y;
            activeCount++;
        }

        if (activeCount == 0)
        {
            // Fallback to arena center or current camera position
            targetPan = arenaBounds.width > 2f ? arenaBounds.center : (Vector2)targetCamera.transform.position;
            targetSize = 6.0f;
            return;
        }

        // Geometric centroid: P_target = ((X_min + X_max)/2, (Y_min + Y_max)/2)
        targetPan = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

        // Aspect ratio: A = Width / Height
        float aspect = targetCamera.aspect > 0.1f ? targetCamera.aspect : (16f / 9f);

        // Derivation from dominant axis with safety margin factor M
        float sWidth = ((maxX - minX) * (1f + marginFactor)) / (2f * aspect);
        float sHeight = ((maxY - minY) * (1f + marginFactor)) * 0.5f;

        float requiredSize = Mathf.Max(sWidth, sHeight);
        targetSize = Mathf.Clamp(requiredSize, minOrthoSize, maxOrthoSize);
    }
}
