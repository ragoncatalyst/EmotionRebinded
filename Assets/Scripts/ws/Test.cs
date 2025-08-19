using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    public SkillMananer sm;
    private void Start()
    {
        SkillMananer.AddMethod(nameof(LogHelloWorld),LogHelloWorld);//Ҫôʹ�ú���
        SkillMananer.AddMethod("LogNiHao", () =>//Ҫôʹ��lambda���ʽ
        {
            Debug.Log("���");
        });

        SkillData sd1 = new()//������������
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

        sm.RegistSkill(0, sd1);//ע��0����
        sm.RegistSkill(1, sd2);//ע��1����
    }

    public static void LogHelloWorld()
    {
        Debug.Log("Hallo World!");
    }
}
