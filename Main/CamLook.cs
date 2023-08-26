using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class CamLook : MonoBehaviour
{
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    public float sensX;
    public float sensY;
    public Transform orientation;
    float xRotation;
    float yRotation;
    public Transform camHold;
    private void Update()
    {
        // input camera move
        float x = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensX;
        float y = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sensY;
        // camera rotation
        yRotation += x;
        xRotation -= y;
        // camera limit 90 degress
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        camHold.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);
    }
    public void DoFov(float endValue)
    {
        GetComponent<Camera>().DOFieldOfView(endValue, 0.25f);
    }
    public void DoTilt(float zTilt)
    {
        transform.DOLocalRotate(new Vector3(0, 0, zTilt), 0.25f);
    }
}
