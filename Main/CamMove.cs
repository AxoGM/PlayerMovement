using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamMove : MonoBehaviour
{
    public Transform camPos;
    private void Update()
    {
        // camera follow the player object
        transform.position = camPos.position;
    }
}
