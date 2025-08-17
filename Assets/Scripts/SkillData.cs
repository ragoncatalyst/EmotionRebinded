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
        
        // 使用硬编码的技能数据（基于Skills.txt内容）
        LoadDefaultSkills();

        isLoaded = true;
        Debug.Log($"[SkillDatabase] 已加载 {skillDict.Count} 个技能");
    }

    /// <summary>
    /// 加载默认技能数据
    /// </summary>
    private static void LoadDefaultSkills()
    {
        skillDict["00"] = new SkillInfo("00", "未绑定", "", 0, "未绑定技能");
        skillDict["01"] = new SkillInfo("01", "移动-上", "xyinput", 0, "向上方移动");
        skillDict["02"] = new SkillInfo("02", "移动-左", "xyinput", 0, "向左侧移动");
        skillDict["03"] = new SkillInfo("03", "移动-下", "xyinput", 0, "向下方移动");
        skillDict["04"] = new SkillInfo("04", "移动-右", "xyinput", 0, "向右侧移动");
    }

    /// <summary>
    /// 获取技能信息
    /// </summary>
    public static SkillInfo GetSkillInfo(string skillId)
    {
        if (!isLoaded) LoadSkillDatabase();
        
        if (skillDict.TryGetValue(skillId, out SkillInfo skill))
            return skill;
        
        // 返回默认未知技能
        return new SkillInfo(skillId, "未知技能", "", 0, $"未知技能ID: {skillId}");
    }

    /// <summary>
    /// 检查技能是否存在
    /// </summary>
    public static bool HasSkill(string skillId)
    {
        if (!isLoaded) LoadSkillDatabase();
        return skillDict.ContainsKey(skillId);
    }
}
