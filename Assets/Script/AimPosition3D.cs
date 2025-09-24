using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AimPosition3D : MonoBehaviour
{
    [SerializeField] private Camera mainCam;
    [SerializeField] private LayerMask layerMask;

    private void Update()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if(Physics.Raycast(ray,out RaycastHit raycastHit,float.MaxValue, layerMask))
        {
            transform.position = raycastHit.point;
        }
    }
}
