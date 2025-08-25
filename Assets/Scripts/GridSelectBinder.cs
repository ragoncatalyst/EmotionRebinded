using UnityEngine;
using MyGame.UI;

public class GridSelectBinder : MonoBehaviour
{
    [Header("抉择用按键绑定器")]
    public NineButtons correspondingBattleButton;    // 与此抉择用按键一对一绑定的战斗中按键
    
    [Header("绑定完成后行为")]
    public bool autoClosePanel = true;               // 绑定完成后自动关闭面板
    public bool updateSlotIndicator = true;          // 更新槽位占用指示器
    
    private static string pendingSkillId;            // 待绑定的技能ID（静态，所有实例共享）
    private SlotOccupancyIndicator slotIndicator;    // 槽位占用指示器

    private void Start()
    {
        // 获取槽位占用指示器组件
        slotIndicator = GetComponent<SlotOccupancyIndicator>();
        
        // 如果有指示器，更新显示
        if (slotIndicator != null && updateSlotIndicator)
        {
            slotIndicator.UpdateStatusDisplay();
        }
    }

    public void BeginSelection(string skillId)
    {
        pendingSkillId = skillId;
        // Debug.Log($"[GridSelectBinder] 开始绑定技能: {skillId}");
        
        // 更新所有槽位指示器显示
        if (updateSlotIndicator)
        {
            // Debug.Log("[GridSelectBinder] 更新所有槽位指示器状态");
            SlotOccupancyIndicator.UpdateAllSlotIndicators();
        }
    }

    public void OnButtonClicked()
    {
        if (correspondingBattleButton == null)
        {
            // Debug.LogWarning("[GridSelectBinder] 对应的战斗按键未设置");
            return;
        }
        if (string.IsNullOrEmpty(pendingSkillId))
        {
            // Debug.LogWarning("[GridSelectBinder] 没有待绑定的技能 ID");
            return;
        }

        // 将技能绑定到对应的战斗中按键
        correspondingBattleButton.SetSkill(pendingSkillId, correspondingBattleButton.keyBind);
        // Debug.Log($"[GridSelectBinder] 技能 {pendingSkillId} 已绑定到战斗按键 {correspondingBattleButton.row}{(int)correspondingBattleButton.column}");
        
        // 更新槽位指示器
        if (updateSlotIndicator)
        {
            // Debug.Log("[GridSelectBinder] 技能绑定完成，更新所有槽位指示器");
            SlotOccupancyIndicator.UpdateAllSlotIndicators();
        }
        
        // 自动关闭升级面板
        if (autoClosePanel)
        {
            UpgradePanelController upgradePanel = FindObjectOfType<UpgradePanelController>();
            if (upgradePanel != null)
            {
                upgradePanel.ClosePanel();
            }
        }
        
        pendingSkillId = null;
    }

    /// <summary>
    /// 获取当前待绑定的技能ID
    /// </summary>
    /// <returns>待绑定的技能ID，如果没有则返回null</returns>
    public static string GetPendingSkillId()
    {
        return pendingSkillId;
    }

    /// <summary>
    /// 清除待绑定的技能ID
    /// </summary>
    public static void ClearPendingSkillId()
    {
        pendingSkillId = null;
        // Debug.Log("[GridSelectBinder] 已清除待绑定的技能ID");
    }

    // 兼容旧版本的方法
    public void BindToSlot(NineButtons slot)
    {
        OnButtonClicked();
    }
}