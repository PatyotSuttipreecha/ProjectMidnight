using UnityEngine;

public class PatrolPoint : MonoBehaviour
{
    public bool overrideWaitTime;
    [Min(0f)] public float waitTime = 2f;
}
