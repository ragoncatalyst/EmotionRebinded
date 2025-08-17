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
            openButton.onClick.AddListener(OpenPanel);
            Debug.Log("[UpgradePanelController] openButton 已绑定 OpenPanel 方法");
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

        // 直接显示选项，不使用动画
        foreach (var opt in options)
        {
            if (opt.rect != null)
            {
                // 直接设置到目标位置
                opt.rect.anchoredPosition = opt.targetAnchoredPos;
                Debug.Log($"[UpgradePanelController] 选项位置设置为: {opt.rect.anchoredPosition}");
            }
        }
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
        // 启动选项滑出动画
        StartCoroutine(AnimateOptionsOut(skillId));
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
}