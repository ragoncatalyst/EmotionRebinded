using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MyGame.UI;

/// <summary>
/// 升级面板中的九宫格选择按钮组件
/// 负责显示按钮编号、键盘按键，以及与对应战斗按钮的关联
/// </summary>
public class NineSelectionButtons : MonoBehaviour
{
    [Header("UI组件引用")]
    public TextMeshProUGUI positionLabel;    // 显示位置编号（如A1, A2）
    public TextMeshProUGUI keyBindLabel;     // 显示键盘按键（如Q, W, E）
    public TextMeshProUGUI skillIdLabel;     // 显示当前绑定的技能编号
    public Button selectionButton;           // 选择按钮组件
    public TextMeshProUGUI confirmLabel;     // 第一次点击后显示的确认提示（初始隐藏）

    [Header("自动匹配配置")]
    [SerializeField] private bool autoMatchByPosition = true;  // 是否通过位置自动匹配
    [SerializeField] private bool autoMatchByName = true;      // 是否通过名称自动匹配

    [Header("匹配信息（只读）")]
    [SerializeField] private NineButtons correspondingBattleButton; // 对应的战斗按钮
    [SerializeField] private string extractedPosition = "";         // 提取的位置信息
    [SerializeField] private string displayPosition = "";          // 显示的位置（如A1）
    [SerializeField] private string displayKeyBind = "";           // 显示的按键（如Q）
    [SerializeField] private string displaySkillId = "";           // 显示的技能编号（如03）

    [Header("键盘输入配置")]
    [SerializeField] private bool enableKeyboardInput = true;     // 是否启用键盘输入
    
    [Header("调试信息")]
    [SerializeField] private bool isMatched = false;               // 是否已匹配成功

    [Header("确认交互")]
    [SerializeField] private bool awaitingConfirm = false;         // 是否处于确认状态
    [SerializeField] private float scaleUpFactor = 1.05f;          // 放大比例
    [SerializeField] private float scaleAnimTime = 0.08f;          // 放大动画时长

    private void Start()
    {
        // 自动获取组件引用
        if (selectionButton == null)
            selectionButton = GetComponent<Button>();

        if (confirmLabel != null)
            confirmLabel.gameObject.SetActive(false);

        // 尝试自动匹配对应的战斗按钮
        if (correspondingBattleButton == null)
        {
            // 优先尝试位置匹配
            if (autoMatchByPosition)
            {
                AutoMatchByPosition();
            }

            // 如果位置匹配失败，尝试名称匹配
            if (correspondingBattleButton == null && autoMatchByName)
            {
                AutoMatchByName();
            }
        }

        // 绑定按钮点击事件
        if (selectionButton != null)
        {
            selectionButton.onClick.AddListener(OnSelectionButtonClicked);
        }

        // 更新显示
        UpdateDisplay();
    }

    /// <summary>
    /// 检测键盘输入
    /// </summary>
    private void Update()
    {
        // 只有在启用键盘输入且升级面板打开时才检测键盘
        if (!enableKeyboardInput || correspondingBattleButton == null)
            return;

        // 检查升级面板是否打开
        UpgradePanelController upgradePanel = FindObjectOfType<UpgradePanelController>();
        if (upgradePanel == null || !IsPanelOpen(upgradePanel))
            return;

        // 仅触发自身绑定的键（避免同一行多个按钮同时响应导致“最右侧覆盖”）
        KeyCode targetKey = correspondingBattleButton.keyBind;
        if (targetKey != KeyCode.None && Input.GetKeyDown(targetKey))
        {
            OnSelectionButtonClicked();
        }
    }

