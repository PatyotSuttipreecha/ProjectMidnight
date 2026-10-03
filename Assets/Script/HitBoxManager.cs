using UnityEngine;

public class HitBoxManager : MonoBehaviour
{
    [Header("Enemy Settings")]
    public float health = 100f;
    private bool isDead;

    public void TakeDamage(HitBoxPart hitPart, float baseDamage = 0f, Transform source = null,
        Collider hitCollider = null, Vector3 hitPoint = default, Vector3 hitDirection = default)
    {
        if (isDead) return;
        float minDamage = 10f;
        float maxDamage = 15f;
        float multiplier = 0.5f;
        switch (hitPart)
        {
            case HitBoxPart.Head:
                minDamage = 30f; maxDamage = 45f; multiplier = 1.75f;
                break;
            case HitBoxPart.Body:
                minDamage = 20f; maxDamage = 25f; multiplier = 1f;
                break;
            case HitBoxPart.Hip:
            case HitBoxPart.UpperArm:
            case HitBoxPart.UpperLeg:
                minDamage = 15f; maxDamage = 20f; multiplier = 0.75f;
                break;
        }
        // Existing weapons have damage = 0. Preserve their old damage until configured.
        float damage = baseDamage > 0f ? baseDamage * multiplier : Random.Range(minDamage, maxDamage);
        health = Mathf.Max(0f, health - damage);
        Debug.Log($"{hitPart} hit! -{damage} HP (Remaining: {health})");
        if (TryGetComponent(out EnemyController enemy)) enemy.AlertTo(source);
        if (health <= 0f)
        {
            isDead = true;
            if (enemy != null) enemy.BeginDeath();
            else Destroy(gameObject);
        }
        if (enemy != null) enemy.ReceiveBulletImpact(hitCollider, hitPoint, hitDirection);
    }
}
