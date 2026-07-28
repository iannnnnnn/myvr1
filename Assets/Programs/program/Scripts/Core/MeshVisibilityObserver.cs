using UnityEngine;

/// <summary>
/// 訂閱碳排變更，以 MeshRenderer.enabled 切換 3D 可見性。
/// 禁止 GameObject.SetActive（會觸發 VR 一體機 Render Pipeline re-batch 卡頓）。
/// OnDisable 必須 -=，避免靜態事件殭屍參考洩漏。
/// </summary>
public sealed class MeshVisibilityObserver : MonoBehaviour
{
    [SerializeField] private MeshRenderer _meshRenderer;

    [Tooltip("碳排達到／超過此值時的顯示策略由 showWhenAtOrAbove 決定")]
    [SerializeField] private float _carbonThreshold = 50f;

    [Tooltip("true：碳排 ≥ 閾值時顯示；false：碳排 ≥ 閾值時隱藏")]
    [SerializeField] private bool _showWhenAtOrAbove = true;

    private void Awake()
    {
        if (_meshRenderer == null)
            _meshRenderer = GetComponent<MeshRenderer>();
    }

    private void OnEnable()
    {
        EnvironmentEventHub.OnCarbonChanged += HandleCarbonChanged;
    }

    private void OnDisable()
    {
        EnvironmentEventHub.OnCarbonChanged -= HandleCarbonChanged;
    }

    private void HandleCarbonChanged(float newLevel)
    {
        if (_meshRenderer == null)
            return;

        bool shouldShow = _showWhenAtOrAbove
            ? newLevel >= _carbonThreshold
            : newLevel < _carbonThreshold;

        // 僅切換 renderer，不動 GameObject 啟用狀態
        if (_meshRenderer.enabled != shouldShow)
            _meshRenderer.enabled = shouldShow;
    }
}
