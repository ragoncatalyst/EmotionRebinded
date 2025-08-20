using UnityEngine;

namespace MyGame.UI
{
    /// <summary>
    /// 调试按钮适配器：将 UI Button 的 OnClick 事件绑定到本脚本的 AddExp 方法即可。
    /// - target: 指向场景中的 ExperienceBar
    /// - amount: 增加的经验值（默认10，可在Inspector里修改）
    /// - autoFind: 若未手动指定 target，启动时自动在场景中查找一个 ExperienceBar
    /// </summary>
    [DisallowMultipleComponent]
    public class ExperienceDebugButton : MonoBehaviour
    {
        [Header("目标经验条")]
        [SerializeField] private ExperienceBar target;
        [SerializeField] private bool autoFind = true;

        [Header("调试参数")]
        [SerializeField] private int amount = 10; // 点击一次增加的经验值

        private void Awake()
        {
            if (autoFind && target == null)
            {
                target = FindObjectOfType<ExperienceBar>();
            }
        }

        /// <summary>
        /// 供 UI Button.onClick 调用：增加指定经验值
        /// </summary>
        public void AddExp()
        {
            if (target == null)
            {
                Debug.LogWarning("[ExperienceDebugButton] 未找到 ExperienceBar，无法增加经验。");
                return;
            }
            target.AddExperience(Mathf.Max(0, amount));
        }

        // 便捷测试
        [ContextMenu("点击一次（+amount）")]
        private void ClickOnce()
        {
            AddExp();
        }
    }
}