    /// <summary>
    /// 检查升级面板是否打开
    /// </summary>
    private bool IsPanelOpen(UpgradePanelController panel)
    {
        // 通过检查面板的GameObject是否激活来判断
        return panel != null && panel.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// 通过行列位置自动匹配对应的战斗按钮
    /// </summary>
    private void AutoMatchByPosition()
    {
        string currentName = gameObject.name;

        // 从名称提取行列位置（最后两个字符）
        if (currentName.Length >= 2)
        {
            string suffix = currentName.Substring(currentName.Length - 2);

            // 验证格式：第一个字符是A/B/C，第二个字符是1/2/3
            if (suffix.Length == 2 &&
                (suffix[0] == 'A' || suffix[0] == 'B' || suffix[0] == 'C') &&
                (suffix[1] == '1' || suffix[1] == '2' || suffix[1] == '3'))
            {
                extractedPosition = suffix;
                Debug.Log($"[NineSelectionButtons] {currentName} 提取目标位置: '{extractedPosition}'");

                // 转换为枚举值
                NineButtons.Row targetRow = suffix[0] == 'A' ? NineButtons.Row.A :
                                           suffix[0] == 'B' ? NineButtons.Row.B :
                                           NineButtons.Row.C;

                NineButtons.Column targetColumn = suffix[1] == '1' ? NineButtons.Column.One :
                                                 suffix[1] == '2' ? NineButtons.Column.Two :
                                                 NineButtons.Column.Three;

                // 查找所有NineButtons组件
                NineButtons[] allBattleButtons = FindObjectsOfType<NineButtons>();
                Debug.Log($"[NineSelectionButtons] 找到 {allBattleButtons.Length} 个战斗按键，查找位置 {targetRow}{(int)targetColumn}");

                foreach (var battleButton in allBattleButtons)
                {
                    if (battleButton.row == targetRow && battleButton.column == targetColumn)
                    {
                        correspondingBattleButton = battleButton;
                        isMatched = true;
                        Debug.Log($"[NineSelectionButtons] 位置匹配成功: {currentName} -> {battleButton.gameObject.name} (位置: {targetRow}{(int)targetColumn})");
                        return;
                    }
                }

                Debug.LogWarning($"[NineSelectionButtons] 未找到位置为 {targetRow}{(int)targetColumn} 的战斗按键");
            }
            else
            {
                Debug.LogWarning($"[NineSelectionButtons] {currentName} 的后缀 '{suffix}' 格式不正确，应为 A1-C3");
            }
        }
        else
        {
            Debug.LogError($"[NineSelectionButtons] {currentName} 名称长度不足，无法提取位置信息");
        }
    }

    /// <summary>
    /// 通过名称后缀自动匹配对应的战斗按钮
    /// </summary>
    private void AutoMatchByName()
    {
        string currentName = gameObject.name;

        if (currentName.Length >= 2)
        {
            string suffix = currentName.Substring(currentName.Length - 2);
            Debug.Log($"[NineSelectionButtons] {currentName} 尝试名称匹配，后缀: '{suffix}'");

            NineButtons[] allBattleButtons = FindObjectsOfType<NineButtons>();

            foreach (var battleButton in allBattleButtons)
            {
                if (battleButton.gameObject.name.EndsWith(suffix))
                {
                    correspondingBattleButton = battleButton;
                    isMatched = true;
                    Debug.Log($"[NineSelectionButtons] 名称匹配成功: {currentName} -> {battleButton.gameObject.name}");
                    return;
                }
            }

            Debug.LogWarning($"[NineSelectionButtons] 未找到名称匹配的战斗按键（后缀: '{suffix}'）");
        }
    }

    /// <summary>
    /// 更新显示信息
    /// </summary>
    private void UpdateDisplay()
    {
        if (correspondingBattleButton != null)
        {
            // 获取位置信息
            displayPosition = $"{correspondingBattleButton.row}{(int)correspondingBattleButton.column}";
            
            // 获取按键信息
            displayKeyBind = correspondingBattleButton.keyBind.ToString();
            
            // 获取技能编号信息
            displaySkillId = correspondingBattleButton.skillId;

            // 更新UI显示
            if (positionLabel != null)
            {
                positionLabel.text = displayPosition;
                positionLabel.alignment = TMPro.TextAlignmentOptions.Center;
            }

            if (keyBindLabel != null)
            {
                keyBindLabel.text = displayKeyBind;
                keyBindLabel.alignment = TMPro.TextAlignmentOptions.Center;
            }
            
            if (skillIdLabel != null)
            {
                // 如果是00技能（未绑定），显示空格；否则显示技能编号
                skillIdLabel.text = (displaySkillId == "00") ? "  " : displaySkillId;
                skillIdLabel.alignment = TMPro.TextAlignmentOptions.Center;
            }

            Debug.Log($"[NineSelectionButtons] {gameObject.name} 更新显示: 位置={displayPosition}, 按键={displayKeyBind}, 技能={displaySkillId}");
        }
        else
        {
            // 如果没有匹配到战斗按钮，显示默认信息
            displayPosition = "??";
            displayKeyBind = "?";
            displaySkillId = "??";

            if (positionLabel != null)
            {
                positionLabel.text = displayPosition;
                positionLabel.alignment = TMPro.TextAlignmentOptions.Center;
            }

            if (keyBindLabel != null)
            {
                keyBindLabel.text = displayKeyBind;
                keyBindLabel.alignment = TMPro.TextAlignmentOptions.Center;
            }
                
            if (skillIdLabel != null)
            {
                skillIdLabel.text = displaySkillId;
                skillIdLabel.alignment = TMPro.TextAlignmentOptions.Center;
            }

            Debug.LogWarning($"[NineSelectionButtons] {gameObject.name} 未匹配到战斗按钮，显示默认信息");
        }
    }

    /// <summary>
    /// 手动设置对应的战斗按钮
    /// </summary>
    public void SetCorrespondingBattleButton(NineButtons battleButton)
    {
        correspondingBattleButton = battleButton;
        isMatched = (battleButton != null);
        UpdateDisplay();
    }

    /// <summary>
    /// 获取对应的战斗按钮
    /// </summary>
    public NineButtons GetCorrespondingBattleButton()
    {
        return correspondingBattleButton;
    }

    /// <summary>
    /// 获取显示的位置信息
    /// </summary>
    public string GetDisplayPosition()
    {
        return displayPosition;
    }

    /// <summary>
    /// 获取显示的按键信息
    /// </summary>
    public string GetDisplayKeyBind()
    {
        return displayKeyBind;
    }
    
    /// <summary>
    /// 获取显示的技能编号
    /// </summary>
    public string GetDisplaySkillId()
    {
        return displaySkillId;
    }
    
    /// <summary>
    /// 刷新显示信息（当战斗按钮的技能绑定改变时调用）
    /// </summary>
    public void RefreshDisplay()
    {
        UpdateDisplay();
    }

    /// <summary>
    /// 启用或禁用键盘输入
    /// </summary>
    public void SetKeyboardInputEnabled(bool enabled)
    {
        enableKeyboardInput = enabled;
        Debug.Log($"[NineSelectionButtons] {gameObject.name} 键盘输入 {(enabled ? "启用" : "禁用")}");
    }

    /// <summary>
    /// 获取键盘输入状态
    /// </summary>
    public bool IsKeyboardInputEnabled()
    {
        return enableKeyboardInput;
    }

    /// <summary>
    /// 获取对应的按键
    /// </summary>
    public KeyCode GetTargetKey()
    {
        return correspondingBattleButton != null ? correspondingBattleButton.keyBind : KeyCode.None;
    }

    /// <summary>
    /// 启用所有选择按钮的键盘输入
    /// </summary>
    public static void EnableAllKeyboardInput()
    {
        NineSelectionButtons[] allSelectionButtons = FindObjectsOfType<NineSelectionButtons>();
        foreach (var button in allSelectionButtons)
        {
            button.SetKeyboardInputEnabled(true);
        }
        Debug.Log($"[NineSelectionButtons] 已启用所有 {allSelectionButtons.Length} 个选择按钮的键盘输入");
    }

    /// <summary>
    /// 禁用所有选择按钮的键盘输入
    /// </summary>
    public static void DisableAllKeyboardInput()
    {
        NineSelectionButtons[] allSelectionButtons = FindObjectsOfType<NineSelectionButtons>();
        foreach (var button in allSelectionButtons)
        {
            button.SetKeyboardInputEnabled(false);
        }
        Debug.Log($"[NineSelectionButtons] 已禁用所有 {allSelectionButtons.Length} 个选择按钮的键盘输入");
    }

    /// <summary>
    /// 处理选择按钮点击事件
    /// </summary>
    private void OnSelectionButtonClicked()
    {
        Debug.Log($"[NineSelectionButtons] {gameObject.name} 被点击，位置: {displayPosition}");

        if (correspondingBattleButton == null)
        {
            Debug.LogError($"[NineSelectionButtons] {gameObject.name} 没有对应的战斗按钮，无法绑定技能");
            return;
        }

        // 查找升级面板控制器来获取当前选中的技能
        UpgradePanelController upgradePanel = FindObjectOfType<UpgradePanelController>();
        if (upgradePanel == null)
        {
            Debug.LogError("[NineSelectionButtons] 未找到UpgradePanelController，无法获取选中的技能");
            return;
        }

        // 获取当前选中的技能ID
        string selectedSkillId = GetCurrentSelectedSkill();
        if (string.IsNullOrEmpty(selectedSkillId))
        {
            Debug.LogWarning("[NineSelectionButtons] 没有选中的技能，请先在升级面板选择技能");
            return;
        }

        // 双击确认：如果已有技能且未确认，先提示并返回；再次点击才真正绑定
        bool alreadyBound = !string.IsNullOrEmpty(correspondingBattleButton.skillId) && correspondingBattleButton.skillId != "00";
        if (alreadyBound && !awaitingConfirm)
        {
            awaitingConfirm = true;
            ShowConfirmUI(true);
            return;
        }

        // 将技能绑定到对应的战斗按钮
        correspondingBattleButton.SetSkill(selectedSkillId, correspondingBattleButton.keyBind);
        Debug.Log($"[NineSelectionButtons] 技能 {selectedSkillId} 已绑定到位置 {displayPosition}");

        // 清除待绑定的技能ID
        GridSelectBinder.ClearPendingSkillId();

        // 更新显示
        RefreshDisplay();

        // 更新所有槽位指示器
        SlotOccupancyIndicator.UpdateAllSlotIndicators();

        // 关闭升级面板
        // 抑制刚刚用来选择的按键，直到玩家松开该键，避免瞬间触发战斗场景技能
        if (correspondingBattleButton != null)
        {
            NineButtons.SuppressKeyUntilRelease(correspondingBattleButton.keyBind);
        }
        upgradePanel.ClosePanel();
        awaitingConfirm = false;
        ShowConfirmUI(false);
    }

    /// <summary>
    /// 获取当前选中的技能ID（从GridSelectBinder）
    /// </summary>
    private string GetCurrentSelectedSkill()
    {
        string pendingSkillId = GridSelectBinder.GetPendingSkillId();
        
        if (!string.IsNullOrEmpty(pendingSkillId))
        {
            Debug.Log($"[NineSelectionButtons] 获取到待绑定的技能ID: {pendingSkillId}");
            return pendingSkillId;
        }
        else
        {
            Debug.LogWarning("[NineSelectionButtons] 没有待绑定的技能ID，请先在升级面板选择技能");
            return "";
        }
    }

    // ===== 确认提示与缩放动画 =====
    private void ShowConfirmUI(bool show)
    {
        if (confirmLabel != null)
        {
            confirmLabel.gameObject.SetActive(show);
        }
        // 若当前对象处于未激活或组件未启用，直接设置缩放值，避免协程启动报错
        if (!isActiveAndEnabled)
        {
            Transform t0 = selectionButton != null ? selectionButton.transform : transform;
            t0.localScale = Vector3.one * (show ? scaleUpFactor : 1f);
            return;
        }
        StopAllCoroutines();
        StartCoroutine(AnimateScale(show ? scaleUpFactor : 1f));
    }

    private System.Collections.IEnumerator AnimateScale(float target)
    {
        Transform t = selectionButton != null ? selectionButton.transform : transform;
        Vector3 start = t.localScale;
        Vector3 goal = Vector3.one * target;
        float t0 = 0f;
        while (t0 < scaleAnimTime)
        {
            t0 += Time.deltaTime;
            float p = Mathf.Clamp01(t0 / scaleAnimTime);
            t.localScale = Vector3.Lerp(start, goal, p);
            yield return null;
        }
        t.localScale = goal;
    }

    #region Context Menu 调试方法

    /// <summary>
    /// 在Inspector中手动测试位置匹配
    /// </summary>
    [ContextMenu("测试位置匹配")]
    public void TestPositionMatch()
    {
        Debug.Log($"[NineSelectionButtons] 测试位置匹配 {gameObject.name}");
        correspondingBattleButton = null;
        isMatched = false;
        AutoMatchByPosition();
        UpdateDisplay();
    }

    /// <summary>
    /// 在Inspector中手动测试名称匹配
    /// </summary>
    [ContextMenu("测试名称匹配")]
    public void TestNameMatch()
    {
        Debug.Log($"[NineSelectionButtons] 测试名称匹配 {gameObject.name}");
        correspondingBattleButton = null;
        isMatched = false;
        AutoMatchByName();
        UpdateDisplay();
    }

    /// <summary>
    /// 在Inspector中手动更新显示
    /// </summary>
    [ContextMenu("更新显示")]
    public void TestUpdateDisplay()
    {
        Debug.Log($"[NineSelectionButtons] 手动更新显示 {gameObject.name}");
        UpdateDisplay();
    }

    /// <summary>
    /// 在Inspector中手动测试技能绑定
    /// </summary>
    [ContextMenu("测试技能绑定")]
    public void TestSkillBinding()
    {
        Debug.Log($"[NineSelectionButtons] 手动测试技能绑定 {gameObject.name}");
        OnSelectionButtonClicked();
    }

    /// <summary>
    /// 在Inspector中测试键盘输入
    /// </summary>
    [ContextMenu("测试键盘输入")]
    public void TestKeyboardInput()
    {
        KeyCode targetKey = GetTargetKey();
        Debug.Log($"[NineSelectionButtons] {gameObject.name} 键盘输入测试:");
        Debug.Log($"  - 目标按键: {targetKey}");
        Debug.Log($"  - 键盘输入启用: {enableKeyboardInput}");
        Debug.Log($"  - 匹配状态: {isMatched}");
        Debug.Log($"  - 对应战斗按钮: {(correspondingBattleButton != null ? correspondingBattleButton.gameObject.name : "null")}");
        
        // 模拟键盘输入
        if (correspondingBattleButton != null)
        {
            Debug.Log($"[NineSelectionButtons] 模拟键盘输入 {targetKey}");
            OnSelectionButtonClicked();
        }
    }

    /// <summary>
    /// 切换键盘输入启用状态
    /// </summary>
    [ContextMenu("切换键盘输入")]
    public void ToggleKeyboardInput()
    {
        SetKeyboardInputEnabled(!enableKeyboardInput);
    }

    #endregion
}
