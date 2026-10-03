using UnityEngine;

public class HitBox : MonoBehaviour
{
    [Header("HitBox")]
    public HitBoxPart hitBox;

    private void OnTriggerEnter(Collider other)
    {
        Bullet bullet = other.GetComponentInParent<Bullet>();
        if (bullet != null) bullet.TryHit(GetComponent<Collider>());
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
