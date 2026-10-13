using UnityEngine;

// Swept collision with a snapshot trajectory: no homing, no damage through walls.
public class EnemyStoneProjectile : MonoBehaviour
{
    private Transform owner;
    private Vector3 velocity;
    private float gravity, radius, remaining, damage, protection;
    private int obstacleMask;
    public bool Finished { get; private set; }
    public static EnemyStoneProjectile Launch(Transform source, Vector3 origin, Vector3 aim, EnemyWeaponSO module, float hitProtection)
    {
        GameObject visual = module.projectileModel != null ? Instantiate(module.projectileModel) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Enemy Stone (prototype projectile)";
        visual.transform.position = origin;
        if (module.projectileModel == null) visual.transform.localScale = Vector3.one * module.projectileRadius * 2;
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
        foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(visual, source.gameObject.scene);
        var projectile = visual.AddComponent<EnemyStoneProjectile>();
        projectile.owner = source;
        projectile.gravity = Mathf.Max(0, module.projectileGravity);
        projectile.radius = Mathf.Max(.01f, module.projectileRadius);
        projectile.remaining = Mathf.Max(.1f, module.projectileLifetime);
        projectile.damage = Random.Range(Mathf.Min(module.minDamage, module.maxDamage), Mathf.Max(module.minDamage, module.maxDamage));
        projectile.protection = hitProtection;
        projectile.obstacleMask = module.projectileObstacles;
        float flightTime = Mathf.Max(.05f, Vector3.Distance(origin, aim) / Mathf.Max(.1f, module.projectileSpeed));
        projectile.velocity = (aim - origin) / flightTime + Vector3.up * (.5f * projectile.gravity * flightTime);
        return projectile;
    }
    private void Update()
    {
        if (Finished) return;
        foreach (var collider in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (collider.transform.IsChildOf(transform) || owner != null && collider.transform.IsChildOf(owner)) continue;
            var target = collider.GetComponentInParent<PlayerController>();
            if (target == null && (obstacleMask & (1 << collider.gameObject.layer)) == 0) continue;
            if (target != null) EnemyPlayerEffects.For(target).TryHit(damage, protection);
            Finish(); return;
        }
        float step = Mathf.Min(Time.deltaTime, remaining);
        remaining -= step;
        Vector3 motion = velocity * step + Vector3.down * (.5f * gravity * step * step);
        velocity += Vector3.down * gravity * step;
        if (motion.sqrMagnitude > .000001f)
        {
            RaycastHit? nearest = null;
            foreach (var hit in Physics.SphereCastAll(transform.position, radius, motion.normalized, motion.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || owner != null && hit.collider.transform.IsChildOf(owner)) continue;
                var target = hit.collider.GetComponentInParent<PlayerController>();
                if (target == null && (obstacleMask & (1 << hit.collider.gameObject.layer)) == 0) continue;
                if (nearest == null || hit.distance < nearest.Value.distance) nearest = hit;
            }
            if (nearest.HasValue)
            {
                var target = nearest.Value.collider.GetComponentInParent<PlayerController>();
                if (target != null) EnemyPlayerEffects.For(target).TryHit(damage, protection);
                Finish(); return;
            }
            transform.position += motion;
        }
        if (remaining <= 0) Finish();
    }
    public void Cancel() => Finish();
    private void Finish() { Finished = true; Destroy(gameObject); }
}
