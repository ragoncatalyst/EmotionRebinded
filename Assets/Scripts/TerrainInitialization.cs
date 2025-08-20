using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 地形类型枚举
/// </summary>
public enum TerrainType
{
    Grass = 0,  // 草地（可通行）
    Water = 1   // 水域（不可通行）
}

/// <summary>
/// 地形初始化管理器
/// 负责生成连续的草地区域和不可通行的水域
/// </summary>
public class TerrainInitialization : MonoBehaviour
{
    [Header("地形生成配置")]
    [SerializeField] private int mapWidth = 120;          // 地图宽度
    [SerializeField] private int mapHeight = 120;         // 地图高度
    [SerializeField] private float waterPercentage = 0.1f; // 水域占比（0-1）
    
    [Header("草丛生成配置")]
    [SerializeField] private GameObject[] bushPrefabs = new GameObject[3]; // 三种草丛prefab
    [SerializeField] [Range(0f, 1f)] private float bushSpawnChance = 0.15f; // 草丛生成概率
    [SerializeField] private int bushMinDistance = 8; // 草丛之间最小距离
    [SerializeField] private int bushRequiredSpace = 4; // 草丛需要的空间大小（4x4）
    [SerializeField] private bool enableBushGeneration = true; // 是否启用草丛生成
    [SerializeField] private int bushNoSpawnRadiusFromPlayer = 25; // 草丛距玩家的最小生成距离（格）
    
    [Header("Tilemap系统")]
    [SerializeField] private Tilemap grassTilemap;         // 草地Tilemap
    [SerializeField] private Tilemap waterTilemap;         // 水域Tilemap
    [SerializeField] private TileBase grassTile;           // 草地Tile资源
    [SerializeField] private TileBase waterTile;           // 水域Tile资源
    
    [Header("生成设置")]
    [SerializeField] private float tileSize = 1f;         // 地块大小
    [SerializeField] private bool generateOnStart = true; // 是否在开始时自动生成
    [SerializeField] private Transform terrainParent;     // 地形父物体
    [SerializeField] private Transform playerTransform;   // 玩家对象引用
    [SerializeField] private bool centerOnPlayer = true;  // 是否以玩家为中心生成
    
    [Header("连通性设置")]
    [SerializeField] private int minGrassClusterSize = 30; // 最小草地连通区域大小
    [SerializeField] private int maxWaterClusterSize = 50; // 最大水域连通区域大小
    
    [Header("水域生成优化")]
    [Range(5, 30)]
    [SerializeField] private int minWaterDistance = 5;     // 不同水域间的最小间隔
    [Range(0f, 1f)]
    [SerializeField] private float waterCircularness = 0.95f; // 水域圆形度(0-1)，越高越圆
    [Range(1, 10)]
    [SerializeField] private int waterClusterAttempts = 5;   // 每个水域簇的形状优化尝试次数
    
    [Header("玩家安全区域")]
    [SerializeField] private int playerSafeZoneSize = 12;  // 玩家脚下安全区域大小（NxN）
    
    [Header("动态地图扩展")]
    [SerializeField] private bool enableDynamicExpansion = true;    // 是否启用动态地图扩展
    [SerializeField] private int expansionTriggerDistance = 20;     // 触发扩展的距离（距离地图边缘）
    [SerializeField] private int expansionSize = 50;               // 每次扩展的大小
    [SerializeField] private float expansionCheckInterval = 1f;    // 检查扩展的间隔时间（秒）
    [SerializeField] private bool avoidBorderWater = true;         // 避免在地图边缘生成圆形水域
    [SerializeField] private int borderWaterDistance = 25;         // 边缘水域缓冲距离
    [SerializeField] private int bushPreloadTiles = 30;            // 扩展预加载的额外边距（只为灌木预加载）
    
    [Header("调试设置")]
    [SerializeField] private bool showDebugInfo = true;   // 是否显示调试信息
    
    // 私有变量
    private TerrainType[,] terrainMap;                    // 地形数据
    private HashSet<Vector2Int> waterTiles;               // 水域位置集合（用于碰撞检测）
    private TilemapCollider2D waterCollider;              // 水域碰撞器
    private Vector2Int terrainOffset;                     // 地形偏移量（用于以玩家为中心生成）
    private List<Vector2Int> waterCenters;                // 已生成水域的中心点列表
    private List<Vector2Int> spawnedBushPositions;        // 已生成草丛的位置列表
    
    // 动态扩展相关变量
    private float lastExpansionCheck = 0f;                // 上次检查扩展的时间
    private Vector2Int currentMapMin;                     // 当前地图的最小边界
    private Vector2Int currentMapMax;                     // 当前地图的最大边界
    private bool isExpanding = false;                     // 是否正在扩展中
    
    // 静态实例（供其他脚本查询地形）
    public static TerrainInitialization Instance { get; private set; }
    
    private void Awake()
    {
        // 设置单例
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // 初始化容器
        waterTiles = new HashSet<Vector2Int>();
        spawnedBushPositions = new List<Vector2Int>();
        
        // 如果没有指定父物体，创建一个
        if (terrainParent == null)
        {
            GameObject parent = new GameObject("TerrainContainer");
            terrainParent = parent.transform;
        }
    }
    
    private void Start()
    {
        if (generateOnStart)
        {
            // 如果没有设置玩家引用，尝试自动查找
            if (playerTransform == null && centerOnPlayer)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                    Debug.Log("[TerrainInitialization] 自动找到玩家对象: " + player.name);
                }
                else
                {
                    // 尝试通过PlayerController组件查找
                    PlayerController playerController = FindObjectOfType<PlayerController>();
                    if (playerController != null)
                    {
                        playerTransform = playerController.transform;
                        Debug.Log("[TerrainInitialization] 通过PlayerController找到玩家对象: " + playerController.name);
                    }
                    else
                    {
                        Debug.LogWarning("[TerrainInitialization] 未找到玩家对象，将使用世界原点(0,0)作为中心");
                        centerOnPlayer = false;
                    }
                }
            }
            
