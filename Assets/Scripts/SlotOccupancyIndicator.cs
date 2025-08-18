using UnityEngine;
using UnityEngine.UI;
using MyGame.UI;
using System.Collections;

public class SlotOccupancyIndicator : MonoBehaviour
{
    [Header("状态显示配置")]
    public Image statusImage;                           // 状态显示Image（原来的cooldownFill）
    public Color availableColor = Color.green;          // 可用状态颜色（绿色 - 未占用，技能00）
    public Color occupiedColor = Color.red;             // 已占用状态颜色（红色 - 已占用，非00技能）
    
    [Header("绑定配置")]
    public NineSelectionButtons selectionButton;        // 关联的选择按钮组件
    
    [Header("调试信息")]
    [SerializeField] private string currentStatus = ""; // 当前状态（仅用于调试显示）
    
    [Header("自动更新配置")]
    public bool enablePeriodicUpdate = true;            // 是否启用定期自动更新
    public float updateInterval = 1.0f;                 // 更新间隔（秒）

    private void Start()
    {
        // 如果没有手动分配选择按钮组件，尝试从同一GameObject获取
        if (selectionButton == null)
        {
            selectionButton = GetComponent<NineSelectionButtons>();
        }
        
        // 延迟更新状态，确保所有组件都已初始化完成
        StartCoroutine(DelayedStatusUpdate());
    }
    
    /// <summary>
    /// 延迟状态更新，确保所有组件初始化完成
    /// </summary>
    private IEnumerator DelayedStatusUpdate()
    {
        // 等待1帧，确保所有Start方法都执行完成
        yield return null;
        
        // 再等待0.1秒，确保NineSelectionButtons的自动匹配完成
        yield return new WaitForSeconds(0.1f);
        
        Debug.Log($"[SlotOccupancyIndicator] {gameObject.name} 开始延迟状态更新");
        UpdateStatusDisplay();
        
        // 再等待0.5秒后再次更新，确保万无一失
        yield return new WaitForSeconds(0.5f);
        UpdateStatusDisplay();
        
        // 如果启用定期更新，开始定期更新循环
        if (enablePeriodicUpdate)
        {
            StartCoroutine(PeriodicUpdate());
        }
    }
    
    /// <summary>
    /// 定期更新状态，确保状态始终正确
    /// </summary>
    private IEnumerator PeriodicUpdate()
    {
        while (enablePeriodicUpdate)
        {
            yield return new WaitForSeconds(updateInterval);
            
            // 只在游戏运行时更新（不在暂停时更新）
            if (Time.timeScale > 0)
            {
                UpdateStatusDisplay();
            }
        }
    }

    /// <summary>
    /// 更新状态显示
    /// </summary>
    public void UpdateStatusDisplay()
    {
        if (statusImage == null) 
        {
            Debug.LogWarning($"[SlotOccupancyIndicator] {gameObject.name} 的 statusImage 为空！");
            return;
        }

        // 如果选择按钮组件为空，尝试重新获取
        if (selectionButton == null)
        {
            selectionButton = GetComponent<NineSelectionButtons>();
            if (selectionButton == null)
            {
                Debug.LogWarning($"[SlotOccupancyIndicator] {gameObject.name} 无法找到 NineSelectionButtons 组件！");
                return;
            }
        }

        // 通过选择按钮组件获取对应的战斗按钮
        MyGame.UI.NineButtons correspondingBattleButton = selectionButton.GetCorrespondingBattleButton();
        
        // 如果战斗按钮为空，记录详细信息并使用默认状态
        if (correspondingBattleButton == null)
        {
            Debug.LogWarning($"[SlotOccupancyIndicator] {gameObject.name} 无法获取对应的战斗按钮！");
            Debug.LogWarning($"[SlotOccupancyIndicator] 选择按钮: {(selectionButton != null ? selectionButton.name : "NULL")}");
            
            // 使用默认可用状态
            statusImage.color = availableColor;
            statusImage.fillAmount = 1f;
            currentStatus = "无法获取战斗按钮: 默认可用";
            return;
        }

        // 现在我们确定有有效的战斗按钮，更新状态
        string buttonPosition = selectionButton.GetDisplayPosition();
        string skillId = correspondingBattleButton.skillId;
        
        // 如果绑定的是00技能（未绑定），显示绿色（可用）
        if (skillId == "00")
        {
            statusImage.color = availableColor;  // 绿色 - 可用
            statusImage.fillAmount = 1f; // 满条显示
            currentStatus = $"{buttonPosition}: 可用 (技能{skillId})";
            Debug.Log($"[SlotOccupancyIndicator] {buttonPosition} 状态: 可用（绿色）- 技能{skillId}");
        }
        else
        {
            // 如果已绑定其他技能，显示红色（已占用）
            statusImage.color = occupiedColor;   // 红色 - 已占用
            statusImage.fillAmount = 1f; // 满条显示
            currentStatus = $"{buttonPosition}: 已占用 (技能{skillId})";
            Debug.Log($"[SlotOccupancyIndicator] {buttonPosition} 状态: 已占用（红色）- 技能{skillId}");
        }
        
        // 确保状态Image可见
        statusImage.gameObject.SetActive(true);
        statusImage.enabled = true;
    }

