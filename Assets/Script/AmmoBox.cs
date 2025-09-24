using TMPro;
using UnityEngine;

public class AmmoBox : MonoBehaviour
{
    [Header("InteractionText")]
    [SerializeField] private TMP_Text text;

    public bool isInArea;
    public bool isPickup;
    private void Start()
    {
        
        text.gameObject.SetActive(false);
    }
    private void OnTriggerStay(Collider other)
    {
        if(other.gameObject.CompareTag("Player"))
        {
            isInArea = true;
            Pickup();
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            isInArea = false;
            Pickup();
        }
    }

    void Pickup()
    {
        if(isInArea)
        {
            text.gameObject.SetActive(true);
            if(Input.GetKeyDown(KeyCode.F))
            {
                Destroy(this.gameObject);
                isInArea = false;
            }
        }
        else
        {
            text.gameObject.SetActive(false);
        }
    }
}
