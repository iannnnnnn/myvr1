using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// 地圖漂浮灑水壺 = 按鈕站。
/// 按一下：右手生成可澆水的 WateringCan；再按：收回銷毀。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRSimpleInteractable))]
public class WateringCanStationButton : MonoBehaviour
{
    [Header("Held Can")]
    [SerializeField] GameObject heldCanPrefab;
    [SerializeField] NearFarInteractor rightHandInteractor;
    [SerializeField] float spawnAttachDelay = 0.05f;
    [Tooltip("手上壺縮放（場景漂浮壺常被縮到很小，實用壺需相同尺度）")]
    [SerializeField] Vector3 heldLocalScale = new Vector3(0.015f, 0.015f, 0.025f);
    [Tooltip("按鈕站上原本的噴水粒子；生成手上壺時會複製過去")]
    [SerializeField] ParticleSystem waterParticleTemplate;

    [Header("Button Look")]
    [SerializeField] float buttonScaleMultiplier = 1.75f;
    [SerializeField] bool applyScaleOnAwake = true;

    [Header("Glow")]
    [SerializeField] bool enableGlow = true;
    [ColorUsage(true, true)]
    [SerializeField] Color emissionColor = new Color(0.35f, 0.85f, 1.2f, 1f);
    [SerializeField] float pulseSpeed = 2f;
    [SerializeField] float minIntensity = 0.35f;
    [SerializeField] float maxIntensity = 2.2f;
    [SerializeField] bool addPulseLight = true;
    [SerializeField] float lightIntensity = 1.4f;
    [SerializeField] float lightRange = 1.25f;

    [Header("Station Setup")]
    [SerializeField] bool convertGrabToButtonOnAwake = true;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    XRSimpleInteractable _simple;
    ToolFloatingDisplay _floating;
    GameObject _heldInstance;
    bool _busy;
    float _cooldownUntil;
    MaterialPropertyBlock _block;
    Renderer[] _renderers;
    Light _pulseLight;
    Vector3 _baseScale;

    public GameObject HeldCanPrefab
    {
        get => heldCanPrefab;
        set => heldCanPrefab = value;
    }

    void Awake()
    {
        _baseScale = transform.localScale;
        if (applyScaleOnAwake)
            transform.localScale = _baseScale * buttonScaleMultiplier;

        _simple = GetComponent<XRSimpleInteractable>();
        _floating = GetComponent<ToolFloatingDisplay>();
        _block = new MaterialPropertyBlock();
        _renderers = GetComponentsInChildren<Renderer>(true);

        if (convertGrabToButtonOnAwake)
            ConvertToButtonStation();

        EnsureFloatingDisplay();
        EnsureEmissionEnabled();
        EnsureGlowLight();
        ResolveRightHand();
    }

    void OnEnable()
    {
        if (_simple == null)
            _simple = GetComponent<XRSimpleInteractable>();
        if (_simple != null)
            _simple.selectEntered.AddListener(OnStationSelected);
    }

    void OnDisable()
    {
        if (_simple != null)
            _simple.selectEntered.RemoveListener(OnStationSelected);
    }

    void OnDestroy()
    {
        DestroyHeldCan();
    }

    void Update()
    {
        if (!enableGlow || _renderers == null)
            return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
        ApplyEmission(emissionColor * intensity);

        if (_pulseLight != null)
            _pulseLight.intensity = lightIntensity * Mathf.Lerp(0.55f, 1f, t);
    }

    void OnStationSelected(SelectEnterEventArgs args)
    {
        if (!Application.isPlaying || _busy)
            return;
        if (Time.time < _cooldownUntil)
            return;

        _cooldownUntil = Time.time + 0.35f;
        StartCoroutine(ToggleHeldCanRoutine(args));
    }

