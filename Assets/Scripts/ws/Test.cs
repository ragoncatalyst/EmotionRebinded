using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    public SkillMananer sm;
    private void Start()
    {
        //SkillMananer.AddMethod(nameof(LogHelloWorld),LogHelloWorld);//要么使用函数
        //SkillMananer.AddMethod("LogNiHao", () =>//要么使用lambda表达式
        //{
        //    Debug.Log("你好");
        //});

        //SkillData sd1 = new()//创建两个技能
        //{
        //    name = "Test1",
        //    methodsName = new()
        //    {
        //        "LogNiHao",
        //    },
        //    collingTime = 2f
        //};
        //SkillData sd2 = new()
        //{
        //    name = "Test2",
        //    methodsName = new()
        //    {
        //        nameof(LogHelloWorld),
        //    },
        //    collingTime = 3f
        //};

        //sm.RegistSkill(0, sd1);//注册0技能
        //sm.RegistSkill(1, sd2);//注册1技能


        Missile.methods.Add("destroyEnemy", (collider, missile) =>
        {
            if(collider.CompareTag("Enemy"))
            {
                collider.SetActive(false);
                Destroy(collider);
            }
        });
        Missile.methods.Add("sliptIn3", (collider, missile) =>
        {
            if (collider.CompareTag("Enemy"))
            {
                float a = missile.Angle;
                Missile.Load("normal", missile.transform.position, SMath.GetVector(SMath.AngleStandardization(a + 30)));
                Missile.Load("normal", missile.transform.position, SMath.GetVector(SMath.AngleStandardization(a - 30)));
                Missile.Load("normal", missile.transform.position, SMath.GetVector(a));
            }
        }); 
        Missile.methods.Add("destroySelf", (collider, missile) =>
        {
            if (collider.CompareTag("Enemy"))
            {
                missile.gameObject.SetActive(false);
                Destroy(missile.gameObject);
            }
        });

        MissileData md1 = new()
        {
            name = "normal",
            speed = 30,
            objectPath = "ws/normal",
            collisionMethods = new()
            {
                "destroyEnemy",
                "destroySelf"
            }
        };
        MissileData md2 = new()
        {
            name = "split",
            speed = 30,
            objectPath = "ws/split",
            collisionMethods = new()
            {
                "destroyEnemy",
                "sliptIn3",
                "destroySelf"
            }
        };
        Missile.datas.Add(md1.name, md1);
        Missile.datas.Add(md2.name, md2);
    }

    public static void LogHelloWorld()
    {
        Debug.Log("Hallo World!");
    }
    public void Shoot()
    {
        Missile.Load("split", new(-5,0), Vector2.right);
    }
}
