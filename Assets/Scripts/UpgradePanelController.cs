using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

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

    [Header("面板架构 - 按钮")]
    public Button openButton;                   // 打开面板的按钮
    public Button closeButton;                  // 关闭面板的按钮

    [Header("面板架构 - 选项")]
    public ChoiceOption[] options;              // 三个选项（选项1、选项2、选项3）

    [Header("面板架构 - 抉择用UI按键合集")]
    public Transform gridParent;                // 抉择用UI按键合集（空母体）
    public GridSelectBinder gridBinder;        // 网格绑定器

    private bool isPanelOpen;

    void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (openButton != null) openButton.onClick.AddListener(OpenPanel);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);

        foreach (var opt in options)
        {
            if (opt.button != null)
            {
                string captured = opt.skillId;
                opt.button.onClick.AddListener(() => OnChoiceSelected(captured));
                if (opt.skillIdLabel != null) opt.skillIdLabel.text = captured;
            }
        }

        // ⭐ 初始隐藏抉择九宫格
        if (gridParent != null)
            gridParent.gameObject.SetActive(false);
    }

    public void OpenPanel()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);
        isPanelOpen = true;

        // ⭐ 打开时隐藏抉择九宫格
        if (gridParent != null) gridParent.gameObject.SetActive(false);

        // ⭐ 打开时停止所有敌人
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (var e in enemies) e.SetCanMove(false);

        if (characterImage != null)
            StartCoroutine(AnimateCharacterIn());

        foreach (var opt in options)
            if (opt.rect != null) StartCoroutine(AnimateOptionIn(opt));
    }

    public void ClosePanel()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(false);
        isPanelOpen = false;

        // ⭐ 关闭时恢复敌人移动
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (var e in enemies) e.SetCanMove(true);
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

        float duration = 1f, t = 0f;
        while (t < duration)
        {
            float eval = opt.accelCurve.Evaluate(t / duration);
            rect.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eval);
            t += Time.deltaTime;
            yield return null;
        }
        rect.anchoredPosition = endPos;
    }

    private void OnChoiceSelected(string skillId)
    {
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
}