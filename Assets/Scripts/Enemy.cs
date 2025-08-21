using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    [Header("AI")]
    public Transform target;                    // Follow target (player)
    public float moveSpeed = 2f;                // Move speed
    public float detectionRange = 8f;           // Detect range
    public float stopDistance = 1.5f;           // Stop distance

    [Header("Movement Smoothing")]
    public float acceleration = 10f;            // Acceleration for smooth start/stop
    public float velocityThreshold = 0.01f;     // Below this, treat as stopped
    
    [Header("Rendering")]
    public float fixedZPosition = 0f;           // Fixed Z to keep 2D look

    [Header("Shadow")]
    [SerializeField] private bool enableShadow = false;
    [SerializeField] private Vector3 shadowOffset = new Vector3(0f, -0.1f, 0f);
    [SerializeField] private Vector3 shadowScale = new Vector3(1f, 0.5f, 1f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private int shadowOrderOffset = -1;
    [SerializeField] private bool placeShadowAtSpriteBottom = true; // place on sprite bottom instead of center
    [SerializeField] private float shadowWorldYOffset = -0.02f;    // small push below bottom

    [Header("Health System")]
    [SerializeField] private float maxHealth = 10f;
    [SerializeField] private float currentHealth = 10f;
    [SerializeField] private SpriteRenderer healthBar;    // Assign the square sprite renderer as health bar
    [SerializeField] private Vector2 healthBarSize = new Vector2(1f, 0.12f);
    [SerializeField] private bool healthBarUseSliced = true; // use sliced so width can change without stretching corners
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField] private Color healthBarColor = new Color(0.2f, 0.9f, 0.2f, 1f);
    [SerializeField] private Color lowHealthBarColor = new Color(0.95f, 0.3f, 0.2f, 1f);
    [SerializeField] private float deathBlinkDuration = 1.5f; // seconds
    [SerializeField] private int deathBlinkCount = 6;         // toggles (on/off pairs -> ~1.5s total with step duration)

    private BoxCollider2D enemyCol;
    private Rigidbody2D rb2d;                  // Rigidbody2D
    private SpriteRenderer spriteRenderer;     // Main sprite
    private SpriteRenderer shadowRenderer;     // Shadow sprite
    private bool canMove = true;               // Can move
    private Vector2 moveDirection;             // Move dir
    private bool isDying = false;
    public bool IsDying => isDying;
    public bool IsTargetable { get; private set; } = true;
    
    // Targeting / wander support
    [SerializeField] private float retargetInterval = 3f; // interval to re-acquire cached Player reference
    private float lastRetargetTime = -999f;
    private Transform playerRef;               // cached Player transform used to lock when inside detectionRange
    
    [Header("Targeting Policy")]
    [SerializeField] private bool onlyChasePlayer = true; // ignore prefab-assigned targets that are not the Player

    [Header("Wander (when no target)")]
    [SerializeField] private bool enableWander = true;
    [SerializeField] private float wanderChangeInterval = 2f;
    [SerializeField] private float wanderSpeedFactor = 0.6f; // moveSpeed multiplier
    private float wanderTimer = 0f;
    private Vector2 wanderDirection = Vector2.zero;
    

    void Start()
    {
        enemyCol = GetComponent<BoxCollider2D>();
        rb2d = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerRef = GameObject.FindWithTag("Player")?.transform;
        if (onlyChasePlayer)
        {
            // Prefab上若意外拖了其他Transform（例如出生点），清空，避免朝错误目标移动
            if (target != null && (playerRef == null || target != playerRef))
            {
                target = null;
            }
        }
        
        // Rigidbody2D setup
        if (rb2d != null)
        {
            rb2d.gravityScale = 0f;
            rb2d.drag = 5f;
            rb2d.angularDrag = 0f;
            rb2d.freezeRotation = true;
            rb2d.bodyType = RigidbodyType2D.Dynamic;
            rb2d.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb2d.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }
        
        // Fix Z
        Vector3 pos = transform.position;
        pos.z = fixedZPosition;
        transform.position = pos;

        if (currentHealth > maxHealth) currentHealth = maxHealth;
        IsTargetable = true;
        SetupHealthBar();
        if (enableShadow) SetupShadow(); else CleanupShadow();
    }

    void Update()
    {
        UpdateHealthBar();
        UpdateShadow();

        // refresh cached player reference if missing
        if (playerRef == null && Time.time - lastRetargetTime > retargetInterval)
        {
            lastRetargetTime = Time.time;
            var p = GameObject.FindWithTag("Player");
            if (p != null) playerRef = p.transform;
        }

        if (!canMove || rb2d == null || isDying)
        {
            moveDirection = Vector2.zero;
            return;
        }

        // lock target if player within detection range
        if (target == null && playerRef != null)
        {
            float pd = Vector2.Distance(transform.position, playerRef.position);
            if (pd <= detectionRange)
            {
                target = playerRef;
            }
        }

        // 若存在一个非Player的旧target并开启onlyChasePlayer，则忽略它
        bool hasValidTarget = target != null && (!onlyChasePlayer || (target != null && target.CompareTag("Player")));
        if (hasValidTarget)
        {
            float dist = Vector2.Distance(transform.position, target.position);
            if (dist > stopDistance)
            {
                Vector2 desiredDirection = (target.position - transform.position).normalized;
                Vector3 targetPosition = transform.position + (Vector3)desiredDirection * moveSpeed * Time.deltaTime;
                moveDirection = CanMoveTo(targetPosition) ? desiredDirection : FindAlternativeDirection(desiredDirection);
            }
            else
            {
                moveDirection = Vector2.zero;
            }
        }
        else
        {
            // wander when no target
            if (enableWander)
            {
                wanderTimer -= Time.deltaTime;
                if (wanderTimer <= 0f || wanderDirection == Vector2.zero)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    wanderDirection = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)).normalized;
                    wanderTimer = wanderChangeInterval;
                }
                Vector2 desired = wanderDirection;
                Vector3 next = transform.position + (Vector3)desired * (moveSpeed * wanderSpeedFactor) * Time.deltaTime;
                if (!CanMoveTo(next))
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    wanderDirection = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)).normalized;
                }
                moveDirection = wanderDirection * wanderSpeedFactor;
            }
            else
            {
                moveDirection = Vector2.zero;
            }
        }
    }

    private void ResolveTarget() { }

    void FixedUpdate()
    {
        if (rb2d == null || isDying) return;

        Vector2 targetVelocity = moveDirection * moveSpeed;
        Vector2 velocityChange = targetVelocity - rb2d.velocity;
        float maxVelocityChange = acceleration * Time.fixedDeltaTime;
        velocityChange = Vector2.ClampMagnitude(velocityChange, maxVelocityChange);
        rb2d.velocity += velocityChange;
        if (rb2d.velocity.magnitude < velocityThreshold) rb2d.velocity = Vector2.zero;

        if (Mathf.Abs(transform.position.z - fixedZPosition) > 0.001f)
        {
            Vector3 pos = transform.position; pos.z = fixedZPosition; transform.position = pos;
        }
    }

    // ===== Health API =====
    public void TakeDamage(float amount)
    {
        if (isDying) return;
        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Abs(amount));
        UpdateHealthBar();
        if (currentHealth <= 0f)
        {
            IsTargetable = false; // stop being a valid target immediately
            if (enemyCol != null) enemyCol.enabled = false; // avoid blocking bullets
            StartCoroutine(DeathBlinkAndDestroy());
        }
    }

    public void Heal(float amount)
    {
        if (isDying) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Abs(amount));
        UpdateHealthBar();
    }

    private void SetupHealthBar()
    {
        if (healthBar == null) return;
        healthBar.drawMode = healthBarUseSliced ? SpriteDrawMode.Sliced : SpriteDrawMode.Tiled;
        healthBar.transform.localPosition = healthBarOffset;
        healthBar.size = healthBarSize;
        healthBar.color = healthBarColor;
        healthBar.enabled = true;
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthBar == null) return;
        float ratio = Mathf.Approximately(maxHealth, 0f) ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
        healthBar.size = new Vector2(Mathf.Max(0.01f, healthBarSize.x * ratio), healthBarSize.y);
        healthBar.color = Color.Lerp(lowHealthBarColor, healthBarColor, ratio);
        healthBar.enabled = ratio > 0f || isDying; // keep visible until death sequence starts
    }

    private System.Collections.IEnumerator DeathBlinkAndDestroy()
    {
        isDying = true;
        canMove = false;
        if (rb2d) rb2d.velocity = Vector2.zero;
        if (enemyCol != null) enemyCol.enabled = false;
        float total = deathBlinkDuration;
        float step = total / Mathf.Max(1, deathBlinkCount * 2); // on+off pairs
        float elapsed = 0f;
        while (elapsed < total)
        {
            SetVisible(false);
            yield return new WaitForSeconds(step);
            elapsed += step;
            SetVisible(true);
            yield return new WaitForSeconds(step);
            elapsed += step;
        }
        Destroy(gameObject);
    }

    private void SetVisible(bool v)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = v;
        if (healthBar != null) healthBar.enabled = v;
        if (shadowRenderer != null) shadowRenderer.enabled = v;
    }

    private void SetupShadow()
    {
        if (!enableShadow || spriteRenderer == null) return;
        if (shadowRenderer == null)
        {
            GameObject s = new GameObject("Shadow");
            s.transform.SetParent(transform);
            s.transform.localPosition = shadowOffset;
            s.transform.localScale = shadowScale;
            shadowRenderer = s.AddComponent<SpriteRenderer>();
        }
        shadowRenderer.sprite = spriteRenderer.sprite;
        shadowRenderer.color = shadowColor;
        shadowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = spriteRenderer.sortingOrder + shadowOrderOffset;
        shadowRenderer.enabled = enableShadow;
    }

    private void UpdateShadow()
    {
        if (!enableShadow || shadowRenderer == null || spriteRenderer == null) return;
        shadowRenderer.sprite = spriteRenderer.sprite;
        shadowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = spriteRenderer.sortingOrder + shadowOrderOffset;
        if (placeShadowAtSpriteBottom)
        {
            float bottom = spriteRenderer.bounds.min.y + shadowWorldYOffset;
            var p = shadowRenderer.transform.position;
            p.x = transform.position.x;
            p.y = bottom;
            p.z = transform.position.z;
            shadowRenderer.transform.position = p;
            shadowRenderer.transform.localScale = shadowScale;
        }
        else
        {
            shadowRenderer.transform.localPosition = shadowOffset;
            shadowRenderer.transform.localScale = shadowScale;
        }
        shadowRenderer.color = shadowColor;
    }

    private void CleanupShadow()
    {
        // Destroy runtime shadow child if any exists
        Transform child = transform.Find("Shadow");
        if (child != null) Destroy(child.gameObject);
    }

    // ===== External control =====
    public void SetCanMove(bool state)
    {
        canMove = state && !isDying;
    }
    
    private bool CanMoveTo(Vector3 targetPosition)
    {
        if (TerrainInitialization.Instance == null) return true;
        return TerrainInitialization.Instance.IsWalkable(targetPosition);
    }
    
    private Vector2 FindAlternativeDirection(Vector2 desiredDirection)
    {
        Vector2[] alternativeDirections = {
            new Vector2(desiredDirection.x, 0).normalized,
            new Vector2(0, desiredDirection.y).normalized,
            new Vector2(desiredDirection.x + 0.5f, desiredDirection.y).normalized,
            new Vector2(desiredDirection.x - 0.5f, desiredDirection.y).normalized,
            new Vector2(desiredDirection.x, desiredDirection.y + 0.5f).normalized,
            new Vector2(desiredDirection.x, desiredDirection.y - 0.5f).normalized
        };
        foreach (var altDir in alternativeDirections)
        {
            Vector3 altTargetPos = transform.position + (Vector3)altDir * moveSpeed * Time.deltaTime;
            if (CanMoveTo(altTargetPos)) return altDir;
        }
        return Vector2.zero;
    }

    public void SetPosition(Vector3 newPosition)
    {
        newPosition.z = fixedZPosition;
        if (rb2d != null) rb2d.velocity = Vector2.zero;
        transform.position = newPosition;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
    }
}