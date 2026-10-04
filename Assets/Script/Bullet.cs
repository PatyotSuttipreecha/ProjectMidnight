using UnityEngine;
using System.Collections;

public class Bullet : MonoBehaviour
{
    private float damage;
    private Vector3 direction;
    private Guns gun;
    private float speed;
    private Transform source;
    private Vector3 previousPosition;
    private bool isReady;
    private bool hasHit;

    public void SetDamage(float dmg) => damage = dmg;
    public void SetSource(Transform shooter) => source = shooter;

    public void SetDirection(Vector3 dir, float projectileSpeed = -1f)
    {
        gun = projectileSpeed < 0f ? FindAnyObjectByType<Guns>() : null;
        speed = projectileSpeed >= 0f ? projectileSpeed : gun != null ? gun.weaponStat.bulletSpeed : 0f;
        Destroy(gameObject, 5f);
        direction = dir.normalized;
        previousPosition = transform.position;
        isReady = true;
    }

    private void Update()
    {
        if (!isReady || hasHit) return;
        float moveDistance = speed * Time.deltaTime;
        Vector3 nextPosition = transform.position + direction * moveDistance;
        Debug.DrawLine(previousPosition, nextPosition, Color.red, 0.1f);
        RaycastHit[] hits = Physics.RaycastAll(previousPosition, direction, moveDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        RaycastHit hit = default;
        bool found = false;
        foreach (RaycastHit candidate in hits)
            if (!ShouldIgnore(candidate.collider) && (!found || candidate.distance < hit.distance))
            { hit = candidate; found = true; }
        if (found)
        {
            transform.position = hit.point;
            TryHit(hit.collider);
            return;
        }
        transform.position = nextPosition;
        previousPosition = transform.position;
    }

    // Both swept ray hits and trigger hits consume this bullet exactly once.
    public bool TryHit(Collider target)
    {
        if (hasHit || target == null || ShouldIgnore(target)) return false;
        hasHit = true;
        HitBox hitBox = target.GetComponent<HitBox>();
        if (hitBox != null)
        {
            HitBoxManager manager = hitBox.GetComponentInParent<HitBoxManager>();
            if (manager != null) manager.TakeDamage(hitBox.hitBox, damage, source,
                target, target.ClosestPoint(transform.position), direction);
        }
        Destroy(gameObject);
        return true;
    }
    private bool ShouldIgnore(Collider target)
    {
        if (target == null) return true;
        if (target.GetComponentInParent<Bullet>() != null) return true;
        if (source != null && target.transform.IsChildOf(source)) return true;
        return target.isTrigger && target.GetComponent<HitBox>() == null;
    }
}