    /// <summary>
    /// 手动设置关联的选择按钮组件
    /// </summary>
    public void SetSelectionButton(NineSelectionButtons button)
    {
        selectionButton = button;
        UpdateStatusDisplay();
    }

    /// <summary>
    /// 更新所有槽位指示器的显示状态
    /// </summary>
    public static void UpdateAllSlotIndicators()
    {
        SlotOccupancyIndicator[] allIndicators = FindObjectsOfType<SlotOccupancyIndicator>();
        Debug.Log($"[SlotOccupancyIndicator] 找到 {allIndicators.Length} 个槽位指示器，开始更新状态");
        
        foreach (var indicator in allIndicators)
        {
            indicator.UpdateStatusDisplay();
        }
        
        // 同时更新所有选择按钮的显示
        NineSelectionButtons[] allSelectionButtons = FindObjectsOfType<NineSelectionButtons>();
        Debug.Log($"[SlotOccupancyIndicator] 找到 {allSelectionButtons.Length} 个选择按钮，开始更新显示");
        
        foreach (var selectionButton in allSelectionButtons)
        {
            selectionButton.RefreshDisplay();
        }
    }

    /// <summary>
    /// 在Inspector中手动重新关联选择按钮组件
    /// </summary>
    [ContextMenu("重新关联选择按钮")]
    public void TestReconnectSelectionButton()
    {
        Debug.Log($"[SlotOccupancyIndicator] 重新关联选择按钮 {gameObject.name}");
        selectionButton = GetComponent<NineSelectionButtons>();
        UpdateStatusDisplay();
    }

    /// <summary>
    /// 在Inspector中手动测试状态更新
    /// </summary>
    [ContextMenu("测试状态更新")]
    public void TestStatusUpdate()
    {
        Debug.Log($"[SlotOccupancyIndicator] 测试更新 {gameObject.name} 的状态");
        UpdateStatusDisplay();
    }

    /// <summary>
    /// 强制刷新所有状态（用于调试首次升级时的显示问题）
    /// </summary>
    [ContextMenu("强制刷新所有状态")]
    public static void ForceRefreshAllStates()
    {
        Debug.Log("[SlotOccupancyIndicator] ===== 强制刷新所有状态 =====");
        
        SlotOccupancyIndicator[] allIndicators = FindObjectsOfType<SlotOccupancyIndicator>();
        Debug.Log($"[SlotOccupancyIndicator] 找到 {allIndicators.Length} 个状态指示器");
        
        foreach (var indicator in allIndicators)
        {
            if (indicator.selectionButton != null)
            {
                var battleButton = indicator.selectionButton.GetCorrespondingBattleButton();
                if (battleButton != null)
                {
                    string pos = indicator.selectionButton.GetDisplayPosition();
                    string skill = battleButton.skillId;
                    Debug.Log($"[SlotOccupancyIndicator] {pos} 按钮当前技能: {skill}");
                }
            }
            indicator.UpdateStatusDisplay();
        }
        
        Debug.Log("[SlotOccupancyIndicator] ===== 状态刷新完成 =====");
    }
}