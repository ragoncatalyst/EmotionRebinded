using UnityEngine;

namespace MyGame.Environment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(FacingCamera))]
    [RequireComponent(typeof(DynamicSorting))]
    public class Bush : MonoBehaviour
    {
        [Header("障碍物配置")]
        [SerializeField] private bool isDestructible = false;     // 是否可破坏
        [SerializeField] private int health = 3;                  // 血量（如果可破坏）

        
        [Header("视觉效果")]
        [SerializeField] private float swayAmount = 0.1f;         // 基础摇摆幅度
        [SerializeField] private float swaySpeed = 0.8f;          // 基础摇摆速度（降低）
        [SerializeField] private bool enableSway = true;          // 是否启用摇摆效果
        
        [Header("碰撞摇摆效果")]
        [SerializeField] private float collisionSwayAmount = 0.3f;  // 碰撞时的摇摆幅度（增强）
        [SerializeField] private float collisionSwaySpeed = 6f;     // 碰撞时的摇摆速度（增强）
        [SerializeField] private float collisionSwayDuration = 1.5f; // 碰撞摇摆持续时间
        
        [Header("碰撞检测")]
        [SerializeField] private LayerMask playerLayer = 1 << 6;  // 玩家层级
        [SerializeField] private LayerMask enemyLayer = 1 << 7;   // 敌人层级

        
        [Header("深度设置")]
        [SerializeField] private float fixedZPosition = 0f;       // 固定Z轴位置
        [SerializeField] private int baseSortingOrder = 0;        // 基础渲染层级
        
        // 私有变量
        private SpriteRenderer spriteRenderer;
        private BoxCollider2D boxCollider;
        private FacingCamera facingCamera;
        private DynamicSorting dynamicSorting;
        private Vector3 originalPosition;
        private Vector3 originalScale;
        private int currentHealth;
        private bool isDestroyed = false;
        
        // 摇摆动画相关
        private float swayTimer = 0f;
        private bool isSwaying = false;
        private float swayStartTime = 0f;
        
        private void Awake()
        {
            // 获取必要组件
            spriteRenderer = GetComponent<SpriteRenderer>();
            boxCollider = GetComponent<BoxCollider2D>();
            facingCamera = GetComponent<FacingCamera>();
            dynamicSorting = GetComponent<DynamicSorting>();
            
            // 检查组件是否存在
            if (spriteRenderer == null)
            {
                Debug.LogError($"[Bush] {gameObject.name} 缺少 SpriteRenderer 组件！");
            }
            else if (spriteRenderer.sprite == null)
            {
                Debug.LogWarning($"[Bush] {gameObject.name} 的 SpriteRenderer 没有分配 Sprite！请在Inspector中分配图片。");
                // 创建一个临时的默认方块作为占位符
                CreateDefaultSprite();
            }
            
            if (boxCollider == null)
            {
                Debug.LogError($"[Bush] {gameObject.name} 缺少 BoxCollider2D 组件！");
            }
            
            if (facingCamera == null)
            {
                Debug.LogError($"[Bush] {gameObject.name} 缺少 FacingCamera 组件！");
            }
            
            if (dynamicSorting == null)
            {
                Debug.LogError($"[Bush] {gameObject.name} 缺少 DynamicSorting 组件！");
            }
            
            // 保存原始状态
            originalPosition = transform.position;
            originalScale = transform.localScale;
            currentHealth = health;
            
            // 设置碰撞体为触发器（用于检测碰撞但不物理阻挡）
            boxCollider.isTrigger = true; // 设为true，不阻挡移动，只检测碰撞
            
            // 确保三渲二设置：固定Z轴位置和正确的旋转
            Vector3 pos = transform.position;
            pos.z = fixedZPosition;
            transform.position = pos;
            
            // 设置初始旋转（面向摄像机的三渲二角度）
            transform.rotation = Quaternion.Euler(-45f, 0f, 0f);
            
            Debug.Log($"[Bush] {gameObject.name} 初始化完成，位置: {transform.position}，旋转: {transform.rotation.eulerAngles}");
            Debug.Log($"[Bush] SpriteRenderer: {(spriteRenderer != null ? "存在" : "缺失")}");
            Debug.Log($"[Bush] Sprite: {(spriteRenderer != null && spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "未分配")}");
            Debug.Log($"[Bush] FacingCamera: {(facingCamera != null ? "存在" : "缺失")}");
        }
        
        private void Start()
        {
            // 配置动态排序组件
            if (dynamicSorting != null)
            {
                dynamicSorting.SetBaseSortingOrder(baseSortingOrder);
            }
            
            // 随机化摇摆起始时间，避免所有灌木同步摇摆
            swayTimer = Random.Range(0f, Mathf.PI * 2f);
        }
        
        private void Update()
        {
            // 确保Z轴位置始终固定
            if (transform.position.z != fixedZPosition)
            {
                Vector3 pos = transform.position;
                pos.z = fixedZPosition;
                transform.position = pos;
            }
            
            // 摇摆动画
            if (enableSway && !isDestroyed)
            {
                UpdateSwayAnimation();
            }
            
            // DynamicSorting 组件会自动处理渲染层级更新
        }
        
        /// <summary>
        /// 创建默认的占位符Sprite
        /// </summary>
        private void CreateDefaultSprite()
        {
            // 创建一个简单的绿色方块作为占位符
            Texture2D texture = new Texture2D(64, 64);
            Color[] pixels = new Color[64 * 64];
            
            // 填充绿色
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0.2f, 0.8f, 0.3f, 1f); // 绿色
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            // 创建Sprite
            Sprite defaultSprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            defaultSprite.name = "DefaultBushSprite";
            
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = defaultSprite;
                Debug.Log($"[Bush] {gameObject.name} 已创建默认占位符Sprite");
            }
        }
        
        /// <summary>
        /// 更新摇摆动画
        /// </summary>
        private void UpdateSwayAnimation()
        {
            swayTimer += Time.deltaTime * swaySpeed;
            
            // 基础轻微摇摆（降低了速度）
            float baseSway = Mathf.Sin(swayTimer) * swayAmount * 0.3f;
            
            // 如果正在摇摆（被撞击后），添加强化摇摆
            float collisionSway = 0f;
            if (isSwaying)
            {
                float swayElapsed = Time.time - swayStartTime;
                float swayProgress = swayElapsed / collisionSwayDuration;
                
                if (swayProgress <= 1f)
                {
                    // 使用增强的幅度和速度进行碰撞摇摆
                    float intensityFalloff = 1f - swayProgress; // 强度随时间衰减
                    float fastOscillation = Mathf.Sin(swayElapsed * collisionSwaySpeed) * intensityFalloff;
                    collisionSway = fastOscillation * collisionSwayAmount;
                }
                else
                {
                    isSwaying = false;
                }
            }
            
            // 应用摇摆效果 - 始终基于原始位置
            Vector3 swayOffset = new Vector3(baseSway + collisionSway, 0f, 0f);
            transform.position = originalPosition + swayOffset;
        }
        
        /// <summary>
        /// 显示渲染层级调试信息
        /// </summary>
        [ContextMenu("显示渲染层级")]
        public void ShowSortingOrder()
        {
            if (dynamicSorting != null)
            {
                dynamicSorting.ShowSortingInfo();
            }
            
            // 查找附近的Player和Enemy进行比较
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null && spriteRenderer != null)
            {
                SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
                if (playerRenderer != null)
                {
                    Debug.Log($"[Bush] === 与Player比较 ===");
                    Debug.Log($"[Bush] Player Y坐标: {player.transform.position.y:F3}, 排序层级: {playerRenderer.sortingOrder}");
                    Debug.Log($"[Bush] Bush Y坐标: {transform.position.y:F3}, 排序层级: {spriteRenderer.sortingOrder}");
                    bool bushInFront = spriteRenderer.sortingOrder > playerRenderer.sortingOrder;
                    Debug.Log($"[Bush] Bush相对Player: {(bushInFront ? "在前面" : "在后面")}");
                }
            }
        }
        
        /// <summary>
        /// 强制重新配置为Bush排序
        /// </summary>
        [ContextMenu("强制配置Bush排序")]
        public void ForceConfigureBushSorting()
        {
            Debug.Log($"[Bush] === 开始强制配置Bush排序 ===");
            
            if (dynamicSorting == null)
            {
                Debug.LogError($"[Bush] {gameObject.name} 缺少DynamicSorting组件！");
                return;
            }
            
            // 手动设置Bush专用的排序参数
            dynamicSorting.SetBaseSortingOrder(500);  // Bush中等基础层级
            dynamicSorting.SetSortingOffset(new Vector2(0f, -0.2f)); // Bush底部偏移
            
            Debug.Log($"[Bush] {gameObject.name} 已强制配置为Bush排序:");
            Debug.Log($"[Bush]   - 基础排序层级: 500");
            Debug.Log($"[Bush]   - 排序偏移: (0, -0.2)");
            
            // 立即更新排序
            dynamicSorting.UpdateSortingOrder();
            
            // 显示最终结果
            Debug.Log($"[Bush] 配置完成后的排序层级: {spriteRenderer.sortingOrder}");
            
            // 显示比较结果
            ShowSortingOrder();
        }
        
        /// <summary>
        /// 被触碰时调用（触发器模式）
        /// </summary>
        /// <param name="other">触碰的碰撞体</param>
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 检查是否是玩家或敌人触碰
            bool isPlayerHit = ((1 << other.gameObject.layer) & playerLayer) != 0;
            bool isEnemyHit = ((1 << other.gameObject.layer) & enemyLayer) != 0;
            
            if (isPlayerHit || isEnemyHit)
            {
                // 触发摇摆效果
                TriggerSway();
                
                Debug.Log($"[Bush] {gameObject.name} 被 {other.gameObject.name} 触碰，触发摇摆");
                
                // 如果可破坏，减少血量
                if (isDestructible && !isDestroyed)
                {
                    TakeDamage(1);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            // 玩家或敌人离开时也触发一次强化摇摆
            bool isPlayer = ((1 << other.gameObject.layer) & playerLayer) != 0;
            bool isEnemy  = ((1 << other.gameObject.layer) & enemyLayer) != 0;
            if (isPlayer || isEnemy)
            {
                TriggerSway();
                Debug.Log($"[Bush] {gameObject.name} 结束碰撞于 {other.gameObject.name}，再次触发摇摆");
            }
        }
        
        /// <summary>
        /// 触发摇摆效果
        /// </summary>
        public void TriggerSway()
        {
            if (!isDestroyed)
            {
                isSwaying = true;
                swayStartTime = Time.time; // 记录摇摆开始时间
                Debug.Log($"[Bush] {gameObject.name} 开始强化摇摆，幅度: {collisionSwayAmount}, 速度: {collisionSwaySpeed}, 持续: {collisionSwayDuration}s");
            }
        }
        
        /// <summary>
        /// 受到伤害
        /// </summary>
        /// <param name="damage">伤害值</param>
        public void TakeDamage(int damage)
        {
            if (isDestroyed || !isDestructible) return;
            
            currentHealth -= damage;
            Debug.Log($"[Bush] {gameObject.name} 受到 {damage} 伤害，剩余血量: {currentHealth}");
            
            // 血量归零时销毁
            if (currentHealth <= 0)
            {
                DestroyBush();
            }
            else
            {
                // 受伤效果：短暂变色
                StartCoroutine(DamageFlash());
            }
        }
        
        /// <summary>
        /// 销毁灌木
        /// </summary>
        private void DestroyBush()
        {
            if (isDestroyed) return;
            
            isDestroyed = true;
            Debug.Log($"[Bush] {gameObject.name} 被摧毁");
            
            // 播放销毁动画
            StartCoroutine(DestructionAnimation());
        }
        
        /// <summary>
        /// 受伤闪烁效果
        /// </summary>
        private System.Collections.IEnumerator DamageFlash()
        {
            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = Color.red;
            
            yield return new WaitForSeconds(0.1f);
            
            spriteRenderer.color = originalColor;
        }
        
        /// <summary>
        /// 销毁动画
        /// </summary>
        private System.Collections.IEnumerator DestructionAnimation()
        {
            float duration = 0.5f;
            float elapsed = 0f;
            
            Vector3 startScale = transform.localScale;
            Color startColor = spriteRenderer.color;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                
                // 缩放和透明度动画
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
                spriteRenderer.color = Color.Lerp(startColor, Color.clear, progress);
                
                yield return null;
            }
            
            // 销毁游戏对象
            Destroy(gameObject);
        }
        
        /// <summary>
        /// 重置灌木状态（用于调试）
        /// </summary>
        [ContextMenu("重置灌木")]
        public void ResetBush()
        {
            currentHealth = health;
            isDestroyed = false;
            isSwaying = false;
            transform.position = originalPosition;
            transform.localScale = originalScale;
            
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
            
            Debug.Log($"[Bush] {gameObject.name} 已重置");
        }
        
        /// <summary>
        /// 测试摇摆效果
        /// </summary>
        [ContextMenu("测试摇摆")]
        public void TestSway()
        {
            TriggerSway();
        }
        
        /// <summary>
        /// 测试受伤效果
        /// </summary>
        [ContextMenu("测试受伤")]
        public void TestDamage()
        {
            TakeDamage(1);
        }
        
        /// <summary>
        /// 获取当前状态信息
        /// </summary>
        [ContextMenu("显示状态")]
        public void ShowStatus()
        {
            Debug.Log($"[Bush] === {gameObject.name} 状态信息 ===");
            Debug.Log($"[Bush] 位置: {transform.position}");
            Debug.Log($"[Bush] 血量: {currentHealth}/{health}");
            Debug.Log($"[Bush] 是否可破坏: {isDestructible}");
            Debug.Log($"[Bush] 是否已摧毁: {isDestroyed}");
            Debug.Log($"[Bush] 渲染层级: {(spriteRenderer != null ? spriteRenderer.sortingOrder.ToString() : "null")}");
            Debug.Log($"[Bush] SpriteRenderer: {(spriteRenderer != null ? "存在" : "缺失")}");
            Debug.Log($"[Bush] Sprite: {(spriteRenderer != null && spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "未分配")}");
            Debug.Log($"[Bush] SpriteRenderer颜色: {(spriteRenderer != null ? spriteRenderer.color.ToString() : "N/A")}");
            Debug.Log($"[Bush] GameObject激活: {gameObject.activeInHierarchy}");
        }
        
        /// <summary>
        /// 检查并修复组件配置
        /// </summary>
        [ContextMenu("检查组件配置")]
        public void CheckComponents()
        {
            Debug.Log($"[Bush] === 检查 {gameObject.name} 组件配置 ===");
            
            // 基础信息
            Debug.Log($"[Bush] GameObject 激活状态: {gameObject.activeInHierarchy}");
            Debug.Log($"[Bush] Transform 位置: {transform.position}");
            Debug.Log($"[Bush] Transform 缩放: {transform.localScale}");
            Debug.Log($"[Bush] Transform 旋转: {transform.rotation.eulerAngles}");
            
            // 检查SpriteRenderer
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    Debug.LogError("[Bush] 缺少 SpriteRenderer 组件！请添加 SpriteRenderer 组件。");
                    return;
                }
            }
            
            if (spriteRenderer != null)
            {
                Debug.Log($"[Bush] SpriteRenderer 启用状态: {spriteRenderer.enabled}");
                Debug.Log($"[Bush] SpriteRenderer 颜色: {spriteRenderer.color}");
                Debug.Log($"[Bush] SpriteRenderer 渲染层级: {spriteRenderer.sortingOrder}");
                Debug.Log($"[Bush] SpriteRenderer 排序层: {spriteRenderer.sortingLayerName}");
                
                if (spriteRenderer.sprite == null)
                {
                    Debug.LogWarning("[Bush] SpriteRenderer 没有分配 Sprite！创建默认占位符...");
                    CreateDefaultSprite();
                }
                else
                {
                    Debug.Log($"[Bush] Sprite: {spriteRenderer.sprite.name}");
                    Debug.Log($"[Bush] Sprite 尺寸: {spriteRenderer.sprite.rect}");
                }
            }
            
            // 检查BoxCollider2D
            if (boxCollider == null)
            {
                boxCollider = GetComponent<BoxCollider2D>();
                if (boxCollider == null)
                {
                    Debug.LogError("[Bush] 缺少 BoxCollider2D 组件！请添加 BoxCollider2D 组件。");
                }
                else
                {
                    Debug.Log($"[Bush] BoxCollider2D 正常，尺寸: {boxCollider.size}");
                }
            }
        }
        
        /// <summary>
        /// 强制显示Bush（调试用）
        /// </summary>
        [ContextMenu("强制显示")]
        public void ForceVisible()
        {
            Debug.Log($"[Bush] 强制显示 {gameObject.name}");
            
            // 确保GameObject激活
            gameObject.SetActive(true);
            
            // 如果缺少SpriteRenderer，添加它
            if (spriteRenderer == null)
            {
                Debug.Log("[Bush] 添加缺失的 SpriteRenderer 组件");
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
            
            // 如果缺少BoxCollider2D，添加它
            if (boxCollider == null)
            {
                Debug.Log("[Bush] 添加缺失的 BoxCollider2D 组件");
                boxCollider = gameObject.AddComponent<BoxCollider2D>();
                boxCollider.size = Vector2.one; // 设置默认大小
                boxCollider.isTrigger = true; // 设为触发器，不阻挡移动
            }
            
            // 如果缺少FacingCamera，添加它
            if (facingCamera == null)
            {
                Debug.Log("[Bush] 添加缺失的 FacingCamera 组件");
                facingCamera = gameObject.AddComponent<FacingCamera>();
            }
            
            // 如果缺少DynamicSorting，添加它
            if (dynamicSorting == null)
            {
                Debug.Log("[Bush] 添加缺失的 DynamicSorting 组件");
                dynamicSorting = gameObject.AddComponent<DynamicSorting>();
                dynamicSorting.SetBaseSortingOrder(baseSortingOrder);
            }
            
            // 确保SpriteRenderer启用且可见
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = Color.white; // 确保不透明
                
                // 设置高渲染层级确保可见
                spriteRenderer.sortingOrder = 100;
                
                // 如果没有Sprite，创建一个
                if (spriteRenderer.sprite == null)
                {
                    Debug.Log("[Bush] 创建默认Sprite");
                    CreateDefaultSprite();
                }
                
                Debug.Log($"[Bush] SpriteRenderer 已强制启用，渲染层级: {spriteRenderer.sortingOrder}");
            }
            
            // 确保三渲二设置
            transform.rotation = Quaternion.Euler(-45f, 0f, 0f);
            Vector3 pos = transform.position;
            pos.z = fixedZPosition;
            transform.position = pos;
            
            // 确保缩放不为0
            if (transform.localScale == Vector3.zero)
            {
                transform.localScale = Vector3.one;
                Debug.Log("[Bush] 修复了缩放为0的问题");
            }
            
            // 移动到摄像机前方
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 camPos = mainCam.transform.position;
                transform.position = new Vector3(camPos.x, camPos.y - 2f, fixedZPosition);
                Debug.Log($"[Bush] 移动到摄像机前方: {transform.position}");
            }
            
            Debug.Log("[Bush] 强制显示完成！Bush现在应该可见了。");
        }
        
        private void OnDrawGizmos()
        {
            // 在Scene视图中绘制碰撞范围
            if (boxCollider != null)
            {
                Gizmos.color = isDestructible ? Color.red : Color.green;
                Gizmos.DrawWireCube(transform.position + (Vector3)boxCollider.offset, boxCollider.size);
            }
            
            // 绘制摇摆范围
            if (enableSway)
            {
                Gizmos.color = Color.yellow;
                Vector3 leftPos = transform.position + Vector3.left * swayAmount;
                Vector3 rightPos = transform.position + Vector3.right * swayAmount;
                Gizmos.DrawLine(leftPos, rightPos);
            }
        }
    }
}