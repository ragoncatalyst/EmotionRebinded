using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    public SkillMananer sm;
    private void Start()
    {
        SkillMananer.AddMethod(nameof(LogHelloWorld),LogHelloWorld);//要么使用函数
        SkillMananer.AddMethod("LogNiHao", () =>//要么使用lambda表达式
        {
            Debug.Log("你好");
        });

        SkillData sd1 = new()//创建两个技能
        {
            name = "Test1",
            methodsName = new()
            {
                "LogNiHao",
            },
            collingTime = 2f
        };
        SkillData sd2 = new()
        {
            name = "Test2",
            methodsName = new()
            {
                nameof(LogHelloWorld),
            },
            collingTime = 3f
        };

        sm.RegistSkill(0, sd1);//注册0技能
        sm.RegistSkill(1, sd2);//注册1技能
    }

    public static void LogHelloWorld()
    {
        Debug.Log("Hallo World!");
    }
}
