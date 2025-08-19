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
    
    // 移除了重复的键盘输入处理逻辑，现在由NineButtons.cs直接处理
    // 移除了battleButtons数组，避免功能重复
    
    void Start()
    {
        // 获取物理组件
        rb2d = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<BoxCollider2D>();
        
        // 配置物理组件以避免与Tilemap碰撞冲突
        ConfigurePhysicsComponents();
        
        Debug.Log($"[PlayerController] 玩家初始化完成，位置: {transform.position}, 使用物理移动: {usePhysicsMovement}");
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
                
                Debug.Log("[PlayerController] 已配置为运动学刚体，使用Transform移动");
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
                
                Debug.Log("[PlayerController] 已配置为动态刚体，使用物理移动");
            }
        }
        
        if (playerCollider != null)
        {
            // 设置碰撞体为触发器，避免物理碰撞
            playerCollider.isTrigger = true;
            Debug.Log("[PlayerController] 玩家碰撞体设置为触发器");
        }
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
        Debug.Log($"[PlayerController] 执行技能 {skillId}");

        // 统一的移动逻辑，支持持续移动和单次移动
        if (IsMovementSkill(skillId))
        {
            ExecuteMovement(skillId, Time.deltaTime); // 使用deltaTime支持持续移动
        }
        else
        {
            Debug.Log($"[技能{skillId}] 未知技能或未绑定");
        }
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
            Debug.Log($"[PlayerController] 无法移动到 {targetPosition}，地形不可通行");
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