using UnityEngine;
using MyGame.UI;

public class GridSelectBinder : MonoBehaviour
{
    [Header("抉择用按键绑定器")]
    public NineButtons correspondingBattleButton;    // 与此抉择用按键一对一绑定的战斗中按键
    
    private static string pendingSkillId;            // 待绑定的技能ID（静态，所有实例共享）

    public void BeginSelection(string skillId)
    {
        pendingSkillId = skillId;
        Debug.Log($"[GridSelectBinder] 开始绑定技能: {skillId}");
    }

    public void OnButtonClicked()
    {
        if (correspondingBattleButton == null)
        {
            Debug.LogWarning("[GridSelectBinder] 对应的战斗按键未设置");
            return;
        }
        if (string.IsNullOrEmpty(pendingSkillId))
        {
            Debug.LogWarning("[GridSelectBinder] 没有待绑定的技能 ID");
            return;
        }

        // 将技能绑定到对应的战斗中按键
        correspondingBattleButton.skillId = pendingSkillId;
        Debug.Log($"[GridSelectBinder] 技能 {pendingSkillId} 已绑定到战斗按键 {correspondingBattleButton.row}{(int)correspondingBattleButton.column}");
        
        // 关闭升级面板
        UpgradePanelController upgradePanel = FindObjectOfType<UpgradePanelController>();
        if (upgradePanel != null)
        {
            upgradePanel.ClosePanel();
        }
        
        pendingSkillId = null;
    }

    // 兼容旧版本的方法
    public void BindToSlot(NineButtons slot)
    {
        OnButtonClicked();
    }
}