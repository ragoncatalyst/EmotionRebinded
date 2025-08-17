using UnityEngine;
using UnityEngine.UI;
using MyGame.UI;

public class SlotOccupancyIndicator : MonoBehaviour
{
    [Header("状态显示配置")]
    public Image statusImage;                           // 状态显示Image（原来的cooldownFill）
    public Color availableColor = Color.green;          // 可用状态颜色（绿色 - 未占用）
    public Color occupiedColor = Color.red;             // 已占用状态颜色（红色 - 已占用）
    
    [Header("绑定配置")]
    public NineSelectionButtons selectionButton;        // 关联的选择按钮组件
    
    [Header("调试信息")]
    [SerializeField] private string currentStatus = ""; // 当前状态（仅用于调试显示）

    private void Start()
    {
        // 如果没有手动分配选择按钮组件，尝试从同一GameObject获取
        if (selectionButton == null)
        {
            selectionButton = GetComponent<NineSelectionButtons>();
        }
        
        UpdateStatusDisplay();
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

        // 通过选择按钮组件获取对应的战斗按钮
        MyGame.UI.NineButtons correspondingBattleButton = null;
        if (selectionButton != null)
        {
            correspondingBattleButton = selectionButton.GetCorrespondingBattleButton();
        }

        if (correspondingBattleButton != null)
        {
            string buttonPosition = selectionButton.GetDisplayPosition();
            string skillId = correspondingBattleButton.skillId;
            
            // 如果绑定的是00技能（未绑定），显示绿色（可用）
            if (skillId == "00")
            {
                statusImage.color = availableColor;
                statusImage.fillAmount = 1f; // 满条显示
                currentStatus = $"{buttonPosition}: 可用 (技能{skillId})";
                Debug.Log($"[SlotOccupancyIndicator] {buttonPosition} 状态: 可用（绿色）");
            }
            else
            {
                // 如果已绑定其他技能，显示红色（已占用）
                statusImage.color = occupiedColor;
                statusImage.fillAmount = 1f; // 满条显示
                currentStatus = $"{buttonPosition}: 已占用 (技能{skillId})";
                Debug.Log($"[SlotOccupancyIndicator] {buttonPosition} 状态: 已占用（红色）- 技能{skillId}");
            }
        }
        else
        {
            // 如果没有对应的战斗按键，默认显示可用状态
            statusImage.color = availableColor;
            statusImage.fillAmount = 1f;
            currentStatus = "未关联选择按钮或战斗按键: 默认可用";
            Debug.LogWarning($"[SlotOccupancyIndicator] {gameObject.name} 没有关联的选择按钮或战斗按键，显示默认可用状态");
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
}