    IEnumerator ToggleHeldCanRoutine(SelectEnterEventArgs args)
    {
        _busy = true;
        try
        {
            // 等一幀再放開按鈕，避免在 selectEntered 回呼內直接 SelectExit
            yield return null;
            if (args.manager != null && args.interactorObject != null && _simple != null &&
                _simple.isSelected)
            {
                args.manager.SelectExit(args.interactorObject, _simple);
            }

            if (_heldInstance != null)
            {
                DestroyHeldCan();
                yield break;
            }

            if (heldCanPrefab == null)
            {
                Debug.LogWarning("WateringCanStationButton: 未指定 heldCanPrefab。", this);
                yield break;
            }

            ResolveRightHand();
            if (rightHandInteractor == null)
            {
                Debug.LogWarning("WateringCanStationButton: 找不到右手 NearFarInteractor。", this);
                yield break;
            }

            var manager = rightHandInteractor.interactionManager;
            if (manager == null)
            {
                Debug.LogWarning("WateringCanStationButton: 右手 Interactor 沒有 Interaction Manager。", this);
                yield break;
            }

            // 若右手已抓其他物，先放開
            if (rightHandInteractor.hasSelection)
            {
                var selecting = rightHandInteractor.interactablesSelected;
                for (int i = selecting.Count - 1; i >= 0; i--)
                    manager.SelectExit(rightHandInteractor, selecting[i]);
            }

            Vector3 spawnPos = rightHandInteractor.transform.position;
            Quaternion spawnRot = rightHandInteractor.transform.rotation;
            _heldInstance = Instantiate(heldCanPrefab, spawnPos, spawnRot);
            _heldInstance.name = heldCanPrefab.name + "_Held";
            _heldInstance.transform.localScale = heldLocalScale;

            // 手上實例不要再漂浮／當按鈕
            var heldFloating = _heldInstance.GetComponent<ToolFloatingDisplay>();
            if (heldFloating != null)
                heldFloating.enabled = false;

            var heldStation = _heldInstance.GetComponent<WateringCanStationButton>();
            if (heldStation != null)
                Destroy(heldStation);

            var heldSimple = _heldInstance.GetComponent<XRSimpleInteractable>();
            if (heldSimple != null)
                Destroy(heldSimple);

            var grab = _heldInstance.GetComponent<XRGrabInteractable>();
            var rb = _heldInstance.GetComponent<Rigidbody>();
            if (grab == null)
            {
                Debug.LogWarning("WateringCanStationButton: held prefab 缺少 XRGrabInteractable。", this);
                DestroyHeldCan();
                yield break;
            }

            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
#endif
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            grab.enabled = true;

            var watering = _heldInstance.GetComponent<WateringCan>();
            if (watering != null)
            {
                // Prefab 實例化時可能已自動建過預設粒子；先關掉再覆寫成場景原本設定
                watering.enabled = false;
                TransferWateringSetup(watering);
                watering.enabled = true;
            }

            var toolGrab = _heldInstance.GetComponent<ToolGrabController>();
            if (toolGrab != null)
                toolGrab.enabled = true;

            yield return null;
            if (spawnAttachDelay > 0f)
                yield return new WaitForSeconds(spawnAttachDelay);

            if (_heldInstance == null || grab == null || rightHandInteractor == null)
                yield break;

            if (!grab.isSelected)
                manager.SelectEnter((IXRSelectInteractor)rightHandInteractor, (IXRSelectInteractable)grab);

            if (!grab.isSelected)
                Debug.LogWarning("WateringCanStationButton: 自動上手失敗，請檢查 Interaction Layer。", this);
        }
        finally
        {
            _busy = false;
        }
    }

    void DestroyHeldCan()
    {
        if (_heldInstance == null)
            return;

        var grab = _heldInstance.GetComponent<XRGrabInteractable>();
        if (grab != null && grab.isSelected && grab.interactionManager != null)
        {
            var selecting = grab.interactorsSelecting;
            for (int i = selecting.Count - 1; i >= 0; i--)
                grab.interactionManager.SelectExit(selecting[i], grab);
        }

        Destroy(_heldInstance);
        _heldInstance = null;
    }

    void ConvertToButtonStation()
    {
        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.enabled = false;

        var watering = GetComponent<WateringCan>();
        if (watering != null)
            watering.enabled = false;

        var toolGrab = GetComponent<ToolGrabController>();
        if (toolGrab != null)
            toolGrab.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
        }

