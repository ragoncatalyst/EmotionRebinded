using System.Collections.Generic;
using UnityEngine;

public class PiercingShot : MonoBehaviour
{
    public float growSpeed = 30f;       // 伸长速度（更快）
    public float maxLength = 12f;       // 最大长度（由 PlayerController 动态赋值，可到很大）
    public float damage = 10f;          // 对每个敌人的总伤害（按逗留时间近似）
    public Transform origin;            // 玩家
    public float segmentLength = 0.5f;  // 每段长度
    public float thickness = 0.35f;     // 粗细
    public float startFadeDelay = 0.5f; // 段出现后多久开始淡出
    public float fadeInterval = 0.15f;  // 每次降低透明度的间隔
    public float fadeStep = 0.2f;       // 每次降低的alpha

    private struct Segment
    {
        public GameObject go;
        public SpriteRenderer sr;
        public BoxCollider2D box;
        public float born;
        public float alpha;
    }

    private readonly List<Segment> segments = new List<Segment>();
    private float currentLength = 0f;
    private Vector2 dir = Vector2.right;
    private bool growthFinished = false; // 达到最大长度后不再补段
    private Vector3 spawnPos;

    public void SetDirection(Vector2 d)
    {
        dir = d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
    }

    private void Start()
    {
        if (origin == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) origin = p.transform;
        }
        spawnPos = origin != null ? origin.position : transform.position;
        transform.position = spawnPos;
    }

    private void Update()
    {
        if (origin == null && spawnPos == Vector3.zero) { Destroy(gameObject); return; }

        // 固定在生成时的位置，不再跟随玩家
        transform.position = spawnPos;
        transform.right = dir;

        // 目标长度
        float targetLen = Mathf.Min(maxLength, currentLength + growSpeed * Time.deltaTime);
        if (!growthFinished)
        {
            // 按段生成，仅在未完成生长时补充
            while (segments.Count * segmentLength < targetLen)
            {
                AddSegment();
            }
            currentLength = targetLen;
            if (Mathf.Approximately(currentLength, maxLength)) growthFinished = true;
        }

        // 更新每段位置与渐隐
        float now = Time.time;
        for (int i = segments.Count - 1; i >= 0; i--)
        {
            var seg = segments[i];
            float centerDist = (i + 0.5f) * segmentLength;
            Vector3 center = spawnPos + (Vector3)(dir * centerDist);
            seg.go.transform.position = center;
            seg.go.transform.right = dir;
            // 渐隐：等生长完成后才开始；出现startFadeDelay后按间隔/步进降低alpha
            float age = now - seg.born;
            float alpha = 1f;
            if (growthFinished && age > startFadeDelay)
            {
                int steps = Mathf.Max(0, Mathf.FloorToInt((age - startFadeDelay) / fadeInterval) + 1);
                alpha = Mathf.Clamp01(1f - steps * fadeStep);
            }
            if (!Mathf.Approximately(alpha, seg.alpha))
            {
                var c = seg.sr.color; c.a = alpha; seg.sr.color = c;
                seg.alpha = alpha;
                segments[i] = seg;
            }
            if (alpha <= 0f)
            {
                Destroy(seg.go);
                segments.RemoveAt(i);
            }
        }

        // 若所有段都消失，销毁根
        if (segments.Count == 0 && growthFinished)
        {
            Destroy(gameObject);
        }
    }

    private void AddSegment()
    {
        GameObject seg = new GameObject("PS_Segment");
        seg.transform.SetParent(transform); // 附属于根，根销毁时一并清理
        var sr = seg.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), new Vector2(0.5f,0.5f), 1f);
        sr.color = new Color(1f,1f,1f,1f);
        seg.transform.localScale = new Vector3(segmentLength, thickness, 1f);
        var box = seg.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(segmentLength, thickness);
        var hit = seg.AddComponent<PiercingSegment>();
        hit.damagePerSecond = damage; // 平均分配：每秒造成damage（叠加段覆盖更快）
        hit.parent = this;

        Segment s = new Segment
        {
            go = seg,
            sr = sr,
            box = box,
            born = Time.time,
            alpha = 1f
        };
        segments.Add(s);
    }
}



