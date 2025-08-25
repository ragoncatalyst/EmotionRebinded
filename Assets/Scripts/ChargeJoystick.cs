using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 按住键(F)旋转“摇杆”并进行充能，松开(或短按)立即释放对应技能。
/// 将本脚本挂在一个带有 UI 元件(如 Image) 的 GameObject 上，
/// 并在 Inspector 里绑定要旋转的 RectTransform 以及技能等参数。
/// </summary>
public class ChargeJoystick : MonoBehaviour
{
    [Header("Key Bindings")]
    public KeyCode chargeKey = KeyCode.F;

    [Header("Visual")]
    public RectTransform knob;                 // 旋转的摇杆(可为任意RectTransform)
    public float rotationSpeed = 240f;         // 按住时每秒旋转角速度
    public bool resetRotationOnRelease = true; // 释放后是否重置到0度

    [Header("Charge Settings")]
    public float maxChargeTime = 2.0f;         // 达到满充所需时间
    public Image optionalFill;                 // 可选：用于显示充能进度(0-1)
    public AnimationCurve chargeCurve = AnimationCurve.Linear(0, 0, 1, 1); // 充能曲线

    [Header("Skill Casting")]
    public string skillId = "06";              // 释放的技能编号
    public PlayerController player;            // 自动在场景内查找Tag=Player
    public bool executeSkillOnRelease = true;  // 释放时是否自动调用技能

    [Header("Block Conditions")]
    public bool disableWhenUpgradePanelOpen = true; // 升级面板打开时禁用
    public UpgradePanelController upgradePanel;     // 自动场景查找

    [Header("Events")]
    public UnityEvent<float> onRelease;        // 参数为[0,1]的充能系数
    public UnityEvent onStartCharge;           // 开始充能时
    public UnityEvent<float> onCharging;       // 充能进行中(每帧传递进度)

    private bool isCharging;
    private float chargeTimer;

    private void Awake()
    {
        if (player == null)
        {
            var pgo = GameObject.FindWithTag("Player");
            if (pgo != null) player = pgo.GetComponent<PlayerController>();
        }

        if (upgradePanel == null)
        {
            // 先尝试通过 Tag 查找，再兜底场景扫描
            var owner = GameObject.FindWithTag("GameController");
            if (owner != null)
            {
                upgradePanel = owner.GetComponentInChildren<UpgradePanelController>();
            }
            if (upgradePanel == null)
            {
                upgradePanel = FindObjectOfType<UpgradePanelController>();
            }
        }
    }

    private void Update()
    {
        if (IsInputBlocked())
        {
            // 若被阻断且仍在充能，复位显示
            if (isCharging)
            {
                isCharging = false;
                if (resetRotationOnRelease && knob != null)
                {
                    knob.localRotation = Quaternion.identity;
                }
                UpdateFill(0f);
            }
            return;
        }

        // 开始充能
        if (Input.GetKeyDown(chargeKey))
        {
            isCharging = true;
            chargeTimer = 0f;
            onStartCharge?.Invoke();
        }

        // 按住充能 + 旋转摇杆
        if (isCharging && Input.GetKey(chargeKey))
        {
            chargeTimer += Time.deltaTime;
            if (knob != null)
            {
                knob.Rotate(0f, 0f, -rotationSpeed * Time.deltaTime);
            }
            UpdateFill();
            onCharging?.Invoke(Mathf.Clamp01(chargeTimer / Mathf.Max(0.0001f, maxChargeTime)));
        }

        // 释放：计算充能系数并施放技能
        if (isCharging && Input.GetKeyUp(chargeKey))
        {
            isCharging = false;
            float ratio = Mathf.Clamp01(chargeTimer / Mathf.Max(0.0001f, maxChargeTime));
            ratio = Mathf.Clamp01(chargeCurve.Evaluate(ratio));

            // 回调事件(可用于驱动VFX/音效)
            onRelease?.Invoke(ratio);

            // 释放技能
            if (executeSkillOnRelease && player != null && !string.IsNullOrEmpty(skillId))
            {
                player.ExecuteSkill(skillId);
            }

            if (resetRotationOnRelease && knob != null)
            {
                knob.localRotation = Quaternion.identity;
            }

            UpdateFill(0f);
        }
    }

    private bool IsInputBlocked()
    {
        if (!disableWhenUpgradePanelOpen || upgradePanel == null) return false;
        // 面板打开或正在选择时不响应
        if (upgradePanel.IsPanelOpen) return true;
        if (upgradePanel.IsChoosingOptions) return true;
        return false;
    }

    private void UpdateFill(float overrideValue = -1f)
    {
        if (optionalFill == null) return;
        float v = overrideValue >= 0f ? overrideValue : Mathf.Clamp01(chargeTimer / Mathf.Max(0.0001f, maxChargeTime));
        optionalFill.fillAmount = v;
    }
}


