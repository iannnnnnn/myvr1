using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// 地圖漂浮斧頭 = 按鈕站。
/// 按一下：右手生成可砍樹的斧頭；再按：收回銷毀。
/// 流程與 WateringCanStationButton 相同。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRSimpleInteractable))]
public class AxeStationButton : MonoBehaviour
{
    [Header("Held Axe")]
    [SerializeField] GameObject heldAxePrefab;
    [SerializeField] NearFarInteractor rightHandInteractor;
    [SerializeField] float spawnAttachDelay = 0.05f;
    [Tooltip("手上斧頭縮放")]
    [SerializeField] Vector3 heldLocalScale = Vector3.one;
    [Tooltip("手上斧頭的 Tag，供 TreeChopController 判定")]
    [SerializeField] string heldAxeTag = "Axe";

    [Header("Button Look")]
    [SerializeField] float buttonScaleMultiplier = 1.75f;
    [SerializeField] bool applyScaleOnAwake = true;
    [Tooltip("站點沒有展示元件時自動加上 ToolFloatingDisplay")]
    [SerializeField] bool ensureFloatingDisplay = false;

    [Header("Glow")]
    [SerializeField] bool enableGlow = true;
    [ColorUsage(true, true)]
    [SerializeField] Color emissionColor = new Color(1.1f, 0.75f, 0.3f, 1f);
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

    public GameObject HeldAxePrefab
    {
        get => heldAxePrefab;
        set => heldAxePrefab = value;
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

        if (ensureFloatingDisplay)
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
        DestroyHeldAxe();
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
        StartCoroutine(ToggleHeldAxeRoutine(args));
    }

    IEnumerator ToggleHeldAxeRoutine(SelectEnterEventArgs args)
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
                DestroyHeldAxe();
                yield break;
            }

            var prefab = ResolveHeldPrefab();
            if (prefab == null)
            {
                Debug.LogWarning("AxeStationButton: 未指定 heldAxePrefab，也無法從 Prefab 來源解析。", this);
                yield break;
            }

            ResolveRightHand();
            if (rightHandInteractor == null)
            {
                Debug.LogWarning("AxeStationButton: 找不到右手 NearFarInteractor。", this);
                yield break;
            }

            var manager = rightHandInteractor.interactionManager;
            if (manager == null)
            {
                Debug.LogWarning("AxeStationButton: 右手 Interactor 沒有 Interaction Manager。", this);
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
            _heldInstance = Instantiate(prefab, spawnPos, spawnRot);
            _heldInstance.name = prefab.name + "_Held";
            _heldInstance.transform.localScale = heldLocalScale;

            StripStationComponents(_heldInstance);
            ApplyAxeTag(_heldInstance);

            var grab = _heldInstance.GetComponent<XRGrabInteractable>();
            var rb = _heldInstance.GetComponent<Rigidbody>();
            if (grab == null)
            {
                Debug.LogWarning("AxeStationButton: held prefab 缺少 XRGrabInteractable。", this);
                DestroyHeldAxe();
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

            var toolGrab = _heldInstance.GetComponent<ToolGrabController>();
            if (toolGrab != null)
                toolGrab.enabled = true;

            yield return null;
            if (spawnAttachDelay > 0f)
                yield return new WaitForSeconds(spawnAttachDelay);

            if (_heldInstance == null || grab == null || rightHandInteractor == null)
                yield break;

            // 強制上手時改 Sticky，否則 StateChange 在鬆開 Grip 後會立刻掉落
            if (rightHandInteractor is XRBaseInputInteractor inputInteractor)
                inputInteractor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Sticky;

            if (!grab.isSelected)
                manager.SelectEnter((IXRSelectInteractor)rightHandInteractor, (IXRSelectInteractable)grab);

            if (!grab.isSelected)
                Debug.LogWarning("AxeStationButton: 自動上手失敗，請檢查 Interaction Layer。", this);
        }
        finally
        {
            _busy = false;
        }
    }

    void DestroyHeldAxe()
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

    /// <summary>手上斧頭不再是展示品或按鈕站。</summary>
    void StripStationComponents(GameObject instance)
    {
        var heldFloating = instance.GetComponent<ToolFloatingDisplay>();
        if (heldFloating != null)
        {
            heldFloating.enabled = false;
            Destroy(heldFloating);
        }

        var heldRotate = instance.GetComponent<RotateY>();
        if (heldRotate != null)
        {
            heldRotate.enabled = false;
            Destroy(heldRotate);
        }

        var heldStation = instance.GetComponent<AxeStationButton>();
        if (heldStation != null)
        {
            heldStation.enabled = false;
            Destroy(heldStation);
        }

        var heldSimple = instance.GetComponent<XRSimpleInteractable>();
        if (heldSimple != null)
        {
            heldSimple.enabled = false;
            Destroy(heldSimple);
        }
    }

    void ApplyAxeTag(GameObject instance)
    {
        if (string.IsNullOrEmpty(heldAxeTag))
            return;

        try
        {
            instance.tag = heldAxeTag;
            foreach (var col in instance.GetComponentsInChildren<Collider>(true))
            {
                if (col != null)
                    col.gameObject.tag = heldAxeTag;
            }
        }
        catch (UnityException)
        {
            // Tag 尚未在 TagManager 建立
            Debug.LogWarning($"AxeStationButton: 專案沒有 Tag「{heldAxeTag}」，砍樹判定會失效。", this);
        }
    }

    void ConvertToButtonStation()
    {
        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.enabled = false;

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

        // 站點自己不能砍樹，碰撞體只留給指向選取用
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col != null)
                col.isTrigger = false;
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
        _pulseLight.color = new Color(1f, 0.85f, 0.5f, 1f);
        _pulseLight.range = lightRange;
        _pulseLight.intensity = lightIntensity;
        _pulseLight.shadows = LightShadows.None;
    }

    GameObject ResolveHeldPrefab()
    {
        if (heldAxePrefab != null)
            return heldAxePrefab;

#if UNITY_EDITOR
        var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
        if (source != null)
        {
            heldAxePrefab = source;
            return heldAxePrefab;
        }
#endif

        // 最後備援：複製站點自身（稍後會拆掉按鈕元件）
        return gameObject;
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
