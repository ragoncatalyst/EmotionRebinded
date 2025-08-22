using UnityEngine;

public class PiercingSegment : MonoBehaviour
{
    public PiercingShot parent;
    public float damagePerSecond = 10f;

    private void OnTriggerStay2D(Collider2D other)
    {
        var e = other.GetComponent<Enemy>();
        if (e != null && e.IsTargetable)
        {
            e.TakeDamage(damagePerSecond * Time.deltaTime);
        }
    }
}


