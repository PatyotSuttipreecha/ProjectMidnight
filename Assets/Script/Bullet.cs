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
        // ���������� Enemy ����� Health
        //Destroy(collision.gameObject);
        //Destroy(this.gameObject); // ź����ع��ѧ��
    }
}
