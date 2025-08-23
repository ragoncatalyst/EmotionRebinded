using UnityEngine;

public class Chest : MonoBehaviour
{
    [Header("Visual & Sorting")]
    [Tooltip("Attach and configure DynamicSorting to make chest stand upright and sort like bushes/players.")]
    public bool autoAddDynamicSorting = true;
    public ObjectType sortingType = ObjectType.Bush; // 让箱子像灌木一样参与遮挡
    public Vector2 sortingOffset = new Vector2(0f, -0.5f);

    [Header("Trigger Settings")]
    [Tooltip("Tag required to open the chest (usually 'Player').")]
    public string requiredTag = "Player";

    [Tooltip("Use a trigger collider to auto-open when the player touches.")]
    public bool useTrigger = true;

    [Tooltip("Delay (seconds) before opening the upgrade panel after contact.")]
    public float openDelay = 0f;

    [Header("Upgrade Panel")]
    [Tooltip("Reference to the UpgradePanelController in the scene.")]
    public UpgradePanelController upgradePanel;

    [Tooltip("Auto-find the upgrade panel at runtime if not assigned.")]
    public bool autoFindPanelInScene = true;

    [Tooltip("When auto-finding, try this tag first (e.g. 'GameManager'). If empty, will scan the scene.")]
    public string panelOwnerTag = "GameManager";

    [Header("Chest Lifetime")]
    [Tooltip("If true, this chest can be opened only once.")]
    public bool oneTimeUse = true;

    [Tooltip("Destroy the chest GameObject after a successful open.")]
    public bool destroyOnUse = true;

    [Tooltip("If not one-time, minimum seconds between two opens.")]
    public float reuseCooldown = 5f;

    private bool hasBeenUsed = false;
    private float lastOpenTime = -9999f;
    private bool isOpening = false;

    private void Reset()
    {
        // Try to auto-assign a trigger collider for convenience
        var col = GetComponent<Collider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        // Ensure there is a SpriteRenderer for correct visual orientation
        if (GetComponent<SpriteRenderer>() == null)
        {
            gameObject.AddComponent<SpriteRenderer>();
        }
        // Ensure facing camera style so sprite“立起来”
        EnsureFacingCamera();
    }

    private void OnValidate()
    {
        if (reuseCooldown < 0f) reuseCooldown = 0f;
        if (openDelay < 0f) openDelay = 0f;
        if (autoAddDynamicSorting)
        {
            EnsureDynamicSorting();
        }
        EnsureFacingCamera();
    } 

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!useTrigger) return;
        TryOpenFromObject(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (useTrigger) return;
        TryOpenFromObject(collision.gameObject);
    }

    private void TryOpenFromObject(GameObject obj)
    {
        if (obj == null) return;
        if (!string.IsNullOrEmpty(requiredTag) && !obj.CompareTag(requiredTag)) return;
        TryOpen();
    }

    public void TryOpen()
    {
        ResolvePanelIfNeeded();
        if (isOpening) return;
        if (oneTimeUse && hasBeenUsed) return;
        if (!oneTimeUse && Time.time - lastOpenTime < reuseCooldown) return;
        if (upgradePanel == null)
        {
            Debug.LogWarning("[Chest] upgradePanel is not assigned. Please set it in Inspector.");
            return;
        }

        isOpening = true;
        if (openDelay > 0f)
        {
            Invoke(nameof(DoOpen), openDelay);
        }
        else
        {
            DoOpen();
        }
    }

    private void DoOpen()
    {
        // Use the same entry point as level-up to leverage existing queue/refresh logic
        upgradePanel.RequestOpenFromLevelUp();
        lastOpenTime = Time.time;
        hasBeenUsed = true;
        isOpening = false;

        if (destroyOnUse && oneTimeUse)
        {
            Destroy(gameObject);
        }
    }

    private void ResolvePanelIfNeeded()
    {
        if (!autoFindPanelInScene || upgradePanel != null) return;
        // 1) Try tagged owner first
        if (!string.IsNullOrEmpty(panelOwnerTag))
        {
            try
            {
                var owner = GameObject.FindWithTag(panelOwnerTag);
                if (owner != null)
                {
                    upgradePanel = owner.GetComponentInChildren<UpgradePanelController>(true);
                    if (upgradePanel != null) return;
                    upgradePanel = owner.GetComponent<UpgradePanelController>();
                    if (upgradePanel != null) return;
                }
            }
            catch (System.Exception)
            {
                // Tag 未定义时，跳过该路径，回退到场景扫描
            }
        }
        // 2) Fallback: scan the scene
        upgradePanel = FindObjectOfType<UpgradePanelController>(true);
    }

    private void Awake()
    {
        if (autoAddDynamicSorting)
        {
            EnsureDynamicSorting();
        }
        EnsureFacingCamera();
        // 保证Z轴为0
        var tr = transform;
        var pos = tr.position; pos.z = 0f; tr.position = pos;
        var ls = tr.localScale; tr.localScale = new Vector3(Mathf.Abs(ls.x) < 0.0001f ? 1f : Mathf.Abs(ls.x), Mathf.Abs(ls.y) < 0.0001f ? 1f : Mathf.Abs(ls.y), 1f);
    }

    private void LateUpdate()
    {
        // FacingCamera 会在 LateUpdate 设置旋转为 -45,0,0；这里只保证 z=0
        var p = transform.position; p.z = 0f; transform.position = p;
    }

    private void EnsureDynamicSorting()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

        var ds = GetComponent<DynamicSorting>();
        if (ds == null) ds = gameObject.AddComponent<DynamicSorting>();
        // 配置为类似灌木/角色的立式遮挡
        var dsTypeField = typeof(DynamicSorting).GetField("objectType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (dsTypeField != null) dsTypeField.SetValue(ds, sortingType);
        ds.SetSortingOffset(sortingOffset);
        ds.SetBaseSortingOrder(60); // 介于Bush(50)与Enemy(80)之间
        ds.UpdateSortingOrder();
    }

    private void EnsureFacingCamera()
    {
        var fc = GetComponent<FacingCamera>();
        if (fc == null) fc = gameObject.AddComponent<FacingCamera>();
        // 立即对齐一帧，防止首帧仍旧“躺平”
        transform.rotation = Quaternion.Euler(-45f, 0f, 0f);
    }
}


