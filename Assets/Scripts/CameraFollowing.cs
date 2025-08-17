using UnityEngine;

public class CameraFollowing : MonoBehaviour
{
    public Transform target;       // 跟随的目标（比如 Player）
    public float smoothTime = 0.2f; // 越小跟随越快，越大越慢（0.15~0.3 比较自然）

    private Vector3 offset;        // 初始相对位置
    private Vector3 velocity = Vector3.zero; // SmoothDamp 的速度缓存

    void Start()
    {
        if (target == null)
        {
            // 自动寻找 Player
            GameObject playerObj = GameObject.Find("Player");
            if (playerObj != null)
                target = playerObj.transform;
        }

        if (target != null)
        {
            offset = transform.position - target.position;
        }
        else
        {
            Debug.LogError("CameraFollowing: 没有找到 Player，请手动设置 target");
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 目标位置 = 玩家位置 + 初始偏移
        Vector3 targetPosition = target.position + offset;

        // 平滑移动摄像机
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }
}