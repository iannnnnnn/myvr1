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

    bool _hasSelected;
    float _defaultCastDistance = 10f;
    float _defaultVisualDistance = 10f;
    LineDynamicsMode[] _defaultLineDynamics;
    bool[] _defaultExtendToEmptyHit;
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
        EnterOverview();
    }

    void CacheCasters()
    {
        _casters = FindObjectsByType<CurveInteractionCaster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _visuals = FindObjectsByType<CurveVisualController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (_casters != null && _casters.Length > 0)
            _defaultCastDistance = _casters[0].castDistance;

        if (_visuals != null && _visuals.Length > 0)
        {
            _defaultVisualDistance = _visuals[0].maxVisualCurveDistance;
            _defaultLineDynamics = new LineDynamicsMode[_visuals.Length];
            _defaultExtendToEmptyHit = new bool[_visuals.Length];
            for (int i = 0; i < _visuals.Length; i++)
            {
                if (_visuals[i] == null)
                    continue;
                _defaultLineDynamics[i] = _visuals[i].lineDynamicsMode;
                _defaultExtendToEmptyHit[i] = _visuals[i].extendLineToEmptyHit;
            }
        }

        _castCached = true;
    }

    public void EnterOverview()
    {
        _hasSelected = false;

        // VR 裡不能把 XR Origin 整組 pitch/roll，否則追蹤空間傾斜，射線會扎進地面或看不到
        PlaceXrOrigin(overviewPoint, yawOnly: true);
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
        PlaceXrOrigin(fp, yawOnly: true);
        SetOverviewCastRange(false);
        SetLocomotionEnabled(true);

        if (hideMarkersAfterSelect)
            SetMarkersVisible(false);
    }

    void PlaceXrOrigin(Transform point, bool yawOnly)
    {
        if (xrOrigin == null || point == null)
            return;

        if (yawOnly)
        {
            float yaw = point.eulerAngles.y;
            xrOrigin.SetPositionAndRotation(point.position, Quaternion.Euler(0f, yaw, 0f));
            return;
        }

        xrOrigin.SetPositionAndRotation(point.position, point.rotation);
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
                if (_visuals[i] == null)
                    continue;

                _visuals[i].maxVisualCurveDistance = visual;

                // 俯瞰時強制顯示長射線，避免 RetractOnHitLoss 把線縮到幾乎看不見
                if (overview)
                {
                    _visuals[i].lineDynamicsMode = LineDynamicsMode.Traditional;
                    _visuals[i].extendLineToEmptyHit = true;
                    if (_visuals[i].noValidHitProperties != null)
                    {
                        _visuals[i].noValidHitProperties.startWidth = 0.02f;
                        _visuals[i].noValidHitProperties.endWidth = 0.02f;
                    }
                }
                else
                {
                    if (_defaultLineDynamics != null && i < _defaultLineDynamics.Length)
                        _visuals[i].lineDynamicsMode = _defaultLineDynamics[i];
                    if (_defaultExtendToEmptyHit != null && i < _defaultExtendToEmptyHit.Length)
                        _visuals[i].extendLineToEmptyHit = _defaultExtendToEmptyHit[i];
                    if (_visuals[i].noValidHitProperties != null)
                    {
                        _visuals[i].noValidHitProperties.startWidth = 0.003f;
                        _visuals[i].noValidHitProperties.endWidth = 0.003f;
                    }
                }
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
