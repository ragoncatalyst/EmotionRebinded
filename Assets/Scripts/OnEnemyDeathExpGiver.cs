using UnityEngine;

/// <summary>
/// Attach to each enemy to grant EXP to the player when this enemy is destroyed.
/// Uses OnDestroy to ensure it fires even if no coroutines can complete.
/// </summary>
public class OnEnemyDeathExpGiver : MonoBehaviour
{
    public int expAmount = 10;

    private bool rewarded = false;

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        if (rewarded) return;
        var enemy = GetComponent<Enemy>();
        if (enemy != null && !enemy.grantExpOnDestroy) return; // 静默回收：不计入经验
        var bar = Object.FindObjectOfType<MyGame.UI.ExperienceBar>();
        if (bar != null)
        {
            rewarded = true;
            bar.AddExperience(expAmount);
        }
    }
}


