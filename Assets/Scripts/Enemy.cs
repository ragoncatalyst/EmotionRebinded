using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Enemy : MonoBehaviour
{
    [Header("敌人AI配置")]
    public Transform target;                    // 追踪目标（玩家）
    public float moveSpeed = 2f;               // 移动速度
    public float detectionRange = 8f;          // 检测范围
    public float stopDistance = 1.5f;          // 停止距离（到一定距离后停下）

    private BoxCollider2D enemyCol;
    private bool canMove = true; // ⭐ 新增：是否允许移动

    void Start()
    {
        enemyCol = GetComponent<BoxCollider2D>();
    }

    void Update()
    {
        if (!canMove || target == null) return;

        float dist = Vector2.Distance(transform.position, target.position);
        if (dist <= detectionRange && dist > stopDistance)
        {
            Vector2 dir = (target.position - transform.position).normalized;
            transform.position += (Vector3)(dir * moveSpeed * Time.deltaTime);
        }
    }

    // ⭐ 外部接口：控制敌人能否移动
    public void SetCanMove(bool state)
    {
        canMove = state;
    }
}