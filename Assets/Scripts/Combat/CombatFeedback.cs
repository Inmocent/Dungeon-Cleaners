using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Spawns floating combat numbers (damage, heals, buffs) and visual feedback.
/// Lightweight and mobile-friendly.
/// </summary>
public class CombatFeedback : MonoBehaviour
{
    private static CombatFeedback _instance;
    public static CombatFeedback Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("CombatFeedback");
                _instance = go.AddComponent<CombatFeedback>();
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    /// <summary>
    /// Spawns floating text (e.g. "-12", "+25 HP", "CRIT!") above a world position.
    /// </summary>
    public void ShowText(Vector3 worldPos, string message, Color color, float size = 3.5f)
    {
        StartCoroutine(FloatTextCoroutine(worldPos + Vector3.up * 0.4f, message, color, size));
    }

    IEnumerator FloatTextCoroutine(Vector3 startPos, string message, Color color, float size)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = startPos;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = message;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = 50;

        float duration = 0.7f;
        float elapsed = 0f;
        Vector3 endPos = startPos + new Vector3(Random.Range(-0.3f, 0.3f), 0.8f, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            go.transform.position = Vector3.Lerp(startPos, endPos, t);
            tmp.color = new Color(color.r, color.g, color.b, Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        Destroy(go);
    }
}
