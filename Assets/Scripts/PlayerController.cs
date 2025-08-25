using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("玩家控制配置")]
    public float moveSpeed = 0.1f;       // 移动速度（单位/秒，可在Inspector中编辑）
    
    [Header("物理配置")]
    public bool usePhysicsMovement = false; // 是否使用物理移动（建议关闭以避免与Tilemap碰撞冲突）

    // 组件引用
    private Rigidbody2D rb2d;
    private BoxCollider2D playerCollider;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer shadowRenderer;

    [Header("Shadow")]
    public bool enableShadow = false;
    public Vector3 shadowOffset = new Vector3(0f, -0.1f, 0f);
    public Vector3 shadowScale = new Vector3(1f, 0.5f, 1f);
    public Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
    public int shadowOrderOffset = -1;
    public bool placeShadowAtSpriteBottom = true;
    public float shadowWorldYOffset = -0.02f;
    
    // 移除了重复的键盘输入处理逻辑，现在由NineButtons.cs直接处理
    // 移除了battleButtons数组，避免功能重复
    
    void Start()
    {
        // 获取物理组件
        rb2d = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // 配置物理组件以避免与Tilemap碰撞冲突
        ConfigurePhysicsComponents();
        if (enableShadow) SetupShadow(); else CleanupShadow();
        
        // Debug.Log($"[PlayerController] 玩家初始化完成，位置: {transform.position}, 使用物理移动: {usePhysicsMovement}");
    }

    private void LateUpdate()
    {
        // Keep shadow stuck to sprite bottom
        if (!enableShadow || shadowRenderer == null || spriteRenderer == null) return;
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
        shadowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = spriteRenderer.sortingOrder + shadowOrderOffset;
    }

    private void CleanupShadow()
    {
        Transform child = transform.Find("Shadow");
        if (child != null) Destroy(child.gameObject);
    }
    
    /// <summary>
    /// 配置物理组件，避免与Tilemap产生冲突
    /// </summary>
    private void ConfigurePhysicsComponents()
    {
        if (rb2d != null)
        {
            if (!usePhysicsMovement)
            {
                // 关闭物理移动，使用Transform直接移动
                rb2d.bodyType = RigidbodyType2D.Kinematic; // 运动学刚体，不受物理影响
                rb2d.gravityScale = 0f;
                rb2d.drag = 0f;
                rb2d.angularDrag = 0f;
                rb2d.freezeRotation = true;
                rb2d.interpolation = RigidbodyInterpolation2D.None; // 关闭插值
                rb2d.sleepMode = RigidbodySleepMode2D.StartAwake;
                
                // Debug.Log("[PlayerController] 已配置为运动学刚体，使用Transform移动");
            }
            else
            {
                // 启用物理移动
                rb2d.bodyType = RigidbodyType2D.Dynamic;
                rb2d.gravityScale = 0f; // 2D俯视角游戏通常不需要重力
                rb2d.drag = 2f; // 适度阻力
                rb2d.angularDrag = 0f;
                rb2d.freezeRotation = true;
                rb2d.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb2d.sleepMode = RigidbodySleepMode2D.NeverSleep;
                
                // Debug.Log("[PlayerController] 已配置为动态刚体，使用物理移动");
            }
        }
        
        if (playerCollider != null)
        {
            // 设置碰撞体为触发器，避免物理碰撞
            playerCollider.isTrigger = true;
            // Debug.Log("[PlayerController] 玩家碰撞体设置为触发器");
        }
    }

    private void SetupShadow()
    {
        if (!enableShadow) return;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;
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
    
    /// <summary>
    /// 判断是否为移动技能
    /// </summary>
    public bool IsMovementSkill(string skillId)
    {
        return skillId == "01" || skillId == "02" || skillId == "03" || skillId == "04";
    }

    /// <summary>
    /// 执行技能（供NineButtons调用）
    /// </summary>
    public void ExecuteSkill(string skillId)
    {
        // Debug.Log($"[PlayerController] 执行技能 {skillId}");

        // 统一的移动逻辑，支持持续移动和单次移动
        if (IsMovementSkill(skillId))
        {
            ExecuteMovement(skillId, Time.deltaTime); // 使用deltaTime支持持续移动
        }
        else
        {
            switch (skillId)
            {
                case "05":
                    CastHomingBullet();
                    break;
                case "06":
                    CastPiercingShot();
                    break;
                case "07":
                    CastNovaBlast();
                    break;
                default:
                    // Debug.Log($"[技能{skillId}] 未知技能或未绑定");
                    break;
            }
        }
    }

    [Header("Skill Prefabs")]
    public GameObject homingBulletPrefab;
    public GameObject piercingShotPrefab;
    [Header("Piercing Shot Config")]
    public float piercingLengthPerSecond = 80f; // 伸长速度（单位/秒）
    public float piercingMaxLength = 50f;       // 最大长度（单位）
    public float piercingThickness = 0.35f;    // 粗细
    public float piercingDamage = 10f;         // 伤害

    private void CastHomingBullet()
    {
        if (homingBulletPrefab == null)
        {
            // Debug.LogWarning("[PlayerController] Homing bullet prefab not assigned.");
            return;
        }
        var go = Instantiate(homingBulletPrefab, transform.position, Quaternion.identity);
        // face right initially; HomingBullet will steer towards target
        go.transform.right = Vector3.right;
    }

    private void CastPiercingShot()
    {
        // 方向：指向最近敌人；若没有则向右
        Vector3 dir = Vector3.right;
        Enemy nearest = FindNearestEnemyWithin(30f); // 与子弹一致只考虑30格内
        if (nearest != null)
            dir = (nearest.transform.position - transform.position).normalized;

        // 生成并配置 PiercingShot（分段生成、渐隐）
        GameObject go = new GameObject("PiercingShot");
        go.transform.position = transform.position;
        go.transform.right = dir;
        var ps = go.AddComponent<PiercingShot>();
        ps.growSpeed = Mathf.Max(0.1f, piercingLengthPerSecond);
        ps.maxLength = Mathf.Max(0.1f, piercingMaxLength);
        ps.damage = Mathf.Max(0f, piercingDamage);
        ps.origin = transform;
        // 动态调整段长度：保证段数≈50，避免增长受限；可非常长
        ps.segmentLength = Mathf.Max(piercingMaxLength / 50f, 0.5f);
        ps.thickness = piercingThickness;
        ps.SetDirection(dir);
        // Debug.Log($"[PlayerController] PiercingShot spawn dir={dir}, maxLen={ps.maxLength}, grow={ps.growSpeed}, segLen={ps.segmentLength}");
    }

    private void CastNovaBlast()
    {
        // Simple close-range radial damage: overlap circle
        float radius = 1.6f;
        float damage = 4f;
        var hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var h in hits)
        {
            var e = h.GetComponent<Enemy>();
            if (e != null)
            {
                e.TakeDamage(damage);
            }
        }
        // could add a VFX prefab here
    }

    private Enemy FindNearestEnemy()
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        float best = float.PositiveInfinity;
        Enemy bestE = null;
        foreach (var e in enemies)
        {
            if (e == null || !e.gameObject.activeInHierarchy) continue;
            float d = (e.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; bestE = e; }
        }
        return bestE;
    }

    private Enemy FindNearestEnemyWithin(float maxDist)
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        float best = float.PositiveInfinity;
        Enemy bestE = null;
        float maxSqr = maxDist * maxDist;
        foreach (var e in enemies)
        {
            if (e == null || !e.gameObject.activeInHierarchy) continue;
            float d = (e.transform.position - transform.position).sqrMagnitude;
            if (d <= maxSqr && d < best) { best = d; bestE = e; }
        }
        return bestE;
    }

    /// <summary>
    /// 执行移动（统一的移动逻辑，包含地形碰撞检测）
    /// </summary>
    public void ExecuteMovement(string skillId, float deltaTime)
    {
        float moveAmount = moveSpeed * deltaTime;
        Vector3 moveVector = Vector3.zero;
        
        switch (skillId)
        {
            case "01":
                moveVector = new Vector3(0, moveAmount, 0);  // 向上移动（Y轴正方向）
                break;
            case "02":
                moveVector = new Vector3(-moveAmount, 0, 0); // 向左移动（X轴负方向）
                break;
            case "03":
                moveVector = new Vector3(0, -moveAmount, 0); // 向下移动（Y轴负方向）
                break;
            case "04":
                moveVector = new Vector3(moveAmount, 0, 0);  // 向右移动（X轴正方向）
                break;
        }
        
        // 检查目标位置是否可通行
        Vector3 targetPosition = transform.position + moveVector;
        
        if (CanMoveTo(targetPosition))
        {
            transform.Translate(moveVector, Space.World);
        }
        else
        {
            // Debug.Log($"[PlayerController] 无法移动到 {targetPosition}，地形不可通行");
        }
    }
    
    /// <summary>
    /// 检查是否可以移动到指定位置
    /// </summary>
    private bool CanMoveTo(Vector3 targetPosition)
    {
        // 如果没有地形系统，允许移动
        if (TerrainInitialization.Instance == null)
        {
            return true;
        }
        
        // 检查目标位置是否可通行
        return TerrainInitialization.Instance.IsWalkable(targetPosition);
    }
    
    /// <summary>
    /// 修复玩家物理问题（Context Menu）
    /// </summary>
    [ContextMenu("修复玩家物理问题")]
    public void FixPlayerPhysics()
    {
        Debug.Log("[PlayerController] 开始修复玩家物理问题...");
        
        // 重新获取组件
        rb2d = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<BoxCollider2D>();
        
        // 重新配置物理组件
        ConfigurePhysicsComponents();
        
        // 检查位置是否合理
        Vector3 currentPos = transform.position;
        if (Mathf.Abs(currentPos.x) > 1000 || Mathf.Abs(currentPos.y) > 1000)
        {
            Debug.LogWarning($"[PlayerController] 玩家位置异常: {currentPos}，重置到原点");
            transform.position = Vector3.zero;
        }
        
        // 如果有Rigidbody2D，停止所有速度
        if (rb2d != null)
        {
            rb2d.velocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
            Debug.Log("[PlayerController] 已清零所有速度");
        }
        
        Debug.Log("[PlayerController] 物理问题修复完成！");
    }
    
    /// <summary>
    /// 强制重置玩家位置到原点
    /// </summary>
    [ContextMenu("重置玩家位置")]
    public void ResetPlayerPosition()
    {
        transform.position = Vector3.zero;
        if (rb2d != null)
        {
            rb2d.velocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }
        Debug.Log("[PlayerController] 玩家位置已重置到原点");
    }
    
    /// <summary>
    /// 切换物理移动模式
    /// </summary>
    [ContextMenu("切换物理移动模式")]
    public void TogglePhysicsMovement()
    {
        usePhysicsMovement = !usePhysicsMovement;
        ConfigurePhysicsComponents();
        Debug.Log($"[PlayerController] 物理移动模式切换为: {(usePhysicsMovement ? "启用" : "禁用")}");
    }
}