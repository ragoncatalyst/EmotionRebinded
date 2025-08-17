using UnityEngine;
using UnityEngine.UI;
using MyGame.UI;

public class SlotOccupancyIndicator : MonoBehaviour
{
    [Header("按键占用状态指示器")]
    public NineButtons targetSlot;              // 对应的抉择用按键
    public Image statusImage;                   // 按键占用状态圈Image

    [Header("状态颜色配置")]
    public Color freeColor = Color.green;       // 未绑定时显示绿色
    public Color occupiedColor = new Color(1f, 0.6f, 0.6f); // 已绑定时显示淡红色 

    void Update()
    {
        if (targetSlot == null || statusImage == null)
        {
            Debug.LogWarning("[SlotOccupancyIndicator] Inspector 引用未设置完整");
            return;
        }

        statusImage.color = (targetSlot.skillId == "00") ? freeColor : occupiedColor;
    }
}