using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    [Header("敌人AI配置")]
    public Transform target;                    // 追踪目标（玩家）
    public float moveSpeed = 2f;               // 移动速度
    public float detectionRange = 8f;          // 检测范围
    public float stopDistance = 1.5f;          // 停止距离（到一定距离后停下）

    [Header("移动优化")]
    public float acceleration = 10f;           // 加速度，控制启动和停止的平滑度
    public float velocityThreshold = 0.01f;    // 速度阈值，低于此值视为停止
    
    [Header("渲染配置")]
    public float fixedZPosition = 0f;          // 固定的Z轴位置，防止深度冲突

    private BoxCollider2D enemyCol;
    private Rigidbody2D rb2d;                  // 2D刚体组件
    private SpriteRenderer spriteRenderer;     // 精灵渲染器
    private bool canMove = true;               // 是否允许移动
    private Vector2 moveDirection;             // 移动方向

    void Start()
    {
        // 获取组件
        enemyCol = GetComponent<BoxCollider2D>();
        rb2d = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // 配置Rigidbody2D
        if (rb2d != null)
        {
            rb2d.gravityScale = 0f;           // 关闭重力
            rb2d.drag = 5f;                   // 适度阻力，帮助平滑停止
            rb2d.angularDrag = 0f;            // 无角阻力
            rb2d.freezeRotation = true;       // 锁定旋转
            rb2d.bodyType = RigidbodyType2D.Dynamic; // 动态刚体
            rb2d.interpolation = RigidbodyInterpolation2D.Interpolate; // 启用插值，减少卡顿
            rb2d.sleepMode = RigidbodySleepMode2D.NeverSleep; // 永不休眠，避免突然停止
        }
        
        // 确保Z轴位置固定
        Vector3 pos = transform.position;
        pos.z = fixedZPosition;
        transform.position = pos;
        
        // 检查必要组件
        if (spriteRenderer == null)
        {
            Debug.LogWarning($"[Enemy] {gameObject.name} 缺少SpriteRenderer组件，可能导致渲染问题");
        }
        
        if (rb2d == null)
        {
            Debug.LogError($"[Enemy] {gameObject.name} 缺少Rigidbody2D组件！");
        }
        
        Debug.Log($"[Enemy] {gameObject.name} 初始化完成，位置: {transform.position}");
    }

    void Update()
    {
        // Update中只计算移动方向，实际移动在FixedUpdate中进行
        if (!canMove || target == null || rb2d == null)
        {
            moveDirection = Vector2.zero;
            return;
        }

        float dist = Vector2.Distance(transform.position, target.position);
        if (dist <= detectionRange && dist > stopDistance)
        {
            // 计算移动方向
            Vector2 desiredDirection = (target.position - transform.position).normalized;
            
            // 检查移动方向是否可通行
            Vector3 targetPosition = transform.position + (Vector3)desiredDirection * moveSpeed * Time.deltaTime;
            
            if (CanMoveTo(targetPosition))
            {
                moveDirection = desiredDirection;
            }
            else
            {
                // 如果直接路径被阻挡，尝试寻找替代路径
                moveDirection = FindAlternativeDirection(desiredDirection);
            }
        }
        else
        {
            moveDirection = Vector2.zero;
        }
    }

    void FixedUpdate()
    {
        // 在FixedUpdate中进行物理移动
        if (rb2d == null) return;

        // 计算目标速度
        Vector2 targetVelocity = moveDirection * moveSpeed;
        
        // 使用平滑加速度来改变速度，而不是直接设置
        Vector2 velocityChange = targetVelocity - rb2d.velocity;
        
        // 限制加速度，避免突然的速度变化
        float maxVelocityChange = acceleration * Time.fixedDeltaTime;
        velocityChange = Vector2.ClampMagnitude(velocityChange, maxVelocityChange);
        
        // 应用速度变化
        rb2d.velocity += velocityChange;
        
        // 如果速度很小，直接设为零，避免微小抖动
        if (rb2d.velocity.magnitude < velocityThreshold)
        {
            rb2d.velocity = Vector2.zero;
        }
        
        // 确保Z轴位置始终固定
        if (Mathf.Abs(transform.position.z - fixedZPosition) > 0.001f)
        {
            Vector3 pos = transform.position;
            pos.z = fixedZPosition;
            transform.position = pos;
        }
    }

    // ⭐ 外部接口：控制敌人能否移动
    public void SetCanMove(bool state)
    {
        canMove = state;
        Debug.Log($"[Enemy] {gameObject.name} 移动状态设置为: {(canMove ? "启用" : "禁用")}");
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
    /// 寻找替代移动方向（简单的避障逻辑）
    /// </summary>
    private Vector2 FindAlternativeDirection(Vector2 desiredDirection)
    {
        // 尝试几个替代方向
        Vector2[] alternativeDirections = {
            new Vector2(desiredDirection.x, 0).normalized,  // 只水平移动
            new Vector2(0, desiredDirection.y).normalized,  // 只垂直移动
            new Vector2(desiredDirection.x + 0.5f, desiredDirection.y).normalized,  // 稍微偏右
            new Vector2(desiredDirection.x - 0.5f, desiredDirection.y).normalized,  // 稍微偏左
            new Vector2(desiredDirection.x, desiredDirection.y + 0.5f).normalized,  // 稍微偏上
            new Vector2(desiredDirection.x, desiredDirection.y - 0.5f).normalized   // 稍微偏下
        };
        
        foreach (var altDir in alternativeDirections)
        {
            Vector3 altTargetPos = transform.position + (Vector3)altDir * moveSpeed * Time.deltaTime;
            if (CanMoveTo(altTargetPos))
            {
                return altDir;
            }
        }
        
        // 如果所有方向都被阻挡，停止移动
        return Vector2.zero;
    }

    /// <summary>
    /// 强制设置敌人位置并确保渲染正确
    /// </summary>
    public void SetPosition(Vector3 newPosition)
    {
        newPosition.z = fixedZPosition;
        
        // 停止物理移动
        if (rb2d != null)
        {
            rb2d.velocity = Vector2.zero;
        }
        
        transform.position = newPosition;
        
        // 确保渲染器可见
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }
        
        Debug.Log($"[Enemy] {gameObject.name} 位置设置为: {transform.position}");
    }

    /// <summary>
    /// 检查并修复渲染问题
    /// </summary>
    [ContextMenu("检查渲染状态")]
    public void CheckRenderingState()
    {
        Debug.Log($"[Enemy] {gameObject.name} 渲染状态检查:");
        Debug.Log($"  - GameObject激活: {gameObject.activeSelf}");
        Debug.Log($"  - Transform位置: {transform.position}");
        Debug.Log($"  - 可以移动: {canMove}");
        Debug.Log($"  - 移动方向: {moveDirection}");
        
        if (rb2d != null)
        {
            Debug.Log($"  - Rigidbody2D速度: {rb2d.velocity}");
            Debug.Log($"  - Rigidbody2D类型: {rb2d.bodyType}");
            Debug.Log($"  - 重力缩放: {rb2d.gravityScale}");
        }
        else
        {
            Debug.LogError($"  - Rigidbody2D组件缺失！");
        }
        
        if (spriteRenderer != null)
        {
            Debug.Log($"  - SpriteRenderer启用: {spriteRenderer.enabled}");
            Debug.Log($"  - Sprite: {(spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "null")}");
            Debug.Log($"  - 颜色: {spriteRenderer.color}");
            Debug.Log($"  - 排序层: {spriteRenderer.sortingLayerName}");
            Debug.Log($"  - 排序顺序: {spriteRenderer.sortingOrder}");
        }
        else
        {
            Debug.LogError($"  - SpriteRenderer组件缺失！");
        }
    }

    /// <summary>
    /// 强制修复渲染问题
    /// </summary>
    [ContextMenu("修复渲染问题")]
    public void FixRenderingIssues()
    {
        Debug.Log($"[Enemy] {gameObject.name} 修复渲染问题...");
        
        // 确保GameObject激活
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            Debug.Log("  - 已激活GameObject");
        }
        
        // 确保Z轴位置正确
        Vector3 pos = transform.position;
        pos.z = fixedZPosition;
        transform.position = pos;
        Debug.Log($"  - Z轴位置设置为: {fixedZPosition}");
        
        // 确保Rigidbody2D正常
        if (rb2d == null)
        {
            rb2d = GetComponent<Rigidbody2D>();
        }
        
        if (rb2d != null)
        {
            rb2d.gravityScale = 0f;
            rb2d.drag = 5f;
            rb2d.angularDrag = 0f;
            rb2d.freezeRotation = true;
            rb2d.bodyType = RigidbodyType2D.Dynamic;
            rb2d.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb2d.sleepMode = RigidbodySleepMode2D.NeverSleep;
            Debug.Log("  - Rigidbody2D已修复");
        }
        else
        {
            Debug.LogError("  - 无法找到Rigidbody2D组件！");
        }
        
        // 确保SpriteRenderer正常
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            Color color = spriteRenderer.color;
            color.a = 1f;
            spriteRenderer.color = color;
            Debug.Log("  - SpriteRenderer已修复");
        }
        else
        {
            Debug.LogError("  - 无法找到SpriteRenderer组件！");
        }
    }

    /// <summary>
    /// 强制停止移动
    /// </summary>
    [ContextMenu("强制停止移动")]
    public void ForceStop()
    {
        moveDirection = Vector2.zero;
        if (rb2d != null)
        {
            rb2d.velocity = Vector2.zero;
        }
        Debug.Log($"[Enemy] {gameObject.name} 已强制停止移动");
    }

    /// <summary>
    /// 测试移动性能
    /// </summary>
    [ContextMenu("测试移动性能")]
    public void TestMovementPerformance()
    {
        Debug.Log($"[Enemy] {gameObject.name} 移动性能测试:");
        Debug.Log($"  - 当前帧率: {1f / Time.unscaledDeltaTime:F1} FPS");
        Debug.Log($"  - Fixed时间步长: {Time.fixedDeltaTime:F4}s");
        Debug.Log($"  - 加速度设置: {acceleration}");
        Debug.Log($"  - 速度阈值: {velocityThreshold}");
        Debug.Log($"  - 当前速度大小: {(rb2d != null ? rb2d.velocity.magnitude.ToString("F3") : "N/A")}");
        Debug.Log($"  - 目标速度大小: {(moveDirection * moveSpeed).magnitude:F3}");
        
        if (rb2d != null)
        {
            Debug.Log($"  - Rigidbody2D插值: {rb2d.interpolation}");
            Debug.Log($"  - 休眠模式: {rb2d.sleepMode}");
            Debug.Log($"  - 阻力系数: {rb2d.drag}");
        }
    }
}