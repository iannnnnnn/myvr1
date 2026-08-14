using UnityEngine;

public class ObjectRotator : MonoBehaviour
{
    [Header("要旋轉的物件")]
    [SerializeField] private Transform targetObject;

    [Header("旋轉速度")]
    [SerializeField] private float rotateSpeed = 20f;

    [Header("旋轉軸")]
    [SerializeField] private Vector3 rotateAxis = Vector3.up;

    void Update()
    {
        if (targetObject == null)
            return;

        targetObject.Rotate(
            rotateAxis,
            rotateSpeed * Time.deltaTime,
            Space.Self
        );
    }
}