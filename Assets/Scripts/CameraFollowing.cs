using UnityEngine;

public class CameraFollowing : MonoBehaviour
{
    public Transform target;       // 跟随的目标（比如 Player）
    public float smoothTime = 0.1f; // 越小跟随越快，越大越慢（0.05~0.15 更同步）
    public Vector3 offsetMultiplier = Vector3.one; // 偏移倍数（可在Inspector中调整）
    public float lookAheadDistance = 1f; // 前瞻距离，避免玩家靠近屏幕边缘（可在Inspector中调整）
    public float lookAheadSmoothTime = 0.2f; // 前瞻平滑时间（可在Inspector中调整）

    private Vector3 baseOffset;    // 基础相对位置
    private Vector3 velocity = Vector3.zero; // SmoothDamp 的速度缓存
    private Vector3 lastTargetPosition; // 上一帧目标位置
    private Vector3 lookAheadVelocity = Vector3.zero; // 前瞻速度缓存

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
            baseOffset = transform.position - target.position;
            lastTargetPosition = target.position;
        }
        else
        {
            Debug.LogError("CameraFollowing: 没有找到 Player，请手动设置 target");
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 计算移动速度
        Vector3 targetVelocity = (target.position - lastTargetPosition) / Time.deltaTime;
        lastTargetPosition = target.position;

        // 计算前瞻偏移，让摄像机提前移动到玩家要去的方向
        Vector3 lookAheadOffset = Vector3.SmoothDamp(Vector3.zero, targetVelocity.normalized * lookAheadDistance, ref lookAheadVelocity, lookAheadSmoothTime);

        // 计算实际偏移 = 基础偏移 × 偏移倍数
        Vector3 actualOffset = Vector3.Scale(baseOffset, offsetMultiplier);
        
        // 目标位置 = 玩家位置 + 实际偏移 + 前瞻偏移
        Vector3 targetPosition = target.position + actualOffset + lookAheadOffset;

        // 平滑移动摄像机
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }
}