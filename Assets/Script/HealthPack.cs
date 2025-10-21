using TMPro;
using UnityEngine;

public class HealthPack : MonoBehaviour
{
    [Header("Health Pack Setting")]
    [SerializeField] private HealthPackType type;
    [HideInInspector] public int healthPackCapacity;

    [Header("Interaction Text")]
    [SerializeField] private TMP_Text text;

    [HideInInspector] public bool isInArea;

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.tag == "Player")
        {

        }
    }
    public void Healing()
    {

    }
}
public enum HealthPackType
{
    Bandage,
    PainKiller,
    FirstAidKit,
}
