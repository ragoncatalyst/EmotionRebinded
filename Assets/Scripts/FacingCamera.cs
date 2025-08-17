using UnityEngine;

public class FacingCamera : MonoBehaviour
{
    void LateUpdate()
    {
        transform.rotation = Quaternion.Euler(-45f, 0f, 0f);
    }
}