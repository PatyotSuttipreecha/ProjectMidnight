using UnityEngine;

public class Bullet : MonoBehaviour
{
    private float damage;

    public void SetDamage(float dmg)
    {
        damage = dmg;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // สมมติว่ามี Enemy ที่มี Health
        //Destroy(collision.gameObject);
        //Destroy(this.gameObject); // ลบกระสุนหลังชน
    }
}
