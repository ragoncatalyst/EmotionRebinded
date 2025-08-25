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
    public RectTransform bg;                   // 背景圆(ImageBG)
    public RectTransform handle;               // 把手圆(Handle)
    public float rotationSpeed = 240f;         // 按住时每秒旋转角速度（用于模拟轴旋转）
    public float handleEdgePadding = 4f;       // 把手距离BG边缘的最小空隙
    public float handleSmoothing = 20f;        // 把手插值平滑移动

    [Header("Charge Settings")]
    public float maxChargeTime = 2.0f;         // 兼容保留：原基于时间的充能（不再作为主逻辑）
    public Image optionalFill;                 // 可选：用于显示充能进度(0-1)
    public AnimationCurve chargeCurve = AnimationCurve.Linear(0, 0, 1, 1); // 充能曲线
    public float requiredRotationsToFull = 10f; // 需要旋转多少圈才算满（按住F模拟）

    [Header("Skill Casting")]
    public string skillId = "06";              // 释放的技能编号
    public PlayerController player;            // 自动在场景内查找Tag=Player
    public bool executeSkillOnRelease = true;  // 释放时是否自动调用技能
    public bool simulateAxisByHoldKey = true;  // 按住F时以旋转来模拟轴

    [Header("Ultimate Trigger")]
    public KeyCode ultimateKey = KeyCode.K;     // 终极释放键（映射Arduino摇杆点击）
    public float ultimateDamage = 20f;          // 全场攻击伤害
    public Vector2 ultimateAreaSize = new Vector2(30f, 30f); // 终极攻击区域（宽x高），以玩家为中心

    [Header("Block Conditions")]
    public bool disableWhenUpgradePanelOpen = true; // 升级面板打开时禁用
    public UpgradePanelController upgradePanel;     // 自动场景查找

    [Header("Events")]
    public UnityEvent<float> onRelease;        // 参数为[0,1]的充能系数
    public UnityEvent onStartCharge;           // 开始充能时
    public UnityEvent<float> onCharging;       // 充能进行中(每帧传递进度)
    public UnityEvent<Vector2> onAxis;         // 轴变更事件，值域[-1,1]

    private bool isCharging;
    private float chargeTimer;
    private float handleMaxRadius;             // BG半径-边距
    private Vector2 currentAxis;               // 归一化轴 [-1,1]
    private float spinAngle;                   // 模拟用角度
    private float totalAngleAbs;               // 累计旋转角度绝对值（弧度）
    private float chargeFill01;                // 当前充能[0..1]

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
        RecalculateHandleRadius();
        // 初始化为0填充
        ClearCharge();
        SetAxis(Vector2.zero);
    }

    private void Update()
    {
        if (IsInputBlocked())
        {
            // 若被阻断且仍在充能，复位显示
            if (isCharging)
            {
                isCharging = false;
                // 不重置进度，保持填充
                SetAxis(Vector2.zero);
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
            // 不再旋转单独的knob，仅通过轴模拟与把手位置表示
            // 以旋转圈数作为主进度来源
            float deltaRad = rotationSpeed * Mathf.Deg2Rad * Time.deltaTime;
            spinAngle += deltaRad;
            totalAngleAbs += Mathf.Abs(deltaRad);
            float fullAngle = Mathf.Max(0.0001f, requiredRotationsToFull) * Mathf.PI * 2f;
            chargeFill01 = Mathf.Clamp01(totalAngleAbs / fullAngle);
            UpdateFill(chargeFill01);
            onCharging?.Invoke(chargeFill01);
            if (simulateAxisByHoldKey)
            {
                spinAngle += rotationSpeed * Mathf.Deg2Rad * Time.deltaTime;
                Vector2 sim = new Vector2(Mathf.Cos(spinAngle), Mathf.Sin(spinAngle));
                SetAxis(sim);
            }
        }

        // 释放：计算充能系数并施放技能
        if (isCharging && Input.GetKeyUp(chargeKey))
        {
            isCharging = false;
            // 基于旋转圈数的充能比例
            float ratio = chargeFill01;

            // 回调事件(可用于驱动VFX/音效)
            onRelease?.Invoke(ratio);

            // 释放技能
            if (executeSkillOnRelease && player != null && !string.IsNullOrEmpty(skillId))
            {
                player.ExecuteSkill(skillId);
            }

            // 不重置进度，等待K键（或Arduino点击）触发清空
            SetAxis(Vector2.zero);
        }

        // 终极触发：需要满充，按下K后清空并全场攻击
        if (Input.GetKeyDown(ultimateKey) && chargeFill01 >= 1f)
        {
            CastGlobalUltimate();
            ClearCharge();
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
        float v = overrideValue >= 0f ? overrideValue : chargeFill01;
        optionalFill.fillAmount = v;
    }

    private void RecalculateHandleRadius()
    {
        if (bg == null) { handleMaxRadius = 0f; return; }
        var size = bg.rect.size;
        float r = Mathf.Min(size.x, size.y) * 0.5f;
        handleMaxRadius = Mathf.Max(0f, r - handleEdgePadding);
    }

    private void LateUpdate()
    {
        // 轻量更新半径，适配分辨率/布局变化
        RecalculateHandleRadius();
        if (handle != null && handleMaxRadius > 0f)
        {
            Vector2 target = currentAxis * handleMaxRadius;
            Vector2 pos = Vector2.Lerp(handle.anchoredPosition, target, 1f - Mathf.Exp(-handleSmoothing * Time.unscaledDeltaTime));
            handle.anchoredPosition = pos;
        }
    }

    // 设置摇杆轴（-1..1），自动截断并驱动把手
    public void SetAxis(Vector2 axis)
    {
        axis = Vector2.ClampMagnitude(axis, 1f);
        currentAxis = axis;
        onAxis?.Invoke(currentAxis);
    }

    // 获取当前摇杆值（-1..1）
    public Vector2 GetAxis() => currentAxis;

    public void ClearCharge()
    {
        totalAngleAbs = 0f;
        chargeFill01 = 0f;
        UpdateFill(chargeFill01);
    }

    private void CastGlobalUltimate()
    {
        if (player == null) return;
        Vector2 center = player.transform.position;
        Vector2 size = new Vector2(Mathf.Max(0.01f, ultimateAreaSize.x), Mathf.Max(0.01f, ultimateAreaSize.y));
        var hits = Physics2D.OverlapBoxAll(center, size, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            var e = hits[i].GetComponent<Enemy>();
            if (e != null && e.gameObject.activeInHierarchy)
            {
                e.TakeDamage(ultimateDamage);
            }
        }
    }
}


