using UnityEngine;
using System.Collections.Generic;

namespace MyGame.UI
{
    // 挂载在GameManager空物体上
    // 记录哪些按钮属于战斗中九宫格按键，这个代码可以用tag代替
    public class NineButtonsManager : MonoBehaviour
    {
        [Header("战斗中九宫格按键管理")]
        public List<NineButtons> buttons = new List<NineButtons>();    // 所有战斗用九宫格按键

        private void Start()
        {
            if (buttons.Count == 0)
                Debug.LogWarning("[NineButtonsManager] 未分配任何战斗用按键");
        }
    }
}