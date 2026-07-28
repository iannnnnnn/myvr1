using UnityEngine;

/// <summary>
/// 以 Time.time 時間戳做互動節流，攔截連點／連續 Trigger。
/// 禁止 Coroutine + WaitForSeconds（會 new、產生 GC）。
/// </summary>
public class ThrottledInteractable : MonoBehaviour
{
    [SerializeField] private float _cooldownInterval = 0.5f;

    private float _nextAllowedTime;

    public float CooldownInterval
    {
        get => _cooldownInterval;
        set => _cooldownInterval = Mathf.Max(0f, value);
    }

    public float NextAllowedTime => _nextAllowedTime;

    /// <summary>
    /// 嘗試互動：冷卻中回傳 false；通過則鎖定至 Time.time + cooldown 並回傳 true。
    /// 熱路徑無配置、無 Coroutine。
    /// </summary>
    public bool TryInteract()
    {
        float now = Time.time;
        if (now < _nextAllowedTime)
            return false;

        _nextAllowedTime = now + _cooldownInterval;
        return true;
    }

    /// <summary>立即解除冷卻（例如關卡重置）。</summary>
    public void ResetCooldown()
    {
        _nextAllowedTime = 0f;
    }
}
