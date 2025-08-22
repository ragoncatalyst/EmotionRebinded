using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
#if TMP_PRESENT || UNITY_2018_3_OR_NEWER
using TMPro;
#endif

namespace MyGame.UI
{
    /// <summary>
    /// 简单的经验值/等级显示条：
    /// - 初始等级0
    /// - 基础每级所需经验为 baseExpPerLevel（默认20）
    /// - 每提升levelsPerTier（默认5）级，所需经验 + expIncrementPerTier（默认5）
    /// - 在UI上显示当前等级与进度条（Image 或 Slider）
    /// - 所有参数可在Inspector里查看与手动编辑
    /// </summary>
    [DisallowMultipleComponent]
    public class ExperienceBar : MonoBehaviour
    {
        [Header("等级/经验配置")]
        [SerializeField] private int currentLevel = 0;           // 当前等级（从0开始）
        [SerializeField] private int currentExp = 0;             // 当前经验（当前等级的累积值）
        [SerializeField] private int baseExpPerLevel = 20;       // 0-4级每级经验
        [SerializeField] private int levelsPerTier = 5;          // 每多少级增加一次所需经验
        [SerializeField] private int expIncrementPerTier = 5;    // 每个区间增加的经验

        [Header("UI 绑定（可选）")]
        [SerializeField] private Image progressFillImage;        // 填充型进度条（Type=Filled）
        [SerializeField] private Slider progressSlider;          // 或者使用Slider作为进度条
#if TMP_PRESENT || UNITY_2018_3_OR_NEWER
        [SerializeField] private TMP_Text levelText;             // 显示等级的文本
        [SerializeField] private TMP_Text expText;               // 显示经验的文本（cur/req）
#else
        [SerializeField] private Text levelText;
        [SerializeField] private Text expText;
#endif

        [Header("调试/显示")]
        [SerializeField] private bool autoUpdateUI = true;       // 在Update内自动刷新（也可手动调用UpdateUI）

        [Header("事件（升级时触发）")]
        [SerializeField] private IntEvent onLevelUp = new IntEvent(); // 参数为升级后的新等级

        [System.Serializable]
        public class IntEvent : UnityEvent<int> { }

        [Header("自动打开升级面板（可选）")]
        [Tooltip("当等级提升时，是否自动播放升级动画并打开升级面板。")]
        [SerializeField] private bool openUpgradePanelOnLevelUp = true;
        [Tooltip("若未手动指定，将在运行时自动查找场景中的 UpgradePanelController。")]
        [SerializeField] private UpgradePanelController upgradePanel;

        [Header("进度动画（仅经验增长时）")]
        [SerializeField] private bool animateFillOnGain = true;   // 增长时播放动画
        [SerializeField] private float minSegmentDuration = 0.10f;// 单段最短时长
        [SerializeField] private float maxSegmentDuration = 0.35f;// 单段最长时长
        [SerializeField] private AnimationCurve fillCurve = AnimationCurve.EaseInOut(0,0,1,1);

        private float displayedFill = 0f; // 当前显示中的进度（0..1）
        private Coroutine fillAnimCo;

        public int CurrentLevel => Mathf.Max(0, currentLevel);
        public int CurrentExp   => Mathf.Max(0, currentExp);

        private void Awake()
        {
            // 若绑定的是Image但未设置为Filled，自动纠正，避免看不到填充
            if (progressFillImage != null && progressFillImage.type != Image.Type.Filled)
            {
                progressFillImage.type = Image.Type.Filled;
                progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            }
            // 若绑定的是Slider，统一成0..1范围
            if (progressSlider != null)
            {
                progressSlider.minValue = 0f;
                progressSlider.maxValue = 1f;
                progressSlider.wholeNumbers = false;
            }

            if (openUpgradePanelOnLevelUp && upgradePanel == null)
            {
                upgradePanel = FindObjectOfType<UpgradePanelController>();
            }

            // 初始化显示中的进度
            displayedFill = CalcCurrentFill();
            UpdateUI();
        }

        private void Update()
        {
            if (autoUpdateUI)
                UpdateUI();
        }

        private void OnValidate()
        {
            currentLevel = Mathf.Max(0, currentLevel);
            currentExp   = Mathf.Max(0, currentExp);
            baseExpPerLevel = Mathf.Max(1, baseExpPerLevel);
            levelsPerTier   = Mathf.Max(1, levelsPerTier);
            expIncrementPerTier = Mathf.Max(0, expIncrementPerTier);

            // 防止当前经验超过本级上限
            int req = GetRequiredExpForLevel(currentLevel);
            if (currentExp > req) currentExp = req;
            UpdateUI();
        }

        public int GetRequiredExpForLevel(int level)
        {
            if (level < 0) level = 0;
            int tier = level / Mathf.Max(1, levelsPerTier);
            return baseExpPerLevel + tier * expIncrementPerTier;
        }

