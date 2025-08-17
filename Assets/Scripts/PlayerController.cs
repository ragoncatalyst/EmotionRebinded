using UnityEngine;
using MyGame.UI;

public class PlayerController : MonoBehaviour
{
    [Header("玩家控制配置")]
    public NineButtons[] battleButtons;  // 改成一维数组
    public float moveSpeed = 0.1f;       // 移动速度（单位/秒，可在Inspector中编辑）

    void Update()
    {
        if (battleButtons == null) return;

        foreach (var btn in battleButtons)
        {
            if (btn != null)
            {
                // 检查按住状态 - 对移动技能使用持续移动
                if (Input.GetKey(btn.boundKey) && IsMovementSkill(btn.boundSkillId))
                {
                    HandleHoldMovement(btn.boundSkillId);
                }
                // 检查按下状态 - 对非移动技能使用单次触发
                else if (Input.GetKeyDown(btn.boundKey) && !IsMovementSkill(btn.boundSkillId))
                {
                    Debug.Log($"[PlayerController] 按下 {btn.boundKey}，技能 = {btn.boundSkillId}");
                    ExecuteSkill(btn.boundSkillId);
                }
            }
        }
    }
    
    /// <summary>
    /// 判断是否为移动技能
    /// </summary>
    private bool IsMovementSkill(string skillId)
    {
        return skillId == "01" || skillId == "02" || skillId == "03" || skillId == "04";
    }
    
    /// <summary>
    /// 处理按住移动
    /// </summary>
    private void HandleHoldMovement(string skillId)
    {
        float moveAmount = moveSpeed * Time.deltaTime;
        
        switch (skillId)
        {
            case "01":
                transform.Translate(new Vector3(0, moveAmount, 0), Space.World);  // 向上移动（Y轴正方向）
                break;
            case "02":
                transform.Translate(new Vector3(-moveAmount, 0, 0), Space.World); // 向左移动（X轴负方向）
                break;
            case "03":
                transform.Translate(new Vector3(0, -moveAmount, 0), Space.World); // 向下移动（Y轴负方向）
                break;
            case "04":
                transform.Translate(new Vector3(moveAmount, 0, 0), Space.World);  // 向右移动（X轴正方向）
                break;
        }
    }

    /// <summary>
    /// 执行技能（供NineButtons调用）
    /// </summary>
    public void ExecuteSkill(string skillId)
    {
        Debug.Log($"[PlayerController] 执行技能 {skillId}");

        // 如果是移动技能，使用单次移动距离
        if (IsMovementSkill(skillId))
        {
            switch (skillId)
            {
                case "01":
                    Debug.Log("[技能01] 向上移动");
                    transform.Translate(new Vector3(0, moveSpeed * Time.fixedDeltaTime, 0), Space.World);  // 向上移动（Y轴正方向）
                    break;
                case "02":
                    Debug.Log("[技能02] 向左移动");
                    transform.Translate(new Vector3(-moveSpeed * Time.fixedDeltaTime, 0, 0), Space.World); // 向左移动（X轴负方向）
                    break;
                case "03":
                    Debug.Log("[技能03] 向下移动");
                    transform.Translate(new Vector3(0, -moveSpeed * Time.fixedDeltaTime, 0), Space.World); // 向下移动（Y轴负方向）
                    break;
                case "04":
                    Debug.Log("[技能04] 向右移动");
                    transform.Translate(new Vector3(moveSpeed * Time.fixedDeltaTime, 0, 0), Space.World);  // 向右移动（X轴正方向）
                    break;
            }
        }
        else
        {
            Debug.Log($"[技能{skillId}] 未知技能或未绑定");
        }
    }
}