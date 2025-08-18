using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

// 强制Unity重新编译此脚本

public class UpgradePanelController : MonoBehaviour
{
    [System.Serializable]
    public class ChoiceOption
    {
        [Header("技能选项配置")]
        public string skillId = "01";           // 选项的技能编号（01、02、04等）
        public RectTransform rect;              // 选项Image的RectTransform
        public Button button;                   // 选项按钮
        public TMP_Text skillIdLabel;           // 显示技能编号的Textmeshpro
        public Vector2 targetAnchoredPos;       // 选项的目标位置
        public float rightOffset = 600f;       // 从右往左滑入的起始偏移
        public AnimationCurve accelCurve;       // 选项滑入的变速度曲线
    }

    [Header("面板架构 - 基本组件")]
    public GameObject panelRoot;                // panel根物体
    public Image characterImage;                // 角色立绘Image
    public RectTransform characterTargetPos;    // 立绘的目标位置
    
    [Header("面板架构 - 动画配置")]
    public float characterDuration = 1.2f;      // 立绘滑出时长
    public AnimationCurve characterCurve;       // 立绘从下往上变速度滑出曲线
    public float optionAnimationDuration = 1f;  // 选项滑出动画时长
    public float optionSlideOutDuration = 0.8f; // 选项滑出屏幕时长

    [Header("面板架构 - 按钮")]
    public Button openButton;                   // 打开面板的按钮
    public Button closeButton;                  // 关闭面板的按钮

    [Header("面板架构 - 选项")]
    public ChoiceOption[] options;              // 三个选项（选项1、选项2、选项3）

    [Header("面板架构 - 抉择用UI按键合集")]
    public Transform gridParent;                // 抉择用UI按键合集（空母体）
    
    [Header("战斗按键控制")]
    public bool disableBattleButtonsWhenOpen = true; // 打开面板时禁用战斗按键

    [Header("升级动画配置")]
    public Animator upgradeAnimator;                 // 升级动画控制器（通常是Player的Animator）
    public string upgradeAnimationTrigger = "PlayUpgrade"; // 升级动画触发器名称
    public string upgradeAnimationStateName = "Upgrade"; // 升级动画状态名称
    public bool playAnimationOnUpgrade = true;       // 是否在升级时播放动画
    public float animationDelay = 0.1f;             // 动画延迟时间（秒）
    public float postAnimationDelay = 0.2f;         // 动画完成后的延迟时间
    
    [Header("动画等待配置")]
    public bool useFixedWaitTime = false;           // 是否使用固定等待时间而不是检测动画完成
    public float fixedAnimationWaitTime = 2.0f;     // 固定等待时间（秒）
    
    [Header("面板显示延迟配置")]
    public float panelShowDelay = 0.5f;             // 动画结束后到面板显示的延迟时间（秒，可在Inspector中编辑）

    private bool isPanelOpen;

    void Start()
    {
        Debug.Log("[UpgradePanelController] Start 初始化开始");
        
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
            Debug.Log("[UpgradePanelController] panelRoot 已设置为隐藏");
        }
        else
        {
            Debug.LogError("[UpgradePanelController] panelRoot 为空！请在Inspector中设置");
        }

        if (openButton != null) 
        {
            // 先清除所有现有的监听器（包括在Inspector中设置的）
            openButton.onClick.RemoveAllListeners();
            // 然后添加我们的新方法
            openButton.onClick.AddListener(OnUpgradeButtonClicked);
            Debug.Log("[UpgradePanelController] openButton 已清除所有监听器并重新绑定 OnUpgradeButtonClicked 方法");
        }
        else
        {
            Debug.LogError("[UpgradePanelController] openButton 为空！请在Inspector中设置");
        }
        
        if (closeButton != null) 
        {
            closeButton.onClick.AddListener(ClosePanel);
            Debug.Log("[UpgradePanelController] closeButton 已绑定 ClosePanel 方法");
        }
        else
        {
            Debug.LogWarning("[UpgradePanelController] closeButton 为空");
        }

        // 设置三个选项对应的技能编号：01、02、04
        string[] skillIds = { "01", "02", "04" };
        
        for (int i = 0; i < options.Length && i < skillIds.Length; i++)
        {
            if (options[i].button != null)
            {
                // 设置技能编号
                options[i].skillId = skillIds[i];
                
                // 创建局部变量来避免闭包问题
                string currentSkillId = skillIds[i];
                int currentIndex = i;
                
                // 清除之前的监听器
                options[i].button.onClick.RemoveAllListeners();
                
                // 添加新的监听器，使用局部变量
                options[i].button.onClick.AddListener(() => {
                    Debug.Log($"[UpgradePanelController] 选项 {currentIndex} 被点击，技能编号: {currentSkillId}");
                    
                    // 添加按钮点击的视觉反馈
                    Button clickedButton = options[currentIndex].button;
                    if (clickedButton != null)
                    {
                        StartCoroutine(ButtonClickEffect(clickedButton));
                    }
                    
                    OnChoiceSelected(currentSkillId);
                });
                
                // 更新显示的技能编号文字
                if (options[i].skillIdLabel != null) 
                {
                    options[i].skillIdLabel.text = currentSkillId;
                }
                
                Debug.Log($"[UpgradePanelController] 选项 {i} 设置为技能编号: {currentSkillId}");
            }
        }

