using UnityEngine;

/// <summary>
/// 通用工具展示效果：旋轉及上下懸浮。
/// </summary>
public class ToolFloatingDisplay : MonoBehaviour
{
    [Header("旋轉設定")]
    [SerializeField] float rotationSpeed = 45f;
    [SerializeField] bool useLocalAxis = true;
    [SerializeField] bool rotateOnStart = true;

    [Header("懸浮設定")]
    [SerializeField] bool enableFloating;
    [SerializeField] float floatingHeight = 0.08f;
    [SerializeField] float floatingSpeed = 1.5f;

    bool _displayActive;
    Vector3 _startPosition;

    protected virtual void Awake()
    {
        _startPosition = transform.position;
        _displayActive = rotateOnStart || enableFloating;
    }

    protected virtual void Update()
    {
        if (!_displayActive)
            return;

        if (rotateOnStart)
        {
            var space = useLocalAxis ? Space.Self : Space.World;
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, space);
        }

        if (enableFloating)
        {
            float offset = Mathf.Sin(Time.time * floatingSpeed) * floatingHeight;
            transform.position = _startPosition + Vector3.up * offset;
        }
    }

    public void StartRotation() => _displayActive = true;

    public void StopRotation() => _displayActive = false;

    public void StopDisplayEffect() => _displayActive = false;

    public void RestartDisplayEffect()
    {
        _startPosition = transform.position;
        _displayActive = true;
    }

    public bool IsRotating() => _displayActive && rotateOnStart;
}
