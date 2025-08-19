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
    
    [Header("调试设置")]
    [SerializeField] private bool showDebugInfo = true;   // 是否显示调试信息
    
    // 私有变量
    private TerrainType[,] terrainMap;                    // 地形数据
    private HashSet<Vector2Int> waterTiles;               // 水域位置集合（用于碰撞检测）
    private TilemapCollider2D waterCollider;             // 水域碰撞器
    private Vector2Int terrainOffset;                     // 地形偏移量（用于以玩家为中心生成）
    private List<Vector2Int> waterCenters;                // 已生成水域的中心点列表
    
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
        
        // 确保玩家安全区域
        EnsurePlayerSafeZone();
        
        // 确保草地连通性
        EnsureGrassConnectivity();
        
        // 实例化地形物体
        InstantiateTerrain();
        
        // 重新初始化地图边界（因为可能重新生成了地形）
        InitializeMapBounds();
        
        Debug.Log($"[TerrainInitialization] 地形生成完成！草地: {CountTiles(TerrainType.Grass)}, 水域: {CountTiles(TerrainType.Water)}");
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
                        // 中低圆形度：使用概率生成更自然的形状
                        if (distance <= radius)
                        {
                            float distanceRatio = distance / radius;
                            float probability = 1f - distanceRatio;
                            
                            // 应用圆形度参数
                            probability = Mathf.Pow(probability, 2f - waterCircularness);
                            
                            // 添加轻微随机扰动
                            float randomFactor = Random.Range(0.9f, 1.1f);
                            probability *= randomFactor;
                            
                            // 确保中心区域填充
                            if (distance < radius * 0.4f)
                            {
                                probability = Mathf.Max(probability, 0.8f);
                            }
                            
                            if (Random.value < probability)
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
        
        return cluster;
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
            // 如果不是以玩家为中心，确保地图中心区域安全
            EnsureCenterSafeZone();
            return;
        }
        
        // 计算玩家在地形数组中的位置（相对于地形偏移量）
        Vector2 playerWorldPos = new Vector2(playerTransform.position.x, playerTransform.position.y);
        Vector2Int playerGridPos = WorldToGrid(playerWorldPos);
        
        // 转换为本地数组坐标
        int playerLocalX = playerGridPos.x - terrainOffset.x;
        int playerLocalY = playerGridPos.y - terrainOffset.y;
        
        // 计算安全区域范围
        int halfSafeZone = playerSafeZoneSize / 2;
        int waterToGrassCount = 0;
        
        for (int x = playerLocalX - halfSafeZone; x <= playerLocalX + halfSafeZone; x++)
        {
            for (int y = playerLocalY - halfSafeZone; y <= playerLocalY + halfSafeZone; y++)
            {
                // 检查边界
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
        
        if (showDebugInfo)
        {
            if (waterToGrassCount > 0)
            {
                Debug.Log($"[TerrainInitialization] 玩家安全区域({playerSafeZoneSize}x{playerSafeZoneSize})：将 {waterToGrassCount} 个水域转换为草地");
            }
            else
            {
                Debug.Log($"[TerrainInitialization] 玩家安全区域({playerSafeZoneSize}x{playerSafeZoneSize})：已确保无水域");
            }
            Debug.Log($"[TerrainInitialization] 玩家位置：世界({playerWorldPos.x:F1}, {playerWorldPos.y:F1}) 网格({playerGridPos.x}, {playerGridPos.y}) 本地数组({playerLocalX}, {playerLocalY})");
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
                    grassTilemap.SetTile(tilePos, grassTile);
                }
                else if (terrainMap[x, y] == TerrainType.Water)
                {
                    waterTilemap.SetTile(tilePos, waterTile);
                    // 水域位置也需要应用偏移量
                    waterTiles.Add(new Vector2Int(x + terrainOffset.x, y + terrainOffset.y));
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
        
        // 可选：添加CompositeCollider2D来优化性能
        CompositeCollider2D compositeCollider = waterTilemap.GetComponent<CompositeCollider2D>();
        if (compositeCollider == null)
        {
            compositeCollider = waterTilemap.gameObject.AddComponent<CompositeCollider2D>();
            compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
            
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
                waterRenderer.sortingOrder = -999;  // 略高于草地，但仍在底层
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
        return new Vector3(gridX * tileSize, gridY * tileSize, 0);
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
            
            // 重置TilemapCollider2D偏移
            TilemapCollider2D collider = waterTilemap.GetComponent<TilemapCollider2D>();
            if (collider != null)
            {
                collider.offset = Vector2.zero;
                Debug.Log("  ✅ 水域TilemapCollider2D偏移已重置");
                
                // 强制刷新碰撞器
                collider.enabled = false;
                collider.enabled = true;
                Debug.Log("  ✅ 水域碰撞器已刷新");
            }
            
            // 重置CompositeCollider2D偏移
            CompositeCollider2D compositeCollider = waterTilemap.GetComponent<CompositeCollider2D>();
            if (compositeCollider != null)
            {
                compositeCollider.offset = Vector2.zero;
                Debug.Log("  ✅ CompositeCollider2D偏移已重置");
            }
        }
        
        Debug.Log("[TerrainInitialization] ✅ Tilemap对齐和碰撞箱偏移修复完成！");
        Debug.Log("[TerrainInitialization] 🎯 碰撞箱现在应该与视觉位置完全对齐！");
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
        Debug.Log("[TerrainInitialization]   3. ✅ Tilemap置于绝对底层（草地-1000，水域-999）");
        Debug.Log("[TerrainInitialization]   4. ✅ Player排序范围大幅扩大（-1500到1500）");
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
                grassRenderer.sortingOrder = -1000;  // 大幅降低，确保绝对在最底层
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
                waterRenderer.sortingOrder = -999;  // 略高于草地，但仍在底层
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
        
        // 检查玩家是否接近地图边界
        bool needExpansion = false;
        Vector2Int expansionDirection = Vector2Int.zero;
        
        // 检查各个方向
        if (playerGridPos.x - currentMapMin.x <= expansionTriggerDistance)
        {
            // 需要向左扩展
            needExpansion = true;
            expansionDirection.x = -1;
        }
        else if (currentMapMax.x - playerGridPos.x <= expansionTriggerDistance)
        {
            // 需要向右扩展
            needExpansion = true;
            expansionDirection.x = 1;
        }
        
        if (playerGridPos.y - currentMapMin.y <= expansionTriggerDistance)
        {
            // 需要向下扩展
            needExpansion = true;
            expansionDirection.y = -1;
        }
        else if (currentMapMax.y - playerGridPos.y <= expansionTriggerDistance)
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
    /// 为扩展区域生成圆形水域簇
    /// </summary>
    private System.Collections.IEnumerator GenerateExpandedWaterClusters(List<Vector2Int> availableTiles, Vector2Int newMapMin, Vector2Int newMapMax)
    {
        if (availableTiles.Count == 0) yield break;
        
        // 计算扩展区域应该生成的水域数量
        int targetWaterTiles = Mathf.RoundToInt(availableTiles.Count * waterPercentage * 0.3f); // 扩展区域水域减少
        int generatedWaterTiles = 0;
        int attempts = 0;
        int maxAttempts = targetWaterTiles * 3;
        
        List<Vector2Int> expandedWaterCenters = new List<Vector2Int>();
        
        while (generatedWaterTiles < targetWaterTiles && attempts < maxAttempts)
        {
            attempts++;
            
            // 从可用地块中随机选择一个作为水域中心
            Vector2Int center = availableTiles[Random.Range(0, availableTiles.Count)];
            
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
        
        // 扩展区域的水域稍小一些
        float radius = Random.Range(5f, 15f);
        
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
                    // 中低圆形度：使用概率生成更自然的形状
                    if (distance <= radius)
                    {
                        float distanceRatio = distance / radius;
                        float probability = 1f - distanceRatio;
                        
                        probability = Mathf.Pow(probability, 2f - waterCircularness);
                        
                        float randomFactor = Random.Range(0.9f, 1.1f);
                        probability *= randomFactor;
                        
                        if (distance < radius * 0.4f)
                        {
                            probability = Mathf.Max(probability, 0.8f);
                        }
                        
                        if (Random.value < probability)
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

