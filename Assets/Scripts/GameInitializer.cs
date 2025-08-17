using UnityEngine;
using MyGame.UI;

public class GameInitializer : MonoBehaviour
{
    [Header("游戏初始化配置")]
    public NineButtons[] slots;                 // 所有战斗用按键

    void Start()
    {
        // 初始化：清空所有技能槽，绑定为00（未绑定技能）
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.skillId = "00";
            }
        }

        // 将C2按钮（下方中央）设为向下移动（03）
        foreach (var slot in slots)
        {
            if (slot != null && slot.row == NineButtons.Row.C && slot.column == NineButtons.Column.Two)
            {
                slot.skillId = "03"; // 向下移动
                Debug.Log("[GameInitializer] C2按钮已设置为向下移动（03）");
                break;
            }
        }
    }
}