        // 避免按鈕站自己噴水；保留 emission 設定供複製到手上壺
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null)
                continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (waterParticleTemplate == null)
        {
            var spray = transform.Find("Spout/SprayWater")
                        ?? transform.Find("SprayWater");
            if (spray != null)
                waterParticleTemplate = spray.GetComponent<ParticleSystem>();
            if (waterParticleTemplate == null)
            {
                var all = GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].name.Contains("Spray"))
                    {
                        waterParticleTemplate = all[i];
                        break;
                    }
                }
            }
        }
    }

    void TransferWateringSetup(WateringCan heldWatering)
    {
        if (heldWatering == null)
            return;

        var sourceTuning = GetComponent<WateringCan>();
        if (sourceTuning != null)
            heldWatering.CopyTuningFrom(sourceTuning);

        var sourceSpout = transform.Find("Spout");
        var heldSpout = heldWatering.Spout;
        if (heldSpout == null)
        {
            var found = _heldInstance.transform.Find("Spout");
            if (found != null)
                heldSpout = found;
        }

        // 把按鈕站上調好的壺嘴位置／旋轉複製到手上壺
        if (sourceSpout != null && heldSpout != null)
        {
            heldSpout.localPosition = sourceSpout.localPosition;
            heldSpout.localRotation = sourceSpout.localRotation;
            heldSpout.localScale = sourceSpout.localScale;
        }

        if (heldSpout != null)
        {
            // 清掉 Prefab Awake 自動產生的預設粒子
            for (int i = heldSpout.childCount - 1; i >= 0; i--)
            {
                var child = heldSpout.GetChild(i).gameObject;
                if (child.GetComponent<ParticleSystem>() == null)
                    continue;
                if (child.name.Contains("Water") || child.name.Contains("Spray"))
                    Destroy(child);
            }
        }

        ParticleSystem template = waterParticleTemplate;
        if (template == null && sourceTuning != null)
            template = sourceTuning.WaterParticles;
        if (template == null)
        {
            var spray = transform.Find("Spout/SprayWater") ?? transform.Find("SprayWater");
            if (spray != null)
                template = spray.GetComponent<ParticleSystem>();
        }

        if (template != null && heldSpout != null)
        {
            var srcT = template.transform;
            var cloneGo = Instantiate(template.gameObject, heldSpout);
            cloneGo.name = "SprayWater";
            cloneGo.transform.localPosition = srcT.localPosition;
            cloneGo.transform.localRotation = srcT.localRotation;
            cloneGo.transform.localScale = srcT.localScale;
            cloneGo.SetActive(true);

            var clonePs = cloneGo.GetComponent<ParticleSystem>();
            if (clonePs != null)
            {
                var emission = clonePs.emission;
                emission.enabled = true;
                clonePs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                heldWatering.BindWaterParticles(clonePs);
            }
        }
    }

    void EnsureFloatingDisplay()
    {
        if (_floating == null)
            _floating = gameObject.AddComponent<ToolFloatingDisplay>();
        _floating.enabled = true;
        _floating.RestartDisplayEffect();
    }

    void EnsureEmissionEnabled()
    {
        if (_renderers == null)
            return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null)
                continue;

            // 建立材質實例，開啟 Emission keyword，避免改到共用資產
            var mats = r.materials;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat == null)
                    continue;
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor(EmissionColorId, Color.black);
            }
        }
    }

    void EnsureGlowLight()
    {
        if (!addPulseLight)
            return;

        var existing = transform.Find("StationGlowLight");
        if (existing != null)
        {
            _pulseLight = existing.GetComponent<Light>();
            return;
        }

        var go = new GameObject("StationGlowLight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        _pulseLight = go.AddComponent<Light>();
        _pulseLight.type = LightType.Point;
        _pulseLight.color = new Color(0.45f, 0.85f, 1f, 1f);
        _pulseLight.range = lightRange;
        _pulseLight.intensity = lightIntensity;
        _pulseLight.shadows = LightShadows.None;
    }

    void ResolveRightHand()
    {
        if (rightHandInteractor != null)
            return;

        var all = FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].handedness == InteractorHandedness.Right)
            {
                rightHandInteractor = all[i];
                return;
            }
        }

        // 備援：名稱含 Right
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name.Contains("Right"))
            {
                rightHandInteractor = all[i];
                return;
            }
        }
    }

    void ApplyEmission(Color color)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null)
                continue;
            r.GetPropertyBlock(_block);
            _block.SetColor(EmissionColorId, color);
            r.SetPropertyBlock(_block);
        }
    }
}
