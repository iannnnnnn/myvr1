using UnityEngine;

/// <summary>
/// Simple Y-axis rotator used by axe display scripts.
/// </summary>
public class RotateY : MonoBehaviour
{
    [SerializeField] float speed = 45f;

    void Update()
    {
        transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
    }
}
