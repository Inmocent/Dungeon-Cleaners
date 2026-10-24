using UnityEngine;
using System.Collections;

/// <summary>
/// Ranged projectile used by Mages (Fireball) and Healers (Healing Orb).
/// Flies toward the target with visual trail and executes an impact effect.
/// </summary>
public class CombatProjectile : MonoBehaviour
{
    private Vector3 targetPos;
    private Transform targetTransform;
    private float speed = 8f;
    private System.Action onHit;
    private Color impactColor = Color.yellow;
    private SpriteRenderer sr;

    private static Sprite _cachedOrbSprite;

    public static Sprite GetOrbSprite()
    {
        if (_cachedOrbSprite != null) return _cachedOrbSprite;

        int size = 16;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;
        float radius = size * 0.45f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= radius)
                {
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        _cachedOrbSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        return _cachedOrbSprite;
    }

    /// <summary>
    /// Spawns and launches a projectile.
    /// </summary>
    public static CombatProjectile Spawn(Vector3 start, Transform target, Color color, float speed, System.Action onHitCallback, float scale = 0.5f)
    {
        var go = new GameObject("Projectile");
        go.transform.position = start;
        go.transform.localScale = Vector3.one * scale;

        var proj = go.AddComponent<CombatProjectile>();
        proj.Init(target, color, speed, onHitCallback);
        return proj;
    }

    void Init(Transform target, Color color, float spd, System.Action callback)
    {
        targetTransform = target;
        targetPos = target != null ? target.position : transform.position;
        speed = spd;
        onHit = callback;
        impactColor = color;

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = GetOrbSprite();
        sr.color = color;
        sr.sortingOrder = 25; // Above characters

        StartCoroutine(FlyCoroutine());
    }

    IEnumerator FlyCoroutine()
    {
        float timeout = 2.5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (targetTransform != null)
                targetPos = targetTransform.position;

            float step = speed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, step);

            // Rotate towards direction of movement
            Vector3 dir = targetPos - transform.position;
            if (dir != Vector3.zero)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            if (Vector3.Distance(transform.position, targetPos) < 0.2f)
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Hit effect (quick scale burst)
        onHit?.Invoke();
        yield return StartCoroutine(ImpactBurst());

        Destroy(gameObject);
    }

    IEnumerator ImpactBurst()
    {
        Vector3 baseScale = transform.localScale;
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            float progress = t / 0.12f;
            transform.localScale = Vector3.Lerp(baseScale, baseScale * 1.8f, progress);
            if (sr) sr.color = new Color(impactColor.r, impactColor.g, impactColor.b, 1f - progress);
            yield return null;
        }
    }
}
