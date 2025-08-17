using UnityEngine;
using MyGame.UI;

public class GameInitializer : MonoBehaviour
{
    [Header("游戏初始化配置")]
    public NineButtons[] slots;                 // 所有战斗用按键

    void Start()
    {
        // 如果slots数组为空，自动查找所有NineButtons组件
        if (slots == null || slots.Length == 0)
        {
            slots = FindObjectsOfType<NineButtons>();
            Debug.Log($"[GameInitializer] 自动找到 {slots.Length} 个NineButtons组件");
        }

        Debug.Log($"[GameInitializer] 开始初始化，共有 {slots?.Length ?? 0} 个按钮");

        // 初始化：清空所有技能槽，绑定为00（未绑定技能）
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.skillId = "00";
                // 强制更新按钮状态
                if (slot.skillId == "00") slot.GetComponent<NineButtons>()?.ApplyUnboundState();
            }
        }

        // 将C2按钮（下方中央）设为向下移动（03）
        bool foundC2 = false;
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                Debug.Log($"[GameInitializer] 检查按钮: {slot.name} - Row={slot.row}, Column={slot.column}");
                if (slot.row == NineButtons.Row.C && slot.column == NineButtons.Column.Two)
                {
                    slot.skillId = "03"; // 向下移动
                    // 更新技能信息和冷却时间
                    slot.UpdateSkillInfo();
                    // 强制更新按钮状态
                    slot.ApplyBoundState();
                    Debug.Log($"[GameInitializer] ✅ C2按钮已设置为向下移动（03）: {slot.name}, CD: {slot.cooldownSeconds}秒");
                    foundC2 = true;
                    break;
                }
            }
        }

        if (!foundC2)
        {
            Debug.LogWarning("[GameInitializer] ❌ 未找到C2按钮！请检查按钮的Row和Column设置。");
        }
    }
}