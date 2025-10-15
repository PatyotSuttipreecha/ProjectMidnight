using UnityEngine;
using System.Collections;

public class Bullet : MonoBehaviour
{
    private float damage;
    private Vector3 direction;
    private Guns gun;
    private Vector3 previousPosition;
    private bool isReady = false;

    public void SetDamage(float dmg)
    {
        damage = dmg;
    }

    public void SetDirection(Vector3 dir)
    {
        gun = FindAnyObjectByType<Guns>();
        direction = dir.normalized;
        previousPosition = transform.position;
        StartCoroutine(WaitOneFrame()); // ✅ รอ 1 เฟรมก่อนเริ่มเช็คชน
    }

    private IEnumerator WaitOneFrame()
    {
        yield return new WaitForSecondsRealtime(0.1f); // รอ 1 เฟรม
        isReady = true;
    }

    void Update()
    {
        if (!isReady || gun == null) return;

        float moveDistance = gun.weaponStat.bulletSpeed * Time.deltaTime;
        Vector3 nextPosition = transform.position + direction * moveDistance;

        // ✅ Debug line เพื่อดูวิถีกระสุน
        Debug.DrawLine(previousPosition, nextPosition, Color.red, 0.1f);

        // ✅ ตรวจการชนระหว่างตำแหน่งก่อนหน้า → ตำแหน่งใหม่
        if (Physics.Linecast(previousPosition, nextPosition, out RaycastHit hit))
        {
            Debug.Log($"Bullet hit: {hit.collider.name} at {hit.point}");

            HitBox hitBox = hit.collider.GetComponent<HitBox>();
            if (hitBox != null)
            {
                HitBoxManager manager = hitBox.GetComponentInParent<HitBoxManager>();
                if (manager != null)
                {
                    Debug.Log($"Damage applied to {hitBox.hitBox} on {manager.name}");
                    manager.TakeDamage(hitBox.hitBox);
                }
            }

            transform.position = hit.point;
            Destroy(gameObject);
            return;
        }

        transform.position = nextPosition;
        previousPosition = transform.position;
    }
}
