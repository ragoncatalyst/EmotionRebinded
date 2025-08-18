using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyGame.UI;

public class God : MonoBehaviour
{
    [Header("测试配置")]
    [SerializeField] private bool enableTestButton = true;  // 是否启用测试按钮
    
    [Header("技能绑定测试")]
    [SerializeField] private string skill01 = "01";  // 技能01编号
    [SerializeField] private string skill02 = "02";  // 技能02编号  
    [SerializeField] private string skill03 = "03";  // 技能03编号
    [SerializeField] private string skill04 = "04";  // 技能04编号
    
    [Header("Monster控制")]
    [SerializeField] private bool disableMonsters = true;  // 是否禁用Monster物体
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    /// <summary>
    /// 测试按钮：绑定技能到指定位置并关闭monster
    /// </summary>
    [ContextMenu("执行技能绑定测试")]
    public void ExecuteSkillBindingTest()
    {
        if (!enableTestButton)
        {
            Debug.Log("[God] 测试按钮已禁用");
            return;
        }
        
        Debug.Log("[God] === 开始执行技能绑定测试 ===");
        
        // 绑定技能到指定位置
        BindSkillToPosition(skill01, NineButtons.Row.B, NineButtons.Column.Two);   // 01 -> B2
        BindSkillToPosition(skill02, NineButtons.Row.C, NineButtons.Column.One);   // 02 -> C1
        BindSkillToPosition(skill03, NineButtons.Row.C, NineButtons.Column.Two);   // 03 -> C2
        BindSkillToPosition(skill04, NineButtons.Row.C, NineButtons.Column.Three); // 04 -> C3
        
        // 关闭Monster物体
        if (disableMonsters)
        {
            DisableAllMonsters();
        }
        
        Debug.Log("[God] === 技能绑定测试完成 ===");
    }
    
    /// <summary>
    /// 将技能绑定到指定的行列位置
    /// </summary>
    private void BindSkillToPosition(string skillId, NineButtons.Row targetRow, NineButtons.Column targetColumn)
    {
        // 查找所有NineButtons组件
        NineButtons[] allButtons = FindObjectsOfType<NineButtons>();
        
        foreach (var button in allButtons)
        {
            if (button.row == targetRow && button.column == targetColumn)
            {
                // 绑定技能到这个位置
                button.SetSkill(skillId, button.keyBind);
                Debug.Log($"[God] 技能 {skillId} 已绑定到位置 {targetRow}{(int)targetColumn} ({button.gameObject.name})");
                return;
            }
        }
        
        Debug.LogWarning($"[God] 未找到位置 {targetRow}{(int)targetColumn} 的按钮");
    }
    
    /// <summary>
    /// 禁用所有Monster/Enemy物体
    /// </summary>
    private void DisableAllMonsters()
    {
        // 查找所有Enemy组件
        Enemy[] allEnemies = FindObjectsOfType<Enemy>();
        int disabledCount = 0;
        
        foreach (var enemy in allEnemies)
        {
            if (enemy.gameObject.activeSelf)
            {
                enemy.gameObject.SetActive(false);
                disabledCount++;
                Debug.Log($"[God] 已禁用Monster: {enemy.gameObject.name}");
            }
        }
        
        // 也可以通过Tag查找
        GameObject[] monstersByTag = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var monster in monstersByTag)
        {
            if (monster.activeSelf)
            {
                monster.SetActive(false);
                disabledCount++;
                Debug.Log($"[God] 已禁用Monster (通过Tag): {monster.name}");
            }
        }
        
        Debug.Log($"[God] 总共禁用了 {disabledCount} 个Monster物体");
    }
    
    /// <summary>
    /// 重新启用所有Monster物体（用于测试）
    /// </summary>
    [ContextMenu("重新启用所有Monster")]
    public void EnableAllMonsters()
    {
        // 查找所有Enemy组件（包括未激活的）
        Enemy[] allEnemies = FindObjectsOfType<Enemy>(true);
        int enabledCount = 0;
        
        foreach (var enemy in allEnemies)
        {
            if (!enemy.gameObject.activeSelf)
            {
                enemy.gameObject.SetActive(true);
                enabledCount++;
                Debug.Log($"[God] 已启用Monster: {enemy.gameObject.name}");
            }
        }
        
        Debug.Log($"[God] 总共启用了 {enabledCount} 个Monster物体");
    }
    
    /// <summary>
    /// 显示当前技能绑定状态
    /// </summary>
    [ContextMenu("显示技能绑定状态")]
    public void ShowSkillBindingStatus()
    {
        Debug.Log("[God] === 当前技能绑定状态 ===");
        
        NineButtons[] allButtons = FindObjectsOfType<NineButtons>();
        foreach (var button in allButtons)
        {
            string position = $"{button.row}{(int)button.column}";
            string keyBind = button.keyBind.ToString();
            string skillId = button.skillId;
            
            Debug.Log($"[God] 位置 {position} (键盘: {keyBind}): 技能 {skillId}");
        }
        
        Debug.Log("[God] ========================");
    }
}
