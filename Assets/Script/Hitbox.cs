using UnityEngine;

public class HitBox : MonoBehaviour
{
    [Header("HitBox")]
    public HitBoxPart hitBox;
    private HitBoxManager hitBoxManager;

   

    private void Start()
    {
        hitBoxManager = GetComponentInParent<HitBoxManager>(); // หาพ่อของมัน (Enemy)
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bullet"))
        {
            Debug.Log($"Hit on {hitBox}");
            hitBoxManager.TakeDamage(hitBox);
        }
    }
}
public enum HitBoxPart
{
    Head,
    Body,
    Hip,
    UpperArm,
    LowerArm,
    UpperLeg,
    LowerLeg,
    Foot,
    Hand
}