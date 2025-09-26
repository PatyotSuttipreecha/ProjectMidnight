using TMPro;
using UnityEngine;

public class AmmoBox : MonoBehaviour
{
    [Header("AmmoSetting")]
    public Guns.WeaponType weaponType;
    public int ammoGive;
    [Header("InteractionText")]
    [SerializeField] private TMP_Text text;

    public bool isInArea;

    private void Start()
    {
        text.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            text.gameObject.SetActive(true);
            isInArea = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            isInArea = false;
            text.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // ���������㹾�鹷������ ��С� F
        if (isInArea && Input.GetKeyDown(KeyCode.F))
        {
            // TODO: �����к����� Ammo ��� Player ��ç���
            Pickup();
            Debug.Log("Picked up Ammo!");
        }
    }
    void Pickup()
    {
        Guns[] allGun = Resources.FindObjectsOfTypeAll<Guns>();

        foreach (Guns gun in allGun)
        {
            if(gun.weaponStat.weaponName == weaponType)
            {
                ammoGive = Random.Range(6, 12);
                gun.weaponStat.ammoReserve += ammoGive;
                Destroy(this.gameObject);
                text.gameObject.SetActive(false);
                break;
                
            }
        }
    }
}
