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
        public Image backgroundImage;  // 底色 Image
        public CanvasGroup canvasGroup;

        [Header("颜色配置")]
        public Color readyColor = Color.green;
        public Color cooldownColor = Color.gray;
        public Color unboundColor = Color.gray;

        [Header("行为配置")]
        public bool listenKeyboard = false;

        private Button button;
        private bool isOnCooldown = false;
        private float cooldownTimer = 0f;
        private float cooldownDuration = 0f;

        public System.Action<NineButtons> OnSkillChanged;

        private void Reset()
        {
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (button != null) button.onClick.AddListener(Press);

            cooldownDuration = cooldownSeconds;
            UpdateLabels();

            if (cooldownFill != null)
            {
                cooldownFill.type = Image.Type.Filled;
                cooldownFill.fillMethod = Image.FillMethod.Radial360;
                cooldownFill.fillOrigin = (int)Image.Origin360.Top;
                cooldownFill.fillClockwise = true;
                cooldownFill.fillAmount = 0f;
                cooldownFill.color = unboundColor;
            }

            if (skillId == "00") ApplyUnboundState();
            else ApplyBoundState();
        }

        private void Update()
        {
            if (listenKeyboard && keyBind != KeyCode.None && Input.GetKeyDown(keyBind))
                Press();

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
                    if (cooldownFill) cooldownFill.fillAmount = 0f;
                    if (backgroundImage) backgroundImage.color = readyColor;
                }
            }
        }

        public void Press()
        {
            if (isOnCooldown || skillId == "00") return;
            Debug.Log($"[NineButtons] Click: {row}{(int)column} | Key:{keyBind} | Skill:{skillId}");
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
                if (backgroundImage) backgroundImage.color = readyColor;
                return;
            }

            cooldownDuration = cdSeconds;
            cooldownTimer = cdSeconds;
            isOnCooldown = true;

            if (button) button.interactable = false;
            if (cooldownFill) cooldownFill.fillAmount = 1f;
            if (backgroundImage) backgroundImage.color = cooldownColor;
        }

        public void SetSkill(string newSkillId, KeyCode newKey, float newCdSeconds = -1f)
        {
            skillId = newSkillId;
            keyBind = newKey;
            if (newCdSeconds >= 0f) cooldownSeconds = newCdSeconds;
            UpdateLabels();

            if (skillId == "00") ApplyUnboundState();
            else ApplyBoundState();

            OnSkillChanged?.Invoke(this);
        }

        private void ApplyUnboundState()
        {
            if (button) button.interactable = false;
            listenKeyboard = false;
            if (backgroundImage) backgroundImage.color = unboundColor;
            if (cooldownFill) cooldownFill.color = unboundColor;
            if (canvasGroup) canvasGroup.alpha = 0.2f;
            isOnCooldown = false;
            cooldownTimer = 0f;
            if (cooldownFill) cooldownFill.fillAmount = 0f;
        }

        private void ApplyBoundState()
        {
            if (button) button.interactable = true;
            listenKeyboard = true;
            if (backgroundImage) backgroundImage.color = readyColor;
            if (cooldownFill) cooldownFill.color = readyColor;
            if (canvasGroup) canvasGroup.alpha = 1f;
        }

        private void UpdateLabels()
        {
            if (slotLabelText) slotLabelText.text = $"{row}{(int)column}";
            if (keyBindText) keyBindText.text = keyBind != KeyCode.None ? keyBind.ToString() : "";
        }
    }
}