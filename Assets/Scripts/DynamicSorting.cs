using UnityEngine;

/// <summary>
/// 物体类型枚举（用于快速配置参考位置）
/// </summary>
public enum ObjectType
{
    Custom = 0,     // 自定义配置
    Player = 1,     // 玩家角色
    Enemy = 2,      // 敌人
    Bush = 3,       // 灌木丛
    Building = 4    // 建筑物
}

/// <summary>
/// 动态排序组件 - 基于Y坐标自动调整渲染层级
/// Y坐标越小（越靠下），渲染层级越高（越靠前）
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DynamicSorting : MonoBehaviour
{
    [Header("动态排序设置")]
    [SerializeField] private int baseSortingOrder = 100;      // 基础排序层级
    [SerializeField] private float sortingPrecision = 10f;    // 排序精度（Y坐标乘数）
    [SerializeField] private bool updateEveryFrame = true;    // 是否每帧更新
    
    [Header("排序层级限制")]
    [SerializeField] private int minSortingOrder = -30000;    // 最小排序层级（但仍高于地面-32768）
    [SerializeField] private int maxSortingOrder = 32767;     // 最大排序层级（最大可能值）
    
    [Header("参考位置配置")]
    [SerializeField] private Vector2 sortingOffset = new Vector2(0f, -0.5f); // 排序计算的偏移量（默认向下偏移到脚部）
    [SerializeField] private bool showSortingPoint = true;    // 是否在Scene视图中显示排序点（默认开启）
    
    [Header("物体类型快速配置")]
    [SerializeField] private ObjectType objectType = ObjectType.Custom;      // 物体类型（用于快速配置参考位置）
    
    private SpriteRenderer spriteRenderer;
    private int lastSortingOrder = int.MinValue;
    private ObjectType lastObjectType = ObjectType.Custom;  // 记录上次的物体类型，用于检测变化
    
    private void Awake()
    {
        // 检查是否有重复的DynamicSorting组件
        DynamicSorting[] sortingComponents = GetComponents<DynamicSorting>();
        if (sortingComponents.Length > 1)
        {
            Debug.LogWarning($"[DynamicSorting] {gameObject.name} 有多个DynamicSorting组件！建议只保留一个。");
            
            // 如果这不是第一个组件，销毁自己
            if (sortingComponents[0] != this)
            {
                Debug.Log($"[DynamicSorting] 销毁重复的DynamicSorting组件");
                Destroy(this);
                return;
            }
        }
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"[DynamicSorting] {gameObject.name} 缺少 SpriteRenderer 组件！");
        }
    }
    
    private void Start()
    {
        // 初始化排序层级
        UpdateSortingOrder();
    }
    
    private void Update()
    {
        // 检查物体类型是否在Inspector中被修改
        if (objectType != lastObjectType)
        {
            ApplyObjectTypeConfiguration();
            lastObjectType = objectType;
        }
        
        if (updateEveryFrame)
        {
            UpdateSortingOrder();
        }
    }
    
    /// <summary>
    /// 更新排序层级
    /// </summary>
    public void UpdateSortingOrder()
    {
        if (spriteRenderer == null) return;
        
        // 计算用于排序的实际位置（加上偏移量）
        Vector3 sortingPosition = transform.position + (Vector3)sortingOffset;
        
        // 基于Y坐标计算排序层级
        // Y坐标越小（越靠下），排序层级越高（越靠前）
        int calculatedSortingOrder = baseSortingOrder - Mathf.RoundToInt(sortingPosition.y * sortingPrecision);
        
        // 限制排序层级在安全范围内，防止过小或过大导致渲染问题
        int newSortingOrder = Mathf.Clamp(calculatedSortingOrder, minSortingOrder, maxSortingOrder);
        
        // 如果计算值被限制了，输出警告
        if (calculatedSortingOrder != newSortingOrder)
        {
            Debug.LogWarning($"[DynamicSorting] {gameObject.name} 排序层级被限制: 计算值{calculatedSortingOrder} → 限制后{newSortingOrder} (Y:{sortingPosition.y:F2})");
        }
        
        // 只在排序层级改变时更新，避免不必要的性能消耗
        if (lastSortingOrder != newSortingOrder)
        {
            spriteRenderer.sortingOrder = newSortingOrder;
            lastSortingOrder = newSortingOrder;
            
            // Debug.Log($"[DynamicSorting] {gameObject.name} 排序Y:{sortingPosition.y:F2} → 排序层级:{newSortingOrder}");
        }
    }
    
    /// <summary>
    /// 获取排序计算位置
    /// </summary>
    public Vector3 GetSortingPosition()
    {
        return transform.position + (Vector3)sortingOffset;
    }
    
    /// <summary>
    /// 设置排序偏移量
    /// </summary>
    public void SetSortingOffset(Vector2 offset)
    {
        sortingOffset = offset;
        UpdateSortingOrder();
    }
    
    /// <summary>
    /// 设置基础排序层级
    /// </summary>
    public void SetBaseSortingOrder(int baseOrder)
    {
        baseSortingOrder = baseOrder;
        UpdateSortingOrder();
    }
    
    /// <summary>
    /// 获取当前排序层级
    /// </summary>
    public int GetCurrentSortingOrder()
    {
        return spriteRenderer != null ? spriteRenderer.sortingOrder : 0;
    }
    
    /// <summary>
    /// 根据物体类型应用预设配置
    /// </summary>
    private void ApplyObjectTypeConfiguration()
    {
        switch (objectType)
        {
            case ObjectType.Player:
                baseSortingOrder = 100;   // 正常基础层级
                // 保留 Inspector 中的 sortingOffset，不再强制为 (0,0)
                Debug.Log($"[DynamicSorting] {gameObject.name} 自动配置为Player类型 (基础层级: {baseSortingOrder}, 保留偏移: {sortingOffset})");
                break;
                
            case ObjectType.Enemy:
                baseSortingOrder = 80;    // 略低于Player
                // 保留 Inspector 配置，不再强制改写 Y 偏移
                Debug.Log($"[DynamicSorting] {gameObject.name} 自动配置为Enemy类型 (基础层级: {baseSortingOrder}, 偏移: {sortingOffset})");
                break;
                
            case ObjectType.Bush:
                baseSortingOrder = 50;    // 低于Player，确保能被Player遮挡，但能遮挡Player
                // 保留 Inspector 配置，不再强制改写 Y 偏移
                Debug.Log($"[DynamicSorting] {gameObject.name} 自动配置为Bush类型 (基础层级: {baseSortingOrder}, 偏移: {sortingOffset})");
                break;
                
            case ObjectType.Building:
                baseSortingOrder = 20;    // 最低基础层级
                // 保留 Inspector 配置，不再强制改写 Y 偏移
                Debug.Log($"[DynamicSorting] {gameObject.name} 自动配置为Building类型 (基础层级: {baseSortingOrder}, 偏移: {sortingOffset})");
                break;
                
            case ObjectType.Custom:
                Debug.Log($"[DynamicSorting] {gameObject.name} 设置为Custom类型，保持当前配置 (基础层级: {baseSortingOrder}, 偏移: {sortingOffset})");
                break;
        }
        
        // 立即更新排序层级
        UpdateSortingOrder();
    }
    
    /// <summary>
    /// 显示排序信息（调试用）
    /// </summary>
    [ContextMenu("显示排序信息")]
    public void ShowSortingInfo()
    {
        if (spriteRenderer != null)
        {
            Vector3 sortingPos = GetSortingPosition();
            int calculatedOrder = baseSortingOrder - Mathf.RoundToInt(sortingPos.y * sortingPrecision);
            int clampedOrder = Mathf.Clamp(calculatedOrder, minSortingOrder, maxSortingOrder);
            
            Debug.Log($"[DynamicSorting] === {gameObject.name} 排序信息 ===");
            Debug.Log($"[DynamicSorting] GameObject Y坐标: {transform.position.y:F3}");
            Debug.Log($"[DynamicSorting] 排序偏移量: {sortingOffset}");
            Debug.Log($"[DynamicSorting] 排序计算Y坐标: {sortingPos.y:F3}");
            Debug.Log($"[DynamicSorting] 基础排序层级: {baseSortingOrder}");
            Debug.Log($"[DynamicSorting] 计算排序层级: {calculatedOrder}");
            Debug.Log($"[DynamicSorting] 限制后排序层级: {clampedOrder}");
            Debug.Log($"[DynamicSorting] 当前排序层级: {spriteRenderer.sortingOrder}");
            Debug.Log($"[DynamicSorting] 排序精度: {sortingPrecision}");
            Debug.Log($"[DynamicSorting] 排序范围: [{minSortingOrder}, {maxSortingOrder}]");
            Debug.Log($"[DynamicSorting] 每帧更新: {updateEveryFrame}");
            
            // 查找并比较Player和Bush
            CompareWithOtherObjects();
        }
    }
    
    /// <summary>
    /// 测试高Y坐标排序（调试用）
    /// </summary>
    [ContextMenu("测试高Y坐标排序")]
    public void TestHighYSorting()
    {
        Debug.Log($"[DynamicSorting] === {gameObject.name} 高Y坐标测试 ===");
        
        float[] testYValues = { 0f, 1f, 1.5f, 2f, 3f, 5f, 10f };
        
        foreach (float testY in testYValues)
        {
            Vector3 testPos = new Vector3(transform.position.x, testY, transform.position.z);
            Vector3 sortingPos = testPos + (Vector3)sortingOffset;
            int calculatedOrder = baseSortingOrder - Mathf.RoundToInt(sortingPos.y * sortingPrecision);
            int clampedOrder = Mathf.Clamp(calculatedOrder, minSortingOrder, maxSortingOrder);
            
            string status = (calculatedOrder == clampedOrder) ? "正常" : "被限制";
            Debug.Log($"[DynamicSorting] Y={testY:F1} → 计算值:{calculatedOrder} → 最终值:{clampedOrder} ({status})");
        }
    }
    
    /// <summary>
    /// 比较与其他对象的排序层级
    /// </summary>
    private void CompareWithOtherObjects()
    {
        DynamicSorting[] allSorting = FindObjectsOfType<DynamicSorting>();
        Debug.Log($"[DynamicSorting] === 与其他对象排序比较 ===");
        
        foreach (var other in allSorting)
        {
            if (other != this && other.spriteRenderer != null)
            {
                Vector3 otherSortingPos = other.GetSortingPosition();
                int otherSortingOrder = other.spriteRenderer.sortingOrder;
                
                string relationship = spriteRenderer.sortingOrder > otherSortingOrder ? "在前面" : 
                                    spriteRenderer.sortingOrder < otherSortingOrder ? "在后面" : "同层级";
                
                Debug.Log($"[DynamicSorting] {gameObject.name} 相对 {other.gameObject.name}: {relationship}");
                Debug.Log($"[DynamicSorting]   - {gameObject.name}: Y={GetSortingPosition().y:F2}, Sort={spriteRenderer.sortingOrder}");
                Debug.Log($"[DynamicSorting]   - {other.gameObject.name}: Y={otherSortingPos.y:F2}, Sort={otherSortingOrder}");
            }
        }
    }
    
    /// <summary>
    /// 快速配置为Player排序
    /// </summary>
    [ContextMenu("配置为Player排序")]
    public void ConfigureForPlayer()
    {
        baseSortingOrder = 100;  // 正常基础层级
        // 不再强制写入 sortingOffset，保留 Inspector 设置
        showSortingPoint = true;
        UpdateSortingOrder();
        Debug.Log($"[DynamicSorting] {gameObject.name} 已配置为Player排序设置 (基础层级: {baseSortingOrder}, 保留偏移: {sortingOffset})");
    }
    
    /// <summary>
    /// 快速配置为Bush排序
    /// </summary>
    [ContextMenu("配置为Bush排序")]
    public void ConfigureForBush()
    {
        baseSortingOrder = 50;    // 低于Player，确保正常Y排序遮挡
        // 不再强制写入 sortingOffset，保留 Inspector 设置
        showSortingPoint = true;
        UpdateSortingOrder();
        Debug.Log($"[DynamicSorting] {gameObject.name} 已配置为Bush排序设置 (基础层级: {baseSortingOrder}, 偏移: {sortingOffset})");
    }
    
    /// <summary>
    /// 快速配置为Enemy排序
    /// </summary>
    [ContextMenu("配置为Enemy排序")]
    public void ConfigureForEnemy()
    {
        baseSortingOrder = 80;    // 略低于Player
        // 不再强制写入 sortingOffset，保留 Inspector 设置
        showSortingPoint = true;
        UpdateSortingOrder();
        Debug.Log($"[DynamicSorting] {gameObject.name} 已配置为Enemy排序设置 (基础层级: {baseSortingOrder}, 偏移: {sortingOffset})");
    }
    
    /// <summary>
    /// 修复Player高Y坐标消失问题
    /// </summary>
    [ContextMenu("修复Player高Y坐标消失问题")]
    public void FixPlayerHighYDisappearance()
    {
        Debug.Log($"[DynamicSorting] 修复Player高Y坐标消失问题...");
        
        // 设置绝对安全的参数，确保永远不会被地面遮挡
        baseSortingOrder = 100;   // 保持正常基础层级
        sortingPrecision = 10f;   // 保持正常精度
        minSortingOrder = -30000; // 绝对安全范围，但仍高于地面-32768
        maxSortingOrder = 32767;  // 最大可能值
        // 不再强制写入 sortingOffset，保留 Inspector 设置
        
        UpdateSortingOrder();
        
        Vector3 sortingPos = GetSortingPosition();
        int calculatedOrder = baseSortingOrder - Mathf.RoundToInt(sortingPos.y * sortingPrecision);
        int finalOrder = Mathf.Clamp(calculatedOrder, minSortingOrder, maxSortingOrder);
        
        Debug.Log($"[DynamicSorting] 修复完成！");
        Debug.Log($"[DynamicSorting] 当前Y坐标: {transform.position.y:F2}");
        Debug.Log($"[DynamicSorting] 基础层级: {baseSortingOrder}");
        Debug.Log($"[DynamicSorting] 计算层级: {calculatedOrder}");
        Debug.Log($"[DynamicSorting] 最终层级: {finalOrder}");
        Debug.Log($"[DynamicSorting] 现在Player在任何Y坐标都不会消失了！");
    }
    
    /// <summary>
    /// 测试极端Y坐标情况
    /// </summary>
    [ContextMenu("🧪 测试极端Y坐标")]
    public void TestExtremeYCoordinates()
    {
        Debug.Log($"[DynamicSorting] === 极端Y坐标测试 ===");
        
        float[] testYValues = { 11f, 20f, 50f, 100f, -20f, -50f };
        
        foreach (float testY in testYValues)
        {
            // 模拟计算
            int calculatedOrder = baseSortingOrder - Mathf.RoundToInt(testY * sortingPrecision);
            int finalOrder = Mathf.Clamp(calculatedOrder, minSortingOrder, maxSortingOrder);
            
            Debug.Log($"[DynamicSorting] Y={testY:F1}: 计算层级={calculatedOrder}, 最终层级={finalOrder}");
            Debug.Log($"[DynamicSorting]   vs 草地(-32768): 差距={finalOrder - (-32768)}");
            Debug.Log($"[DynamicSorting]   vs 水域(-32767): 差距={finalOrder - (-32767)}");
        }
        
        Debug.Log($"[DynamicSorting] === 当前实际状态 ===");
        Vector3 currentPos = transform.position;
        int currentCalculated = baseSortingOrder - Mathf.RoundToInt(currentPos.y * sortingPrecision);
        int currentFinal = Mathf.Clamp(currentCalculated, minSortingOrder, maxSortingOrder);
        
        Debug.Log($"[DynamicSorting] 当前Y={currentPos.y:F2}: 层级={currentFinal}");
        Debug.Log($"[DynamicSorting] 草地层级: -32768");
        Debug.Log($"[DynamicSorting] 水域层级: -32767");
        Debug.Log($"[DynamicSorting] 安全差距: {currentFinal - (-32768)} (应该 > 0)");
    }
    
    /// <summary>
    /// 在Scene视图中绘制排序点
    /// </summary>
    private void OnDrawGizmos()
    {
        if (showSortingPoint)
        {
            Vector3 sortingPos = GetSortingPosition();
            
            // 绘制排序点
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(sortingPos, 0.15f);
            
            // 绘制从GameObject中心到排序点的线
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, sortingPos);
            
            // 绘制排序层级标签
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(sortingPos + Vector3.right * 0.3f, $"Sort: {(spriteRenderer != null ? spriteRenderer.sortingOrder : 0)}");
            #endif
        }
    }
}
