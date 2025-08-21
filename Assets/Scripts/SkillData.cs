using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SkillInfo
{
    public string skillId;          // 技能编号
    public string name;             // 技能名字
    public string functionCode;     // 功能代码
    public float cooldownTime;      // cd时长（秒）
    public string description;      // 功能文字介绍

    public SkillInfo(string id, string skillName, string code, float cd, string desc)
    {
        skillId = id;
        name = skillName;
        functionCode = code;
        cooldownTime = cd;
        description = desc;
    }
}

public static class SkillDatabase
{
    private static Dictionary<string, SkillInfo> skillDict = new Dictionary<string, SkillInfo>();
    private static bool isLoaded = false;

    /// <summary>
    /// 加载技能数据库
    /// </summary>
    public static void LoadSkillDatabase()
    {
        if (isLoaded) return;

        skillDict.Clear();
        
        // 使用硬编码的技能数据（基于 Skills.txt 内容）
        LoadDefaultSkills();

        isLoaded = true;
        Debug.Log($"[SkillDatabase] 已加载 {skillDict.Count} 个技能");
    }

    /// <summary>
    /// 加载默认技能数据
    /// </summary>
    private static void LoadDefaultSkills()
    {
        skillDict["00"] = new SkillInfo("00", "Unbound", "", 0, "No skill bound");
        skillDict["01"] = new SkillInfo("01", "Move Up", "xyinput", 0, "Move upward");
        skillDict["02"] = new SkillInfo("02", "Move Left", "xyinput", 0, "Move left");
        skillDict["03"] = new SkillInfo("03", "Move Down", "xyinput", 0, "Move downward");
        skillDict["04"] = new SkillInfo("04", "Move Right", "xyinput", 0, "Move right");
        // All new attack skills use a 2.0s cooldown (as requested)
        skillDict["05"] = new SkillInfo("05", "Homing Bullet", "skill05_homing", 2.0f, "Fire a homing bullet at the nearest enemy");
        skillDict["06"] = new SkillInfo("06", "Piercing Shot", "skill06_pierce", 2.0f, "Shoot a fast piercing bolt through enemies");
        skillDict["07"] = new SkillInfo("07", "Nova Blast", "skill07_nova", 2.0f, "Emit a short-range radial blast around the player");
    }

    /// <summary>
    /// 获取技能信息
    /// </summary>
    public static SkillInfo GetSkillInfo(string skillId)
    {
        if (!isLoaded) LoadSkillDatabase();
        
        if (skillDict.TryGetValue(skillId, out SkillInfo skill))
            return skill;
        
        // Return default unknown skill (English)
        return new SkillInfo(skillId, "Unknown Skill", "", 0, $"Unknown skill ID: {skillId}");
    }

    /// <summary>
    /// 检查技能是否存在
    /// </summary>
    public static bool HasSkill(string skillId)
    {
        if (!isLoaded) LoadSkillDatabase();
        return skillDict.ContainsKey(skillId);
    }

    /// <summary>
    /// 获取所有技能ID
    /// </summary>
    public static System.Collections.Generic.List<string> GetAllSkillIds(bool excludeUnbound = true)
    {
        if (!isLoaded) LoadSkillDatabase();
        var list = new System.Collections.Generic.List<string>(skillDict.Keys);
        if (excludeUnbound)
        {
            list.Remove("00");
        }
        return list;
    }
}