        // ⭐ 初始隐藏抉择九宫格
        if (gridParent != null)
            gridParent.gameObject.SetActive(false);

        // ⭐ 检查动画组件
        if (upgradeAnimator == null && playAnimationOnUpgrade)
        {
            Debug.LogWarning("[UpgradePanelController] upgradeAnimator 为空，但启用了升级动画。请在Inspector中设置或禁用动画。");
        }
        else if (upgradeAnimator != null)
        {
            Debug.Log($"[UpgradePanelController] 升级动画已配置: {upgradeAnimator.name}, 触发器: {upgradeAnimationTrigger}");
        }
    }

    /// <summary>
    /// 升级按钮点击处理：先播放动画，然后延迟显示面板
    /// </summary>
    public void OnUpgradeButtonClicked()
    {
        Debug.Log("[UpgradePanelController] 升级按钮被点击，开始升级动画序列");
        
        if (playAnimationOnUpgrade)
        {
            StartCoroutine(UpgradeButtonSequence());
        }
        else
        {
            // 如果不播放动画，直接显示面板
            OpenPanel();
        }
    }
    
    /// <summary>
    /// 升级按钮点击后的完整序列：播放动画 → 延迟 → 显示面板
    /// </summary>
    private IEnumerator UpgradeButtonSequence()
    {
        Debug.Log("[UpgradePanelController] ===== 开始升级按钮序列 =====");
        
        // 第1步：停止所有移动
        StopAllEnemies();
        StopPlayer();
        Debug.Log("[UpgradePanelController] 已停止所有移动");
        
        // 第2步：等待按钮效果
        if (animationDelay > 0)
        {
            Debug.Log($"[UpgradePanelController] 等待按钮效果 {animationDelay} 秒");
            yield return new WaitForSeconds(animationDelay);
        }
        
        // 第3步：播放升级动画
        if (upgradeAnimator != null)
        {
            Debug.Log("[UpgradePanelController] 触发升级动画");
            
            if (HasAnimatorTrigger(upgradeAnimator, upgradeAnimationTrigger))
            {
                upgradeAnimator.SetTrigger(upgradeAnimationTrigger);
                Debug.Log($"[UpgradePanelController] 动画触发成功: {upgradeAnimationTrigger}");
            }
            
            // 等待动画完成
            float waitTime = useFixedWaitTime ? fixedAnimationWaitTime : 2.0f;
            Debug.Log($"[UpgradePanelController] 开始等待动画完成，等待时间: {waitTime} 秒");
            
            float startTime = Time.time;
            yield return new WaitForSeconds(waitTime);
            float actualTime = Time.time - startTime;
            
            Debug.Log($"[UpgradePanelController] 动画等待完成！实际等待: {actualTime:F2} 秒");
        }
        else
        {
            Debug.LogWarning("[UpgradePanelController] 没有动画控制器，跳过动画");
        }
        
        // 第4步：动画结束后的延迟
        if (panelShowDelay > 0)
        {
            Debug.Log($"[UpgradePanelController] 动画结束后延迟 {panelShowDelay} 秒");
            yield return new WaitForSeconds(panelShowDelay);
        }
        
        // 第5步：现在显示面板
        Debug.Log("[UpgradePanelController] ===== 现在显示升级面板！ =====");
        OpenPanel();
        
        Debug.Log("[UpgradePanelController] ===== 升级按钮序列完全结束 =====");
    }

    public void OpenPanel()
    {
        Debug.Log("[UpgradePanelController] OpenPanel 被调用");
        
        if (panelRoot == null) 
        {
            Debug.LogError("[UpgradePanelController] panelRoot 为空！请在Inspector中设置panelRoot");
            return;
        }
        
        Debug.Log("[UpgradePanelController] 正在激活面板...");
        panelRoot.SetActive(true);
        isPanelOpen = true;

        // 检查面板内的子对象状态
        Debug.Log($"[UpgradePanelController] 面板子对象数量: {panelRoot.transform.childCount}");
        for (int i = 0; i < panelRoot.transform.childCount; i++)
        {
            Transform child = panelRoot.transform.GetChild(i);
            Debug.Log($"[UpgradePanelController] 子对象 {i}: {child.name}, 激活状态: {child.gameObject.activeSelf}");
        }

        // 检查角色立绘状态
        if (characterImage != null)
        {
            Debug.Log($"[UpgradePanelController] 角色立绘状态: 激活={characterImage.gameObject.activeSelf}, 透明度={characterImage.color.a}");
            Debug.Log($"[UpgradePanelController] 角色立绘Sprite: {(characterImage.sprite != null ? characterImage.sprite.name : "NULL")}");
            Debug.Log($"[UpgradePanelController] 角色立绘CanvasRenderer: {characterImage.canvasRenderer.GetAlpha()}");
            
            // 确保角色立绘可见
            characterImage.gameObject.SetActive(true);
            characterImage.enabled = true;
            
            // 设置颜色和透明度
            Color color = characterImage.color;
            color.a = 1f;
            characterImage.color = color;
            
            // 确保CanvasRenderer透明度正确
            characterImage.canvasRenderer.SetAlpha(1f);
            
            // 如果没有Sprite，只记录警告，不创建纹理
            if (characterImage.sprite == null)
            {
                Debug.LogWarning("[UpgradePanelController] 角色立绘没有Sprite，请在Inspector中分配");
            }
        }

        // 检查选项状态
        for (int i = 0; i < options.Length; i++)
        {
            var opt = options[i];
            if (opt.rect != null)
            {
                Debug.Log($"[UpgradePanelController] 选项 {i}: 激活={opt.rect.gameObject.activeSelf}");
                
                // 确保选项可见
                opt.rect.gameObject.SetActive(true);
                
                // 检查选项的Image组件
                Image optionImage = opt.rect.GetComponent<Image>();
                if (optionImage != null)
                {
                    Debug.Log($"[UpgradePanelController] 选项 {i} Image: Sprite={(optionImage.sprite != null ? optionImage.sprite.name : "NULL")}, 透明度={optionImage.color.a}");
                    
                    // 确保Image可见
                    optionImage.enabled = true;
                    Color color = optionImage.color;
                    color.a = 1f;
                    optionImage.color = color;
                    optionImage.canvasRenderer.SetAlpha(1f);
                    
                    // 如果没有Sprite，只记录警告
                    if (optionImage.sprite == null)
                    {
                        Debug.LogWarning($"[UpgradePanelController] 选项 {i} 没有Sprite，请在Inspector中分配");
                    }
                }
                
                // 检查选项的文字标签
                if (opt.skillIdLabel != null)
                {
                    opt.skillIdLabel.gameObject.SetActive(true);
                    opt.skillIdLabel.enabled = true;
                    Color textColor = opt.skillIdLabel.color;
                    textColor.a = 1f;
                    opt.skillIdLabel.color = textColor;
                    opt.skillIdLabel.canvasRenderer.SetAlpha(1f);
                    Debug.Log($"[UpgradePanelController] 选项 {i} 文字标签: {opt.skillIdLabel.text}");
                }
            }
        }

        // ⭐ 打开时隐藏抉择九宫格
        if (gridParent != null) gridParent.gameObject.SetActive(false);

        // ⭐ 打开时停止所有敌人
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (var e in enemies) e.SetCanMove(false);

        // ⭐ 禁用战斗按键
        if (disableBattleButtonsWhenOpen)
        {
            MyGame.UI.NineButtons[] battleButtons = FindObjectsOfType<MyGame.UI.NineButtons>();
            foreach (var btn in battleButtons)
            {
                btn.listenKeyboard = false;
            }
        }

        // ⭐ 启用选择按钮的键盘输入
        NineSelectionButtons.EnableAllKeyboardInput();

        // 先不执行动画，确保UI元素直接可见
        if (characterImage != null)
        {
            // 直接设置到目标位置，不使用动画
            RectTransform charRect = characterImage.rectTransform;
            if (characterTargetPos != null)
            {
                charRect.anchoredPosition = characterTargetPos.anchoredPosition;
                Debug.Log($"[UpgradePanelController] 角色立绘位置设置为: {charRect.anchoredPosition}");
            }
            else
            {
                // 如果没有目标位置，设置到屏幕中央
                charRect.anchoredPosition = Vector2.zero;
                Debug.Log("[UpgradePanelController] 角色立绘位置设置为屏幕中央");
            }
        }

        // 启动选项滑入动画（面板出现后选项再滑入）
        StartCoroutine(AnimateOptionsIn());
    }

    public void ClosePanel()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(false);
        isPanelOpen = false;

        // ⭐ 关闭时恢复敌人移动
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (var e in enemies) e.SetCanMove(true);

        // ⭐ 重新启用战斗按键
        if (disableBattleButtonsWhenOpen)
        {
            MyGame.UI.NineButtons[] battleButtons = FindObjectsOfType<MyGame.UI.NineButtons>();
            foreach (var btn in battleButtons)
            {
                if (btn.skillId != "00") // 只启用绑定了技能的按键
                {
                    btn.listenKeyboard = true;
                }
            }
        }

        // ⭐ 禁用选择按钮的键盘输入
        NineSelectionButtons.DisableAllKeyboardInput();
    }

    private IEnumerator AnimateCharacterIn()
    {
        RectTransform rect = characterImage.rectTransform;
        Vector2 startPos = new Vector2(rect.anchoredPosition.x, -Screen.height);
        Vector2 endPos = characterTargetPos.anchoredPosition;

        float t = 0f;
        while (t < characterDuration)
        {
            float eval = characterCurve.Evaluate(t / characterDuration);
            rect.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eval);
            t += Time.deltaTime;
            yield return null;
        }
        rect.anchoredPosition = endPos;
    }

    /// <summary>
    /// 所有选项滑入动画
    /// </summary>
    private IEnumerator AnimateOptionsIn()
    {
        Debug.Log("[UpgradePanelController] 开始选项滑入动画");
        
        // 先将所有选项设置到屏幕外的起始位置
        foreach (var opt in options)
        {
            if (opt.rect != null)
            {
                Vector2 startPos = new Vector2(opt.targetAnchoredPos.x + opt.rightOffset, opt.targetAnchoredPos.y);
                opt.rect.anchoredPosition = startPos;
            }
        }
        
        // 同时启动所有选项的滑入动画
        foreach (var opt in options)
        {
            if (opt.rect != null)
                StartCoroutine(AnimateOptionIn(opt));
        }
        
        // 等待动画完成
        yield return new WaitForSeconds(optionAnimationDuration);
        Debug.Log("[UpgradePanelController] 选项滑入动画完成");
    }

    private IEnumerator AnimateOptionIn(ChoiceOption opt)
    {
        RectTransform rect = opt.rect;
        Vector2 startPos = new Vector2(opt.targetAnchoredPos.x + opt.rightOffset, opt.targetAnchoredPos.y);
        Vector2 endPos = opt.targetAnchoredPos;

        float t = 0f;
        while (t < optionAnimationDuration)
        {
            float eval = opt.accelCurve.Evaluate(t / optionAnimationDuration);
            rect.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eval);
            t += Time.deltaTime;
            yield return null;
        }
        rect.anchoredPosition = endPos;
    }

    private void OnChoiceSelected(string skillId)
    {
        Debug.Log($"[UpgradePanelController] 选择了技能: {skillId}，开始选项滑出流程");
        
        // 停止所有正在进行的协程，确保没有冲突
        StopAllCoroutines();
        
        // 直接启动选项滑出动画，不再播放upgrade动画（因为已经在按钮点击时播放过了）
        StartCoroutine(AnimateOptionsOut(skillId));
    }

    /// <summary>
    /// 新的升级序列：简化版本，确保面板等待动画完成
    /// </summary>
    private IEnumerator NewUpgradeSequence(string skillId)
    {
        Debug.Log($"[UpgradePanelController] ===== 开始新升级序列 {skillId} =====");
        
        // 第1步：立即停止所有怪物和玩家
        StopAllEnemies();
        StopPlayer();
        Debug.Log("[UpgradePanelController] 已停止所有移动");
        
        // 第2步：等待按钮效果
        if (animationDelay > 0)
        {
            Debug.Log($"[UpgradePanelController] 等待按钮效果 {animationDelay} 秒");
            yield return new WaitForSeconds(animationDelay);
        }
        
        // 第3步：播放升级动画并强制等待
        if (upgradeAnimator != null)
        {
            Debug.Log("[UpgradePanelController] 触发升级动画");
            
            // 触发动画
            if (HasAnimatorTrigger(upgradeAnimator, upgradeAnimationTrigger))
            {
                upgradeAnimator.SetTrigger(upgradeAnimationTrigger);
                Debug.Log($"[UpgradePanelController] 动画触发成功: {upgradeAnimationTrigger}");
            }
            
            // 强制等待 - 使用最简单的方式
            float waitTime = useFixedWaitTime ? fixedAnimationWaitTime : 2.0f; // 默认2秒
            Debug.Log($"[UpgradePanelController] 开始等待动画完成，等待时间: {waitTime} 秒");
            
            float startTime = Time.time;
            yield return new WaitForSeconds(waitTime);
            float actualTime = Time.time - startTime;
            
            Debug.Log($"[UpgradePanelController] 动画等待完成！实际等待: {actualTime:F2} 秒");
        }
        else
        {
            Debug.LogWarning("[UpgradePanelController] 没有动画控制器，跳过动画");
        }
        
        // 第4步：额外安全延迟
        if (postAnimationDelay > 0)
        {
            Debug.Log($"[UpgradePanelController] 额外安全延迟 {postAnimationDelay} 秒");
            yield return new WaitForSeconds(postAnimationDelay);
        }
        
        // 第5步：动画完成后，显示九宫格选择界面
        Debug.Log("[UpgradePanelController] ===== 动画完成，现在显示九宫格选择界面！ =====");
        
        // 等待动画完成后，执行选项滑出并显示九宫格
        yield return StartCoroutine(AnimateOptionsOut(skillId));
        
        Debug.Log("[UpgradePanelController] ===== 升级序列完全结束 =====");
    }

    /// <summary>
    /// 完整的升级序列：停止所有 → 播放动画 → 停止玩家 → 显示面板
    /// </summary>
    private IEnumerator CompleteUpgradeSequence(string skillId)
    {
        Debug.Log($"[UpgradePanelController] ===== 开始完整升级序列，技能ID: {skillId} =====");
        float startTime = Time.time;
        
        // 步骤1: 立即停止所有怪物
        Debug.Log("[UpgradePanelController] 步骤1: 停止所有怪物");
        StopAllEnemies();
        
        // 步骤2: 延迟一小段时间，让按钮点击效果完成
        if (animationDelay > 0)
        {
            Debug.Log($"[UpgradePanelController] 步骤2: 等待按钮效果完成 ({animationDelay}秒)");
            yield return new WaitForSeconds(animationDelay);
            Debug.Log($"[UpgradePanelController] 步骤2完成，总耗时: {Time.time - startTime:F2}秒");
        }
        
        // 步骤3: 播放玩家升级动画并等待完成
        Debug.Log("[UpgradePanelController] 步骤3: 开始播放玩家升级动画并等待完成");
        float animationStartTime = Time.time;
        yield return StartCoroutine(PlayPlayerUpgradeAnimation());
        float animationEndTime = Time.time;
        Debug.Log($"[UpgradePanelController] 步骤3完成！动画实际耗时: {animationEndTime - animationStartTime:F2}秒");
        
        // 步骤4: 停止玩家移动
        Debug.Log("[UpgradePanelController] 步骤4: 停止玩家移动");
        StopPlayer();
        
        // 步骤5: 额外延迟，让场景安静一下
        if (postAnimationDelay > 0)
        {
            Debug.Log($"[UpgradePanelController] 步骤5: 场景安静延迟 ({postAnimationDelay}秒)");
            yield return new WaitForSeconds(postAnimationDelay);
            Debug.Log($"[UpgradePanelController] 步骤5完成，总耗时: {Time.time - startTime:F2}秒");
        }
        
        // 步骤6: 显示升级面板（选项滑出 → 九宫格显示）
        Debug.Log("[UpgradePanelController] 步骤6: 现在开始显示升级面板！");
        Debug.Log($"[UpgradePanelController] ===== 整个序列总耗时: {Time.time - startTime:F2}秒 =====");
        StartCoroutine(AnimateOptionsOut(skillId));
    }

    /// <summary>
    /// 播放玩家升级动画
    /// </summary>
    private IEnumerator PlayPlayerUpgradeAnimation()
    {
        Debug.Log("[UpgradePanelController] 开始播放玩家升级动画");
        
        // 触发动画
        if (upgradeAnimator != null)
        {
            // 检查动画控制器是否有指定的触发器
            if (HasAnimatorTrigger(upgradeAnimator, upgradeAnimationTrigger))
            {
                upgradeAnimator.SetTrigger(upgradeAnimationTrigger);
                Debug.Log($"[UpgradePanelController] 已触发升级动画: {upgradeAnimationTrigger}");
                
                // 根据配置选择等待方式
                if (useFixedWaitTime)
                {
                    Debug.Log($"[UpgradePanelController] 使用固定等待时间: {fixedAnimationWaitTime}秒");
                    yield return new WaitForSeconds(fixedAnimationWaitTime);
                    Debug.Log("[UpgradePanelController] 固定等待时间结束");
                }
                else
                {
                    Debug.Log("[UpgradePanelController] 使用动画检测等待");
                    yield return StartCoroutine(WaitForAnimationComplete(upgradeAnimationStateName));
                }
                
                Debug.Log("[UpgradePanelController] 玩家升级动画等待完成");
            }
            else
            {
                Debug.LogWarning($"[UpgradePanelController] 动画控制器中未找到触发器: {upgradeAnimationTrigger}");
            }
        }
        else
        {
            Debug.LogWarning("[UpgradePanelController] upgradeAnimator 为空，跳过动画播放");
        }
    }

    /// <summary>
    /// 选项滑出屏幕动画
    /// </summary>
    private IEnumerator AnimateOptionsOut(string skillId)
    {
        // 同时让所有选项滑出屏幕
        foreach (var opt in options)
        {
            if (opt.rect != null)
                StartCoroutine(AnimateOptionOut(opt));
        }

        // 等待滑出动画完成
        yield return new WaitForSeconds(optionSlideOutDuration);

        // 隐藏所有选项
        foreach (var opt in options)
            if (opt.rect != null) opt.rect.gameObject.SetActive(false);

        // 显示抉择用九宫格
        if (gridParent != null) gridParent.gameObject.SetActive(true);
        
        // ⭐ 强制更新所有状态指示器，确保颜色正确显示
        Debug.Log("[UpgradePanelController] 九宫格显示后，强制更新状态指示器");
        SlotOccupancyIndicator.UpdateAllSlotIndicators();
        
        // 通知所有 GridSelectBinder 开始绑定流程
        GridSelectBinder[] allBinders = FindObjectsOfType<GridSelectBinder>();
        foreach (var binder in allBinders)
        {
            binder.BeginSelection(skillId);
        }
    }

    /// <summary>
    /// 单个选项滑出动画
    /// </summary>
    private IEnumerator AnimateOptionOut(ChoiceOption opt)
    {
        RectTransform rect = opt.rect;
        Vector2 startPos = rect.anchoredPosition;
        Vector2 endPos = new Vector2(-opt.rightOffset, startPos.y); // 滑出到屏幕左侧

        float t = 0f;
        while (t < optionSlideOutDuration)
        {
            float eval = opt.accelCurve.Evaluate(t / optionSlideOutDuration);
            rect.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eval);
            t += Time.deltaTime;
            yield return null;
        }
        rect.anchoredPosition = endPos;
    }

    /// <summary>
    /// 测试方法：可以通过Inspector或代码直接调用来测试面板开关
    /// </summary>
    [ContextMenu("测试打开面板")]
    public void TestOpenPanel()
    {
        Debug.Log("[UpgradePanelController] 测试打开面板");
        OpenPanel();
    }

    [ContextMenu("测试关闭面板")]
    public void TestClosePanel()
    {
        Debug.Log("[UpgradePanelController] 测试关闭面板");
        ClosePanel();
    }

    /// <summary>
    /// 强制显示面板所有元素
    /// </summary>
    [ContextMenu("强制显示面板元素")]
    public void ForceShowPanelElements()
    {
        Debug.Log("[UpgradePanelController] 强制显示面板元素");
        
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            
            // 检查Canvas层级和设置
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            Debug.Log($"[UpgradePanelController] 场景中共有 {allCanvases.Length} 个Canvas");
            
            Canvas panelCanvas = panelRoot.GetComponentInParent<Canvas>();
            if (panelCanvas != null)
            {
                Debug.Log($"[UpgradePanelController] 面板Canvas: {panelCanvas.name}, 排序层: {panelCanvas.sortingOrder}, 模式: {panelCanvas.renderMode}");
                
                // 确保Canvas在最前面
                panelCanvas.sortingOrder = 100;
                panelCanvas.overrideSorting = true;
                
                Debug.Log($"[UpgradePanelController] 已设置Canvas排序层为100");
            }
            else
            {
                Debug.LogError("[UpgradePanelController] 面板没有Canvas父对象！");
            }
            
            // 强制显示角色立绘
            if (characterImage != null)
            {
                characterImage.gameObject.SetActive(true);
                characterImage.enabled = true;
                Color color = characterImage.color;
                color.a = 1f;
                characterImage.color = color;
                characterImage.canvasRenderer.SetAlpha(1f);
                
                Debug.Log($"[UpgradePanelController] 角色立绘已强制显示: {characterImage.name}");
                Debug.Log($"[UpgradePanelController] 角色立绘Sprite: {(characterImage.sprite != null ? "有" : "无")}");
                
                // 如果没有Sprite，只记录警告
                if (characterImage.sprite == null)
                {
                    Debug.LogWarning("[UpgradePanelController] 角色立绘没有Sprite，请在Inspector中分配");
                }
            }
            
            // 强制显示所有选项
            foreach (var opt in options)
            {
                if (opt.rect != null)
                {
                    opt.rect.gameObject.SetActive(true);
                    
                    // 重置位置到目标位置
                    opt.rect.anchoredPosition = opt.targetAnchoredPos;
                    
                    Debug.Log($"[UpgradePanelController] 选项已强制显示: {opt.rect.name}");
                }
            }
            
            // 检查Canvas组件
            Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Debug.Log($"[UpgradePanelController] Canvas状态: 激活={canvas.enabled}, 排序层={canvas.sortingOrder}");
            }
            else
            {
                Debug.LogWarning("[UpgradePanelController] 未找到Canvas组件！");
            }
        }
    }

    /// <summary>
    /// 按钮点击视觉效果
    /// </summary>
    private IEnumerator ButtonClickEffect(Button button)
    {
        if (button == null) yield break;
        
        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage == null) yield break;
        
        // 保存原始颜色
        Color originalColor = buttonImage.color;
        
        // 应用tinted效果
        Color tintedColor = originalColor * 0.7f; // 变暗
        tintedColor.a = originalColor.a; // 保持透明度
        buttonImage.color = tintedColor;
        
        // 等待短暂时间
        yield return new WaitForSeconds(0.1f);
        
        // 恢复原始颜色
        buttonImage.color = originalColor;
        
        Debug.Log("[UpgradePanelController] 按钮tinted效果已应用");
    }

    /// <summary>
    /// 停止所有怪物移动
    /// </summary>
    private void StopAllEnemies()
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (var enemy in enemies)
        {
            enemy.SetCanMove(false);
        }
        Debug.Log($"[UpgradePanelController] 已停止 {enemies.Length} 个怪物的移动");
    }

    /// <summary>
    /// 停止玩家移动
    /// </summary>
    private void StopPlayer()
    {
        // 禁用战斗按键，阻止玩家移动
        if (disableBattleButtonsWhenOpen)
        {
            MyGame.UI.NineButtons[] battleButtons = FindObjectsOfType<MyGame.UI.NineButtons>();
            foreach (var btn in battleButtons)
            {
                btn.listenKeyboard = false;
            }
            Debug.Log($"[UpgradePanelController] 已禁用 {battleButtons.Length} 个战斗按键");
        }
        
        // 如果有PlayerController，也可以直接停止
        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            // 这里可以添加PlayerController的停止逻辑，如果需要的话
            Debug.Log("[UpgradePanelController] 已通知PlayerController停止移动");
        }
    }

    /// <summary>
    /// 检查动画控制器是否有指定的触发器
    /// </summary>
    private bool HasAnimatorTrigger(Animator animator, string triggerName)
    {
        if (animator == null)
        {
            Debug.LogWarning($"[UpgradePanelController] Animator为空！");
            return false;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[UpgradePanelController] Animator Controller为空！请检查{animator.gameObject.name}的Animator组件");
            return false;
        }

        Debug.Log($"[UpgradePanelController] 检查触发器 '{triggerName}' 在 {animator.gameObject.name}");
        
        foreach (var parameter in animator.parameters)
        {
            if (parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                Debug.Log($"[UpgradePanelController] ✅ 找到触发器: {triggerName}");
                return true;
            }
        }
        
        Debug.LogWarning($"[UpgradePanelController] 动画控制器中未找到触发器: {triggerName}");
        Debug.Log("[UpgradePanelController] 可用参数列表:");
        foreach (var parameter in animator.parameters)
        {
            Debug.Log($"  - {parameter.name} ({parameter.type})");
        }
        return false;
    }

    /// <summary>
    /// 等待指定动画播放完成
    /// </summary>
    private IEnumerator WaitForAnimationComplete(string animationName)
    {
        if (upgradeAnimator == null) 
        {
            Debug.LogWarning("[UpgradePanelController] upgradeAnimator为空，跳过动画等待");
            yield break;
        }

        Debug.Log($"[UpgradePanelController] 开始等待动画完成: {animationName}");

        // 等待几帧，确保动画开始播放
        for (int i = 0; i < 3; i++)
        {
            yield return null;
        }

        // 检查是否成功进入目标动画状态
        AnimatorStateInfo initialState = upgradeAnimator.GetCurrentAnimatorStateInfo(0);
        Debug.Log($"[UpgradePanelController] 当前动画状态: {initialState.shortNameHash}, 是否为目标动画: {initialState.IsName(animationName)}");

        if (!initialState.IsName(animationName))
        {
            Debug.LogWarning($"[UpgradePanelController] 未能进入目标动画状态 {animationName}，当前状态信息:");
            Debug.LogWarning($"  - 状态哈希: {initialState.shortNameHash}");
            Debug.LogWarning($"  - 标准化时间: {initialState.normalizedTime}");
            
            // 尝试等待一段时间看是否会进入目标状态
            float waitTime = 0f;
            while (waitTime < 1f && !upgradeAnimator.GetCurrentAnimatorStateInfo(0).IsName(animationName))
            {
                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
                AnimatorStateInfo currentState = upgradeAnimator.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"[UpgradePanelController] 等待进入动画状态... 当前: {currentState.shortNameHash}");
            }
        }

        // 等待动画播放完成
        AnimatorStateInfo stateInfo;
        int frameCount = 0;
        do
        {
            yield return null;
            frameCount++;
            stateInfo = upgradeAnimator.GetCurrentAnimatorStateInfo(0);
            
            // 每30帧输出一次调试信息（约0.5秒）
            if (frameCount % 30 == 0)
            {
                Debug.Log($"[UpgradePanelController] 动画播放中... 进度: {stateInfo.normalizedTime:F2}, 状态: {stateInfo.IsName(animationName)}");
            }
        }
        while (stateInfo.IsName(animationName) && stateInfo.normalizedTime < 1.0f);

        Debug.Log($"[UpgradePanelController] 动画播放完成! 最终进度: {stateInfo.normalizedTime:F2}");

        // 额外等待一小段时间，确保动画完全结束和转换完成
        yield return new WaitForSeconds(0.2f);
        
        Debug.Log("[UpgradePanelController] 动画等待完全结束");
    }

    /// <summary>
    /// 手动触发新升级序列（用于测试）
    /// </summary>
    [ContextMenu("测试新升级序列")]
    public void TestNewUpgradeSequence()
    {
        Debug.Log("[UpgradePanelController] 手动测试新升级序列");
        StopAllCoroutines(); // 确保清理
        StartCoroutine(NewUpgradeSequence("TEST"));
    }

    /// <summary>
    /// 检查升级按钮绑定状态
    /// </summary>
    [ContextMenu("检查升级按钮绑定")]
    public void CheckUpgradeButtonBinding()
    {
        if (openButton != null)
        {
            Debug.Log($"[UpgradePanelController] 升级按钮存在: {openButton.name}");
            Debug.Log($"[UpgradePanelController] 按钮是否可交互: {openButton.interactable}");
            Debug.Log($"[UpgradePanelController] 按钮监听器数量: {openButton.onClick.GetPersistentEventCount()}");
            
            // 手动触发我们的方法来测试
            Debug.Log("[UpgradePanelController] 手动调用 OnUpgradeButtonClicked()");
            OnUpgradeButtonClicked();
        }
        else
        {
            Debug.LogError("[UpgradePanelController] openButton 为空！");
        }
    }

    /// <summary>
    /// 测试升级按钮序列：模拟点击升级按钮的完整流程
    /// </summary>
    [ContextMenu("测试升级按钮序列")]
    public void TestUpgradeButtonSequence()
    {
        Debug.Log("[UpgradePanelController] 手动测试升级按钮序列");
        OnUpgradeButtonClicked();
    }

    /// <summary>
    /// 测试完整升级流程：打开面板 → 选择技能 → 播放动画 → 显示九宫格
    /// </summary>
    [ContextMenu("测试完整升级流程")]
    public void TestCompleteUpgradeFlow()
    {
        Debug.Log("[UpgradePanelController] ===== 开始测试完整升级流程 =====");
        StartCoroutine(TestCompleteUpgradeFlowCoroutine());
    }

    private IEnumerator TestCompleteUpgradeFlowCoroutine()
    {
        // 1. 先打开面板显示选项
        Debug.Log("[测试] 第1步：打开升级面板");
        OpenPanel();
        
        // 2. 等待2秒模拟用户思考
        Debug.Log("[测试] 等待2秒模拟用户选择...");
        yield return new WaitForSeconds(2f);
        
        // 3. 模拟选择技能01
        Debug.Log("[测试] 第2步：模拟选择技能01，开始升级动画序列");
        OnChoiceSelected("01");
        
        Debug.Log("[测试] ===== 完整升级流程测试启动完成 =====");
    }

    /// <summary>
    /// 手动触发完整升级序列（用于测试）
    /// </summary>
    [ContextMenu("测试完整升级序列")]
    public void TestCompleteUpgradeSequence()
    {
        Debug.Log("[UpgradePanelController] 手动测试完整升级序列");
        if (playAnimationOnUpgrade && upgradeAnimator != null)
        {
            StartCoroutine(CompleteUpgradeSequence("TEST"));
        }
        else
        {
            Debug.LogWarning("[UpgradePanelController] 升级动画未启用或动画控制器为空");
        }
    }

    /// <summary>
    /// 手动触发玩家升级动画（用于测试）
    /// </summary>
    [ContextMenu("测试玩家升级动画")]
    public void TestPlayerUpgradeAnimation()
    {
        Debug.Log("[UpgradePanelController] 手动测试玩家升级动画");
        if (upgradeAnimator != null)
        {
            StartCoroutine(PlayPlayerUpgradeAnimation());
        }
        else
        {
            Debug.LogWarning("[UpgradePanelController] 动画控制器为空");
        }
    }

    /// <summary>
    /// 测试固定时间等待（用于调试）
    /// </summary>
    [ContextMenu("测试固定时间等待")]
    public void TestFixedTimeWait()
    {
        Debug.Log("[UpgradePanelController] 测试固定时间等待");
        StartCoroutine(TestFixedTimeWaitCoroutine());
    }

    private IEnumerator TestFixedTimeWaitCoroutine()
    {
        Debug.Log($"[UpgradePanelController] 开始等待 {fixedAnimationWaitTime} 秒...");
        float startTime = Time.time;
        yield return new WaitForSeconds(fixedAnimationWaitTime);
        float endTime = Time.time;
        Debug.Log($"[UpgradePanelController] 等待完成！实际耗时: {endTime - startTime:F2} 秒");
    }

    /// <summary>
    /// 检查动画配置状态
    /// </summary>
    [ContextMenu("检查动画配置")]
    public void CheckAnimationConfig()
    {
        Debug.Log("[UpgradePanelController] 动画配置检查:");
        Debug.Log($"  - 启用升级动画: {playAnimationOnUpgrade}");
        Debug.Log($"  - 动画控制器: {(upgradeAnimator != null ? upgradeAnimator.name : "未设置")}");
        Debug.Log($"  - 触发器名称: {upgradeAnimationTrigger}");
        Debug.Log($"  - 动画状态名称: {upgradeAnimationStateName}");
        Debug.Log($"  - 动画延迟: {animationDelay}秒");
        Debug.Log($"  - 动画后延迟: {postAnimationDelay}秒");

        if (upgradeAnimator != null)
        {
            Debug.Log($"  - 动画控制器状态: {(upgradeAnimator.runtimeAnimatorController != null ? "正常" : "缺少AnimatorController")}");
            
            if (upgradeAnimator.runtimeAnimatorController != null)
            {
                bool hasTrigger = HasAnimatorTrigger(upgradeAnimator, upgradeAnimationTrigger);
                Debug.Log($"  - 触发器 '{upgradeAnimationTrigger}' 存在: {hasTrigger}");
                
                Debug.Log("  - 所有参数:");
                foreach (var param in upgradeAnimator.parameters)
                {
                    Debug.Log($"    * {param.name} ({param.type})");
                }
            }
        }
        
        // 检查场景中的相关对象
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        Debug.Log($"  - 场景中怪物数量: {enemies.Length}");
        
        PlayerController player = FindObjectOfType<PlayerController>();
        Debug.Log($"  - 找到玩家控制器: {player != null}");
    }
}