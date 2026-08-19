using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// ForestBasic 關卡選擇：進場先到俯瞰點，選中關卡圖示後傳送到第一人稱並恢復移動。
/// </summary>
public class ForestLevelSelectController : MonoBehaviour
{
    [Header("XR")]
    [SerializeField] Transform xrOrigin;
    [Tooltip("XR Origin 底下的 Locomotion 物件；俯瞰時關閉，選關後開啟")]
    [SerializeField] GameObject locomotionRoot;

    [Header("Overview")]
    [SerializeField] Transform overviewPoint;
    [Tooltip("俯瞰選關時雷射最遠距離（預設 Near-Far 只有 10m，遠距圖示點不到）")]
    [SerializeField] float overviewCastDistance = 300f;

    [Header("Levels")]
    [SerializeField] ForestLevelCamera[] levels;
    [SerializeField] bool hideMarkersAfterSelect = true;

    [Header("Intro Popup")]
    [Tooltip("選完關卡後跳出的介紹圖（整張 PNG 當卡片）；留空則不跳出")]
    [SerializeField] Sprite introImage;
    [Tooltip("介紹圖第 2 頁，可留空")]
    [SerializeField] Sprite introImagePage2;
    [SerializeField] AudioClip introAudio;
    [Tooltip("介紹圖相對落點的本地偏移（z 為玩家正前方）")]
    [SerializeField] Vector3 introOffset = new Vector3(0f, 1.4f, 1.5f);

    bool _hasSelected;
    float _defaultCastDistance = 10f;
    float _defaultVisualDistance = 10f;
    CurveInteractionCaster[] _casters;
    CurveVisualController[] _visuals;
    bool _castCached;

    void Awake()
    {
        if (xrOrigin == null)
        {
            var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null)
                xrOrigin = origin.transform;
        }

        if (locomotionRoot == null && xrOrigin != null)
        {
            var locomotion = xrOrigin.Find("Locomotion");
            if (locomotion != null)
                locomotionRoot = locomotion.gameObject;
        }

        CacheCasters();
        EnsurePhysicsDebugOnCasters();
        EnterOverview();
    }

    void CacheCasters()
    {
        _casters = FindObjectsByType<CurveInteractionCaster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _visuals = FindObjectsByType<CurveVisualController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (_casters != null && _casters.Length > 0)
            _defaultCastDistance = _casters[0].castDistance;
        if (_visuals != null && _visuals.Length > 0)
            _defaultVisualDistance = _visuals[0].maxVisualCurveDistance;

        _castCached = true;
    }

    void EnsurePhysicsDebugOnCasters()
    {
        if (_casters == null)
            return;

        for (int i = 0; i < _casters.Length; i++)
        {
            var caster = _casters[i];
            if (caster == null)
                continue;

            var debug = caster.GetComponent<PhysicsRayDebug>();
            if (debug == null)
                debug = caster.gameObject.AddComponent<PhysicsRayDebug>();

            debug.Configure(caster.transform, 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        }
    }

    public void EnterOverview()
    {
        _hasSelected = false;

        if (xrOrigin != null && overviewPoint != null)
            xrOrigin.SetPositionAndRotation(overviewPoint.position, overviewPoint.rotation);

        SetLocomotionEnabled(false);
        SetMarkersVisible(true);
        SetOverviewCastRange(true);
    }

    public void SelectLevel(int index)
    {
        if (_hasSelected)
            return;

        if (levels == null || index < 0 || index >= levels.Length)
            return;

        var level = levels[index];
        if (level == null || xrOrigin == null)
            return;

        Transform fp = level.FirstPersonPoint;
        if (fp == null)
            return;

        _hasSelected = true;
        xrOrigin.SetPositionAndRotation(fp.position, fp.rotation);
        SetOverviewCastRange(false);
        SetLocomotionEnabled(true);

        ShowIntroPopup(fp);

        if (hideMarkersAfterSelect)
            SetMarkersVisible(false);
    }

    /// <summary>落點前方跳出關卡介紹圖；沿用共用彈窗，UIManager 會自動面向攝影機。</summary>
    void ShowIntroPopup(Transform anchor)
    {
        if (introImage == null || anchor == null)
            return;

        if (UIManager.Instance == null)
        {
            Debug.LogWarning("場景中找不到 UIManager，無法顯示關卡介紹。", this);
            return;
        }

        UIManager.Instance.ShowPopup(string.Empty, string.Empty, introImage, introImagePage2, introAudio, anchor, introOffset);
    }

    void SetOverviewCastRange(bool overview)
    {
        if (!_castCached)
            CacheCasters();

        float cast = overview ? overviewCastDistance : _defaultCastDistance;
        float visual = overview ? overviewCastDistance : _defaultVisualDistance;

        if (_casters != null)
        {
            for (int i = 0; i < _casters.Length; i++)
            {
                if (_casters[i] != null)
                    _casters[i].castDistance = cast;
            }
        }

        if (_visuals != null)
        {
            for (int i = 0; i < _visuals.Length; i++)
            {
                if (_visuals[i] != null)
                    _visuals[i].maxVisualCurveDistance = visual;
            }
        }
    }

    void SetLocomotionEnabled(bool enabled)
    {
        if (locomotionRoot != null)
            locomotionRoot.SetActive(enabled);
    }

    void SetMarkersVisible(bool visible)
    {
        if (levels == null)
            return;

        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] != null)
                levels[i].gameObject.SetActive(visible);
        }
    }
}
