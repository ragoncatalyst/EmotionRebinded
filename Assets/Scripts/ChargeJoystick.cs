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

    [Header("Events")]
    public UnityEvent<float> onRelease;        // 参数为[0,1]的充能系数

    private bool isCharging;
    private float chargeTimer;

    private void Awake()
    {
        if (player == null)
        {
            var pgo = GameObject.FindWithTag("Player");
            if (pgo != null) player = pgo.GetComponent<PlayerController>();
        }
    }

    private void Update()
    {
        // 开始充能
        if (Input.GetKeyDown(chargeKey))
        {
            isCharging = true;
            chargeTimer = 0f;
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
            if (player != null && !string.IsNullOrEmpty(skillId))
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

    private void UpdateFill(float overrideValue = -1f)
    {
        if (optionalFill == null) return;
        float v = overrideValue >= 0f ? overrideValue : Mathf.Clamp01(chargeTimer / Mathf.Max(0.0001f, maxChargeTime));
        optionalFill.fillAmount = v;
    }
}


