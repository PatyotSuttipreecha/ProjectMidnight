using UnityEngine;

// Shared by all Lost attackers hitting this player. Never locks gameplay or camera input.
[DisallowMultipleComponent]
public class EnemyPlayerEffects : MonoBehaviour
{
    private PlayerController player;
    private float protectedUntil, restraintUntil, restraintMultiplier = 1f;
    private float burnUntil, burnStarted, burnDps;
    private GameObject burnVisual;
    public bool IsBurning => Time.time < burnUntil;
    public bool CanReceiveAttack => player != null && player.isActiveAndEnabled && player.currentHealth > 0 &&
        !player.IsCutsceneInputLocked && Time.time >= protectedUntil;
    public float MovementMultiplier => Time.time < restraintUntil ? restraintMultiplier : 1f;
    public static EnemyPlayerEffects For(PlayerController target)
    {
        if (target == null) return null;
        var result = target.GetComponent<EnemyPlayerEffects>();
        if (result == null) result = target.gameObject.AddComponent<EnemyPlayerEffects>();
        result.player = target;
        return result;
    }
    private void Awake() => player = GetComponent<PlayerController>();
    public bool TryHit(float damage, float protection, float restraintDuration = 0, float speedMultiplier = 1)
    {
        if (!CanReceiveAttack) return false;
        player.TakeDamage(Mathf.Max(0, damage));
        protectedUntil = Time.time + Mathf.Max(0, protection);
        // Do not extend a restraint while it is active. Protection is at least as long as restraint.
        if (restraintDuration > 0 && Time.time >= restraintUntil)
        {
            restraintUntil = Time.time + restraintDuration;
            restraintMultiplier = Mathf.Clamp(speedMultiplier, .25f, 1f);
            protectedUntil = Mathf.Max(protectedUntil, restraintUntil);
        }
        return true;
    }
    public void Ignite(float duration, float damagePerSecond, GameObject visualPrefab)
    {
        if (duration <= 0 || damagePerSecond <= 0 || player == null || player.currentHealth <= 0) return;
        if (!IsBurning) burnStarted = Time.time;
        burnUntil = Time.time + duration;
        burnDps = Mathf.Max(0, damagePerSecond);
        if (burnVisual == null && visualPrefab != null)
            burnVisual = Instantiate(visualPrefab, transform.position + Vector3.up, Quaternion.identity, transform);
    }
    private void Update()
    {
        if (player == null || !player.isActiveAndEnabled || player.currentHealth <= 0 || player.IsCutsceneInputLocked)
        { Clear(); return; }
        // Integrate only the part of this frame covered by the effect's expiry.
        float burningStep = Mathf.Max(0, Mathf.Min(burnUntil, Time.time) - Mathf.Max(burnStarted, Time.time - Time.deltaTime));
        if (burningStep > 0) player.TakeDamage(burnDps * burningStep);
        if (!IsBurning && burnVisual != null) { Destroy(burnVisual); burnVisual = null; }
    }
    private void Clear()
    {
        protectedUntil = restraintUntil = burnUntil = 0;
        restraintMultiplier = 1;
        if (burnVisual != null) Destroy(burnVisual);
        burnVisual = null;
    }
    private void OnDisable() => Clear();
}
