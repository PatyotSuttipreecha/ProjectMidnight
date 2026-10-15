using UnityEngine;

// Optional adapter: old Animator Controllers retain their original clip playback.
public sealed class EnemyLocomotionAnimator
{
    private readonly Animator animator;
    private readonly EnemyDataSO profile;
    private RuntimeAnimatorController cachedController;
    private int locomotion, blend, horizontal, vertical, attacking, attackState;
    private bool compatible, hasHorizontal, hasVertical, hasAttackBool;
    public float MeasuredSpeed { get; private set; }
    public float TargetBlend { get; private set; }
    public float CurrentBlend => Compatible ? animator.GetFloat(blend) : 0;
    public Vector2 Direction { get; private set; }
    public string ResolvedSpeedParameter { get; private set; }
    public string LastAttackStateName { get; private set; }
    public static Vector2 ReferenceSpeeds(EnemyDataSO profile)
    {
        float walk = profile.autoAnimationReferenceSpeeds ? profile.walkSpeed : profile.animationWalkSpeed;
        float run = profile.autoAnimationReferenceSpeeds ? profile.chaseSpeed * Mathf.Max(1, profile.burstSpeedMultiplier) : profile.animationRunSpeed;
        return new Vector2(Mathf.Max(.01f, walk), Mathf.Max(Mathf.Max(.01f, walk) + .01f, run));
    }
    public static float BlendForMovement(float speed, EnemyDataSO profile)
    {
        if (speed <= Mathf.Max(0, profile.animationStopSpeed)) return 0;
        Vector2 references = ReferenceSpeeds(profile);
        return BlendForSpeed(profile.keepWalkBlendWhileMoving ? Mathf.Max(speed, references.x) : speed, references.x, references.y);
    }
    public EnemyLocomotionAnimator(Animator animator, EnemyDataSO profile)
    { this.animator = animator; this.profile = profile; }
    public bool Compatible
    {
        get
        {
            if (animator == null || profile == null || !profile.useLocomotionBlendTree || animator.runtimeAnimatorController == null) return false;
            if (cachedController != animator.runtimeAnimatorController) Refresh();
            return compatible;
        }
    }
    private bool Parameter(int hash, AnimatorControllerParameterType type)
    {
        foreach (var parameter in animator.parameters)
            if (parameter.nameHash == hash && parameter.type == type) return true;
        return false;
    }
    private static int Hash(string value) => string.IsNullOrWhiteSpace(value) ? 0 : Animator.StringToHash(value);
    private void Refresh()
    {
        cachedController = animator.runtimeAnimatorController;
        locomotion = Hash(profile.locomotionState);
        blend = Hash(profile.locomotionBlendParameter);
        ResolvedSpeedParameter = profile.locomotionBlendParameter;
        if (!Parameter(blend, AnimatorControllerParameterType.Float))
        {
            // Migration support for the project's old Blend and new Speed names.
            foreach (string alias in new[] { "Speed", "Blend" })
                if (Parameter(Hash(alias), AnimatorControllerParameterType.Float))
                { blend = Hash(alias); ResolvedSpeedParameter = alias; break; }
        }
        horizontal = Hash(profile.locomotionHorizontalParameter);
        vertical = Hash(profile.locomotionVerticalParameter);
        attacking = Hash(profile.locomotionAttackParameter);
        attackState = Hash(profile.locomotionAttackState);
        compatible = animator.HasState(0, locomotion) && Parameter(blend, AnimatorControllerParameterType.Float);
        hasHorizontal = Parameter(horizontal, AnimatorControllerParameterType.Float);
        hasVertical = Parameter(vertical, AnimatorControllerParameterType.Float);
        hasAttackBool = Parameter(attacking, AnimatorControllerParameterType.Bool);
    }
    public int ResolveState(int requested, int idle, int walk, int attack)
    {
        if (!Compatible) return requested;
        int run = Hash(profile.runAnimation);
        if (requested == idle || requested == walk || run != 0 && requested == run) return locomotion;
        if (requested == attack && !animator.HasState(0, requested) && animator.HasState(0, attackState)) return attackState;
        return requested;
    }
    public int ResolveAttackState(string preferred, bool armed)
    {
        // Explicit module/base states always win. Do not reinterpret a valid
        // unarmed legacy state as the armed fallback just because hashes match.
        string[] candidates = armed
            ? new[] { preferred, profile != null ? profile.locomotionAttackState : null, "zombie_attack2_1", profile != null ? profile.baseAttackAnimation : null }
            : new[] { preferred, "zombie_attack2_1", profile != null ? profile.locomotionAttackState : null };
        foreach (string candidate in candidates)
            if (!string.IsNullOrWhiteSpace(candidate) && animator != null && animator.HasState(0, Hash(candidate)))
            { LastAttackStateName = candidate; return Hash(candidate); }
        LastAttackStateName = "No valid attack state";
        return Hash("zombie_attack2_1");
    }
    public bool IsLocomotion(int state) => Compatible && state == locomotion;
    public static float BlendForSpeed(float speed, float walkSpeed, float runSpeed)
    {
        walkSpeed = Mathf.Max(.01f, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed + .01f, runSpeed);
        speed = Mathf.Max(0, speed);
        return speed <= walkSpeed ? .5f * Mathf.Clamp01(speed / walkSpeed) :
            Mathf.Lerp(.5f, 1f, Mathf.InverseLerp(walkSpeed, runSpeed, speed));
    }
    public void Update(Vector3 worldVelocity, bool attackActive, float deltaTime, EnemyWeaponSO weapon = null)
    {
        if (!Compatible || !animator.isActiveAndEnabled) return;
        // This controller currently implements TheLost (EnemyType 0).
        if (Parameter(Hash("EnemyType"), AnimatorControllerParameterType.Float)) animator.SetFloat("EnemyType", 0);
        if (Parameter(Hash("WeaponType"), AnimatorControllerParameterType.Float))
        {
            float selection = weapon == null ? 0 : weapon.loadout == EnemyWeaponSO.Loadout.Machete ? .2f :
                weapon.loadout == EnemyWeaponSO.Loadout.Log ? .4f : weapon.loadout == EnemyWeaponSO.Loadout.Torch ? .6f :
                weapon.loadout == EnemyWeaponSO.Loadout.Stone ? .8f : 1f;
            animator.SetFloat("WeaponType", selection);
        }
        Vector3 local = animator.transform.InverseTransformDirection(Vector3.ProjectOnPlane(worldVelocity, Vector3.up));
        float speed = worldVelocity.sqrMagnitude < .0001f ? 0 : Vector3.ProjectOnPlane(worldVelocity, Vector3.up).magnitude;
        MeasuredSpeed = speed;
        TargetBlend = BlendForMovement(speed, profile);
        if (TargetBlend == 0) local = Vector3.zero;
        Vector2 direction = new Vector2(local.x, local.z);
        if (direction.sqrMagnitude > .0001f) direction.Normalize();
        Direction = direction;
        float damp = Mathf.Max(0, profile.locomotionDamping);
        animator.SetFloat(blend, TargetBlend, damp, deltaTime);
        if (hasHorizontal) animator.SetFloat(horizontal, direction.x, damp, deltaTime);
        if (hasVertical) animator.SetFloat(vertical, direction.y, damp, deltaTime);
        if (hasAttackBool) animator.SetBool(attacking, attackActive);
    }
}
