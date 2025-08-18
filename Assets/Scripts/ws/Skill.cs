using System.Collections.Generic;
using System.Data;
using UnityEngine;

public class SkillMananer : MonoBehaviour
{
    public static Dictionary<string, SkillMethod> methods = new();
    /// <summary>
    /// 载入的技能
    /// </summary>
    public List<Skill> skills = new();
    public int maxBindNumb = 0;

    /// <summary>
    /// 绑定技能到skills
    /// </summary>
    public void RegistSkill(int index, SkillData data)
    {
        Skill skill = new(data);
        RegistSkill(index, skill);

    }
    public void RegistSkill(int index, Skill skill)
    {
        if(index <= maxBindNumb)
        {
            Debug.Log("索引溢出！");
            return;
        }
        skills[index] = skill;
    }
    /// <summary>
    /// 释放技能
    /// </summary>
    /// <param name="index"></param>
    public void Release(int index)
    {
        var skill = skills[index];
        if(skill.IsCooling)
        {
            Debug.Log("方法冷却中");
        }

        foreach(var m in skill.data.methodsName)
        {
            if(!methods.ContainsKey(m))//
            {
                Debug.Log($"不存在该方法: {m}");
                continue;
            }
            methods[m].Invoke();
        }
        skill.cooling = Cor(skill.Cooling());
    }
    public SkillMananer(int bindingNumber)
    {
        maxBindNumb = bindingNumber;
        skills.Capacity = bindingNumber;

        while (bindingNumber-- > 0)
        {
            skills.Add(null);
        }
    }
    
    /// <summary>
    /// 加入methods
    /// </summary>
    /// <param name="methName"></param>
    /// <param name="method"></param>
    public static void AddMethod(string methName, SkillMethod method)
    {
        methods.Add(methName, method);
    }

    public Coroutine Cor(System.Collections.IEnumerator enumerator)
    {
        return StartCoroutine(enumerator);
    }
    public void aCor(Coroutine coroutine)
    {
        if (coroutine != null)
        StopCoroutine(coroutine);
    }
}
public class Skill
{
    public SkillData data;
    /// <summary>
    /// 技能是否在冷却中
    /// </summary>
    public bool IsCooling => cooling == null;

    /// <summary>
    /// 冷却
    /// </summary>
    public System.Collections.IEnumerator Cooling()
    {
        yield return new WaitForSeconds(data.collingTime);
    }
    public Coroutine cooling;

    public Skill(SkillData data)
        { this.data = data; }
}
/// <summary>
/// 技能信息
/// </summary>
public class SkillData//须在其内添加技能必要的信息
{
    public string name;
    public int damage;

    /// <summary>
    /// 冷却时间
    /// </summary>
    public float collingTime;
    /// <summary>
    /// 所有技能会调用的方法名称
    /// </summary>
    public List<string> methodsName;

}

public delegate void SkillMethod();