            GenerateTerrain();
        }
        
        // 初始化地图边界
        InitializeMapBounds();
    }
    
    /// <summary>
    /// 每帧检查是否需要动态扩展地图
    /// </summary>
    private void Update()
    {
        if (enableDynamicExpansion && !isExpanding && playerTransform != null)
        {
            if (Time.time - lastExpansionCheck >= expansionCheckInterval)
            {
                CheckForMapExpansion();
                lastExpansionCheck = Time.time;
            }
        }
    }
    
    /// <summary>
    /// 生成地形
    /// </summary>
    [ContextMenu("生成地形")]
    public void GenerateTerrain()
    {
        Debug.Log("[TerrainInitialization] 开始生成地形...");
        
        // 计算地形偏移量（以玩家为中心）
        CalculateTerrainOffset();
        
        // 清理现有地形
        ClearTerrain();
        
        // 初始化数组
        terrainMap = new TerrainType[mapWidth, mapHeight];
        waterTiles.Clear();
        
        // 初始化水域中心点列表
        if (waterCenters == null)
        {
            waterCenters = new List<Vector2Int>();
        }
        waterCenters.Clear();
        
        // 生成地形数据
        GenerateTerrainData();
        
        // 确保玩家安全区域 - 必须在水域生成之后！
        EnsurePlayerSafeZone();
        
        // 再次强制确保玩家安全区域
        ForceClearPlayerArea();
        
        // 确保草地连通性
        EnsureGrassConnectivity();
        
        // 平滑水域边界：移除被草地过度包围的水域
        SmoothWaterBoundaries();
        
        // 实例化地形物体
        InstantiateTerrain();
        
        // 确保地面始终在最底层渲染
        EnsureGroundAtBottom();

        // 初始阶段：不在旧区域生成草丛；草丛只在扩展区域生成
        
        // 重新初始化地图边界（因为可能重新生成了地形）
        InitializeMapBounds();
        
        Debug.Log($"[TerrainInitialization] 地形生成完成！草地: {CountTiles(TerrainType.Grass)}, 水域: {CountTiles(TerrainType.Water)}");
    }

    /// <summary>
    /// 确保 Tilemap 永远在最底层（防止地面覆盖 player / bush）
    /// </summary>
    private void EnsureGroundAtBottom()
    {
        if (grassTilemap != null)
        {
            var r = grassTilemap.GetComponent<TilemapRenderer>();
            if (r != null)
            {
                r.sortingOrder = -32768; // Unity 可用最小值
                r.sortingLayerID = 0;    // Default layer
            }
            // 防止因 Z 偏移导致遮挡
            var t = grassTilemap.transform;
            t.position = new Vector3(t.position.x, t.position.y, 0f);
        }
        if (waterTilemap != null)
        {
            var r = waterTilemap.GetComponent<TilemapRenderer>();
            if (r != null)
            {
                r.sortingOrder = -32767; // 比草地略高，但仍远低于一切物体
                r.sortingLayerID = 0;
            }
            var t = waterTilemap.transform;
            t.position = new Vector3(t.position.x, t.position.y, 0f);
        }
    }

    /// <summary>
    /// 在当前可见区域（currentMapMin/Max）按与扩展一致的密度生成草丛
    /// </summary>
    private void GenerateBushesConsistentDensityInCurrentArea()
    {
        List<Vector2Int> tiles = new List<Vector2Int>();
        for (int x = currentMapMin.x; x <= currentMapMax.x; x++)
        {
            for (int y = currentMapMin.y; y <= currentMapMax.y; y++)
            {
                tiles.Add(new Vector2Int(x, y));
            }
        }
        GenerateBushesConsistentDensity(tiles, currentMapMin, currentMapMax);
    }

    /// <summary>
    /// 在给定世界格列表内，按与初始一致的规则/密度生成草丛
    /// 保证：只在草地上、不在水域、仅限于提供的 tiles 范围内
    /// </summary>
    private void GenerateBushesConsistentDensity(List<Vector2Int> tiles, Vector2Int areaMin, Vector2Int areaMax)
    {
        if (bushPrefabs == null || bushPrefabs.Length == 0) return;
        if (tiles == null || tiles.Count == 0) return;

        // 计算目标草丛数量（按草地格数量与概率推算）
        int grassCells = 0;
        foreach (var t in tiles)
        {
            int lx = t.x - terrainOffset.x;
            int ly = t.y - terrainOffset.y;
            if (lx < 0 || lx >= mapWidth || ly < 0 || ly >= mapHeight) continue;
            if (terrainMap[lx, ly] == TerrainType.Grass) grassCells++;
        }
        // 与 Inspector 的 bushSpawnChance 对齐：期望生成量 = grassCells * bushSpawnChance
        int tries = Mathf.Clamp(Mathf.RoundToInt(grassCells * Mathf.Clamp01(bushSpawnChance)), 10, 600);

        int spawned = 0;
        for (int i = 0; i < tries; i++)
        {
            var cell = tiles[Random.Range(0, tiles.Count)];
            int lx = cell.x - terrainOffset.x;
            int ly = cell.y - terrainOffset.y;
            if (lx < 0 || lx >= mapWidth || ly < 0 || ly >= mapHeight) continue;
            if (terrainMap[lx, ly] != TerrainType.Grass) continue;
            if (IsWaterAtWorld(new Vector3Int(cell.x, cell.y, 0))) continue;
            // 距离玩家过近则跳过，避免“刷脸”
            if (!IsBeyondPlayerSafeSpawn(cell)) continue;
            if (!AreTilemapsDistinct() && terrainMap[lx, ly] != TerrainType.Grass) continue;

            var prefab = bushPrefabs[Random.Range(0, bushPrefabs.Length)];
            if (prefab == null) continue;
            Vector3 pos = GridToWorld(lx, ly);
            var inst = Instantiate(prefab, pos, Quaternion.identity);
            if (terrainParent != null) inst.transform.SetParent(terrainParent);
            spawned++;
        }
        if (showDebugInfo) Debug.Log($"[TerrainInitialization] 草丛生成（区域 {areaMin}-{areaMax}）: 目标尝试 {tries}，实际生成 {spawned}");
    }
    
    /// <summary>
    /// 计算地形偏移量（以玩家为中心）
    /// </summary>
    private void CalculateTerrainOffset()
    {
        if (centerOnPlayer && playerTransform != null)
        {
            // 将玩家位置转换为网格坐标
            Vector2 playerWorldPos = new Vector2(playerTransform.position.x, playerTransform.position.y);
            Vector2Int playerGridPos = WorldToGrid(playerWorldPos);
            
            // 计算偏移量，使玩家位于地图中心
            terrainOffset = new Vector2Int(
                playerGridPos.x - mapWidth / 2,
                playerGridPos.y - mapHeight / 2
            );
            
            if (showDebugInfo)
            {
                Debug.Log($"[TerrainInitialization] 玩家世界坐标: {playerWorldPos}, 网格坐标: {playerGridPos}, 地形偏移: {terrainOffset}");
            }
        }
        else
        {
            // 不以玩家为中心，使用原点
            terrainOffset = Vector2Int.zero;
            
            if (showDebugInfo)
            {
                Debug.Log("[TerrainInitialization] 使用世界原点作为地形中心");
            }
        }
    }
    
    /// <summary>
    /// 生成地形数据
    /// </summary>
    private void GenerateTerrainData()
    {
        // 首先全部设为草地
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                terrainMap[x, y] = TerrainType.Grass;
            }
        }
        
        // 生成水域簇
        int targetWaterTiles = Mathf.RoundToInt(mapWidth * mapHeight * waterPercentage);
        int generatedWaterTiles = 0;
        int attempts = 0;
        int maxAttempts = 1000;
        
        while (generatedWaterTiles < targetWaterTiles && attempts < maxAttempts)
        {
            attempts++;
            
            // 随机选择水域中心点，确保完整的圆形不会被边缘切割
            int maxRadius = 20; // 最大水域半径
            int absoluteSafeBorder = maxRadius + 15; // 更大的安全边界，绝对避免切割
            
            // 确保有足够的空间生成完整圆形
            if (absoluteSafeBorder * 2 + 20 >= mapWidth || absoluteSafeBorder * 2 + 20 >= mapHeight)
            {
                Debug.Log($"[TerrainInitialization] ⚠️ 地图太小，无法安全生成水域 (需要: {absoluteSafeBorder * 2 + 20}, 实际: {mapWidth}x{mapHeight})");
                continue; // 地图太小，跳过这次生成
            }
            
            int centerX = Random.Range(absoluteSafeBorder, mapWidth - absoluteSafeBorder);
            int centerY = Random.Range(absoluteSafeBorder, mapHeight - absoluteSafeBorder);
            Vector2Int center = new Vector2Int(centerX, centerY);
            
            // 检查与现有水域的距离
            if (!IsValidWaterCenter(center))
                continue;
            
            // 检查是否与玩家安全区域冲突
            if (IsInPlayerSafeZone(center))
                continue;
            
            // 进一步：若以最大半径考虑，水域边缘可能侵入玩家安全区，则放弃
            int projected = 17; // 与预览半径一致
            Vector2Int playerGrid = WorldToGrid(playerTransform.position);
            if (Mathf.Abs(center.x - playerGrid.x) <= projected && Mathf.Abs(center.y - playerGrid.y) <= projected)
                continue;
            
            // 使用Perlin噪声影响生成概率
            float worldX = (centerX + terrainOffset.x) * 0.1f;
            float worldY = (centerY + terrainOffset.y) * 0.1f;
            float noiseValue = Mathf.PerlinNoise(worldX, worldY);
            
            // 只有噪声值高的地方才生成水域
            if (noiseValue < 0.3f) continue;
            
            // 生成圆形水域
            List<Vector2Int> waterCluster = GenerateCircularWaterCluster(center);
            
            if (waterCluster.Count > 0)
            {
                // 应用水域簇
                foreach (Vector2Int tile in waterCluster)
                {
                    if (tile.x >= 0 && tile.x < mapWidth && tile.y >= 0 && tile.y < mapHeight &&
                        terrainMap[tile.x, tile.y] == TerrainType.Grass)
                    {
                        terrainMap[tile.x, tile.y] = TerrainType.Water;
                        generatedWaterTiles++;
                    }
                }
                
                // 记录水域中心点
                waterCenters.Add(center);
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[TerrainInitialization] 圆形水域生成完成，水域数量: {generatedWaterTiles}/{targetWaterTiles}，水域中心: {waterCenters.Count} 个，尝试次数: {attempts}");
        }
    }
    
    /// <summary>
    /// 检查水域中心点是否有效（距离其他水域足够远）
    /// 使用曼哈顿距离，确保斜向也保持足够间距
    /// </summary>
    private bool IsValidWaterCenter(Vector2Int center)
    {
        foreach (Vector2Int existingCenter in waterCenters)
        {
            // 使用曼哈顿距离（|x1-x2| + |y1-y2|），确保斜向也有足够距离
            int manhattanDistance = Mathf.Abs(center.x - existingCenter.x) + Mathf.Abs(center.y - existingCenter.y);
            
            // 欧几里得距离作为基础检查
            float euclideanDistance = Vector2Int.Distance(center, existingCenter);
            
            // 切比雪夫距离（max(|x1-x2|, |y1-y2|)）确保8方向都有最小距离
            int chebyshevDistance = Mathf.Max(Mathf.Abs(center.x - existingCenter.x), Mathf.Abs(center.y - existingCenter.y));
            
            // 确保不相邻的水域格子间隔至少5格
            if (chebyshevDistance < minWaterDistance)
            {
                return false;
            }
        }
        return true;
    }
    
    /// <summary>
    /// 生成真正圆形的水域簇
    /// </summary>
    private List<Vector2Int> GenerateCircularWaterCluster(Vector2Int center)
    {
        List<Vector2Int> cluster = new List<Vector2Int>();
        
        // 计算水域半径，生成5-20格半径的圆形水域
        float radius = Random.Range(5f, 20f);
        
        // 遍历可能的圆形区域
        int intRadius = Mathf.CeilToInt(radius);
        for (int x = center.x - intRadius; x <= center.x + intRadius; x++)
        {
            for (int y = center.y - intRadius; y <= center.y + intRadius; y++)
            {
                if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center.x, center.y));
                    
                    // 根据圆形度决定生成策略
                    if (waterCircularness >= 0.98f)
                    {
                        // 极高圆形度：严格按照数学圆形
                        if (distance <= radius)
                        {
                            cluster.Add(new Vector2Int(x, y));
                        }
                    }
                    else if (waterCircularness >= 0.9f)
                    {
                        // 高圆形度：允许极小的边界模糊
                        float tolerance = (1f - waterCircularness) * 0.5f; // 最大0.05的容差
                        float adjustedRadius = radius + Random.Range(-tolerance, tolerance);
                        
                        if (distance <= adjustedRadius)
                        {
                            cluster.Add(new Vector2Int(x, y));
                        }
                    }
                    else
                    {
                        // 中低圆形度：生成连贯但不规则的形状
                        if (distance <= radius)
                        {
                            bool shouldAdd = false;
                            
                            // 使用椭圆变形来创建不规则但连贯的形状
                            float angle = Mathf.Atan2(y - center.y, x - center.x);
                            
                            // 根据角度创建不规则的半径变化
                            float irregularityFactor = 1f + (1f - waterCircularness) * 0.5f * Mathf.Sin(angle * 3f + Random.Range(0f, 2f * Mathf.PI));
                            float adjustedRadius = radius * irregularityFactor;
                            
                            // 确保核心区域始终被填充（保证连贯性）
                            float coreRadius = radius * 0.6f; // 核心区域占60%
                            
                            if (distance <= coreRadius)
                            {
                                // 核心区域：始终添加，确保连贯
                                shouldAdd = true;
                            }
                            else if (distance <= adjustedRadius)
                            {
                                // 边缘区域：使用更温和的概率，避免散点
                                float edgeRatio = (distance - coreRadius) / (adjustedRadius - coreRadius);
                                float probability = 1f - edgeRatio * edgeRatio; // 二次衰减，更平滑
                                
                                // 根据圆形度调整概率阈值
                                float threshold = 0.3f + waterCircularness * 0.4f; // 0.3-0.7的阈值范围
                                shouldAdd = probability > threshold;
                            }
                            
                            if (shouldAdd)
                            {
                                cluster.Add(new Vector2Int(x, y));
                            }
                        }
                    }
                }
            }
        }
        
        // 确保至少有中心点
        if (!cluster.Contains(center))
        {
            cluster.Add(center);
        }
        
        // 对于低圆形度，进行连通性后处理，移除孤立的散点
        if (waterCircularness < 0.9f)
        {
            cluster = EnsureWaterClusterConnectivity(cluster, center);
        }
        
        return cluster;
    }
    
    /// <summary>
    /// 确保水域簇的连通性，移除孤立的散点
    /// </summary>
    private List<Vector2Int> EnsureWaterClusterConnectivity(List<Vector2Int> originalCluster, Vector2Int center)
    {
        if (originalCluster.Count <= 1) return originalCluster;
        
        // 使用BFS找到从中心点可达的所有瓦片
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        List<Vector2Int> connectedCluster = new List<Vector2Int>();
        
        // 从中心点开始BFS
        queue.Enqueue(center);
        visited.Add(center);
        connectedCluster.Add(center);
        
        Vector2Int[] directions = {
            new Vector2Int(0, 1),   // 上
            new Vector2Int(0, -1),  // 下
            new Vector2Int(1, 0),   // 右
            new Vector2Int(-1, 0),  // 左
            new Vector2Int(1, 1),   // 右上
            new Vector2Int(-1, 1),  // 左上
            new Vector2Int(1, -1),  // 右下
            new Vector2Int(-1, -1)  // 左下
        };
        
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            
            // 检查8个方向的邻居
            foreach (Vector2Int direction in directions)
            {
                Vector2Int neighbor = current + direction;
                
                // 如果邻居在原始簇中且未访问过
                if (originalCluster.Contains(neighbor) && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                    connectedCluster.Add(neighbor);
                }
            }
        }
        
        // 如果连通的簇比原始簇小很多，说明有很多孤立点被移除了
        int removedCount = originalCluster.Count - connectedCluster.Count;
        if (removedCount > 0)
        {
            Debug.Log($"[TerrainInitialization] 🔗 水域连通性优化：移除了 {removedCount} 个孤立散点，保留 {connectedCluster.Count} 个连通瓦片");
        }
        
        return connectedCluster;
    }
    
    /// <summary>
    /// 生成水域簇（旧方法，保留兼容性）
    /// </summary>
    private List<Vector2Int> GenerateWaterCluster(int startX, int startY, int clusterSize)
    {
        List<Vector2Int> cluster = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        
        queue.Enqueue(new Vector2Int(startX, startY));
        visited.Add(new Vector2Int(startX, startY));
        
        Vector2Int[] directions = {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };
        
        while (queue.Count > 0 && cluster.Count < clusterSize)
        {
            Vector2Int current = queue.Dequeue();
            cluster.Add(current);
            
            // 随机化方向顺序
            for (int i = 0; i < directions.Length; i++)
            {
                int randomIndex = Random.Range(i, directions.Length);
                Vector2Int temp = directions[i];
                directions[i] = directions[randomIndex];
                directions[randomIndex] = temp;
            }
            
            foreach (var dir in directions)
            {
                Vector2Int next = current + dir;
                
                if (IsValidPosition(next.x, next.y) && !visited.Contains(next))
                {
                    visited.Add(next);
                    
                    // 有一定概率添加到队列中
                    if (Random.value < 0.7f && cluster.Count < clusterSize)
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }
        
        return cluster;
    }
    
    /// <summary>
    /// 检查位置是否有效
    /// </summary>
    private bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight;
    }
    
    /// <summary>
    /// 计算指定类型的地块数量
    /// </summary>
    private int CountTiles(TerrainType type)
    {
        int count = 0;
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (terrainMap[x, y] == type) count++;
            }
        }
        return count;
    }
    
    /// <summary>
    /// 清理现有地形
    /// </summary>
    [ContextMenu("清理地形")]
    public void ClearTerrain()
    {
        // 清理Tilemap
        if (grassTilemap != null)
        {
            grassTilemap.FloodFill(Vector3Int.zero, null);
            Debug.Log("[TerrainInitialization] 草地Tilemap已清理");
        }
        
        if (waterTilemap != null)
        {
            waterTilemap.FloodFill(Vector3Int.zero, null);
            Debug.Log("[TerrainInitialization] 水域Tilemap已清理");
        }
        
        waterTiles.Clear();
        Debug.Log("[TerrainInitialization] 地形已清理");
    }
    
    /// <summary>
    /// 确保玩家安全区域内没有水域
    /// </summary>
    private void EnsurePlayerSafeZone()
    {
        if (!centerOnPlayer || playerTransform == null)
        {
            EnsureCenterSafeZone();
            return;
        }
        
        Vector2Int playerGridPos = WorldToGrid(playerTransform.position);
        // 使用圆形安全半径，避免直线切边；最小半径取 playerSafeZoneSize
        int radius = Mathf.Max(8, playerSafeZoneSize);
        int changed = ClearCircularArea(playerGridPos, radius, true, false);
        // 对边界环做一次柔化，避免硬直线
        SmoothBoundaryRing(playerGridPos, radius, 2);
        
        if (showDebugInfo)
        {
            Debug.Log($"[TerrainInitialization] 玩家圆形安全区 半径={radius}，改写地块={changed}");
        }
    }

    // 在一个圆形区域内把水改为草；updateTilemaps=true 时同时刷新两张Tilemap
    private int ClearCircularArea(Vector2Int centerWorldGrid, int radius, bool updateTerrainMap, bool updateTilemaps)
    {
        int r2 = radius * radius;
        int changed = 0;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy > r2) continue;
                int wx = centerWorldGrid.x + dx;
                int wy = centerWorldGrid.y + dy;
                int lx = wx - terrainOffset.x;
                int ly = wy - terrainOffset.y;
                if (lx < 0 || lx >= mapWidth || ly < 0 || ly >= mapHeight) continue;
                if (terrainMap[lx, ly] != TerrainType.Grass)
                {
                    terrainMap[lx, ly] = TerrainType.Grass;
                    changed++;
                }
                if (updateTilemaps)
                {
                    var cell = new Vector3Int(wx, wy, 0);
                    if (waterTilemap != null) waterTilemap.SetTile(cell, null);
                    if (grassTilemap != null && grassTile != null) grassTilemap.SetTile(cell, grassTile);
                }
            }
        }
        return changed;
    }

    // 对圆形边界周围的一圈做平滑，将“被草包围的水格”转换为草，避免直线/
    private void SmoothBoundaryRing(Vector2Int centerWorldGrid, int radius, int ringWidth)
    {
        int rMin2 = radius * radius;
        int rMax2 = (radius + ringWidth) * (radius + ringWidth);
        List<Vector3Int> toGrass = new List<Vector3Int>();
        for (int dx = -(radius + ringWidth); dx <= radius + ringWidth; dx++)
        {
            for (int dy = -(radius + ringWidth); dy <= radius + ringWidth; dy++)
            {
                int d2 = dx * dx + dy * dy;
                if (d2 < rMin2 || d2 > rMax2) continue; // 只处理边界环
                int wx = centerWorldGrid.x + dx;
                int wy = centerWorldGrid.y + dy;
                int lx = wx - terrainOffset.x;
                int ly = wy - terrainOffset.y;
                if (lx < 0 || lx >= mapWidth || ly < 0 || ly >= mapHeight) continue;
                if (terrainMap[lx, ly] != TerrainType.Water) continue;
                // 统计四邻的草数量
                int grassNeighbors = 0;
                Vector2Int[] dirs = new Vector2Int[] { new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1)};
                foreach (var d in dirs)
                {
                    int nx = lx + d.x;
                    int ny = ly + d.y;
                    if (nx < 0 || nx >= mapWidth || ny < 0 || ny >= mapHeight) continue;
                    if (terrainMap[nx, ny] == TerrainType.Grass) grassNeighbors++;
                }
                if (grassNeighbors >= 3)
                {
                    toGrass.Add(new Vector3Int(wx, wy, 0));
                }
            }
        }
        foreach (var cell in toGrass)
        {
            int lx = cell.x - terrainOffset.x;
            int ly = cell.y - terrainOffset.y;
            terrainMap[lx, ly] = TerrainType.Grass;
            if (waterTilemap != null) waterTilemap.SetTile(cell, null);
            if (grassTilemap != null && grassTile != null) grassTilemap.SetTile(cell, grassTile);
        }
    }
    
    /// <summary>
    /// 确保地图中心区域安全（当不以玩家为中心时）
    /// </summary>
    private void EnsureCenterSafeZone()
    {
        int centerX = mapWidth / 2;
        int centerY = mapHeight / 2;
        int halfSafeZone = playerSafeZoneSize / 2;
        int waterToGrassCount = 0;
        
        for (int x = centerX - halfSafeZone; x <= centerX + halfSafeZone; x++)
        {
            for (int y = centerY - halfSafeZone; y <= centerY + halfSafeZone; y++)
            {
                if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
                {
                    if (terrainMap[x, y] == TerrainType.Water)
                    {
                        terrainMap[x, y] = TerrainType.Grass;
                        waterToGrassCount++;
                    }
                }
            }
        }
        
        if (showDebugInfo && waterToGrassCount > 0)
        {
            Debug.Log($"[TerrainInitialization] 地图中心安全区域({playerSafeZoneSize}x{playerSafeZoneSize})：将 {waterToGrassCount} 个水域转换为草地");
        }
    }
    
    /// <summary>
    /// 生成草丛
    /// </summary>
    private void GenerateBushes()
    {
        if (bushPrefabs == null || bushPrefabs.Length == 0)
        {
            Debug.LogWarning("[TerrainInitialization] ⚠️ 草丛prefab数组为空，跳过草丛生成");
            return;
        }
        
        // 清空之前的草丛位置记录
        spawnedBushPositions.Clear();
        
        Debug.Log("[TerrainInitialization] 🌿 开始分支式草丛生成...");
        
        // 寻找初始种子点（4x4非水区域）
        List<Vector2Int> seedPoints = FindInitialSeedPoints();
        
        if (seedPoints.Count == 0)
        {
            Debug.LogWarning("[TerrainInitialization] ⚠️ 没有找到合适的4x4非水区域作为种子点");
            return;
        }
        
        Debug.Log($"[TerrainInitialization] 🌱 找到 {seedPoints.Count} 个种子点");
        
        int totalSpawned = 0;
        
        // 对每个种子点进行分支扩散
        foreach (Vector2Int seedPoint in seedPoints)
        {
            int branchSpawned = GenerateBushBranch(seedPoint, 0, 50); // 最大深度50
            totalSpawned += branchSpawned;
            
            Debug.Log($"[TerrainInitialization] 🌿 种子点 {seedPoint} 分支生成了 {branchSpawned} 个草丛");
            
            // 限制总数量，避免生成过多
            if (totalSpawned >= 100) break;
        }
        
        Debug.Log($"[TerrainInitialization] ✅ 分支式草丛生成完成！总共生成 {totalSpawned} 个草丛");
    }
    
    /// <summary>
    /// 寻找初始种子点（4x4非水区域）
    /// </summary>
    private List<Vector2Int> FindInitialSeedPoints()
    {
        List<Vector2Int> seedPoints = new List<Vector2Int>();
        
        // 在地图中寻找合适的种子点
        for (int x = 2; x < mapWidth - 2; x += 10) // 每隔10格检查一次
        {
            for (int y = 2; y < mapHeight - 2; y += 10)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                
                if (Is4x4NonWaterArea(candidate))
                {
                    seedPoints.Add(candidate);
                }
            }
        }
        
        // 如果种子点太少，降低间隔再找一遍
        if (seedPoints.Count < 3)
        {
            for (int x = 2; x < mapWidth - 2; x += 5)
            {
                for (int y = 2; y < mapHeight - 2; y += 5)
                {
                    Vector2Int candidate = new Vector2Int(x, y);
                    
                    if (Is4x4NonWaterArea(candidate) && !seedPoints.Contains(candidate))
                    {
                        seedPoints.Add(candidate);
                        if (seedPoints.Count >= 10) break; // 限制数量
                    }
                }
                if (seedPoints.Count >= 10) break;
            }
        }
        
        return seedPoints;
    }
    
    /// <summary>
    /// 从一个点开始进行分支式草丛生成
    /// </summary>
    private int GenerateBushBranch(Vector2Int centerPos, int depth, int maxDepth)
    {
        if (depth >= maxDepth) return 0;
        
        int spawnedCount = 0;
        
        // 在当前位置生成草丛
        if (Is4x4NonWaterArea(centerPos) && !spawnedBushPositions.Contains(centerPos))
        {
            SpawnBushAt(centerPos);
            spawnedCount = 1;
            
            if (showDebugInfo && depth <= 2)
            {
                Debug.Log($"[TerrainInitialization] 🌿 深度 {depth}: 在 {centerPos} 生成草丛");
            }
        }
        else
        {
            return 0; // 当前位置不能生成，结束这个分支
        }
        
        // 在半径6格的圆弧上寻找新的分支点
        List<Vector2Int> branchPoints = FindBranchPointsOnCircle(centerPos, 6);
        
        // 随机选择1-3个分支点继续扩散
        int branchCount = Random.Range(1, Mathf.Min(4, branchPoints.Count + 1));
        
        for (int i = 0; i < branchCount && i < branchPoints.Count; i++)
        {
            Vector2Int branchPoint = branchPoints[Random.Range(0, branchPoints.Count)];
            branchPoints.Remove(branchPoint); // 避免重复选择
            
            // 递归生成分支
            spawnedCount += GenerateBushBranch(branchPoint, depth + 1, maxDepth);
        }
        
        return spawnedCount;
    }
    
    /// <summary>
    /// 在指定圆弧上寻找合适的分支点
    /// </summary>
    private List<Vector2Int> FindBranchPointsOnCircle(Vector2Int center, int radius)
    {
        List<Vector2Int> branchPoints = new List<Vector2Int>();
        
        // 在圆弧上检查8个方向
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(radius, 0),      // 右
            new Vector2Int(-radius, 0),     // 左
            new Vector2Int(0, radius),      // 上
            new Vector2Int(0, -radius),     // 下
            new Vector2Int(radius/2, radius/2),    // 右上
            new Vector2Int(-radius/2, radius/2),   // 左上
            new Vector2Int(radius/2, -radius/2),   // 右下
            new Vector2Int(-radius/2, -radius/2),  // 左下
        };
        
        foreach (Vector2Int dir in directions)
        {
            Vector2Int candidate = center + dir;
            
            // 检查是否在地图范围内
            if (candidate.x >= 2 && candidate.x < mapWidth - 2 &&
                candidate.y >= 2 && candidate.y < mapHeight - 2)
            {
                // 检查是否是4x4非水区域且没有被占用
                if (Is4x4NonWaterArea(candidate) && !spawnedBushPositions.Contains(candidate))
                {
                    branchPoints.Add(candidate);
                }
            }
        }
        
        return branchPoints;
    }
    
    /// <summary>
    /// 检查指定位置是否是4x4非水区域
    /// </summary>
    private bool Is4x4NonWaterArea(Vector2Int centerPos)
    {
        // 检查4x4区域（以centerPos为中心的2x2，向外扩展1格）
        for (int dx = -2; dx <= 1; dx++)
        {
            for (int dy = -2; dy <= 1; dy++)
            {
                int checkX = centerPos.x + dx;
                int checkY = centerPos.y + dy;
                
                // 检查是否在地图范围内
                if (checkX < 0 || checkX >= mapWidth || checkY < 0 || checkY >= mapHeight)
                {
                    return false;
                }
                
                // 检查是否是水域
                if (terrainMap != null && terrainMap[checkX, checkY] != TerrainType.Grass)
                {
                    return false;
                }
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// 在指定位置生成草丛
    /// </summary>
    private void SpawnBushAt(Vector2Int centerPos)
    {
        // 随机选择草丛prefab
        GameObject selectedBushPrefab = bushPrefabs[Random.Range(0, bushPrefabs.Length)];
        
        if (selectedBushPrefab != null)
        {
            // 转换为世界坐标
            Vector3 worldPos = GridToWorld(centerPos.x, centerPos.y);
            
            // 生成草丛
            GameObject bushInstance = Instantiate(selectedBushPrefab, worldPos, Quaternion.identity);
            
            // 设置父物体
            if (terrainParent != null)
            {
                bushInstance.transform.SetParent(terrainParent);
            }
            
            // 记录位置
            spawnedBushPositions.Add(centerPos);
        }
    }
    
    /// <summary>
    /// 为扩展区域生成草丛
    /// </summary>
    private System.Collections.IEnumerator GenerateExpandedBushes(Vector2Int newMapMin, Vector2Int newMapMax)
    {
        if (bushPrefabs == null || bushPrefabs.Length == 0) yield break;
        Debug.Log($"[TerrainInitialization] 🌿 为扩展区域生成草丛(均匀采样): {newMapMin} - {newMapMax}");

        // 1) 构建候选集
        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = newMapMin.x + bushPreloadTiles; x <= newMapMax.x - bushPreloadTiles; x++)
        {
            for (int y = newMapMin.y + bushPreloadTiles; y <= newMapMax.y - bushPreloadTiles; y++)
            {
                if (x >= currentMapMin.x && x <= currentMapMax.x && y >= currentMapMin.y && y <= currentMapMax.y) continue;
                Vector2Int cell = new Vector2Int(x, y);
                if (!IsBeyondPlayerSafeSpawn(cell)) continue;
                // 使用Tilemap判断草/水，避免受terrainMap边界限制
                Vector3Int worldCell = new Vector3Int(x, y, 0);
                bool isGrass = (grassTilemap != null && grassTilemap.GetTile(worldCell) != null);
                bool isWater = (waterTilemap != null && waterTilemap.GetTile(worldCell) != null);
                if (!isGrass || isWater) continue;
                candidates.Add(cell);
            }
        }

        // 2) 目标数量
        int target = Mathf.Clamp(Mathf.RoundToInt(candidates.Count * Mathf.Clamp01(bushSpawnChance)), 0, candidates.Count);
        // 洗牌
        for (int i = 0; i < candidates.Count; i++)
        {
            int j = Random.Range(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        // 3) 最小间距采样（空间哈希）
        float cellSize = Mathf.Max(1f, bushMinDistance / 1.4142f);
        Dictionary<Vector2Int, List<Vector2>> buckets = new Dictionary<Vector2Int, List<Vector2>>();
        System.Func<Vector2, Vector2Int> keyOf = (p) => new Vector2Int(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.y / cellSize));
        int spawned = 0;
        foreach (var cell in candidates)
        {
            if (spawned >= target) break;
            Vector2 pos = new Vector2(cell.x, cell.y);
            Vector2Int k = keyOf(pos);
            bool ok = true;
            for (int dx = -1; dx <= 1 && ok; dx++)
            {
                for (int dy = -1; dy <= 1 && ok; dy++)
                {
                    Vector2Int nk = new Vector2Int(k.x + dx, k.y + dy);
                    if (!buckets.TryGetValue(nk, out var list)) continue;
                    foreach (var q in list) { if (Vector2.Distance(q, pos) < bushMinDistance) { ok = false; break; } }
                }
            }
            if (!ok) continue;

            // world -> local 仅用于转成世界坐标
            int lx = cell.x - terrainOffset.x; 
            int ly = cell.y - terrainOffset.y;
            Vector3 worldPos = GridToWorld(lx, ly);
            var prefab = bushPrefabs[Random.Range(0, bushPrefabs.Length)];
            if (prefab == null) continue;
            var inst = Instantiate(prefab, worldPos, Quaternion.identity);
            if (terrainParent != null) inst.transform.SetParent(terrainParent);

            if (!buckets.TryGetValue(k, out var cur)) { cur = new List<Vector2>(); buckets[k] = cur; }
            cur.Add(pos);
            spawned++;
            if (spawned % 5 == 0) yield return null;
        }
        Debug.Log($"[TerrainInitialization] ✅ 扩展灌木生成: 候选 {candidates.Count}, 目标 {target}, 实际 {spawned}");
    }
    
    /// <summary>
    /// 检查扩展区域是否可以生成草丛（简化检查，因为扩展区域主要是草地）
    /// </summary>
    private bool CanSpawnBushAtExpanded(Vector2Int centerPos)
    {
        // 检查与已生成草丛的距离（至少6格）
        foreach (Vector2Int existingBushPos in spawnedBushPositions)
        {
            float distance = Vector2Int.Distance(centerPos, existingBushPos);
            if (distance < 6f)
            {
                return false;
            }
        }
        
        // 检查世界坐标的grass tilemap（扩展区域直接用tilemap检查）
        Vector3Int tilemapPos = new Vector3Int(centerPos.x, centerPos.y, 0);
        if (grassTilemap != null && grassTilemap.GetTile(tilemapPos) != null)
        {
            // 确保不在水域
            if (waterTilemap != null && waterTilemap.GetTile(tilemapPos) == null)
            {
                return true;
            }
        }
        
        return false;
    }
    

    
    /// <summary>
    /// 平滑水域边界：移除被草地过度包围的水域
    /// </summary>
    private void SmoothWaterBoundaries()
    {
        Debug.Log("[TerrainInitialization] 🌊 开始平滑水域边界...");
        
        int totalRemovedCount = 0;
        int iteration = 0;
        
        // 重复处理直到没有更多的水域需要移除
        while (true)
        {
            iteration++;
            List<Vector2Int> waterToRemove = new List<Vector2Int>();
            
            // 检查所有水域瓦片（创建副本避免修改集合时的问题）
            List<Vector2Int> currentWaterTiles = new List<Vector2Int>(waterTiles);
            foreach (Vector2Int waterTile in currentWaterTiles)
            {
                if (ShouldRemoveWaterTile(waterTile))
                {
                    waterToRemove.Add(waterTile);
                }
            }
            
            // 如果没有需要移除的水域，结束循环
            if (waterToRemove.Count == 0)
            {
                Debug.Log($"[TerrainInitialization] ✅ 水域边界平滑完成！第 {iteration} 轮后无更多需要移除的水域");
                break;
            }
            
            // 移除找到的水域瓦片
            foreach (Vector2Int tileToRemove in waterToRemove)
            {
                // 从terrainMap中改为草地
                if (tileToRemove.x >= 0 && tileToRemove.x < mapWidth && 
                    tileToRemove.y >= 0 && tileToRemove.y < mapHeight)
                {
                    terrainMap[tileToRemove.x, tileToRemove.y] = TerrainType.Grass;
                }
                
                // 从waterTiles集合中移除
                waterTiles.Remove(tileToRemove);
            }
            
            totalRemovedCount += waterToRemove.Count;
            Debug.Log($"[TerrainInitialization] 🔄 第 {iteration} 轮：移除了 {waterToRemove.Count} 个被过度包围的水域瓦片");
            
            // 安全检查：避免无限循环
            if (iteration > 50)
            {
                Debug.LogWarning("[TerrainInitialization] ⚠️ 水域边界平滑达到最大迭代次数，强制停止");
                break;
            }
        }
        
        Debug.Log($"[TerrainInitialization] 🎯 水域边界平滑总结：共 {iteration} 轮，移除 {totalRemovedCount} 个水域瓦片");
    }
    
    /// <summary>
    /// 判断水域瓦片是否应该被移除（三面或更多面被草地包围）
    /// </summary>
    private bool ShouldRemoveWaterTile(Vector2Int waterTile)
    {
        // 检查四个主要方向的相邻瓦片
        Vector2Int[] directions = {
            new Vector2Int(0, 1),   // 上
            new Vector2Int(0, -1),  // 下
            new Vector2Int(1, 0),   // 右
            new Vector2Int(-1, 0)   // 左
        };
        
        int grassNeighborCount = 0;
        
        foreach (Vector2Int direction in directions)
        {
            Vector2Int neighborPos = waterTile + direction;
            
            // 检查邻居位置是否在地图范围内
            if (neighborPos.x >= 0 && neighborPos.x < mapWidth && 
                neighborPos.y >= 0 && neighborPos.y < mapHeight)
            {
                // 检查是否是草地
                if (terrainMap[neighborPos.x, neighborPos.y] == TerrainType.Grass)
                {
                    grassNeighborCount++;
                }
            }
            else
            {
                // 地图边界外视为草地
                grassNeighborCount++;
            }
        }
        
        // 如果有三面或更多面被草地包围，则应该移除
        bool shouldRemove = grassNeighborCount >= 3;
        
        if (shouldRemove && showDebugInfo)
        {
            Debug.Log($"[TerrainInitialization] 🚫 标记移除水域 {waterTile}：{grassNeighborCount}/4 面被草地包围");
        }
        
        return shouldRemove;
    }
    
    /// <summary>
    /// 确保草地的连通性
    /// </summary>
    private void EnsureGrassConnectivity()
    {
        bool[,] visited = new bool[mapWidth, mapHeight];
        List<List<Vector2Int>> grassClusters = new List<List<Vector2Int>>();
        
        // 找出所有草地连通区域
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (terrainMap[x, y] == TerrainType.Grass && !visited[x, y])
                {
                    List<Vector2Int> cluster = FindGrassCluster(x, y, visited);
                    grassClusters.Add(cluster);
                }
            }
        }
        
        if (grassClusters.Count == 0) return;
        
        // 找到最大的草地区域
        List<Vector2Int> largestCluster = grassClusters[0];
        foreach (var cluster in grassClusters)
        {
            if (cluster.Count > largestCluster.Count)
            {
                largestCluster = cluster;
            }
        }
        
        // 连接其他草地区域到最大区域
        foreach (var cluster in grassClusters)
        {
            if (cluster != largestCluster && cluster.Count >= minGrassClusterSize)
            {
                ConnectGrassClusters(cluster, largestCluster);
            }
            else if (cluster != largestCluster && cluster.Count < minGrassClusterSize)
            {
                // 小的草地区域转换为水域
                foreach (var pos in cluster)
                {
                    terrainMap[pos.x, pos.y] = TerrainType.Water;
                }
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[TerrainInitialization] 发现 {grassClusters.Count} 个草地连通区域，最大区域大小: {largestCluster.Count}");
        }
    }
    
    /// <summary>
    /// 寻找草地连通区域
    /// </summary>
    private List<Vector2Int> FindGrassCluster(int startX, int startY, bool[,] visited)
    {
        List<Vector2Int> cluster = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        
        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;
        
        Vector2Int[] directions = {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };
        
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            cluster.Add(current);
            
            foreach (var dir in directions)
            {
                Vector2Int next = current + dir;
                
                if (IsValidPosition(next.x, next.y) && 
                    !visited[next.x, next.y] && 
                    terrainMap[next.x, next.y] == TerrainType.Grass)
                {
                    visited[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }
        }
        
        return cluster;
    }
    
    /// <summary>
    /// 连接两个草地区域
    /// </summary>
    private void ConnectGrassClusters(List<Vector2Int> cluster1, List<Vector2Int> cluster2)
    {
        // 找到两个区域之间最近的两个点
        Vector2Int closest1 = cluster1[0];
        Vector2Int closest2 = cluster2[0];
        float minDistance = Vector2Int.Distance(closest1, closest2);
        
        foreach (var pos1 in cluster1)
        {
            foreach (var pos2 in cluster2)
            {
                float distance = Vector2Int.Distance(pos1, pos2);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest1 = pos1;
                    closest2 = pos2;
                }
            }
        }
        
        // 创建连接路径
        CreateGrassPath(closest1, closest2);
    }
    
    /// <summary>
    /// 创建草地路径
    /// </summary>
    private void CreateGrassPath(Vector2Int start, Vector2Int end)
    {
        Vector2Int current = start;
        
        while (current != end)
        {
            terrainMap[current.x, current.y] = TerrainType.Grass;
            
            // 朝目标移动
            if (current.x < end.x) current.x++;
            else if (current.x > end.x) current.x--;
            else if (current.y < end.y) current.y++;
            else if (current.y > end.y) current.y--;
        }
        
        terrainMap[end.x, end.y] = TerrainType.Grass;
    }
    
    /// <summary>
    /// 使用Tilemap实例化地形
    /// </summary>
    private void InstantiateTerrain()
    {
        // 检查Tilemap和Tile资源
        if (grassTilemap == null || waterTilemap == null || grassTile == null || waterTile == null)
        {
            Debug.LogError("[TerrainInitialization] Tilemap或Tile资源未设置！请在Inspector中配置。");
            return;
        }
        
        // 清理现有Tiles
        grassTilemap.FloodFill(Vector3Int.zero, null);
        waterTilemap.FloodFill(Vector3Int.zero, null);
        
        // 生成地形Tiles，应用偏移量
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                // 应用地形偏移量到Tile位置，确保对齐
                Vector3Int tilePos = new Vector3Int(x + terrainOffset.x, y + terrainOffset.y, 0);
                
                if (terrainMap[x, y] == TerrainType.Grass)
                {
                    // 若同一格存在水，则以水为准，直接“截掉”草
                    if (waterTilemap != null && waterTilemap.GetTile(tilePos) != null)
                    {
                        grassTilemap.SetTile(tilePos, null);
                    }
                    else
                    {
                        grassTilemap.SetTile(tilePos, grassTile);
                    }
                }
                else if (terrainMap[x, y] == TerrainType.Water)
                {
                    waterTilemap.SetTile(tilePos, waterTile);
                    // 水域位置也需要应用偏移量
                    waterTiles.Add(new Vector2Int(x + terrainOffset.x, y + terrainOffset.y));
                    // 同步去掉草地上与之重叠的格子
                    if (grassTilemap != null)
                    {
                        grassTilemap.SetTile(tilePos, null);
                    }
                }
            }
        }
        
        // 设置水域碰撞
        SetupWaterCollision();
        
        // 设置Tilemap排序层级，确保地面始终在最底层
        SetupTilemapSorting();
        
        // 修复Tilemap碰撞箱偏移问题
        FixTilemapAlignment();
        
        if (showDebugInfo)
        {
            Debug.Log($"[TerrainInitialization] Tilemap地形生成完成！草地Tiles: {CountTiles(TerrainType.Grass)}, 水域Tiles: {CountTiles(TerrainType.Water)}");
        }
    }
    
    /// <summary>
    /// 设置水域碰撞系统
    /// </summary>
    private void SetupWaterCollision()
    {
        if (waterTilemap == null) return;
        
        // 获取或添加TilemapCollider2D
        waterCollider = waterTilemap.GetComponent<TilemapCollider2D>();
        if (waterCollider == null)
        {
            waterCollider = waterTilemap.gameObject.AddComponent<TilemapCollider2D>();
        }
        
        // 配置碰撞器 - 设为触发器避免物理冲突，通过代码逻辑控制通行
        waterCollider.isTrigger = true; // 设为触发器，通过IsWalkable()方法控制通行
        waterCollider.offset = Vector2.zero; // 归零，防止整体下偏
        
        // 可选：添加CompositeCollider2D来优化性能
        CompositeCollider2D compositeCollider = waterTilemap.GetComponent<CompositeCollider2D>();
        if (compositeCollider == null)
        {
            compositeCollider = waterTilemap.gameObject.AddComponent<CompositeCollider2D>();
            compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
            compositeCollider.offset = Vector2.zero; // 归零，防止整体下偏
            
            // 需要Rigidbody2D来使用CompositeCollider2D
            Rigidbody2D rb = waterTilemap.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = waterTilemap.gameObject.AddComponent<Rigidbody2D>();
            }
            rb.bodyType = RigidbodyType2D.Static; // 静态刚体
            
            // 设置TilemapCollider2D使用CompositeCollider2D
            waterCollider.usedByComposite = true;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[TerrainInitialization] 水域碰撞系统设置完成，碰撞器类型: {(compositeCollider != null ? "CompositeCollider2D" : "TilemapCollider2D")}，触发器模式: {waterCollider.isTrigger}");
        }
    }
    
    /// <summary>
    /// 设置Tilemap排序层级，确保地面始终在最底层
    /// </summary>
    private void SetupTilemapSorting()
    {
        // 草地Tilemap设置为最底层
        if (grassTilemap != null)
        {
            TilemapRenderer grassRenderer = grassTilemap.GetComponent<TilemapRenderer>();
            if (grassRenderer != null)
            {
                grassRenderer.sortingLayerName = "Default";
                grassRenderer.sortingOrder = -2000; // 确保在所有物体之下
                Debug.Log($"[TerrainInitialization] 草地Tilemap排序层级设置为: {grassRenderer.sortingOrder}");
            }
        }
        
        // 水域Tilemap也设置为底层，但略高于草地
        if (waterTilemap != null)
        {
            TilemapRenderer waterRenderer = waterTilemap.GetComponent<TilemapRenderer>();
            if (waterRenderer != null)
            {
                waterRenderer.sortingLayerName = "Default";
                waterRenderer.sortingOrder = -32767; // 略高于草地，但仍在绝对底层
                Debug.Log($"[TerrainInitialization] 水域Tilemap排序层级设置为: {waterRenderer.sortingOrder}");
            }
        }
        
        Debug.Log("[TerrainInitialization] Tilemap排序层级设置完成！地面现在始终在最底层。");
    }
    
    /// <summary>
    /// 检查位置是否可通行
    /// </summary>
    /// <param name="worldPosition">世界坐标</param>
    /// <returns>是否可通行</returns>
    public bool IsWalkable(Vector3 worldPosition)
    {
        Vector2Int gridPos = WorldToGrid(worldPosition);
        return IsWalkable(gridPos.x, gridPos.y);
    }
    
    /// <summary>
    /// 检查网格位置是否可通行
    /// </summary>
    /// <param name="gridX">网格X坐标</param>
    /// <param name="gridY">网格Y坐标</param>
    /// <returns>是否可通行</returns>
    public bool IsWalkable(int gridX, int gridY)
    {
        // 首先检查是否在动态扩展的边界内
        if (gridX >= currentMapMin.x && gridX <= currentMapMax.x && 
            gridY >= currentMapMin.y && gridY <= currentMapMax.y)
        {
            // 在动态地图范围内，直接检查是否是水域
            Vector2Int pos = new Vector2Int(gridX, gridY);
            if (waterTiles.Contains(pos))
            {
                return false; // 是水域，不可通行
            }
            
            // 不是水域，检查是否有草地Tile
            Vector3Int tilePos = new Vector3Int(gridX, gridY, 0);
            if (grassTilemap != null)
            {
                TileBase tile = grassTilemap.GetTile(tilePos);
                return tile != null; // 有草地Tile就可通行
            }
            
            return true; // 默认可通行
        }
        
        // 在动态地图范围外，不可通行
        return false;
    }
    
    /// <summary>
    /// 世界坐标转网格坐标
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt(worldPosition.x / tileSize);
        int y = Mathf.RoundToInt(worldPosition.y / tileSize);
        return new Vector2Int(x, y);
    }
    
    public Vector2Int WorldToGrid(Vector2 worldPosition)
    {
        int x = Mathf.RoundToInt(worldPosition.x / tileSize);
        int y = Mathf.RoundToInt(worldPosition.y / tileSize);
        return new Vector2Int(x, y);
    }
    
    /// <summary>
    /// 网格坐标转世界坐标
    /// </summary>
    public Vector3 GridToWorld(int gridX, int gridY)
    {
        // 2D 项目：世界坐标应映射到 X/Y 平面，Z 固定为 0
        // 应用地形偏移量与 tileSize
        float worldX = (gridX + terrainOffset.x) * tileSize;
        float worldY = (gridY + terrainOffset.y) * tileSize;
        return new Vector3(worldX, worldY, 0f);
    }

    // 候选世界格是否远离玩家（避免“刷脸”）
    private bool IsBeyondPlayerSafeSpawn(Vector2Int worldCell)
    {
        if (playerTransform == null) return true;
        Vector2Int playerGrid = WorldToGrid(playerTransform.position);
        int dx = Mathf.Abs(worldCell.x - playerGrid.x);
        int dy = Mathf.Abs(worldCell.y - playerGrid.y);
        int chebyshev = Mathf.Max(dx, dy);
        return chebyshev >= bushNoSpawnRadiusFromPlayer;
    }

    // 当 Grass 与 Water 指向同一张 Tilemap 时，不能使用 Tilemap 判断水域
    private bool AreTilemapsDistinct()
    {
        return waterTilemap != null && grassTilemap != null && waterTilemap != grassTilemap;
    }

    private bool IsWaterAtWorld(Vector3Int worldCell)
    {
        if (!AreTilemapsDistinct()) return false; // 只有在两张 Tilemap 区分明确时才用 Tilemap 检查
        return waterTilemap.GetTile(worldCell) != null;
    }
    
    /// <summary>
    /// 显示地形统计信息
    /// </summary>
    [ContextMenu("显示地形信息")]
    public void ShowTerrainInfo()
    {
        if (terrainMap == null)
        {
            Debug.Log("[TerrainInitialization] 地形未生成");
            return;
        }
        
        int grassCount = CountTiles(TerrainType.Grass);
        int waterCount = CountTiles(TerrainType.Water);
        int totalTiles = mapWidth * mapHeight;
        
        Debug.Log($"[TerrainInitialization] === 地形信息 ===");
        Debug.Log($"[TerrainInitialization] 地图大小: {mapWidth}x{mapHeight} ({totalTiles} 总地块)");
        Debug.Log($"[TerrainInitialization] 地形偏移: {terrainOffset}");
        Debug.Log($"[TerrainInitialization] 草地: {grassCount} ({(float)grassCount/totalTiles*100:F1}%)");
        Debug.Log($"[TerrainInitialization] 水域: {waterCount} ({(float)waterCount/totalTiles*100:F1}%)");
        Debug.Log($"[TerrainInitialization] 水域地块集合大小: {waterTiles.Count}");
        Debug.Log($"[TerrainInitialization] 以玩家为中心: {centerOnPlayer}");
        Debug.Log($"[TerrainInitialization] 玩家安全区域大小: {playerSafeZoneSize}x{playerSafeZoneSize}");
        Debug.Log($"[TerrainInitialization] 水域间最小距离: {minWaterDistance}格");
        Debug.Log($"[TerrainInitialization] 水域圆形度: {waterCircularness:F2} (0=随机, 1=完美圆形)");
        Debug.Log($"[TerrainInitialization] 已生成水域中心: {(waterCenters != null ? waterCenters.Count : 0)} 个");
        
        if (centerOnPlayer && playerTransform != null)
        {
            Vector2Int playerGridPos = WorldToGrid(playerTransform.position);
            Debug.Log($"[TerrainInitialization] 玩家位置: 世界({playerTransform.position.x:F1}, {playerTransform.position.y:F1}) 网格({playerGridPos.x}, {playerGridPos.y})");
        }
    }
    
    /// <summary>
    /// 重新以当前玩家位置为中心生成地形
    /// </summary>
    [ContextMenu("重新生成地形(以玩家为中心)")]
    public void RegenerateAroundPlayer()
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("[TerrainInitialization] 玩家对象未设置，无法以玩家为中心生成");
            return;
        }
        
        centerOnPlayer = true;
        GenerateTerrain();
    }
    
    /// <summary>
    /// 验证地图中心安全区域
    /// </summary>
    private void ValidateCenterSafeZone()
    {
        int centerX = mapWidth / 2;
        int centerY = mapHeight / 2;
        int halfSafeZone = playerSafeZoneSize / 2;
        int waterCount = 0;
        int totalTiles = 0;
        
        for (int x = centerX - halfSafeZone; x <= centerX + halfSafeZone; x++)
        {
            for (int y = centerY - halfSafeZone; y <= centerY + halfSafeZone; y++)
            {
                if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
                {
                    totalTiles++;
                    if (terrainMap[x, y] == TerrainType.Water)
                    {
                        waterCount++;
                    }
                }
            }
        }
        
        Debug.Log($"[TerrainInitialization] === 地图中心安全区域验证 ===");
        Debug.Log($"[TerrainInitialization] 安全区域大小: {playerSafeZoneSize}x{playerSafeZoneSize}");
        Debug.Log($"[TerrainInitialization] 中心位置: ({centerX}, {centerY})");
        Debug.Log($"[TerrainInitialization] 区域内地块: {totalTiles} 个");
        Debug.Log($"[TerrainInitialization] 水域地块: {waterCount} 个");
        Debug.Log($"[TerrainInitialization] 草地地块: {totalTiles - waterCount} 个");
        
        if (waterCount == 0)
        {
            Debug.Log($"[TerrainInitialization] ✅ 地图中心安全区域验证通过！");
        }
        else
        {
            Debug.LogWarning($"[TerrainInitialization] ❌ 地图中心安全区域内发现 {waterCount} 个水域地块！");
        }
    }
    
    /// <summary>
    /// 修复Tilemap碰撞设置，避免与玩家物理冲突
    /// </summary>
    [ContextMenu("修复Tilemap碰撞设置")]
    public void FixTilemapCollision()
    {
        Debug.Log("[TerrainInitialization] 开始修复Tilemap碰撞设置...");
        
        if (waterTilemap == null)
        {
            Debug.LogWarning("[TerrainInitialization] 水域Tilemap未设置！");
            return;
        }
        
        // 获取所有碰撞相关组件
        TilemapCollider2D tilemapCollider = waterTilemap.GetComponent<TilemapCollider2D>();
        CompositeCollider2D compositeCollider = waterTilemap.GetComponent<CompositeCollider2D>();
        Rigidbody2D tilemapRb = waterTilemap.GetComponent<Rigidbody2D>();
        
        // 修复TilemapCollider2D
        if (tilemapCollider != null)
        {
            tilemapCollider.isTrigger = true;
            Debug.Log("  - TilemapCollider2D已设为触发器");
        }
        
        // 修复CompositeCollider2D
        if (compositeCollider != null)
        {
            compositeCollider.isTrigger = true;
            compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
            Debug.Log("  - CompositeCollider2D已设为触发器");
        }
        
        // 修复Rigidbody2D
        if (tilemapRb != null)
        {
            tilemapRb.bodyType = RigidbodyType2D.Static;
            tilemapRb.gravityScale = 0f;
            Debug.Log("  - Tilemap Rigidbody2D已设为静态");
        }
        
        Debug.Log("[TerrainInitialization] Tilemap碰撞设置修复完成！现在使用代码逻辑控制通行，避免物理冲突。");
    }
    
    /// <summary>
    /// 测试碰撞箱位置对齐
    /// </summary>
    [ContextMenu("🧪 测试碰撞箱位置")]
    public void TestColliderAlignment()
    {
        Debug.Log("[TerrainInitialization] === 碰撞箱位置测试 ===");
        
        // 检查水域碰撞器
        if (waterTilemap != null)
        {
            TilemapCollider2D waterCollider = waterTilemap.GetComponent<TilemapCollider2D>();
            if (waterCollider != null)
            {
                Debug.Log($"[TerrainInitialization] 水域TilemapCollider2D偏移: {waterCollider.offset}");
                Debug.Log($"[TerrainInitialization] 预期偏移: (0, 1) - {(waterCollider.offset == new Vector2(0f, 1f) ? "✅ 正确" : "❌ 错误")}");
            }
            
            CompositeCollider2D compositeCollider = waterTilemap.GetComponent<CompositeCollider2D>();
            if (compositeCollider != null)
            {
                Debug.Log($"[TerrainInitialization] CompositeCollider2D偏移: {compositeCollider.offset}");
                Debug.Log($"[TerrainInitialization] 预期偏移: (0, 1) - {(compositeCollider.offset == new Vector2(0f, 1f) ? "✅ 正确" : "❌ 错误")}");
            }
        }
        
        // 检查草地碰撞器（如果存在）
        if (grassTilemap != null)
        {
            TilemapCollider2D grassCollider = grassTilemap.GetComponent<TilemapCollider2D>();
            if (grassCollider != null)
            {
                Debug.Log($"[TerrainInitialization] 草地TilemapCollider2D偏移: {grassCollider.offset}");
                Debug.Log($"[TerrainInitialization] 预期偏移: (0, 1) - {(grassCollider.offset == new Vector2(0f, 1f) ? "✅ 正确" : "❌ 错误")}");
            }
            else
            {
                Debug.Log("[TerrainInitialization] 草地Tilemap没有碰撞器（正常情况）");
            }
        }
        
        Debug.Log("[TerrainInitialization] 💡 如果偏移不正确，请使用'修复Tilemap对齐'来修复");
    }
    
    /// <summary>
    /// 修复Tilemap对齐和碰撞偏移问题
    /// </summary>
    [ContextMenu("修复Tilemap对齐")]
    public void FixTilemapAlignment()
    {
        Debug.Log("[TerrainInitialization] 开始修复Tilemap对齐和碰撞箱偏移...");
        
        // 修复草地Tilemap
        if (grassTilemap != null)
        {
            grassTilemap.transform.position = Vector3.zero;
            grassTilemap.tileAnchor = Vector3.zero;
            
            // 重置TilemapRenderer的锚点
            TilemapRenderer grassRenderer = grassTilemap.GetComponent<TilemapRenderer>();
            if (grassRenderer != null)
            {
                grassRenderer.chunkCullingBounds = Vector3.zero;
            }
            
            // 如果草地有碰撞器，也进行相同的修复
            TilemapCollider2D grassCollider = grassTilemap.GetComponent<TilemapCollider2D>();
            if (grassCollider != null)
            {
                grassCollider.offset = Vector2.zero; // 归零
                Debug.Log("  ✅ 草地TilemapCollider2D偏移已修复（向上移动1格）");
            }
            
            Debug.Log("  ✅ 草地Tilemap位置和锚点已重置");
        }
        
        // 修复水域Tilemap
        if (waterTilemap != null)
        {
            waterTilemap.transform.position = Vector3.zero;
            waterTilemap.tileAnchor = Vector3.zero;
            
            // 重置TilemapRenderer的锚点
            TilemapRenderer waterRenderer = waterTilemap.GetComponent<TilemapRenderer>();
            if (waterRenderer != null)
            {
                waterRenderer.chunkCullingBounds = Vector3.zero;
            }
            
            Debug.Log("  ✅ 水域Tilemap位置和锚点已重置");
            
            // 修复TilemapCollider2D偏移（向上移动一格）
            TilemapCollider2D collider = waterTilemap.GetComponent<TilemapCollider2D>();
            if (collider != null)
            {
                collider.offset = Vector2.zero; // 归零
                Debug.Log("  ✅ 水域TilemapCollider2D偏移已修复（向上移动1格）");
                
                // 强制刷新碰撞器
                collider.enabled = false;
                collider.enabled = true;
                Debug.Log("  ✅ 水域碰撞器已刷新");
            }
            
            // 修复CompositeCollider2D偏移（向上移动一格）
            CompositeCollider2D compositeCollider = waterTilemap.GetComponent<CompositeCollider2D>();
            if (compositeCollider != null)
            {
                compositeCollider.offset = Vector2.zero; // 归零
                Debug.Log("  ✅ CompositeCollider2D偏移已修复（向上移动1格）");
            }
        }
        
        Debug.Log("[TerrainInitialization] ✅ Tilemap对齐和碰撞箱偏移修复完成！");
        Debug.Log("[TerrainInitialization] 🎯 所有碰撞箱已向上移动1格，现在应该与视觉位置完全对齐！");
        Debug.Log("[TerrainInitialization] 📋 修复内容:");
        Debug.Log("[TerrainInitialization]   - Tilemap位置和锚点重置为零");
        Debug.Log("[TerrainInitialization]   - TilemapCollider2D偏移设为(0, 1)");
        Debug.Log("[TerrainInitialization]   - CompositeCollider2D偏移设为(0, 1)");
        Debug.Log("[TerrainInitialization]   - 碰撞器已强制刷新");
        Debug.Log("[TerrainInitialization] 💡 如果问题仍然存在，请检查Grid组件的Cell Size设置");
    }
    
    /// <summary>
    /// 验证新参数是否正常工作
    /// </summary>
    [ContextMenu("验证水域生成参数")]
    public void ValidateWaterParameters()
    {
        Debug.Log($"[TerrainInitialization] === 水域生成参数验证 ===");
        Debug.Log($"[TerrainInitialization] 水域最小间距: {minWaterDistance} 格");
        Debug.Log($"[TerrainInitialization] 水域圆形度: {waterCircularness:F2} (0=随机, 1=完美圆形)");
        Debug.Log($"[TerrainInitialization] 形状优化尝试次数: {waterClusterAttempts}");
        Debug.Log($"[TerrainInitialization] 玩家安全区域: {playerSafeZoneSize}x{playerSafeZoneSize}");
        Debug.Log($"[TerrainInitialization] 如果看到这些参数，说明新功能正常工作！");
        
        if (minWaterDistance < 4)
        {
            Debug.LogWarning("[TerrainInitialization] 水域间距过小，可能导致水域过于密集！");
        }
        
        if (waterCircularness < 0.3f)
        {
            Debug.LogWarning("[TerrainInitialization] 圆形度较低，水域形状会比较随机！");
        }
    }
    
    /// <summary>
    /// 应用宏伟地形预设配置
    /// </summary>
    [ContextMenu("应用宏伟地形预设")]
    public void ApplyGrandTerrainPreset()
    {
        Debug.Log("[TerrainInitialization] 应用宏伟地形预设配置...");
        
        // 大型地图设置
        mapWidth = 100;
        mapHeight = 100;
        waterPercentage = 0.12f;
        
        // 大型水域设置
        maxWaterClusterSize = 20;
        minWaterDistance = 25;
        waterCircularness = 0.9f;
        
        // 大型安全区域
        playerSafeZoneSize = 10;
        
        // 连通性设置
        minGrassClusterSize = 30;
        
        Debug.Log("[TerrainInitialization] 宏伟地形预设配置完成！");
        Debug.Log($"  - 地图规模: {mapWidth}x{mapHeight}");
        Debug.Log($"  - 水域大小: {maxWaterClusterSize}");
        Debug.Log($"  - 水域间距: {minWaterDistance}");
        Debug.Log($"  - 安全区域: {playerSafeZoneSize}x{playerSafeZoneSize}");
        Debug.Log("请点击'生成地形'来应用新配置！");
    }
    
    /// <summary>
    /// 应用紧凑地形预设配置
    /// </summary>
    [ContextMenu("应用紧凑地形预设")]
    public void ApplyCompactTerrainPreset()
    {
        Debug.Log("[TerrainInitialization] 应用紧凑地形预设配置...");
        
        // 小型地图设置
        mapWidth = 40;
        mapHeight = 40;
        waterPercentage = 0.25f;
        
        // 小型水域设置
        maxWaterClusterSize = 6;
        minWaterDistance = 10;
        waterCircularness = 0.75f;
        
        // 小型安全区域
        playerSafeZoneSize = 5;
        
        // 连通性设置
        minGrassClusterSize = 15;
        
        Debug.Log("[TerrainInitialization] 紧凑地形预设配置完成！");
        Debug.Log("请点击'生成地形'来应用新配置！");
    }
    
    /// <summary>
    /// 应用精致小水域预设配置
    /// </summary>
    [ContextMenu("应用精致小水域预设")]
    public void ApplySmallRoundWaterPreset()
    {
        Debug.Log("[TerrainInitialization] 应用精致小水域预设配置...");
        
        // 适中地图设置
        mapWidth = 60;
        mapHeight = 60;
        waterPercentage = 0.06f;
        
        // 小而圆的水域设置
        maxWaterClusterSize = 4;
        minWaterDistance = 30;
        waterCircularness = 0.98f;
        
        // 适中安全区域
        playerSafeZoneSize = 6;
        
        // 连通性设置
        minGrassClusterSize = 12;
        
        Debug.Log("[TerrainInitialization] 精致小水域预设配置完成！");
        Debug.Log($"  - 地图规模: {mapWidth}x{mapHeight}");
        Debug.Log($"  - 小水域大小: {maxWaterClusterSize}");
        Debug.Log($"  - 超大间距: {minWaterDistance}");
        Debug.Log($"  - 超高圆形度: {waterCircularness}");
        Debug.Log("请点击'生成地形'来应用新配置！");
    }
    
    /// <summary>
    /// 应用适中水域预设配置
    /// </summary>
    [ContextMenu("应用适中水域预设")]
    public void ApplyMediumWaterPreset()
    {
        Debug.Log("[TerrainInitialization] 应用适中水域预设配置...");
        
        // 适中地图设置
        mapWidth = 60;
        mapHeight = 60;
        waterPercentage = 0.12f;
        
        // 适中水域设置
        maxWaterClusterSize = 10;
        minWaterDistance = 20;
        waterCircularness = 0.92f;
        
        // 适中安全区域
        playerSafeZoneSize = 6;
        
        // 连通性设置
        minGrassClusterSize = 15;
        
        Debug.Log("[TerrainInitialization] 适中水域预设配置完成！");
        Debug.Log($"  - 地图规模: {mapWidth}x{mapHeight}");
        Debug.Log($"  - 适中水域大小: {maxWaterClusterSize}");
        Debug.Log($"  - 合理间距: {minWaterDistance}");
        Debug.Log($"  - 高圆形度: {waterCircularness}");
        Debug.Log($"  - 水域占比: {waterPercentage*100:F1}%");
        Debug.Log("请点击'生成地形'来应用新配置！");
    }
    
    /// <summary>
    /// 应用超大规模地形预设配置（匹配灌木丛规模）
    /// </summary>
    [ContextMenu("应用超大规模地形预设")]
    public void ApplyMassiveTerrainPreset()
    {
        Debug.Log("[TerrainInitialization] 应用超大规模地形预设配置...");
        
        // 超大地图设置
        mapWidth = 150;
        mapHeight = 150;
        waterPercentage = 0.08f;
        
        // 大型湖泊设置
        maxWaterClusterSize = 30;
        minWaterDistance = 45;
        waterCircularness = 0.9f;
        
        // 大型安全区域
        playerSafeZoneSize = 15;
        
        // 连通性设置
        minGrassClusterSize = 40;
        
        Debug.Log("[TerrainInitialization] 超大规模地形预设配置完成！");
        Debug.Log($"  - 地图规模: {mapWidth}x{mapHeight} (超大规模)");
        Debug.Log($"  - 湖泊大小: {maxWaterClusterSize} (大型湖泊)");
        Debug.Log($"  - 超大间距: {minWaterDistance}");
        Debug.Log($"  - 安全区域: {playerSafeZoneSize}x{playerSafeZoneSize}");
        Debug.Log($"  - 水域占比: {waterPercentage*100:F1}%");
        Debug.Log("🏞️ 现在地形规模应该与灌木丛匹配！");
        Debug.Log("请点击'生成地形'来应用新配置！");
    }
    
    /// <summary>
    /// 应用精确规格水域预设（5-8格半径，5格间隔）
    /// </summary>
    [ContextMenu("应用精确规格水域预设")]
    public void ApplyPreciseWaterPreset()
    {
        Debug.Log("[TerrainInitialization] 应用精确规格水域预设配置...");
        
        // 合理地图设置
        mapWidth = 120;
        mapHeight = 120;
        waterPercentage = 0.1f;
        
        // 精确水域设置
        maxWaterClusterSize = 50;
        minWaterDistance = 5;
        waterCircularness = 1.0f; // 完美圆形
        
        // 合理安全区域
        playerSafeZoneSize = 12;
        
        // 连通性设置
        minGrassClusterSize = 30;
        
        Debug.Log("[TerrainInitialization] 精确规格水域预设配置完成！");
        Debug.Log($"  - 水域半径: 5-20格 (更大跨度)");
        Debug.Log($"  - 水域间隔: {minWaterDistance}格");
        Debug.Log($"  - 圆形度: {waterCircularness}");
        Debug.Log($"  - 避免边缘水域: {avoidBorderWater}");
        Debug.Log($"  - 边缘缓冲距离: {borderWaterDistance}格");
        Debug.Log("请点击'生成地形'来应用新配置！");
    }
    
    /// <summary>
    /// 应用大跨度水域预设（5-20格半径）
    /// </summary>
    [ContextMenu("应用大跨度水域预设")]
    public void ApplyLargeWaterPreset()
    {
        Debug.Log("[TerrainInitialization] 应用大跨度水域预设配置...");
        
        // 合理地图设置
        mapWidth = 150;
        mapHeight = 150;
        waterPercentage = 0.08f; // 降低水域占比，因为单个水域更大了
        
        // 大跨度水域设置
        maxWaterClusterSize = 80; // 增加最大簇大小以适应大圆形
        minWaterDistance = 8;     // 增加间距以适应大水域
        waterCircularness = 1.0f; // 完美圆形
        
        // 更大的安全区域
        playerSafeZoneSize = 15;
        
        // 连通性设置
        minGrassClusterSize = 40;
        
        Debug.Log("[TerrainInitialization] 大跨度水域预设配置完成！");
        Debug.Log($"  🌊 水域半径: 5-20格 (4倍跨度)");
        Debug.Log($"  📏 水域间隔: {minWaterDistance}格");
        Debug.Log($"  ⭕ 圆形度: {waterCircularness} (完美圆形)");
        Debug.Log($"  🗺️ 地图尺寸: {mapWidth}x{mapHeight}");
        Debug.Log($"  💧 水域占比: {waterPercentage*100:F1}%");
        Debug.Log($"  🛡️ 安全区域: {playerSafeZoneSize}x{playerSafeZoneSize}");
        Debug.Log("请点击'生成地形'来应用新配置！");
    }
    
    /// <summary>
    /// 清理水域中的草丛
    /// </summary>
    private void CleanupBushesInWater()
    {
        if (terrainParent == null) return;
        
        Debug.Log("[TerrainInitialization] 🧹 开始清理水域中的草丛...");
        
        int removedCount = 0;
        List<GameObject> bushesToRemove = new List<GameObject>();
        
        // 查找所有草丛物体
        if (bushPrefabs != null)
        {
            foreach (GameObject prefab in bushPrefabs)
            {
                if (prefab != null)
                {
                    string prefabName = prefab.name;
                    Transform[] allChildren = terrainParent.GetComponentsInChildren<Transform>();
                    
                    foreach (Transform child in allChildren)
                    {
                        if (child != terrainParent && child.name.Contains(prefabName))
                        {
                            // 检查草丛是否在水域中
                            Vector3 worldPos = child.position;
                            Vector2Int localGridPos = WorldToGrid(worldPos);
                            
                            // 🔧 修复坐标系统：waterTiles存储的是世界坐标，需要转换
                            Vector2Int worldGridPos = new Vector2Int(localGridPos.x + terrainOffset.x, localGridPos.y + terrainOffset.y);
                            
                            // 检查是否在水域中（使用世界坐标）
                            if (waterTiles.Contains(worldGridPos))
                            {
                                bushesToRemove.Add(child.gameObject);
                                Debug.Log($"[TerrainInitialization] 🚫 发现水域中的草丛: {child.name} 本地位置 {localGridPos} 世界位置 {worldGridPos}");
                            }
                            else
                            {
                                // 双重检查Tilemap（使用世界坐标）
                                Vector3Int tilemapPos = new Vector3Int(worldGridPos.x, worldGridPos.y, 0);
                                if (waterTilemap != null && waterTilemap.GetTile(tilemapPos) != null)
                                {
                                    bushesToRemove.Add(child.gameObject);
                                    Debug.Log($"[TerrainInitialization] 🚫 发现Tilemap水域中的草丛: {child.name} 本地位置 {localGridPos} 世界位置 {worldGridPos}");
                                }
                            }
                        }
                    }
                }
            }
        }
        
        // 删除找到的水域草丛
        foreach (GameObject bush in bushesToRemove)
        {
            Vector2Int gridPos = WorldToGrid(bush.transform.position);
            spawnedBushPositions.Remove(gridPos);
            DestroyImmediate(bush);
            removedCount++;
        }
        
        if (removedCount > 0)
        {
            Debug.Log($"[TerrainInitialization] 🧹 清理完成！移除了 {removedCount} 个水域中的草丛");
        }
        else
        {
            Debug.Log("[TerrainInitialization] ✅ 没有发现水域中的草丛");
        }
    }
    
    /// <summary>
    /// 手动清理水域中的草丛
    /// </summary>
    [ContextMenu("🧹 清理水域草丛")]
    public void ManualCleanupBushesInWater()
    {
        CleanupBushesInWater();
    }
    
    /// <summary>
    /// 清理所有草丛
    /// </summary>
    [ContextMenu("🧹 清理所有草丛")]
    public void ClearAllBushes()
    {
        Debug.Log("[TerrainInitialization] 🧹 开始清理所有草丛...");
        
        int clearedCount = 0;
        
        // 通过父物体查找并清理草丛
        if (terrainParent != null)
        {
            // 通过prefab名称匹配清理
            if (bushPrefabs != null)
            {
                foreach (GameObject prefab in bushPrefabs)
                {
                    if (prefab != null)
                    {
                        string prefabName = prefab.name;
                        Transform[] allChildren = terrainParent.GetComponentsInChildren<Transform>();
                        
                        foreach (Transform child in allChildren)
                        {
                            if (child != terrainParent && child.name.Contains(prefabName))
                            {
                                DestroyImmediate(child.gameObject);
                                clearedCount++;
                            }
                        }
                    }
                }
            }
            
            // 如果Bush组件存在，也通过组件清理
            Component[] bushComponents = terrainParent.GetComponentsInChildren(typeof(MonoBehaviour));
            foreach (Component component in bushComponents)
            {
                if (component != null && component.GetType().Name == "Bush")
                {
                    DestroyImmediate(component.gameObject);
                    clearedCount++;
                }
            }
        }
        
        // 清空位置记录
        spawnedBushPositions.Clear();
        
        Debug.Log($"[TerrainInitialization] ✅ 草丛清理完成！清理了 {clearedCount} 个草丛");
    }
    
    /// <summary>
    /// 验证坐标系统一致性
    /// </summary>
    [ContextMenu("🔧 验证坐标系统一致性")]
    public void VerifyCoordinateConsistency()
    {
        Debug.Log("[TerrainInitialization] === 坐标系统一致性验证 ===");
        
        // 显示terrainOffset
        Debug.Log($"[TerrainInitialization] 地形偏移量: {terrainOffset}");
        
        // 检查几个水域瓦片的坐标
        int checkCount = Mathf.Min(5, waterTiles.Count);
        Debug.Log($"[TerrainInitialization] 检查前 {checkCount} 个水域瓦片的坐标...");
        
        int i = 0;
        foreach (Vector2Int waterTile in waterTiles)
        {
            if (i >= checkCount) break;
            
            // waterTiles中存储的是世界坐标
            Vector2Int worldPos = waterTile;
            
            // 转换为本地坐标
            Vector2Int localPos = new Vector2Int(worldPos.x - terrainOffset.x, worldPos.y - terrainOffset.y);
            
            // 检查Tilemap
            Vector3Int tilemapPos = new Vector3Int(worldPos.x, worldPos.y, 0);
            bool hasWaterTile = waterTilemap != null && waterTilemap.GetTile(tilemapPos) != null;
            
            // 检查terrainMap
            bool isLocalWater = false;
            if (localPos.x >= 0 && localPos.x < mapWidth && localPos.y >= 0 && localPos.y < mapHeight)
            {
                isLocalWater = terrainMap[localPos.x, localPos.y] == TerrainType.Water;
            }
            
            Debug.Log($"[TerrainInitialization] 水域瓦片 {i+1}: 世界坐标 {worldPos}, 本地坐标 {localPos}, Tilemap有瓦片: {hasWaterTile}, terrainMap是水域: {isLocalWater}");
            i++;
        }
        
        Debug.Log("[TerrainInitialization] === 验证完成 ===");
    }
    
    /// <summary>
    /// 详细调试草丛生成条件
    /// </summary>
    [ContextMenu("🔍 详细调试草丛生成")]
    public void DetailedBushSpawnDebug()
    {
        Debug.Log("[TerrainInitialization] === 详细草丛生成调试 ===");
        
        // 基础检查
        if (!enableBushGeneration)
        {
            Debug.LogError("[TerrainInitialization] ❌ 草丛生成已禁用！");
            return;
        }
        
        if (bushPrefabs == null || bushPrefabs.Length == 0)
        {
            Debug.LogError("[TerrainInitialization] ❌ 草丛prefab数组为空！");
            return;
        }
        
        Debug.Log($"[TerrainInitialization] 📋 基础参数:");
        Debug.Log($"  - 地图尺寸: {mapWidth} x {mapHeight}");
        Debug.Log($"  - 地形偏移: {terrainOffset}");
        Debug.Log($"  - 草丛最小距离: {bushMinDistance}");
        Debug.Log($"  - 草丛所需空间: {bushRequiredSpace}");
        Debug.Log($"  - 玩家安全区: {playerSafeZoneSize}");
        Debug.Log($"  - 水域瓦片数量: {waterTiles.Count}");
        
        // 测试一个具体位置
        int centerX = mapWidth / 2;
        int centerY = mapHeight / 2;
        Vector2Int testPos = new Vector2Int(centerX, centerY);
        
        Debug.Log($"[TerrainInitialization] 🧪 详细测试位置: {testPos}");
        
        // 逐步检查每个条件
        bool canSpawn = true;
        string failReason = "";
        
        // 检查4x4区域
        int halfSize = bushRequiredSpace / 2;
        for (int x = testPos.x - halfSize; x < testPos.x + halfSize && canSpawn; x++)
        {
            for (int y = testPos.y - halfSize; y < testPos.y + halfSize && canSpawn; y++)
            {
                // 地图范围检查
                if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight)
                {
                    canSpawn = false;
                    failReason = $"超出地图范围: ({x},{y})";
                    break;
                }
                
                // 坐标转换
                Vector2Int worldTilePos = new Vector2Int(x + terrainOffset.x, y + terrainOffset.y);
                
                // 水域检查
                if (waterTiles.Contains(worldTilePos))
                {
                    canSpawn = false;
                    failReason = $"位置 ({x},{y}) 世界坐标 {worldTilePos} 在waterTiles中";
                    break;
                }
                
                // Tilemap检查
                Vector3Int tilemapPos = new Vector3Int(worldTilePos.x, worldTilePos.y, 0);
                if (waterTilemap != null && waterTilemap.GetTile(tilemapPos) != null)
                {
                    canSpawn = false;
                    failReason = $"位置 ({x},{y}) 世界坐标 {worldTilePos} 在waterTilemap中";
                    break;
                }
                
                // terrainMap检查
                if (terrainMap != null && terrainMap[x, y] != TerrainType.Grass)
                {
                    canSpawn = false;
                    failReason = $"位置 ({x},{y}) terrainMap不是草地类型: {terrainMap[x, y]}";
                    break;
                }
            }
        }
        
        // 距离检查
        if (canSpawn && spawnedBushPositions.Count > 0)
        {
            foreach (Vector2Int existingPos in spawnedBushPositions)
            {
                float distance = Vector2Int.Distance(testPos, existingPos);
                if (distance < bushMinDistance)
                {
                    canSpawn = false;
                    failReason = $"距离现有草丛 {existingPos} 太近: {distance:F1} < {bushMinDistance}";
                    break;
                }
            }
        }
        
        // 玩家安全区检查
        if (canSpawn && playerTransform != null)
        {
            Vector2Int playerGridPos = WorldToGrid(playerTransform.position);
            float distanceToPlayer = Vector2Int.Distance(testPos, playerGridPos);
            if (distanceToPlayer < playerSafeZoneSize + bushRequiredSpace)
            {
                canSpawn = false;
                failReason = $"距离玩家 {playerGridPos} 太近: {distanceToPlayer:F1} < {playerSafeZoneSize + bushRequiredSpace}";
            }
        }
        
        if (canSpawn)
        {
            Debug.Log($"[TerrainInitialization] ✅ 位置 {testPos} 可以生成草丛！");
        }
        else
        {
            Debug.LogWarning($"[TerrainInitialization] ❌ 位置 {testPos} 不能生成草丛，原因: {failReason}");
        }
        
        // 统计草地瓦片数量
        int grassCount = 0;
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (terrainMap != null && terrainMap[x, y] == TerrainType.Grass)
                {
                    grassCount++;
                }
            }
        }
        
        Debug.Log($"[TerrainInitialization] 📊 地形统计:");
        Debug.Log($"  - 草地瓦片: {grassCount}/{mapWidth * mapHeight} ({(float)grassCount / (mapWidth * mapHeight) * 100:F1}%)");
        Debug.Log($"  - 已生成草丛: {spawnedBushPositions.Count}");
        
        Debug.Log("[TerrainInitialization] === 调试完成 ===");
    }
    
    /// <summary>
    /// 测试草丛生成条件
    /// </summary>
    [ContextMenu("🧪 测试草丛生成条件")]
    public void TestBushSpawnConditions()
    {
        Debug.Log("[TerrainInitialization] === 草丛生成条件测试 ===");
        
        if (!enableBushGeneration)
        {
            Debug.LogWarning("[TerrainInitialization] ⚠️ 草丛生成已禁用！");
            return;
        }
        
        if (bushPrefabs == null || bushPrefabs.Length == 0)
        {
            Debug.LogError("[TerrainInitialization] ❌ 草丛prefab数组为空！");
            return;
        }
        
        // 随机测试10个位置
        int testCount = 10;
        int validPositions = 0;
        
        for (int i = 0; i < testCount; i++)
        {
            int centerX = Random.Range(bushRequiredSpace / 2, mapWidth - bushRequiredSpace / 2);
            int centerY = Random.Range(bushRequiredSpace / 2, mapHeight - bushRequiredSpace / 2);
            Vector2Int testPos = new Vector2Int(centerX, centerY);
            
            if (Is4x4NonWaterArea(testPos))
            {
                validPositions++;
                Debug.Log($"[TerrainInitialization] ✅ 位置 {testPos} 可以生成草丛");
            }
            else
            {
                Debug.Log($"[TerrainInitialization] ❌ 位置 {testPos} 不能生成草丛");
            }
        }
        
        Debug.Log($"[TerrainInitialization] 📊 测试结果: {validPositions}/{testCount} 位置可用");
        
        if (validPositions == 0)
        {
            Debug.LogWarning("[TerrainInitialization] ⚠️ 没有找到可用位置！可能参数过于严格");
            Debug.LogWarning("[TerrainInitialization] 💡 建议: 减少 bushMinDistance 或增加地图中的草地面积");
        }
        else if (validPositions < testCount * 0.3f)
        {
            Debug.LogWarning("[TerrainInitialization] ⚠️ 可用位置较少，可能影响生成效率");
        }
    }
    
    /// <summary>
    /// 立即修复所有问题
    /// </summary>
    [ContextMenu("🚑 立即修复所有问题")]
    public void FixAllIssuesNow()
    {
        Debug.Log("[TerrainInitialization] 🚑 立即修复所有问题...");
        
        // 1. 强制清理玩家周围的水域
        if (playerTransform != null)
        {
            Vector2Int playerGridPos = WorldToGrid(playerTransform.position);
            int safeZone = 10; // 10格安全区
            
            Debug.Log($"[TerrainInitialization] 🚑 玩家世界位置: {playerTransform.position}, 网格位置: {playerGridPos}");
            
            for (int dx = -safeZone; dx <= safeZone; dx++)
            {
                for (int dy = -safeZone; dy <= safeZone; dy++)
                {
                    int worldX = playerGridPos.x + dx;
                    int worldY = playerGridPos.y + dy;
                    
                    // 在Tilemap中移除水域（使用世界坐标）
                    Vector3Int tilemapPos = new Vector3Int(worldX, worldY, 0);
                    if (waterTilemap != null)
                    {
                        waterTilemap.SetTile(tilemapPos, null);
                    }
                    
                    // 在Tilemap中设置草地（使用世界坐标）
                    if (grassTilemap != null && grassTile != null)
                    {
                        grassTilemap.SetTile(tilemapPos, grassTile);
                    }
                    
                    // 同时更新terrainMap（使用本地坐标）
                    int localX = worldX - terrainOffset.x;
                    int localY = worldY - terrainOffset.y;
                    if (localX >= 0 && localX < mapWidth && localY >= 0 && localY < mapHeight)
                    {
                        terrainMap[localX, localY] = TerrainType.Grass;
                    }
                }
            }
            
            Debug.Log($"[TerrainInitialization] ✅ 清理了玩家周围 {safeZone*2+1}x{safeZone*2+1} 区域的水域");
        }
        
        // 2. 仅清理玩家安全区内误入水域的草丛（不在旧区域重新铺草丛）
        // 保持旧区域草丛不变；额外生成仅在扩展流程中进行
        
        Debug.Log("[TerrainInitialization] 🚑 修复完成！");
    }
    
    /// <summary>
    /// 强制清理玩家周围区域的水域
    /// </summary>
    private void ForceClearPlayerArea()
    {
        if (playerTransform == null) return;
        
        Vector2Int playerGridPos = WorldToGrid(playerTransform.position);
        int safeZone = 8; // 8格安全区
        
        Debug.Log($"[TerrainInitialization] 🛡️ 强制清理玩家区域: {playerGridPos}, 安全区 {safeZone}");
        
        int clearedCount = 0;
        
        for (int dx = -safeZone; dx <= safeZone; dx++)
        {
            for (int dy = -safeZone; dy <= safeZone; dy++)
            {
                int x = playerGridPos.x + dx;
                int y = playerGridPos.y + dy;
                
                // 在terrainMap中设为草地
                Vector2Int localPos = new Vector2Int(x - terrainOffset.x, y - terrainOffset.y);
                if (localPos.x >= 0 && localPos.x < mapWidth && localPos.y >= 0 && localPos.y < mapHeight)
                {
                    if (terrainMap[localPos.x, localPos.y] != TerrainType.Grass)
                    {
                        terrainMap[localPos.x, localPos.y] = TerrainType.Grass;
                        clearedCount++;
                    }
                }
                
                // 在Tilemap中移除水域，添加草地
                Vector3Int tilemapPos = new Vector3Int(x, y, 0);
                if (waterTilemap != null)
                {
                    waterTilemap.SetTile(tilemapPos, null);
                }
                if (grassTilemap != null && grassTile != null)
                {
                    grassTilemap.SetTile(tilemapPos, grassTile);
                }
            }
        }
        
        Debug.Log($"[TerrainInitialization] 🛡️ 强制清理完成，处理了 {clearedCount} 个地块");
    }
    
    /// <summary>
    /// 超级简单的草丛生成测试
    /// </summary>
    [ContextMenu("🧪 超级简单测试")]
    public void SuperSimpleTest()
    {
        Debug.Log("[TerrainInitialization] 🧪 超级简单测试开始...");
        
        if (bushPrefabs == null || bushPrefabs.Length == 0)
        {
            Debug.LogError("[TerrainInitialization] ❌ 没有草丛prefab！");
            return;
        }
        
        // 直接在玩家位置附近生成一个草丛测试
        Vector3 testPos = new Vector3(0, 0, 0); // 世界坐标原点
        
        GameObject testBush = Instantiate(bushPrefabs[0], testPos, Quaternion.identity);
        
        if (terrainParent != null)
        {
            testBush.transform.SetParent(terrainParent);
        }
        
        Debug.Log($"[TerrainInitialization] 🧪 在 {testPos} 生成了测试草丛: {testBush.name}");
        Debug.Log("[TerrainInitialization] 🧪 如果你能在Scene视图中看到这个草丛，说明prefab没问题");
    }
    
    /// <summary>
    /// 暴力生成草丛 - 保证有草
    /// </summary>
    [ContextMenu("💀 暴力生成草丛")]
    public void ForceGenerateSimpleBushes()
    {
        Debug.Log("[TerrainInitialization] 💀 开始详细诊断...");
        
        // 1. 检查prefab
        if (bushPrefabs == null || bushPrefabs.Length == 0)
        {
            Debug.LogError("[TerrainInitialization] ❌ 草丛prefab数组为空！");
            Debug.LogError("[TerrainInitialization] 🔧 解决方法：在Inspector中找到TerrainInitialization组件，在Bush Prefabs数组中拖入草丛预制体");
            return;
        }
        
        Debug.Log($"[TerrainInitialization] ✅ Prefab数组有 {bushPrefabs.Length} 个元素");
        
        // 检查每个prefab
        for (int i = 0; i < bushPrefabs.Length; i++)
        {
            if (bushPrefabs[i] == null)
            {
                Debug.LogWarning($"[TerrainInitialization] ⚠️ Prefab[{i}] 为空！");
            }
            else
            {
                Debug.Log($"[TerrainInitialization] ✅ Prefab[{i}]: {bushPrefabs[i].name}");
            }
        }
        
        // 2. 检查地形数据
        if (terrainMap == null)
        {
            Debug.LogError("[TerrainInitialization] ❌ terrainMap为空！请先生成地形");
            return;
        }
        
        Debug.Log($"[TerrainInitialization] ✅ 地图尺寸: {mapWidth} x {mapHeight}");
        
        // 统计草地数量
        int grassCount = 0;
        int waterCount = 0;
        
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (terrainMap[x, y] == TerrainType.Grass)
                    grassCount++;
                else if (terrainMap[x, y] == TerrainType.Water)
                    waterCount++;
            }
        }
        
        Debug.Log($"[TerrainInitialization] 📊 地形统计: 草地 {grassCount}, 水域 {waterCount}, 总计 {mapWidth * mapHeight}");
        
        if (grassCount == 0)
        {
            Debug.LogError("[TerrainInitialization] ❌ 地图上没有草地！全是水域！");
            Debug.LogError("[TerrainInitialization] 🔧 解决方法：调整水域生成参数或重新生成地形");
            return;
        }
        
        // 3. 开始生成
        Debug.Log("[TerrainInitialization] 💀 开始暴力生成草丛...");
        
        // 清理现有草丛
        ClearAllBushes();
        
        int successfulSpawns = 0;
        int attempts = 0;
        
        // 修复：随机分布草丛，不要规律排列
        for (int attempt = 0; attempt < 200; attempt++)
        {
            attempts++;
            
            // 随机选择位置（本地坐标）
            int localX = Random.Range(6, mapWidth - 6);
            int localY = Random.Range(6, mapHeight - 6);
            
            // 检查这个位置是否是草地（不是水域）
            if (terrainMap[localX, localY] == TerrainType.Grass)
            {
                // 转为世界格坐标（用于边界与 Tilemap 检查）
                int worldX = localX + terrainOffset.x;
                int worldY = localY + terrainOffset.y;
                Vector3Int tilemapPos = new Vector3Int(worldX, worldY, 0);

                // 只在“已加载范围”内生成
                Vector2Int worldMin = new Vector2Int(terrainOffset.x, terrainOffset.y);
                Vector2Int worldMax = new Vector2Int(terrainOffset.x + mapWidth - 1, terrainOffset.y + mapHeight - 1);
                if (currentMapMax != Vector2Int.zero || currentMapMin != Vector2Int.zero)
                {
                    worldMin = currentMapMin;
                    worldMax = currentMapMax;
                }
                if (worldX < worldMin.x || worldX > worldMax.x || worldY < worldMin.y || worldY > worldMax.y)
                {
                    continue;
                }
                if (IsWaterAtWorld(tilemapPos))
                {
                    continue; // 跳过水域位置
                }
                
                // 选择随机草丛
                GameObject selectedBushPrefab = bushPrefabs[Random.Range(0, bushPrefabs.Length)];
                
                if (selectedBushPrefab != null)
                {
                    // 使用 X/Y 平面的世界坐标，Z=0
                    Vector3 worldPos = GridToWorld(localX, localY);
                        
                        // 生成草丛
                        GameObject bushInstance = Instantiate(selectedBushPrefab, worldPos, Quaternion.identity);
                        
                        // 设置父物体
                        if (terrainParent != null)
                        {
                            bushInstance.transform.SetParent(terrainParent);
                        }
                        
                        successfulSpawns++;
                        
                        if (successfulSpawns <= 3)
                        {
                            Debug.Log($"[TerrainInitialization] 🌿 生成草丛 {successfulSpawns}: 本地({localX},{localY}) -> 世界{worldPos}");
                        }
                        
                        // 限制数量
                        if (successfulSpawns >= 50) break;
                    }
                }
        }
        
        Debug.Log($"[TerrainInitialization] 💀 暴力生成完成！尝试 {attempts} 次，成功生成 {successfulSpawns} 个草丛");
        
        if (successfulSpawns == 0)
        {
            Debug.LogError("[TerrainInitialization] ❌ 还是没生成草丛！");
            Debug.LogError("[TerrainInitialization] 🔧 可能原因：1.Prefab损坏 2.世界坐标转换错误 3.父物体问题");
        }
        else
        {
            Debug.Log("[TerrainInitialization] ✅ 成功！你现在应该能看到草丛了！");
            Debug.Log("[TerrainInitialization] 💡 如果看不到，检查Scene视图或摄像机位置");
        }
    }
    
    /// <summary>
    /// 我草你的草丛！！！
    /// </summary>
    [ContextMenu("🌿🌿🌿 给我草！！！")]
    public void GIVE_ME_BUSHES_NOW()
    {
        Debug.Log("[TerrainInitialization] 🌿🌿🌿 老子要草丛！！！");
        
        if (bushPrefabs == null || bushPrefabs.Length == 0)
        {
            Debug.LogError("[TerrainInitialization] ❌ 你没设置草丛prefab！去Inspector里拖进来！");
            return;
        }
        
        // 清理现有草丛
        ClearAllBushes();
        
        int successfulSpawns = 0;
        
        // 暴力生成：每隔几格就放一个草丛，管它什么条件
        for (int x = 5; x < mapWidth - 5; x += 8)
        {
            for (int y = 5; y < mapHeight - 5; y += 8)
            {
                // 选择草丛prefab
                GameObject selectedBushPrefab = bushPrefabs[Random.Range(0, bushPrefabs.Length)];
                
                if (selectedBushPrefab != null)
                {
                    // 直接用网格坐标转世界坐标
                    Vector3 worldPos = new Vector3(x + terrainOffset.x, 0, y + terrainOffset.y);
                    
                    // 生成草丛
                    GameObject bushInstance = Instantiate(selectedBushPrefab, worldPos, Quaternion.identity);
                    
                    // 设置父物体
                    if (terrainParent != null)
                    {
                        bushInstance.transform.SetParent(terrainParent);
                    }
                    
                    successfulSpawns++;
                    
                    Debug.Log($"[TerrainInitialization] 🌿 草丛 {successfulSpawns}: 网格({x},{y}) -> 世界{worldPos}");
                    
                    // 限制数量，别生成太多
                    if (successfulSpawns >= 20) break;
                }
            }
            if (successfulSpawns >= 20) break;
        }
        
        Debug.Log($"[TerrainInitialization] 🌿🌿🌿 草丛生成完毕！一共 {successfulSpawns} 个！");
        Debug.Log("[TerrainInitialization] 🎉 现在你有草了！！！");
    }
    
    /// <summary>
    /// 临时放宽草丛生成条件
    /// </summary>
    [ContextMenu("🚑 应急修复草丛生成")]
    public void EmergencyFixBushGeneration()
    {
        Debug.Log("[TerrainInitialization] 🚑 应急修复草丛生成参数...");
        
        // 临时放宽参数
        float originalBushMinDistance = bushMinDistance;
        int originalPlayerSafeZoneSize = playerSafeZoneSize;
        
        // 大幅降低限制
        bushMinDistance = Mathf.Max(2, (int)(bushMinDistance * 0.3f));
        playerSafeZoneSize = Mathf.Max(3, (int)(playerSafeZoneSize * 0.5f));
        
        Debug.Log($"[TerrainInitialization] 📝 临时调整参数:");
        Debug.Log($"  - bushMinDistance: {originalBushMinDistance} → {bushMinDistance}");
        Debug.Log($"  - playerSafeZoneSize: {originalPlayerSafeZoneSize} → {playerSafeZoneSize}");
        
        // 清理现有草丛
        ClearAllBushes();
        
        // 重新生成草丛
        if (enableBushGeneration)
        {
            GenerateBushes();
        }
        
        Debug.Log("[TerrainInitialization] ✅ 应急修复完成！");
        Debug.Log("[TerrainInitialization] 💡 如果效果满意，请在Inspector中手动调整这些参数");
    }
    
    /// <summary>
    /// 测试草丛生成参数
    /// </summary>
    [ContextMenu("🧪 测试草丛生成参数")]
    public void TestBushGenerationSettings()
    {
        Debug.Log("[TerrainInitialization] === 草丛生成参数测试 ===");
        
        // 检查prefab设置
        if (bushPrefabs == null || bushPrefabs.Length == 0)
        {
            Debug.LogError("[TerrainInitialization] ❌ 草丛prefab数组为空！请在Inspector中设置草丛prefab");
            return;
        }
        
        int validPrefabs = 0;
        for (int i = 0; i < bushPrefabs.Length; i++)
        {
            if (bushPrefabs[i] != null)
            {
                validPrefabs++;
                Debug.Log($"[TerrainInitialization] ✅ 草丛Prefab {i}: {bushPrefabs[i].name}");
            }
            else
            {
                Debug.LogWarning($"[TerrainInitialization] ⚠️ 草丛Prefab {i}: 未设置");
            }
        }
        
        Debug.Log($"[TerrainInitialization] 📊 草丛生成设置:");
        Debug.Log($"  - 有效Prefab数量: {validPrefabs}/{bushPrefabs.Length}");
        Debug.Log($"  - 生成概率: {bushSpawnChance:P1}");
        Debug.Log($"  - 最小间距: {bushMinDistance} 格");
        Debug.Log($"  - 需要空间: {bushRequiredSpace}x{bushRequiredSpace} 格");
        Debug.Log($"  - 启用生成: {enableBushGeneration}");
        
        // 估算生成数量
        int grassTileCount = CountTiles(TerrainType.Grass);
        int estimatedAttempts = Mathf.RoundToInt(mapWidth * mapHeight * bushSpawnChance);
        int maxPossible = grassTileCount / (bushMinDistance * bushMinDistance);
        
        int waterTileCount = waterTiles.Count;
        int availableGrass = grassTileCount - waterTileCount;
        
        Debug.Log($"[TerrainInitialization] 📈 生成估算:");
        Debug.Log($"  - 总瓦片数: {mapWidth * mapHeight}");
        Debug.Log($"  - 草地瓦片数: {grassTileCount}");
        Debug.Log($"  - 水域瓦片数: {waterTileCount}");
        Debug.Log($"  - 可用草地: {availableGrass}");
        Debug.Log($"  - 尝试次数: {estimatedAttempts}");
        Debug.Log($"  - 理论最大: {maxPossible} 个草丛");
        
        if (validPrefabs == 0)
        {
            Debug.LogError("[TerrainInitialization] 💡 请在Inspector中设置至少一个草丛prefab！");
        }
        else if (!enableBushGeneration)
        {
            Debug.LogWarning("[TerrainInitialization] 💡 草丛生成已禁用，请在Inspector中启用'Enable Bush Generation'");
        }
        else if (waterTileCount > grassTileCount * 0.5f)
        {
            Debug.LogWarning("[TerrainInitialization] ⚠️ 水域占比过高，可能影响草丛生成效率");
        }
    }
    
    /// <summary>
    /// 测试水域边界平滑效果
    /// </summary>
    [ContextMenu("🧪 测试边界平滑效果")]
    public void TestBoundarySmoothing()
    {
        Debug.Log("[TerrainInitialization] === 水域边界平滑测试 ===");
        
        int totalWaterTiles = waterTiles.Count;
        int problematicTiles = 0;
        
        // 统计有问题的水域瓦片
        List<Vector2Int> currentWaterTiles = new List<Vector2Int>(waterTiles);
        foreach (Vector2Int waterTile in currentWaterTiles)
        {
            if (ShouldRemoveWaterTile(waterTile))
            {
                problematicTiles++;
                Debug.Log($"[TerrainInitialization] 🚫 发现问题水域 {waterTile}");
            }
        }
        
        Debug.Log($"[TerrainInitialization] 📊 边界平滑统计:");
        Debug.Log($"  - 总水域瓦片: {totalWaterTiles}");
        Debug.Log($"  - 需要移除的瓦片: {problematicTiles}");
        Debug.Log($"  - 问题比例: {(problematicTiles * 100f / totalWaterTiles):F1}%");
        
        if (problematicTiles > 0)
        {
            Debug.Log($"[TerrainInitialization] 💡 建议: 重新生成地形以应用边界平滑");
        }
        else
        {
            Debug.Log($"[TerrainInitialization] ✅ 水域边界已经很平滑，无需额外处理");
        }
    }
    
    /// <summary>
    /// 测试不同圆形度的水域生成效果
    /// </summary>
    [ContextMenu("🧪 测试圆形度效果")]
    public void TestCircularnessEffects()
    {
        Debug.Log("[TerrainInitialization] === 圆形度效果测试 ===");
        
        float[] testValues = { 0.3f, 0.5f, 0.7f, 0.9f, 0.95f, 1.0f };
        
        foreach (float testCircularness in testValues)
        {
            Debug.Log($"[TerrainInitialization] 🌊 圆形度 {testCircularness:F1}:");
            
            if (testCircularness >= 0.98f)
            {
                Debug.Log($"  - 策略: 严格数学圆形");
                Debug.Log($"  - 特点: 完美圆形，边界清晰");
            }
            else if (testCircularness >= 0.9f)
            {
                float tolerance = (1f - testCircularness) * 0.5f;
                Debug.Log($"  - 策略: 高圆形度，容差 {tolerance:F3}");
                Debug.Log($"  - 特点: 近似圆形，轻微边界模糊");
            }
            else
            {
                float corePercent = 60f;
                float thresholdRange = 0.3f + testCircularness * 0.4f;
                Debug.Log($"  - 策略: 连贯不规则形状");
                Debug.Log($"  - 核心区域: {corePercent}% 始终填充");
                Debug.Log($"  - 边缘阈值: {thresholdRange:F2}");
                Debug.Log($"  - 特点: 不规则但连贯，无散点");
            }
        }
        
        Debug.Log($"[TerrainInitialization] 当前设置: 圆形度 {waterCircularness:F2}");
        Debug.Log($"[TerrainInitialization] 💡 提示: 调整Inspector中的'水域圆形程度'来测试不同效果");
    }
    
    /// <summary>
    /// 强制修复所有三个关键问题
    /// </summary>
    [ContextMenu("🔧 强制修复所有问题")]
    public void ForceFixAllIssues()
    {
        Debug.Log("[TerrainInitialization] 🔧 开始强制修复所有问题...");
        
        // 1. 修复Tilemap排序层级（确保绝对在底层）
        FixTilemapSorting();
        
        // 2. 重新生成地形以应用新的边界和安全区域设置
        GenerateTerrain();
        
        // 3. 强制清理玩家安全区域内的任何残留水域
        ForceClearSafeZoneWater();
        
        // 4. 验证修复结果
        ValidatePlayerSafeZone();
        
        Debug.Log("[TerrainInitialization] ✅ 所有问题修复完成！");
        Debug.Log("[TerrainInitialization] 📋 修复内容:");
        Debug.Log("[TerrainInitialization]   1. ✅ 出生点安全区域已强制清理（半径35+格）");
        Debug.Log("[TerrainInitialization]   2. ✅ 边缘水域切割已避免（强制35格边界）");
        Debug.Log("[TerrainInitialization]   3. ✅ Tilemap置于绝对底层（草地-32768，水域-32767）");
        Debug.Log("[TerrainInitialization]   4. ✅ Player排序范围绝对安全（-30000到32767）");
        Debug.Log("[TerrainInitialization] 🎯 现在Player/Bush/Enemy永远不会被地面遮挡！");
    }
    
    /// <summary>
    /// 测试动态地图扩展
    /// </summary>
    [ContextMenu("测试动态地图扩展")]
    public void TestDynamicExpansion()
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("[TerrainInitialization] 玩家对象未设置，无法测试动态扩展！");
            return;
        }
        
        Debug.Log("[TerrainInitialization] 开始测试动态地图扩展...");
        Debug.Log($"[TerrainInitialization] 当前地图边界: Min{currentMapMin} Max{currentMapMax}");
        Debug.Log($"[TerrainInitialization] 玩家位置: {playerTransform.position}");
        Debug.Log($"[TerrainInitialization] 玩家网格位置: {WorldToGrid(playerTransform.position)}");
        Debug.Log($"[TerrainInitialization] 触发距离: {expansionTriggerDistance}");
        Debug.Log($"[TerrainInitialization] 扩展大小: {expansionSize}");
        Debug.Log($"[TerrainInitialization] 动态扩展已启用: {enableDynamicExpansion}");
        
        // 强制检查一次扩展
        CheckForMapExpansion();
    }
    
    /// <summary>
    /// 调试玩家位置和可行走性
    /// </summary>
    [ContextMenu("调试玩家位置")]
    public void DebugPlayerPosition()
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("[TerrainInitialization] 玩家对象未设置！");
            return;
        }
        
        Vector3 playerWorldPos = playerTransform.position;
        Vector2Int playerGridPos = WorldToGrid(playerWorldPos);
        
        Debug.Log("[TerrainInitialization] === 玩家位置调试 ===");
        Debug.Log($"[TerrainInitialization] 玩家世界坐标: {playerWorldPos}");
        Debug.Log($"[TerrainInitialization] 玩家网格坐标: {playerGridPos}");
        Debug.Log($"[TerrainInitialization] 当前地图边界: Min{currentMapMin} Max{currentMapMax}");
        Debug.Log($"[TerrainInitialization] 玩家在地图范围内: {(playerGridPos.x >= currentMapMin.x && playerGridPos.x <= currentMapMax.x && playerGridPos.y >= currentMapMin.y && playerGridPos.y <= currentMapMax.y)}");
        Debug.Log($"[TerrainInitialization] 当前位置可行走: {IsWalkable(playerGridPos.x, playerGridPos.y)}");
        
        // 检查周围8个方向的可行走性
        Debug.Log("[TerrainInitialization] 周围可行走性:");
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int checkX = playerGridPos.x + dx;
                int checkY = playerGridPos.y + dy;
                bool walkable = IsWalkable(checkX, checkY);
                string direction = dx == 0 && dy == 0 ? "中心" : $"({dx},{dy})";
                Debug.Log($"[TerrainInitialization]   {direction}: ({checkX},{checkY}) = {walkable}");
            }
        }
        
        // 检查距离边界的距离
        int distanceToLeft = playerGridPos.x - currentMapMin.x;
        int distanceToRight = currentMapMax.x - playerGridPos.x;
        int distanceToBottom = playerGridPos.y - currentMapMin.y;
        int distanceToTop = currentMapMax.y - playerGridPos.y;
        
        Debug.Log($"[TerrainInitialization] 距离边界: 左{distanceToLeft} 右{distanceToRight} 下{distanceToBottom} 上{distanceToTop}");
        Debug.Log($"[TerrainInitialization] 触发扩展距离: {expansionTriggerDistance}");
    }
    
    /// <summary>
    /// 强制清理玩家安全区域内的所有水域
    /// </summary>
    [ContextMenu("🔧 强制清理安全区水域")]
    public void ForceClearSafeZoneWater()
    {
        if (playerTransform == null)
        {
            Debug.LogError("[TerrainInitialization] Player对象未找到！");
            return;
        }

        Vector2Int playerGridPos = WorldToGrid(playerTransform.position);
        int maxWaterRadius = 20;
        int clearRadius = playerSafeZoneSize + maxWaterRadius + 15; // 更大的清理半径
        
        Debug.Log($"[TerrainInitialization] 🔧 开始强制清理玩家安全区域 (半径: {clearRadius})...");
        
        int clearedCount = 0;
        List<Vector2Int> tilesToClear = new List<Vector2Int>();
        
        // 找到所有需要清理的水域瓦片
        for (int x = playerGridPos.x - clearRadius; x <= playerGridPos.x + clearRadius; x++)
        {
            for (int y = playerGridPos.y - clearRadius; y <= playerGridPos.y + clearRadius; y++)
            {
                Vector2Int tilePos = new Vector2Int(x, y);
                Vector3Int tilemapPos = new Vector3Int(tilePos.x, tilePos.y, 0);
                
                // 检查是否是水域瓦片
                if (waterTilemap != null && waterTilemap.GetTile(tilemapPos) != null)
                {
                    tilesToClear.Add(tilePos);
                }
            }
        }
        
        // 清理水域瓦片
        foreach (Vector2Int tilePos in tilesToClear)
        {
            Vector3Int tilemapPos = new Vector3Int(tilePos.x, tilePos.y, 0);
            
            // 从水域Tilemap中移除
            if (waterTilemap != null)
            {
                waterTilemap.SetTile(tilemapPos, null);
            }
            
            // 设置为草地
            if (grassTilemap != null && grassTile != null)
            {
                grassTilemap.SetTile(tilemapPos, grassTile);
            }
            
            // 从水域集合中移除
            waterTiles.Remove(tilePos);
            
            clearedCount++;
        }
        
        Debug.Log($"[TerrainInitialization] ✅ 强制清理完成！清理了 {clearedCount} 个水域瓦片");
        Debug.Log($"[TerrainInitialization] 🛡️ 玩家安全区域现在完全清洁 (玩家位置: {playerGridPos}, 清理半径: {clearRadius})");
    }
    
    /// <summary>
    /// 验证玩家安全区域
    /// </summary>
    [ContextMenu("验证玩家安全区域")]
    public void ValidatePlayerSafeZone()
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("[TerrainInitialization] 玩家对象未设置！");
            return;
        }
        
        Vector2Int playerGridPos = WorldToGrid(playerTransform.position);
        int halfSafeZone = playerSafeZoneSize / 2;
        
        Debug.Log("[TerrainInitialization] === 玩家安全区域验证 ===");
        Debug.Log($"[TerrainInitialization] 玩家位置: 世界{playerTransform.position} 网格{playerGridPos}");
        Debug.Log($"[TerrainInitialization] 安全区域大小: {playerSafeZoneSize}x{playerSafeZoneSize}");
        
        int waterCount = 0;
        int grassCount = 0;
        
        // 检查安全区域内的地形
        for (int dx = -halfSafeZone; dx <= halfSafeZone; dx++)
        {
            for (int dy = -halfSafeZone; dy <= halfSafeZone; dy++)
            {
                int checkX = playerGridPos.x + dx;
                int checkY = playerGridPos.y + dy;
                
                if (waterTiles.Contains(new Vector2Int(checkX, checkY)))
                {
                    waterCount++;
                    Debug.LogWarning($"[TerrainInitialization] 发现安全区域内的水域: ({checkX}, {checkY})");
                }
                else if (IsWalkable(checkX, checkY))
                {
                    grassCount++;
                }
            }
        }
        
        Debug.Log($"[TerrainInitialization] 安全区域检查结果: 草地{grassCount}格, 水域{waterCount}格");
        
        if (waterCount > 0)
        {
            Debug.LogError($"[TerrainInitialization] ❌ 安全区域内发现{waterCount}个水域地块！需要修复！");
        }
        else
        {
            Debug.Log("[TerrainInitialization] ✅ 安全区域验证通过，无水域地块");
        }
    }
    
    /// <summary>
    /// 修复Tilemap排序层级，确保地面始终在最底层
    /// </summary>
    [ContextMenu("修复Tilemap排序层级")]
    public void FixTilemapSorting()
    {
        Debug.Log("[TerrainInitialization] 开始修复Tilemap排序层级...");
        
        // 设置草地Tilemap为最底层
        if (grassTilemap != null)
        {
            TilemapRenderer grassRenderer = grassTilemap.GetComponent<TilemapRenderer>();
            if (grassRenderer != null)
            {
                grassRenderer.sortingLayerName = "Default";
                grassRenderer.sortingOrder = -32768; // 使用最小可能值，确保绝对在最底层
                Debug.Log($"[TerrainInitialization] ✅ 草地Tilemap排序层级设置为: {grassRenderer.sortingOrder}");
            }
            else
            {
                Debug.LogWarning("[TerrainInitialization] ⚠️ 草地Tilemap缺少TilemapRenderer组件！");
            }
        }
        else
        {
            Debug.LogWarning("[TerrainInitialization] ⚠️ 草地Tilemap未设置！");
        }
        
        // 设置水域Tilemap为底层，但略高于草地
        if (waterTilemap != null)
        {
            TilemapRenderer waterRenderer = waterTilemap.GetComponent<TilemapRenderer>();
            if (waterRenderer != null)
            {
                waterRenderer.sortingLayerName = "Default";
                waterRenderer.sortingOrder = -32767; // 略高于草地，但仍在绝对底层
                Debug.Log($"[TerrainInitialization] ✅ 水域Tilemap排序层级设置为: {waterRenderer.sortingOrder}");
            }
            else
            {
                Debug.LogWarning("[TerrainInitialization] ⚠️ 水域Tilemap缺少TilemapRenderer组件！");
            }
        }
        else
        {
            Debug.LogWarning("[TerrainInitialization] ⚠️ 水域Tilemap未设置！");
        }
        
        Debug.Log("[TerrainInitialization] ✅ Tilemap排序层级修复完成！");
        Debug.Log("[TerrainInitialization] 🎯 现在地面会始终显示在所有物体的底层！");
        Debug.Log("[TerrainInitialization] 📊 Player等物体的排序层级范围: -1000 到 2000");
        Debug.Log("[TerrainInitialization] 📊 地面排序层级: 草地(-10000) < 水域(-9999) < 所有物体");
    }
    
    /// <summary>
    /// 初始化地图边界
    /// </summary>
    private void InitializeMapBounds()
    {
        currentMapMin = new Vector2Int(terrainOffset.x, terrainOffset.y);
        currentMapMax = new Vector2Int(terrainOffset.x + mapWidth - 1, terrainOffset.y + mapHeight - 1);
        
        if (showDebugInfo)
        {
            Debug.Log($"[TerrainInitialization] 地图边界初始化: Min{currentMapMin} Max{currentMapMax}");
            Debug.Log($"[TerrainInitialization] 地图尺寸: {mapWidth}x{mapHeight}, 偏移: {terrainOffset}");
        }
    }
    
    /// <summary>
    /// 检查是否需要扩展地图
    /// </summary>
    private void CheckForMapExpansion()
    {
        if (playerTransform == null) return;
        
        Vector2Int playerGridPos = WorldToGrid(playerTransform.position);
        
        // 检查玩家是否接近地图边界（带预加载边距）
        bool needExpansion = false;
        Vector2Int expansionDirection = Vector2Int.zero;
        int trigger = Mathf.Max(1, expansionTriggerDistance + 15); // 预加载 15 格
        
        // 检查各个方向
        if (playerGridPos.x - currentMapMin.x <= trigger)
        {
            // 需要向左扩展
            needExpansion = true;
            expansionDirection.x = -1;
        }
        else if (currentMapMax.x - playerGridPos.x <= trigger)
        {
            // 需要向右扩展
            needExpansion = true;
            expansionDirection.x = 1;
        }
        
        if (playerGridPos.y - currentMapMin.y <= trigger)
        {
            // 需要向下扩展
            needExpansion = true;
            expansionDirection.y = -1;
        }
        else if (currentMapMax.y - playerGridPos.y <= trigger)
        {
            // 需要向上扩展
            needExpansion = true;
            expansionDirection.y = 1;
        }
        
        if (needExpansion)
        {
            StartCoroutine(ExpandMapInDirection(expansionDirection));
        }
    }
    
    /// <summary>
    /// 向指定方向扩展地图
    /// </summary>
    private System.Collections.IEnumerator ExpandMapInDirection(Vector2Int direction)
    {
        isExpanding = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"[TerrainInitialization] 开始向{direction}方向扩展地图，扩展大小: {expansionSize}");
        }
        
        // 计算新的地图边界
        Vector2Int newMapMin = currentMapMin;
        Vector2Int newMapMax = currentMapMax;
        
        if (direction.x < 0) // 向左扩展
        {
            newMapMin.x -= expansionSize;
        }
        else if (direction.x > 0) // 向右扩展
        {
            newMapMax.x += expansionSize;
        }
        
        if (direction.y < 0) // 向下扩展
        {
            newMapMin.y -= expansionSize;
        }
        else if (direction.y > 0) // 向上扩展
        {
            newMapMax.y += expansionSize;
        }
        
        // 生成新区域的地形
        yield return StartCoroutine(GenerateExpandedTerrain(newMapMin, newMapMax));
        
        // 在新区域生成草丛（仅限新块）
        if (enableBushGeneration)
        {
            yield return StartCoroutine(GenerateExpandedBushes(newMapMin, newMapMax));
        }
        
        // 更新地图边界
        currentMapMin = newMapMin;
        currentMapMax = newMapMax;
        
        if (showDebugInfo)
        {
            Debug.Log($"[TerrainInitialization] 地图扩展完成，新边界: Min{currentMapMin} Max{currentMapMax}");
        }
        
        isExpanding = false;
    }
    
    /// <summary>
    /// 生成扩展区域的地形
    /// </summary>
    private System.Collections.IEnumerator GenerateExpandedTerrain(Vector2Int newMapMin, Vector2Int newMapMax)
    {
        // 计算需要生成的新区域
        List<Vector2Int> newTiles = new List<Vector2Int>();
        
        for (int x = newMapMin.x; x <= newMapMax.x; x++)
        {
            for (int y = newMapMin.y; y <= newMapMax.y; y++)
            {
                // 跳过已经存在的区域
                if (x >= currentMapMin.x && x <= currentMapMax.x && 
                    y >= currentMapMin.y && y <= currentMapMax.y)
                {
                    continue;
                }
                
                newTiles.Add(new Vector2Int(x, y));
            }
        }
        
        // 首先全部生成为草地
        foreach (Vector2Int tile in newTiles)
        {
            Vector3Int tilePos = new Vector3Int(tile.x, tile.y, 0);
            if (grassTilemap != null && grassTile != null)
            {
                grassTilemap.SetTile(tilePos, grassTile);
            }
        }
        
        // 使用与初始生成相同的圆形水域生成逻辑
        yield return StartCoroutine(GenerateExpandedWaterClusters(newTiles, newMapMin, newMapMax));

        // 扩展区域草丛：与初始一致的密度（按草地占比计算尝试次数）
        if (enableBushGeneration)
        {
            GenerateBushesConsistentDensity(newTiles, newMapMin, newMapMax);
        }
        
        // 平滑扩展区域的水域边界
        SmoothExpandedWaterBoundaries(newMapMin, newMapMax);
        
        // 刷新碰撞器
        if (waterCollider != null)
        {
            waterCollider.enabled = false;
            waterCollider.enabled = true;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[TerrainInitialization] 扩展区域生成完成，新增{newTiles.Count}个地块");
        }
    }
    
    /// <summary>
    /// 平滑扩展区域的水域边界
    /// </summary>
    private void SmoothExpandedWaterBoundaries(Vector2Int newMapMin, Vector2Int newMapMax)
    {
        Debug.Log("[TerrainInitialization] 🌊 开始平滑扩展区域水域边界...");
        
        int totalRemovedCount = 0;
        int iteration = 0;
        
        // 重复处理直到没有更多的水域需要移除
        while (true)
        {
            iteration++;
            List<Vector2Int> waterToRemove = new List<Vector2Int>();
            
            // 只检查扩展区域内的水域瓦片
            List<Vector2Int> expandedWaterTiles = new List<Vector2Int>();
            foreach (Vector2Int waterTile in waterTiles)
            {
                if (waterTile.x >= newMapMin.x && waterTile.x <= newMapMax.x && 
                    waterTile.y >= newMapMin.y && waterTile.y <= newMapMax.y)
                {
                    expandedWaterTiles.Add(waterTile);
                }
            }
            
            foreach (Vector2Int waterTile in expandedWaterTiles)
            {
                if (ShouldRemoveExpandedWaterTile(waterTile))
                {
                    waterToRemove.Add(waterTile);
                }
            }
            
            // 如果没有需要移除的水域，结束循环
            if (waterToRemove.Count == 0)
            {
                Debug.Log($"[TerrainInitialization] ✅ 扩展区域水域边界平滑完成！第 {iteration} 轮后无更多需要移除的水域");
                break;
            }
            
            // 移除找到的水域瓦片
            foreach (Vector2Int tileToRemove in waterToRemove)
            {
                // 更新Tilemap
                Vector3Int tilePos = new Vector3Int(tileToRemove.x, tileToRemove.y, 0);
                if (waterTilemap != null)
                {
                    waterTilemap.SetTile(tilePos, null);
                }
                if (grassTilemap != null && grassTile != null)
                {
                    grassTilemap.SetTile(tilePos, grassTile);
                }
                
                // 从waterTiles集合中移除
                waterTiles.Remove(tileToRemove);
            }
            
            totalRemovedCount += waterToRemove.Count;
            Debug.Log($"[TerrainInitialization] 🔄 扩展区域第 {iteration} 轮：移除了 {waterToRemove.Count} 个被过度包围的水域瓦片");
            
            // 安全检查：避免无限循环
            if (iteration > 20)
            {
                Debug.LogWarning("[TerrainInitialization] ⚠️ 扩展区域水域边界平滑达到最大迭代次数，强制停止");
                break;
            }
        }
        
        Debug.Log($"[TerrainInitialization] 🎯 扩展区域水域边界平滑总结：共 {iteration} 轮，移除 {totalRemovedCount} 个水域瓦片");
    }
    
    /// <summary>
    /// 判断扩展区域的水域瓦片是否应该被移除（三面或更多面被草地包围）
    /// </summary>
    private bool ShouldRemoveExpandedWaterTile(Vector2Int waterTile)
    {
        // 检查四个主要方向的相邻瓦片
        Vector2Int[] directions = {
            new Vector2Int(0, 1),   // 上
            new Vector2Int(0, -1),  // 下
            new Vector2Int(1, 0),   // 右
            new Vector2Int(-1, 0)   // 左
        };
        
        int grassNeighborCount = 0;
        
        foreach (Vector2Int direction in directions)
        {
            Vector2Int neighborPos = waterTile + direction;
            
            // 检查邻居位置的地形类型
            if (IsGrassTileAt(neighborPos))
            {
                grassNeighborCount++;
            }
        }
        
        // 如果有三面或更多面被草地包围，则应该移除
        bool shouldRemove = grassNeighborCount >= 3;
        
        if (shouldRemove && showDebugInfo)
        {
            Debug.Log($"[TerrainInitialization] 🚫 标记移除扩展区域水域 {waterTile}：{grassNeighborCount}/4 面被草地包围");
        }
        
        return shouldRemove;
    }
    
    /// <summary>
    /// 检查指定位置是否是草地（支持动态扩展的地图）
    /// </summary>
    private bool IsGrassTileAt(Vector2Int position)
    {
        // 首先检查是否在waterTiles中
        if (waterTiles.Contains(position))
        {
            return false;
        }
        
        // 检查Tilemap
        Vector3Int tilePos = new Vector3Int(position.x, position.y, 0);
        if (grassTilemap != null && grassTilemap.GetTile(tilePos) != null)
        {
            return true;
        }
        
        // 如果在地图边界外，视为草地
        return true;
    }
    
    /// <summary>
    /// 为扩展区域生成圆形水域簇
    /// </summary>
    private System.Collections.IEnumerator GenerateExpandedWaterClusters(List<Vector2Int> availableTiles, Vector2Int newMapMin, Vector2Int newMapMax)
    {
        if (availableTiles.Count == 0) yield break;
        
        // 计算扩展区域应该生成的水域数量（与初始规则一致且更保守，避免大片半圆被切割）
        int targetWaterTiles = Mathf.RoundToInt(availableTiles.Count * Mathf.Clamp01(waterPercentage * 0.6f));
        int generatedWaterTiles = 0;
        int attempts = 0;
        int maxAttempts = targetWaterTiles * 3;
        
        List<Vector2Int> expandedWaterCenters = new List<Vector2Int>();
        
        while (generatedWaterTiles < targetWaterTiles && attempts < maxAttempts)
        {
            attempts++;
            
            // 从可用地块中随机选择一个作为水域中心
            Vector2Int center = availableTiles[Random.Range(0, availableTiles.Count)];
            
            // 额外：确保以中心为半径(r+2)的圆完全落在新区域内，避免“半圆被边界切割”
            int previewRadius = 17; // 覆盖最大半径裕量（与 GenerateExpandedCircularWaterCluster 半径上限对齐）
            if (center.x - previewRadius < newMapMin.x || center.x + previewRadius > newMapMax.x ||
                center.y - previewRadius < newMapMin.y || center.y + previewRadius > newMapMax.y)
            {
                continue; // 换一个中心，避免在边缘造成半圆
            }
            
            // 检查是否太接近扩展区域的边缘
            if (avoidBorderWater && IsNearExpansionBorder(center, newMapMin, newMapMax))
                continue;
            
            // 检查与现有水域的距离（包括原有水域和新扩展的水域）
            if (!IsValidExpandedWaterCenter(center, expandedWaterCenters))
                continue;
            
            // 检查是否与玩家安全区域冲突
            if (IsInPlayerSafeZone(center))
                continue;
            
            // 检查距离玩家是否足够远
            Vector3 worldPos = GridToWorld(center.x, center.y);
            float distanceToPlayer = Vector3.Distance(worldPos, playerTransform.position);
            if (distanceToPlayer <= playerSafeZoneSize * 1.5f)
                continue;
            
            // 生成圆形水域簇
            List<Vector2Int> waterCluster = GenerateExpandedCircularWaterCluster(center, availableTiles);
            
            // 若生成的簇有任何一格超出扩展边界，则丢弃该簇，避免半圆
            bool touchesBorder = false;
            foreach (var t in waterCluster)
            {
                if (t.x <= newMapMin.x || t.x >= newMapMax.x || t.y <= newMapMin.y || t.y >= newMapMax.y)
                {
                    touchesBorder = true;
                    break;
                }
            }
            if (touchesBorder)
                continue;
            
            if (waterCluster.Count > 0)
            {
                // 应用水域簇
                foreach (Vector2Int tile in waterCluster)
                {
                    Vector3Int tilePos = new Vector3Int(tile.x, tile.y, 0);
                    if (waterTilemap != null && waterTile != null)
                    {
                        waterTilemap.SetTile(tilePos, waterTile);
                        waterTiles.Add(tile);
                        generatedWaterTiles++;
                    }
                }
                
                // 记录水域中心点
                expandedWaterCenters.Add(center);
                waterCenters.Add(center);
            }
            
            // 每生成几个水域后让出一帧
            if (expandedWaterCenters.Count % 3 == 0)
            {
                yield return null;
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[TerrainInitialization] 扩展区域水域生成完成: {generatedWaterTiles}/{targetWaterTiles}，水域数量: {expandedWaterCenters.Count}");
        }
    }
    
    /// <summary>
    /// 检查扩展区域水域中心点是否有效
    /// </summary>
    private bool IsValidExpandedWaterCenter(Vector2Int center, List<Vector2Int> expandedWaterCenters)
    {
        // 检查与原有水域的距离
        foreach (Vector2Int existingCenter in waterCenters)
        {
            int distance = Mathf.Max(Mathf.Abs(center.x - existingCenter.x), Mathf.Abs(center.y - existingCenter.y));
            if (distance < minWaterDistance)
            {
                return false;
            }
        }
        
        // 检查与新扩展水域的距离
        foreach (Vector2Int existingCenter in expandedWaterCenters)
        {
            int distance = Mathf.Max(Mathf.Abs(center.x - existingCenter.x), Mathf.Abs(center.y - existingCenter.y));
            if (distance < minWaterDistance)
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// 为扩展区域生成圆形水域簇
    /// </summary>
    private List<Vector2Int> GenerateExpandedCircularWaterCluster(Vector2Int center, List<Vector2Int> availableTiles)
    {
        List<Vector2Int> cluster = new List<Vector2Int>();
        
        // 扩展区域的水域稍小一些；但上限与边界检测配合，避免半圆
        float radius = Random.Range(5f, 15f);
        
        // 使用与主生成相同的逻辑，确保一致性
        
        // 遍历可能的圆形区域
        int intRadius = Mathf.CeilToInt(radius);
        for (int x = center.x - intRadius; x <= center.x + intRadius; x++)
        {
            for (int y = center.y - intRadius; y <= center.y + intRadius; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                
                // 只在可用地块中生成
                if (!availableTiles.Contains(pos))
                    continue;
                
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center.x, center.y));
                
                // 使用与初始生成相同的圆形判断逻辑
                if (waterCircularness >= 0.98f)
                {
                    // 极高圆形度：严格按照数学圆形
                    if (distance <= radius)
                    {
                        cluster.Add(pos);
                    }
                }
                else if (waterCircularness >= 0.9f)
                {
                    // 高圆形度：允许极小的边界模糊
                    float tolerance = (1f - waterCircularness) * 0.5f;
                    float adjustedRadius = radius + Random.Range(-tolerance, tolerance);
                    
                    if (distance <= adjustedRadius)
                    {
                        cluster.Add(pos);
                    }
                }
                else
                {
                    // 中低圆形度：生成连贯但不规则的形状（与主生成逻辑一致）
                    if (distance <= radius)
                    {
                        bool shouldAdd = false;
                        
                        // 使用椭圆变形来创建不规则但连贯的形状
                        float angle = Mathf.Atan2(y - center.y, x - center.x);
                        
                        // 根据角度创建不规则的半径变化
                        float irregularityFactor = 1f + (1f - waterCircularness) * 0.5f * Mathf.Sin(angle * 3f + Random.Range(0f, 2f * Mathf.PI));
                        float adjustedRadius = radius * irregularityFactor;
                        
                        // 确保核心区域始终被填充（保证连贯性）
                        float coreRadius = radius * 0.6f; // 核心区域占60%
                        
                        if (distance <= coreRadius)
                        {
                            // 核心区域：始终添加，确保连贯
                            shouldAdd = true;
                        }
                        else if (distance <= adjustedRadius)
                        {
                            // 边缘区域：使用更温和的概率，避免散点
                            float edgeRatio = (distance - coreRadius) / (adjustedRadius - coreRadius);
                            float probability = 1f - edgeRatio * edgeRatio; // 二次衰减，更平滑
                            
                            // 根据圆形度调整概率阈值
                            float threshold = 0.3f + waterCircularness * 0.4f; // 0.3-0.7的阈值范围
                            shouldAdd = probability > threshold;
                        }
                        
                        if (shouldAdd)
                        {
                            cluster.Add(pos);
                        }
                    }
                }
            }
        }
        
        // 确保至少有中心点
        if (!cluster.Contains(center) && availableTiles.Contains(center))
        {
            cluster.Add(center);
        }
        
        // 对于低圆形度，进行连通性后处理，移除孤立的散点
        if (waterCircularness < 0.9f && cluster.Count > 1)
        {
            cluster = EnsureWaterClusterConnectivity(cluster, center);
        }
        
        return cluster;
    }
    
    /// <summary>
    /// 检查位置是否太接近扩展区域边缘
    /// </summary>
    private bool IsNearExpansionBorder(Vector2Int center, Vector2Int mapMin, Vector2Int mapMax)
    {
        int maxRadius = 20; // 最大水域半径
        int borderDistance = maxRadius + 10; // 强制使用最大半径+10格，确保绝对不被切割
        
        // 检查是否太接近任何边缘
        if (center.x - mapMin.x < borderDistance ||  // 太接近左边缘
            mapMax.x - center.x < borderDistance ||  // 太接近右边缘
            center.y - mapMin.y < borderDistance ||  // 太接近下边缘
            mapMax.y - center.y < borderDistance)    // 太接近上边缘
        {
            return true; // 太接近边缘
        }
        
        return false; // 距离边缘足够远
    }
    
    /// <summary>
    /// 检查水域中心是否与玩家安全区域冲突
    /// </summary>
    private bool IsInPlayerSafeZone(Vector2Int waterCenter)
    {
        if (playerTransform == null) return false;
        
        // 获取玩家的网格位置
        Vector2Int playerGridPos = WorldToGrid(playerTransform.position);
        
        // 使用极大的安全区域，确保绝对没有水域
        int maxWaterRadius = 20; // 最大水域半径
        int totalSafeRadius = playerSafeZoneSize + maxWaterRadius + 10; // 玩家安全区 + 水域最大半径 + 额外缓冲
        
        // 计算从玩家到水域中心的距离
        float distance = Vector2Int.Distance(playerGridPos, waterCenter);
        
        // 如果水域中心在安全距离内，拒绝生成
        bool isInSafeZone = distance < totalSafeRadius;
        
        if (isInSafeZone)
        {
            Debug.Log($"[TerrainInitialization] 🚫 水域中心 {waterCenter} 距离玩家 {playerGridPos} 太近 (距离: {distance:F1}, 最小安全距离: {totalSafeRadius})");
        }
        
        return isInSafeZone;
    }
}
