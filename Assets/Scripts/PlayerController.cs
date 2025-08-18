using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("玩家控制配置")]
    public float moveSpeed = 0.1f;       // 移动速度（单位/秒，可在Inspector中编辑）

    // 移除了重复的键盘输入处理逻辑，现在由NineButtons.cs直接处理
    // 移除了battleButtons数组，避免功能重复
    
    /// <summary>
    /// 判断是否为移动技能
    /// </summary>
    public bool IsMovementSkill(string skillId)
    {
        return skillId == "01" || skillId == "02" || skillId == "03" || skillId == "04";
    }

    /// <summary>
    /// 执行技能（供NineButtons调用）
    /// </summary>
    public void ExecuteSkill(string skillId)
    {
        Debug.Log($"[PlayerController] 执行技能 {skillId}");

        // 统一的移动逻辑，支持持续移动和单次移动
        if (IsMovementSkill(skillId))
        {
            ExecuteMovement(skillId, Time.deltaTime); // 使用deltaTime支持持续移动
        }
        else
        {
            Debug.Log($"[技能{skillId}] 未知技能或未绑定");
        }
    }

    /// <summary>
    /// 执行移动（统一的移动逻辑）
    /// </summary>
    public void ExecuteMovement(string skillId, float deltaTime)
    {
        float moveAmount = moveSpeed * deltaTime;
        
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
}