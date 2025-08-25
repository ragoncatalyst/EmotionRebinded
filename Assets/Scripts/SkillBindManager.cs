using UnityEngine;

// 挂载在GameManager空物体上
public class SkillBindManager : MonoBehaviour
{
    public static SkillBindManager Instance { get; private set; }

    [Header("战斗用按键技能绑定配置")]
    public string defaultSkillId = "00";        // 默认技能编号（00=未绑定技能）
    public float defaultCooldown = 1.0f;        // 默认技能cd时长（秒）

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void OnSkillTriggered(string skillId)
    {
        // Debug.Log($"[SkillBindManager] 执行技能: {skillId}");
    }
}