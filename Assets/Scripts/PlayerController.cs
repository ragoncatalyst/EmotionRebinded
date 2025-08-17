using UnityEngine;
using MyGame.UI;

public class PlayerController : MonoBehaviour
{
    public NineButtons[] battleButtons;  // 改成一维数组

    void Update()
    {
        if (battleButtons == null) return;

        foreach (var btn in battleButtons)
        {
            if (btn != null && Input.GetKeyDown(btn.boundKey))
            {
                Debug.Log($"[PlayerController] 按下 {btn.boundKey}，技能 = {btn.boundSkillId}");

                switch (btn.boundSkillId)
                {
                    case "01":
                        Debug.Log("[技能01] 向上移动");
                        transform.Translate(Vector3.up * 0.2f);
                        break;
                    case "02":
                        Debug.Log("[技能02] 向左移动");
                        transform.Translate(Vector3.left * 0.2f);
                        break;
                    case "03":
                        Debug.Log("[技能03] 向下移动");
                        transform.Translate(Vector3.down * 0.2f);
                        break;
                    case "04":
                        Debug.Log("[技能04] 向右移动");
                        transform.Translate(Vector3.right * 0.2f);
                        break;
                    default:
                        Debug.Log("[技能00] 未绑定技能");
                        break;
                }
            }
        }
    }
}