        public void AddExperience(int amount)
        {
            if (amount <= 0) return;
            // 记录动画起点
            float startFill = CalcCurrentFill();
            int beforeLevel = currentLevel;
            int beforeExp = currentExp;

            currentExp += amount;
            NormalizeLevelAndExp();

            // 计算动画关键帧（可能跨级）
            float endFill = CalcCurrentFill();
            int gained = currentLevel - beforeLevel;
            if (animateFillOnGain)
            {
                System.Collections.Generic.List<float> keys = new System.Collections.Generic.List<float>();
                keys.Add(startFill);
                for (int i = 0; i < gained; i++)
                {
                    // 补到满格，再回到0
                    if (keys[keys.Count-1] < 1f) keys.Add(1f);
                    keys.Add(0f);
                }
                if (keys[keys.Count-1] != endFill) keys.Add(endFill);
                StartFillAnimation(keys);
            }
            else
            {
                displayedFill = endFill;
                UpdateUI();
            }
        }

        public void RemoveExperience(int amount)
        {
            if (amount <= 0) return;
            currentExp -= amount;
            while (currentExp < 0 && currentLevel > 0)
            {
                currentLevel--;
                currentExp += GetRequiredExpForLevel(currentLevel);
            }
            currentExp = Mathf.Max(0, currentExp);
            displayedFill = CalcCurrentFill();
            UpdateUI();
        }

        public void SetLevel(int level, bool resetExp = true)
        {
            currentLevel = Mathf.Max(0, level);
            if (resetExp)
                currentExp = 0;
            else
                currentExp = Mathf.Clamp(currentExp, 0, GetRequiredExpForLevel(currentLevel));
            displayedFill = CalcCurrentFill();
            UpdateUI();
        }

        private void NormalizeLevelAndExp()
        {
            // 连续进级
            int safety = 0;
            int levelsGained = 0;
            while (safety++ < 999)
            {
                int req = GetRequiredExpForLevel(currentLevel);
                if (currentExp >= req)
                {
                    currentExp -= req;
                    currentLevel++;
                    // 逐级触发升级事件
                    try { onLevelUp?.Invoke(currentLevel); }
                    catch (System.Exception e) { Debug.LogException(e, this); }
                    levelsGained++;
                }
                else break;
            }

            // 升级后自动打开升级面板（只调用一次，避免连续多次弹出）
            if (levelsGained > 0 && openUpgradePanelOnLevelUp)
            {
                if (upgradePanel == null)
                    upgradePanel = FindObjectOfType<UpgradePanelController>();
                if (upgradePanel != null)
                {
                    // 摄像机拉近
                    var camFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollowing>() : null;
                    if (camFollow != null) camFollow.BeginLevelUpZoom();
                    // 尝试打开，如果已打开则累积请求
                    upgradePanel.RequestOpenFromLevelUp();
                }
            }
        }

        public void UpdateUI()
        {
            float fill = displayedFill;
            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = fill;
            }
            if (progressSlider != null)
            {
                progressSlider.value = fill;
            }
            if (levelText != null)
            {
                levelText.text = $"Lv {currentLevel}";
            }
            if (expText != null)
            {
                int req = GetRequiredExpForLevel(currentLevel);
                expText.text = $"XP {currentExp}/{req}";
            }
        }

        private float CalcCurrentFill()
        {
            int req = GetRequiredExpForLevel(currentLevel);
            return req > 0 ? Mathf.Clamp01((float)currentExp / req) : 0f;
        }

        private void StartFillAnimation(System.Collections.Generic.List<float> keys)
        {
            if (fillAnimCo != null) StopCoroutine(fillAnimCo);
            fillAnimCo = StartCoroutine(AnimateFillKeys(keys));
        }

        private System.Collections.IEnumerator AnimateFillKeys(System.Collections.Generic.List<float> keys)
        {
            if (keys == null || keys.Count < 2)
            {
                displayedFill = CalcCurrentFill();
                UpdateUI();
                yield break;
            }
            for (int i = 0; i < keys.Count - 1; i++)
            {
                float from = Mathf.Clamp01(keys[i]);
                float to   = Mathf.Clamp01(keys[i+1]);
                float delta = Mathf.Abs(to - from);
                float dur = Mathf.Lerp(minSegmentDuration, maxSegmentDuration, delta);
                float t = 0f;
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float p = dur > 0f ? Mathf.Clamp01(t / dur) : 1f;
                    float eased = fillCurve.Evaluate(p);
                    displayedFill = Mathf.Lerp(from, to, eased);
                    UpdateUI();
                    yield return null;
                }
                displayedFill = to;
                UpdateUI();
            }
            fillAnimCo = null;
        }

        // 便捷测试
        [ContextMenu("+10 经验")]
        private void Add10() { AddExperience(10); }
        [ContextMenu("+20 经验")]
        private void Add20() { AddExperience(20); }
        [ContextMenu("-10 经验")]
        private void Sub10() { RemoveExperience(10); }
        [ContextMenu("升到下一级(精确)")]
        private void LevelUpExactly()
        {
            int req = GetRequiredExpForLevel(currentLevel);
            AddExperience(Mathf.Max(0, req - currentExp));
        }
    }
}


