using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MyGame.UI
{
    [DisallowMultipleComponent]
    public class NineButtons : MonoBehaviour
    {
        public enum Row { A, B, C }
        public enum Column { One = 1, Two = 2, Three = 3 }

        [Header("按键架构 - 基本属性")]
        public Row row = Row.A;               // 行编号（A、B、C）
        public Column column = Column.One;    // 列编号（1、2、3）
        public KeyCode keyBind = KeyCode.None; // 绑定的键盘按键（QWEASDZXC）
        public string skillId = "00";          // 绑定的技能编号（00=未绑定技能）
        public float cooldownSeconds = 2f;     // 技能的cd时长（秒）

        [Header("技能信息显示 (只读)")]
        [SerializeField] private string skillName = "";        // 技能名称
        [SerializeField] private string skillDescription = ""; // 技能描述
        [SerializeField] private float skillCooldown = 0f;     // 技能冷却时间

        // 🔹 兼容旧代码的别名（不要删）
        public string boundSkillId
        {
            get => skillId;
            set => skillId = value;
        }

        public KeyCode boundKey
        {
            get => keyBind;
            set => keyBind = value;
        }

        [Header("按键架构 - UI组件引用")]
        public Image cooldownFill;     // 按键冷却圈Image
        public Image iconImage;        // 按键中央图标Image
        public TMP_Text slotLabelText; // 按键所绑定的行列编号（A1、A2这样的）
        public TMP_Text keyBindText;   // 按键所绑定的键盘按键
        public TMP_Text skillIdText;   // 显示当前绑定的技能编号
        public CanvasGroup canvasGroup;

        [Header("颜色配置")]
        [Header("CD圈颜色")]
        public Color readyCooldownFillColor = new Color(0.5f, 1f, 0.5f, 0.3f); // 有冷却技能的CD圈颜色（非CD期间）
        public Color alwaysReadyFillColor = new Color(0f, 1f, 0f, 0.8f);        // 无冷却技能的CD圈颜色（始终满绿）
        
        [Header("图标按下效果")]
        public Color iconPressedColor = new Color(0.5f, 0.5f, 0.5f, 1f);        // 按下时图标变灰的颜色

        [Header("行为配置")]
        public bool listenKeyboard = false;

        private Button button;
        private bool isOnCooldown = false;
        private float cooldownTimer = 0f;
        private float cooldownDuration = 0f;
        private bool isPressed = false;
        private Color originalIconColor;          // 保存图标的原始颜色

        public System.Action<NineButtons> OnSkillChanged;

        private void Reset()
        {
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
            // 从父物体名字自动读取行列号和按键绑定
            AutoSetRowColumnAndKey();
        }

        private void Awake()
        {
            // 从父物体名字自动读取行列号和按键绑定
            AutoSetRowColumnAndKey();
            
            button = GetComponent<Button>();
            if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (button != null) button.onClick.AddListener(Press);

            // 更新技能信息和cd时长
            UpdateSkillInfo();
            cooldownDuration = cooldownSeconds;
            UpdateLabels();

            if (cooldownFill != null)
            {
                cooldownFill.type = Image.Type.Filled;
                cooldownFill.fillMethod = Image.FillMethod.Radial360;
                cooldownFill.fillOrigin = (int)Image.Origin360.Top;
                cooldownFill.fillClockwise = true;
                cooldownFill.fillAmount = 0f;
                cooldownFill.color = Color.gray;
            }

            // 保存图标的原始颜色
            if (iconImage != null)
                originalIconColor = iconImage.color;

            if (skillId == "00") ApplyUnboundState();
            else ApplyBoundState();
        }

        private void Update()
        {
            // 只要有绑定的按键且不是"00"技能，就监听键盘
            bool shouldListenKeyboard = (keyBind != KeyCode.None && skillId != "00");
            
            if (shouldListenKeyboard)
            {
                // 检查按键状态来应用tinted效果
                bool currentPressed = Input.GetKey(keyBind);
                if (currentPressed != isPressed)
                {
                    isPressed = currentPressed;
                    Debug.Log($"[NineButtons] {gameObject.name} 键盘按键 {keyBind} 状态变化: {(isPressed ? "按下" : "释放")}");
                    Debug.Log($"[NineButtons] 即将调用 ApplyPressedEffect({isPressed})");
                    ApplyPressedEffect(isPressed);
                    Debug.Log($"[NineButtons] ApplyPressedEffect({isPressed}) 调用完成");
                }

                // 对于移动技能，使用按住逻辑；对于其他技能，使用按下逻辑
                bool isMovement = IsMovementSkill(skillId);
                
                if (isMovement)
                {
                    if (Input.GetKey(keyBind))
                        PressHold();
                }
                else
                {
                    if (Input.GetKeyDown(keyBind))
                    {
                        Debug.Log($"[NineButtons] {gameObject.name} 非移动技能按下: {keyBind}");
                        Press();
                    }
                }
            }
            else if (listenKeyboard && (keyBind == KeyCode.None || skillId == "00"))
            {
                // 调试信息：为什么不监听键盘
                Debug.LogWarning($"[NineButtons] {gameObject.name} 不监听键盘: keyBind={keyBind}, skillId={skillId}, listenKeyboard={listenKeyboard}");
            }

            if (isOnCooldown)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownFill && cooldownDuration > 0f)
                    cooldownFill.fillAmount = Mathf.Clamp01(cooldownTimer / cooldownDuration);

                if (cooldownTimer <= 0f)
                {
                    isOnCooldown = false;
                    cooldownTimer = 0f;
                    if (button) button.interactable = true;
                    if (cooldownFill) 
                    {
                        // 如果是CD=0的技能，恢复满绿色CD条
                        if (cooldownSeconds <= 0f)
                        {
                            cooldownFill.color = alwaysReadyFillColor;
                            cooldownFill.fillAmount = 1f;
                        }
                        else
                        {
                            cooldownFill.fillAmount = 0f;
                            cooldownFill.color = readyCooldownFillColor; // 恢复浅绿色CD圈
                        }
                    }

                }
            }
        }

        public void Press()
        {
            if (isOnCooldown || skillId == "00") return;
            Debug.Log($"[NineButtons] Click: {row}{(int)column} | Key:{keyBind} | Skill:{skillId}");
            
            // 应用按下效果
            StartCoroutine(MouseClickTintEffect());
            
            // 执行技能效果
            ExecuteSkill();
            
            // 开始冷却
            if (cooldownSeconds > 0)
                StartCooldown();
        }

        /// <summary>
        /// 按住时持续执行（仅用于移动技能）
        /// </summary>
        public void PressHold()
        {
            if (isOnCooldown || skillId == "00") return;
            
            // 找到PlayerController并执行持续移动
            var playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                if (playerController.IsMovementSkill(skillId))
                {
                    playerController.ExecuteMovement(skillId, Time.deltaTime);
                }
            }
            else
            {
                Debug.LogError("[NineButtons] 未找到PlayerController");
            }
        }

        /// <summary>
        /// 判断是否为移动技能
        /// </summary>
        private bool IsMovementSkill(string skillId)
        {
            var playerController = FindObjectOfType<PlayerController>();
            return playerController != null && playerController.IsMovementSkill(skillId);
        }

        /// <summary>
        /// 执行技能效果
        /// </summary>
        private void ExecuteSkill()
        {
            // 查找PlayerController并执行技能
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                player.ExecuteSkill(skillId);
            }
            else
            {
                Debug.LogWarning("[NineButtons] 未找到PlayerController，无法执行技能");
            }
        }

        public void StartCooldown(float cdSeconds = 0f)
        {
            if (cdSeconds <= 0f) cdSeconds = cooldownSeconds;
            if (cdSeconds <= 0f)
            {
                isOnCooldown = false;
                cooldownTimer = 0f;
                cooldownDuration = cooldownSeconds;
                if (button) button.interactable = true;
                if (cooldownFill) cooldownFill.fillAmount = 0f;
    
                return;
            }

            cooldownDuration = cdSeconds;
            cooldownTimer = cdSeconds;
            isOnCooldown = true;

            if (button) button.interactable = false;
            if (cooldownFill) cooldownFill.fillAmount = 1f;

        }

        public void SetSkill(string newSkillId, KeyCode newKey, float newCdSeconds = -1f)
        {
            skillId = newSkillId;
            keyBind = newKey;
            
            // 更新技能信息和cd时长
            UpdateSkillInfo();
            
            // 如果手动指定了冷却时间，则覆盖从数据库读取的值
            if (newCdSeconds >= 0f) cooldownSeconds = newCdSeconds;
            
            UpdateLabels();

            if (skillId == "00") ApplyUnboundState();
            else ApplyBoundState();

            OnSkillChanged?.Invoke(this);
        }

        public void ApplyUnboundState()
        {
            if (button) button.interactable = false;
            listenKeyboard = false;
            if (cooldownFill) cooldownFill.color = Color.gray;
            if (canvasGroup) canvasGroup.alpha = 0.2f;
            isOnCooldown = false;
            cooldownTimer = 0f;
            if (cooldownFill) cooldownFill.fillAmount = 0f;
            UpdateLabels(); // 确保标签正确更新
        }

        public void ApplyBoundState()
        {
            if (button) button.interactable = true;
            listenKeyboard = true;

            
            if (cooldownFill) 
            {
                // 如果是CD=0的技能，显示满绿色CD条
                if (cooldownSeconds <= 0f)
                {
                    cooldownFill.color = alwaysReadyFillColor;
                    cooldownFill.fillAmount = 1f; // 始终满条
                }
                else
                {
                    cooldownFill.color = readyCooldownFillColor; // 使用浅绿色CD圈
                    cooldownFill.fillAmount = 0f;
                }
            }
            
            if (canvasGroup) canvasGroup.alpha = 1f;
            UpdateLabels(); // 确保标签正确更新
            
            Debug.Log($"[NineButtons] {gameObject.name} ApplyBoundState: skillId={skillId}, keyBind={keyBind}, listenKeyboard={listenKeyboard}");
        }

        /// <summary>
        /// 应用按下效果
        /// </summary>
        private void ApplyPressedEffect(bool pressed)
        {
            Debug.Log($"[NineButtons] === ApplyPressedEffect({pressed}) 开始 ===");
            Debug.Log($"[NineButtons] iconImage: {(iconImage != null ? "存在" : "null")}");
            
            if (pressed)
            {
                // 按下时让中央图标变灰
                if (iconImage != null)
                {
                    Color oldColor = iconImage.color;
                    iconImage.color = iconPressedColor;
                    Debug.Log($"[NineButtons] {gameObject.name} 图标颜色已改变：{oldColor} -> {iconImage.color}");
                    Debug.Log($"[NineButtons] {gameObject.name} iconImage GameObject: {iconImage.gameObject.name}");
                    Debug.Log($"[NineButtons] {gameObject.name} iconImage enabled: {iconImage.enabled}");
                    Debug.Log($"[NineButtons] {gameObject.name} iconImage sprite: {(iconImage.sprite != null ? iconImage.sprite.name : "null")}");
                }
                else
                {
                    Debug.LogWarning($"[NineButtons] {gameObject.name} iconImage 为空！无法变灰");
                }
                

                
                Debug.Log($"[NineButtons] {gameObject.name} 键盘按下效果已应用");
            }
            else
            {
                // 释放时恢复中央图标原始颜色
                if (iconImage != null)
                {
                    iconImage.color = originalIconColor;
                    Debug.Log($"[NineButtons] {gameObject.name} 图标颜色已恢复：{originalIconColor}");
                }
                

                
                Debug.Log($"[NineButtons] {gameObject.name} 键盘释放，颜色已恢复");
            }
            
            Debug.Log($"[NineButtons] === ApplyPressedEffect({pressed}) 结束 ===");
        }

        /// <summary>
        /// 鼠标点击的tint效果
        /// </summary>
        private System.Collections.IEnumerator MouseClickTintEffect()
        {
            // 保存当前图标颜色
            Color originalIconColorTemp = iconImage != null ? iconImage.color : Color.white;
            
            // 鼠标点击时让中央图标变灰
            if (iconImage != null)
            {
                iconImage.color = iconPressedColor;
                Debug.Log($"[NineButtons] {gameObject.name} 鼠标点击图标变灰效果已应用");
            }
            
            // 等待短暂时间
            yield return new WaitForSeconds(0.15f);
            
            // 恢复图标颜色
            if (iconImage != null)
            {
                iconImage.color = originalIconColorTemp;
                Debug.Log($"[NineButtons] {gameObject.name} 鼠标点击图标颜色已恢复");
            }
        }

        /// <summary>
        /// 从父物体名字自动设置行号、列号和键盘绑定
        /// </summary>
        [ContextMenu("自动设置行列和按键")]
        private void AutoSetRowColumnAndKey()
        {
            if (transform.parent == null) return;

            string parentName = transform.parent.name;
            if (parentName.Length < 2) return;

            // 获取名字的最后两位
            string lastTwo = parentName.Substring(parentName.Length - 2);
            
            if (lastTwo.Length == 2)
            {
                char rowChar = lastTwo[0];  // 行号字符 (A, B, C)
                char colChar = lastTwo[1];  // 列号字符 (1, 2, 3)

                // 设置行号
                switch (rowChar)
                {
                    case 'A':
                        row = Row.A;
                        break;
                    case 'B':
                        row = Row.B;
                        break;
                    case 'C':
                        row = Row.C;
                        break;
                }

                // 设置列号
                switch (colChar)
                {
                    case '1':
                        column = Column.One;
                        break;
                    case '2':
                        column = Column.Two;
                        break;
                    case '3':
                        column = Column.Three;
                        break;
                }

                // 自动绑定键盘按键 (A1-Q, A2-W, A3-E, B1-A, B2-S, B3-D, C1-Z, C2-X, C3-C)
                KeyCode newKey = GetKeyCodeForPosition(row, column);
                if (newKey != KeyCode.None)
                    keyBind = newKey;
            }
        }

        /// <summary>
        /// 根据行列位置获取对应的键盘按键
        /// </summary>
        private KeyCode GetKeyCodeForPosition(Row r, Column c)
        {
            // 键盘布局映射
            // A1-Q, A2-W, A3-E
            // B1-A, B2-S, B3-D  
            // C1-Z, C2-X, C3-C
            switch (r)
            {
                case Row.A:
                    switch (c)
                    {
                        case Column.One: return KeyCode.Q;
                        case Column.Two: return KeyCode.W;
                        case Column.Three: return KeyCode.E;
                    }
                    break;
                case Row.B:
                    switch (c)
                    {
                        case Column.One: return KeyCode.A;
                        case Column.Two: return KeyCode.S;
                        case Column.Three: return KeyCode.D;
                    }
                    break;
                case Row.C:
                    switch (c)
                    {
                        case Column.One: return KeyCode.Z;
                        case Column.Two: return KeyCode.X;
                        case Column.Three: return KeyCode.C;
                    }
                    break;
            }
            return KeyCode.None;
        }

        /// <summary>
        /// 更新技能信息
        /// </summary>
        [ContextMenu("更新技能信息")]
        public void UpdateSkillInfo()
        {
            SkillInfo skillInfo = SkillDatabase.GetSkillInfo(skillId);
            skillName = skillInfo.name;
            skillDescription = skillInfo.description;
            skillCooldown = skillInfo.cooldownTime;
            
            // 总是从技能数据库更新冷却时间
            cooldownSeconds = skillInfo.cooldownTime;
            
            // 更新CD条显示
            if (cooldownFill != null && skillId != "00")
            {
                if (cooldownSeconds <= 0f)
                {
                    cooldownFill.color = alwaysReadyFillColor;
                    cooldownFill.fillAmount = 1f; // CD=0技能始终满条
                }
                else
                {
                    cooldownFill.color = readyCooldownFillColor;
                    cooldownFill.fillAmount = 0f;
                }
            }
        }

        /// <summary>
        /// 手动设置为C2按钮（向下移动）
        /// </summary>
        [ContextMenu("设置为C2按钮")]
        public void SetAsC2Button()
        {
            row = Row.C;
            column = Column.Two;
            keyBind = KeyCode.X;
            skillId = "03";
            UpdateSkillInfo();
            ApplyBoundState();
            UpdateLabels();
            Debug.Log($"[NineButtons] {name} 已手动设置为C2按钮（向下移动），CD: {cooldownSeconds}秒");
        }

        private void UpdateLabels()
        {
            if (slotLabelText) 
            {
                slotLabelText.text = $"{row}{(int)column}";
                slotLabelText.alignment = TextAlignmentOptions.Center;
            }
            
            if (keyBindText) 
            {
                keyBindText.text = keyBind != KeyCode.None ? keyBind.ToString() : "";
                keyBindText.alignment = TextAlignmentOptions.Center;
            }
            
            if (skillIdText) 
            {
                // 如果是00技能（未绑定），显示空格；否则显示技能编号
                skillIdText.text = (skillId == "00") ? "  " : skillId;
                skillIdText.alignment = TextAlignmentOptions.Center;
                Debug.Log($"[NineButtons] {gameObject.name} 更新技能编号显示: skillId={skillId}, 显示文本='{skillIdText.text}'");
            }
        }

        #region 测试方法

        [ContextMenu("测试鼠标点击效果")]
        private void TestMouseClickEffect()
        {
            Debug.Log($"[NineButtons] 测试鼠标点击效果: {gameObject.name}");
            StartCoroutine(MouseClickTintEffect());
        }

        [ContextMenu("测试键盘按下效果")]
        private void TestKeyboardPressEffect()
        {
            Debug.Log($"[NineButtons] 测试键盘按下效果: {gameObject.name}");
            ApplyPressedEffect(true);
            // 2秒后恢复
            StartCoroutine(TestKeyboardReleaseEffect());
        }

        [ContextMenu("测试键盘输入检测")]
        private void TestKeyboardInput()
        {
            Debug.Log($"[NineButtons] === 键盘输入检测测试 ===");
            Debug.Log($"[NineButtons] 按钮: {gameObject.name}");
            Debug.Log($"[NineButtons] keyBind: {keyBind}");
            Debug.Log($"[NineButtons] skillId: {skillId}");
            Debug.Log($"[NineButtons] shouldListenKeyboard: {(keyBind != KeyCode.None && skillId != "00")}");
            Debug.Log($"[NineButtons] IsMovementSkill({skillId}): {IsMovementSkill(skillId)}");
            Debug.Log($"[NineButtons] 当前按键状态: {Input.GetKey(keyBind)}");
            Debug.Log($"[NineButtons] 当前按键按下: {Input.GetKeyDown(keyBind)}");
        }

        [ContextMenu("显示按钮状态")]
        private void ShowButtonStatus()
        {
            Debug.Log($"[NineButtons] 按钮状态 {gameObject.name}:");
            Debug.Log($"  - skillId: {skillId}");
            Debug.Log($"  - keyBind: {keyBind}");
            Debug.Log($"  - listenKeyboard: {listenKeyboard}");
            Debug.Log($"  - isPressed: {isPressed}");

            Debug.Log($"  - iconImage: {(iconImage != null ? iconImage.color.ToString() : "null")}");
            Debug.Log($"  - iconImage GameObject: {(iconImage != null ? iconImage.gameObject.name : "null")}");
            Debug.Log($"  - shouldListenKeyboard: {(keyBind != KeyCode.None && skillId != "00")}");
        }

        [ContextMenu("测试图标变灰")]
        private void TestIconGray()
        {
            Debug.Log($"[NineButtons] 测试图标变灰: {gameObject.name}");
            if (iconImage != null)
            {
                Color oldColor = iconImage.color;
                iconImage.color = Color.red; // 先变红测试是否有效果
                Debug.Log($"[NineButtons] 图标颜色测试：{oldColor} -> {iconImage.color}");
                
                // 2秒后恢复
                StartCoroutine(TestRestoreIconColor(oldColor));
            }
            else
            {
                Debug.LogError($"[NineButtons] {gameObject.name} iconImage 为空！请在Inspector中分配");
            }
        }

        [ContextMenu("测试图标变灰效果")]
        private void TestIconGrayEffect()
        {
            Debug.Log($"[NineButtons] 测试实际的图标变灰效果: {gameObject.name}");
            if (iconImage != null)
            {
                Color oldColor = iconImage.color;
                // 使用和ApplyPressedEffect相同的颜色
                iconImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                Debug.Log($"[NineButtons] 图标变灰测试：{oldColor} -> {iconImage.color}");
                Debug.Log($"[NineButtons] 原始颜色 originalIconColor: {originalIconColor}");
                
                // 3秒后恢复
                StartCoroutine(TestRestoreIconColorToOriginal());
            }
            else
            {
                Debug.LogError($"[NineButtons] {gameObject.name} iconImage 为空！");
            }
        }

        private System.Collections.IEnumerator TestRestoreIconColorToOriginal()
        {
            yield return new WaitForSeconds(3f);
            if (iconImage != null)
            {
                iconImage.color = originalIconColor;
                Debug.Log($"[NineButtons] 图标颜色已恢复到原始颜色: {originalIconColor}");
            }
        }

        private System.Collections.IEnumerator TestRestoreIconColor(Color originalColor)
        {
            yield return new WaitForSeconds(2f);
            if (iconImage != null)
            {
                iconImage.color = originalColor;
                Debug.Log($"[NineButtons] 图标颜色已恢复: {originalColor}");
            }
        }

        private System.Collections.IEnumerator TestKeyboardReleaseEffect()
        {
            yield return new WaitForSeconds(2f);
            ApplyPressedEffect(false);
        }

        #endregion
    }
}