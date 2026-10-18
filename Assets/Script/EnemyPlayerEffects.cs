using UnityEngine;

// Shared by all Lost attackers hitting this player. PlayerController presents confirmed charge holds.
[DisallowMultipleComponent]
public class EnemyPlayerEffects : MonoBehaviour
{
    private PlayerController player;
    private float protectedUntil, restraintUntil, restraintMultiplier = 1f;
    private float chargeLockUntil, chargeGraceUntil;
    public UnityEngine.Events.UnityEvent onChargeLockStarted = new UnityEngine.Events.UnityEvent();
    public UnityEngine.Events.UnityEvent onChargeLockEnded = new UnityEngine.Events.UnityEvent();
    private bool chargeLockPresented;
    public bool IsChargeLocked => Time.time < chargeLockUntil;
    public bool BlocksGroupAttack => IsChargeLocked || Time.time < chargeGraceUntil;
    // Finite fallback prevents an event which never completes from freezing combat.
    public void BeginChargeLock(float duration)
    {
        if (duration <= 0) return;
        chargeLockUntil = Time.time + duration;
        if (!chargeLockPresented) { chargeLockPresented = true; onChargeLockStarted.Invoke(); }
        chargeGraceUntil = chargeLockUntil + (EnemyCombatSettings.Shared != null ? Mathf.Max(0, EnemyCombatSettings.Shared.chargePostHoldGrace) : 1f);
    }
    public void EndChargeLock()
    {
        chargeLockUntil = 0;
        FinishChargePresentation();
        restraintUntil = 0;
        chargeGraceUntil = Time.time + (EnemyCombatSettings.Shared != null ? Mathf.Max(0, EnemyCombatSettings.Shared.chargePostHoldGrace) : 1f);
    }
    private float burnUntil, burnStarted, burnDps;
    private GameObject burnVisual;
    public bool IsBurning => Time.time < burnUntil;
    public float ProtectionRemaining => Mathf.Max(0, protectedUntil - Time.time);
    public string LastHitResult { get; private set; } = "No hit attempt";
    public string BlockReason => player == null ? "Missing player" : !player.isActiveAndEnabled || player.currentHealth <= 0 ? "Player dead / disabled" :
        player.IsCutsceneInputLocked ? "Cutscene protection" : BlocksGroupAttack ? "Charge lock / recovery grace" : Time.time < protectedUntil ? "Hit protection: " + ProtectionRemaining.ToString("0.00") + "s" : "Ready";
    public bool CanReceiveAttack => player != null && player.isActiveAndEnabled && player.currentHealth > 0 &&
        !player.IsCutsceneInputLocked && !BlocksGroupAttack && Time.time >= protectedUntil;
    public float MovementMultiplier => IsChargeLocked && (EnemyCombatSettings.Shared == null || EnemyCombatSettings.Shared.chargeLockMovement) ? 0 : Time.time < restraintUntil ? restraintMultiplier : 1f;
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
        if (!CanReceiveAttack) { LastHitResult = BlockReason; return false; }
        if (!player.TryTakeDamage(damage, "Enemy hit")) { LastHitResult = player.LastDamageResult; return false; }
        LastHitResult = player.LastDamageResult;
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
        if (chargeLockPresented && !IsChargeLocked) FinishChargePresentation();
        if (player == null || !player.isActiveAndEnabled || player.currentHealth <= 0 || player.IsCutsceneInputLocked)
        { Clear(); return; }
        // Integrate only the part of this frame covered by the effect's expiry.
        float burningStep = Mathf.Max(0, Mathf.Min(burnUntil, Time.time) - Mathf.Max(burnStarted, Time.time - Time.deltaTime));
        if (burningStep > 0) player.TryTakeDamage(burnDps * burningStep, "Burn");
        if (!IsBurning && burnVisual != null) { Destroy(burnVisual); burnVisual = null; }
    }
    private void Clear()
    {
        FinishChargePresentation();
        chargeLockUntil = chargeGraceUntil = 0;
        protectedUntil = restraintUntil = burnUntil = 0;
        restraintMultiplier = 1;
        if (burnVisual != null) Destroy(burnVisual);
        burnVisual = null;
    }
    private void OnDisable() => Clear();
    private void FinishChargePresentation()
    {
        if (!chargeLockPresented) return;
        chargeLockPresented = false;
        onChargeLockEnded.Invoke();
    }
}
