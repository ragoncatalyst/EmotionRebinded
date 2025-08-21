using UnityEngine;

public class HomingBullet : MonoBehaviour
{
    [Header("Stats")]
    public float damage = 3f;
    public float startSpeed = 10f;
    public float minSpeed = 4f;
    public float deceleration = 2f; // per second
    public float lifeTime = 4f;

    [Header("Physics")]
    public float rotateTowardsFactor = 12f; // how fast to steer towards target
    public bool destroyOnHit = true;

    private Enemy target;
    private Rigidbody2D rb;
    private float currentSpeed;
    private float life;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.isKinematic = true;
            rb.gravityScale = 0f;
        }
        currentSpeed = startSpeed;
    }

    private void Start()
    {
        AcquireNearestTarget();
    }

    private void Update()
    {
        life += Time.deltaTime;
        if (life >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        if (target == null)
        {
            AcquireNearestTarget();
            if (target == null)
            {
                // No target — just move forward
                transform.position += transform.right * (currentSpeed * Time.deltaTime);
                Decelerate();
                return;
            }
        }

        Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        // smooth rotate towards target
        Vector2 curDir = transform.right;
        Vector2 newDir = Vector2.Lerp(curDir, dir, Mathf.Clamp01(rotateTowardsFactor * Time.deltaTime)).normalized;
        transform.right = newDir;
        transform.position += (Vector3)(newDir * currentSpeed * Time.deltaTime);
        Decelerate();
    }

    private void Decelerate()
    {
        currentSpeed = Mathf.Max(minSpeed, currentSpeed - deceleration * Time.deltaTime);
    }

    private void AcquireNearestTarget()
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        float best = float.PositiveInfinity;
        Enemy bestE = null;
        Vector3 p = transform.position;
        foreach (var e in enemies)
        {
            if (e == null || !e.gameObject.activeInHierarchy) continue;
            float d = (e.transform.position - p).sqrMagnitude;
            if (d < best)
            {
                best = d; bestE = e;
            }
        }
        target = bestE;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            if (destroyOnHit) Destroy(gameObject);
        }
    }
}


