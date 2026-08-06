using UnityEngine;

/// <summary>
/// 讓圖示始終面向主攝影機（含 XR 頭顯攝影機），方便俯瞰時點選。
/// </summary>
public class BillboardFacingCamera : MonoBehaviour
{
    [SerializeField] bool flattenY = false;
    [SerializeField] bool invertForward = true;

    Camera _cam;

    void LateUpdate()
    {
        if (_cam == null)
            _cam = Camera.main;
        if (_cam == null)
            return;

        Vector3 lookPos = _cam.transform.position;
        if (flattenY)
            lookPos.y = transform.position.y;

        Vector3 dir = lookPos - transform.position;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        if (invertForward)
            dir = -dir;

        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }
}
