using System.Collections.Generic;
using System.Data;
using UnityEngine;

public class SkillMananer : MonoBehaviour
{
    public static Dictionary<string, SkillMethod> methods = new();
    /// <summary>
    /// ����ļ���
    /// </summary>
    public List<Skill> skills = new();
    public int maxBindNumb = 0;

    /// <summary>
    /// �󶨼��ܵ�skills
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
            Debug.Log("���������");
            return;
        }
        skills[index] = skill;
    }
    /// <summary>
    /// �ͷż���
    /// </summary>
    /// <param name="index"></param>
    public void Release(int index)
    {
        var skill = skills[index];
        if(skill.IsCooling)
        {
            Debug.Log("������ȴ��");
        }

        foreach(var m in skill.data.methodsName)
        {
            if(!methods.ContainsKey(m))//
            {
                Debug.Log($"�����ڸ÷���: {m}");
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
    /// ����methods
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
    /// �����Ƿ�����ȴ��
    /// </summary>
    public bool IsCooling => cooling == null;

    /// <summary>
    /// ��ȴ
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
/// ������Ϣ
/// </summary>
public class SkillData//����������Ӽ��ܱ�Ҫ����Ϣ
{
    public string name;
    public int damage;

    /// <summary>
    /// ��ȴʱ��
    /// </summary>
    public float collingTime;
    /// <summary>
    /// ���м��ܻ���õķ�������
    /// </summary>
    public List<string> methodsName;

}

public delegate void SkillMethod();