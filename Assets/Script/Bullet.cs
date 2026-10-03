using UnityEngine;
using System.Collections;

public class Bullet : MonoBehaviour
{
    private float damage;
    private Vector3 direction;
    private Guns gun;
    private Transform source;
    private Vector3 previousPosition;
    private bool isReady;
    private bool hasHit;

    public void SetDamage(float dmg) => damage = dmg;
    public void SetSource(Transform shooter) => source = shooter;

    public void SetDirection(Vector3 dir)
    {
        gun = FindAnyObjectByType<Guns>();
        direction = dir.normalized;
        previousPosition = transform.position;
        StartCoroutine(WaitOneFrame());
    }

    private IEnumerator WaitOneFrame()
    {
        yield return new WaitForSecondsRealtime(0.1f);
        isReady = true;
    }

    private void Update()
    {
        if (!isReady || hasHit || gun == null) return;
        float moveDistance = gun.weaponStat.bulletSpeed * Time.deltaTime;
        Vector3 nextPosition = transform.position + direction * moveDistance;
        Debug.DrawLine(previousPosition, nextPosition, Color.red, 0.1f);
        if (Physics.Linecast(previousPosition, nextPosition, out RaycastHit hit))
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
        if (hasHit || target == null) return false;
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
}
