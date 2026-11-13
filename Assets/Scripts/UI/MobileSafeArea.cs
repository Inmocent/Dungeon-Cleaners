using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Automatically adapts edge-anchored HUD panels to Screen.safeArea on Android/iOS devices
/// (handling punch-hole cameras, rounded corners, and gesture navigation bars) while keeping
/// full-screen backgrounds and modal backdrops edge-to-edge.
/// Also ensures InputSystemUIInputModule has active Pointer/Touch bindings in every scene.
/// </summary>
public class MobileSafeArea : MonoBehaviour
{
    private Canvas targetCanvas;
    private Rect lastSafeArea = Rect.zero;
    private Vector2Int lastScreenSize = Vector2Int.zero;

    private readonly Dictionary<RectTransform, Vector2> baseAnchoredPositions = new();
    private readonly Dictionary<RectTransform, (Vector2 min, Vector2 max)> baseOffsets = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        AttachToAllCanvases();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToAllCanvases();
    }

    private static void AttachToAllCanvases()
    {
        EnsureTouchInputModule();

        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (canvas.GetComponent<MobileSafeArea>() == null)
            {
                canvas.gameObject.AddComponent<MobileSafeArea>();
            }
        }
    }

    private static void EnsureTouchInputModule()
    {
        var es = FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            var esGo = new GameObject("EventSystem");
            es = esGo.AddComponent<EventSystem>();
        }

        var uiModule = es.GetComponent<InputSystemUIInputModule>();
        if (uiModule == null)
        {
            uiModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        if (uiModule.actionsAsset == null)
        {
            uiModule.AssignDefaultActions();
        }
    }

    void Awake()
    {
        targetCanvas = GetComponent<Canvas>();
    }

    void Start()
    {
        ApplySafeArea();
    }

    void Update()
    {
        if (lastSafeArea != Screen.safeArea ||
            lastScreenSize.x != Screen.width ||
            lastScreenSize.y != Screen.height)
        {
            ApplySafeArea();
        }
    }

    public void ApplySafeArea()
    {
        if (targetCanvas == null || Screen.width <= 0 || Screen.height <= 0) return;

        Rect safe = Screen.safeArea;
        lastSafeArea = safe;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        float scale = targetCanvas.scaleFactor > 0.001f ? targetCanvas.scaleFactor : 1f;
        float leftInset   = safe.x / scale;
        float bottomInset = safe.y / scale;
        float rightInset  = (Screen.width - safe.xMax) / scale;
        float topInset    = (Screen.height - safe.yMax) / scale;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i) as RectTransform;
            if (child == null) continue;

            string lower = child.name.ToLowerInvariant();
            // Keep full-screen backgrounds and dimmed modal overlays edge-to-edge
            if (lower == "background" || lower.Contains("modal")) continue;

            if (!baseAnchoredPositions.ContainsKey(child))
            {
                baseAnchoredPositions[child] = child.anchoredPosition;
                baseOffsets[child] = (child.offsetMin, child.offsetMax);
            }

            Vector2 basePos = baseAnchoredPositions[child];
            var (baseMin, baseMax) = baseOffsets[child];

            bool stretchX = !Mathf.Approximately(child.anchorMin.x, child.anchorMax.x);
            bool stretchY = !Mathf.Approximately(child.anchorMin.y, child.anchorMax.y);

            if (stretchX || stretchY)
            {
                Vector2 oMin = baseMin;
                Vector2 oMax = baseMax;

                if (stretchX && Mathf.Approximately(child.anchorMin.x, 0f) && Mathf.Approximately(child.anchorMax.x, 1f))
                {
                    oMin.x += leftInset;
                    oMax.x -= rightInset;
                }
                if (stretchY && Mathf.Approximately(child.anchorMin.y, 0f) && Mathf.Approximately(child.anchorMax.y, 1f))
                {
                    oMin.y += bottomInset;
                    oMax.y -= topInset;
                }

                child.offsetMin = oMin;
                child.offsetMax = oMax;
            }
            else
            {
                Vector2 pos = basePos;
                if (Mathf.Approximately(child.anchorMin.x, 0f)) pos.x += leftInset;
                else if (Mathf.Approximately(child.anchorMin.x, 1f)) pos.x -= rightInset;

                if (Mathf.Approximately(child.anchorMin.y, 0f)) pos.y += bottomInset;
                else if (Mathf.Approximately(child.anchorMin.y, 1f)) pos.y -= topInset;

                child.anchoredPosition = pos;
            }
        }
    }
}
