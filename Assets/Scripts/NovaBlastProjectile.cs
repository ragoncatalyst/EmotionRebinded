using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Expanding circular AoE projectile: quickly scales up to a target diameter,
/// fades out, and applies damage once per enemy on first contact.
/// Attach to a prefab that has a SpriteRenderer with a circular sprite
/// and a CircleCollider2D set as trigger.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class NovaBlastProjectile : MonoBehaviour
{
    [Header("Runtime (read-only)")]
    [SerializeField] private float damage;
    [SerializeField] private float targetDiameter = 50f; // world units
    [SerializeField] private float expandDuration = 0.35f;
    [SerializeField] private float fadeDuration = 0.8f;

    private CircleCollider2D circleCol;
    private SpriteRenderer sr;
    private float elapsed;
    private float startDiameter;
    private HashSet<Enemy> damaged = new HashSet<Enemy>();

    public void Initialize(float damageOnce, float diameter, float expandTime, float fadeTime)
    {
        damage = damageOnce;
        targetDiameter = Mathf.Max(0.1f, diameter);
        expandDuration = Mathf.Max(0.01f, expandTime);
        fadeDuration = Mathf.Max(0.01f, fadeTime);
    }

    private void Awake()
    {
        circleCol = GetComponent<CircleCollider2D>();
        circleCol.isTrigger = true;
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.enabled = true;
            if (sr.sprite == null)
            {
                // 兜底：无Sprite时使用白色纹理，避免不可见
                var white = Texture2D.whiteTexture;
                sr.sprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f), 100f);
                sr.color = Color.white;
            }
        }
        // 继承玩家的排序层与顺序，确保可见在前景
        var player = GameObject.FindWithTag("Player");
        var playerSr = player != null ? player.GetComponent<SpriteRenderer>() : null;
        if (playerSr != null && sr != null)
        {
            sr.sortingLayerID = playerSr.sortingLayerID;
            sr.sortingOrder = playerSr.sortingOrder + 1;
            gameObject.layer = player.layer;
        }
        // Start very small
        startDiameter = 0.1f;
        SetVisualDiameter(startDiameter);
        SetAlpha(1f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / expandDuration);
        float currentDiameter = Mathf.Lerp(startDiameter, targetDiameter, t);
        SetVisualDiameter(currentDiameter);

        // After expansion completes, start fading
        float fadeT = Mathf.Clamp01((elapsed - expandDuration) / fadeDuration);
        if (fadeT > 0f)
        {
            SetAlpha(1f - fadeT);
        }

        // Destroy after full sequence
        if (elapsed >= expandDuration + fadeDuration)
        {
            Destroy(gameObject);
        }
    }

    private void SetVisualDiameter(float diameter)
    {
        // Scale sprite uniformly assuming sprite unit size is 1 unit per 1 world unit
        float radius = Mathf.Max(0.01f, diameter * 0.5f);
        transform.localScale = new Vector3(diameter, diameter, 1f);
        // Sync collider radius to match visual
        if (circleCol != null)
        {
            circleCol.radius = 0.5f; // with Transform scale, world radius = 0.5 * scale.x
            circleCol.enabled = true;
        }
    }

    private void SetAlpha(float a)
    {
        if (sr == null) return;
        Color c = sr.color; c.a = Mathf.Clamp01(a); sr.color = c;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var enemy = other.GetComponent<Enemy>();
        if (enemy == null) return;
        if (!enemy.IsTargetable) return;
        if (damaged.Contains(enemy)) return;
        enemy.TakeDamage(damage);
        damaged.Add(enemy);
    }
}
