using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 讓可互動物件產生呼吸式提示效果，提示玩家「這裡可以點選」。
/// 材質有 Emission 輸入時脈動發光；沒有時（例如 Skog 樹木的自訂 Shader Graph）
/// 改為脈動染色，讓提示仍然看得見。
/// 修改一律透過 MaterialPropertyBlock，材質探測只在初始化時做一次。
/// 被選取（點擊／Trigger）後預設關閉提示。
/// </summary>
[DisallowMultipleComponent]
public class InteractableGlowHint : MonoBehaviour
{
    static readonly string[] EmissionPropertyNames =
    {
        "_EmissionColor",
        "_EmissiveColor",
    };

    /// <summary>
    /// 沒有 Emission 時的替代目標，取用材質實際擁有的那些。
    /// _RColor / _GColor / _BColor 是 Skog 樹木 Shader 的葉、樹皮、苔蘚染色欄位。
    /// </summary>
    static readonly string[] TintPropertyNames =
    {
        "_BaseColor",
        "_Color",
        "_RColor",
        "_GColor",
        "_BColor",
    };

    const string EmissionKeyword = "_EMISSION";

    [SerializeField] Renderer[] targetRenderers;

    [Header("Glow Color")]
    [ColorUsage(true, true)]
    [SerializeField] Color emissionColor = Color.cyan;

    [Header("Pulse")]
    [SerializeField] float pulseSpeed = 2f;
    [SerializeField] float minIntensity = 0.2f;
    [SerializeField] float maxIntensity = 2.5f;

    [Header("Fallback Tint")]
    [Tooltip("材質沒有 Emission 時的染色強度，0 為完全不染色、1 為完全蓋成提示色")]
    [Range(0f, 1f)]
    [SerializeField] float tintStrength = 0.6f;

    [Header("State")]
    [SerializeField] bool glowing = true;
    [Tooltip("玩家點擊／Trigger 選取後停止發光")]
    [SerializeField] bool stopOnSelect = true;

    MaterialPropertyBlock _block;
    IXRSelectInteractable _interactable;
    GlowTarget[] _targets;

    class GlowTarget
    {
        public Renderer renderer;
        public bool useEmission;
        public int[] propertyIds;
        public Color[] originalColors;
    }

    void Awake()
    {
        _block = new MaterialPropertyBlock();

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>();

        _interactable = GetComponent<IXRSelectInteractable>();
        BuildTargets();
    }

    void OnEnable()
    {
        if (_interactable != null)
            _interactable.selectEntered.AddListener(OnSelectEntered);
    }

    void OnDisable()
    {
        if (_interactable != null)
            _interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (stopOnSelect)
            SetGlowing(false);
    }

    void Update()
    {
        if (!glowing)
            return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        ApplyGlow(t);
    }

    /// <summary>外部呼叫：例如玩家點選後關閉發光提示。</summary>
    public void SetGlowing(bool on)
    {
        glowing = on;
        if (!on)
            ClearGlow();
    }

    public void Configure(Renderer[] renderers, Color color, float speed, float min, float max, bool startGlowing = true, bool stopAfterSelect = true)
    {
        targetRenderers = renderers;
        emissionColor = color;
        pulseSpeed = speed;
        minIntensity = min;
        maxIntensity = max;
        glowing = startGlowing;
        stopOnSelect = stopAfterSelect;

        if (_block == null)
            _block = new MaterialPropertyBlock();

        BuildTargets();

        if (!glowing)
            ClearGlow();
    }

    void ApplyGlow(float t)
    {
        if (_targets == null)
            return;

        Color emissive = emissionColor * Mathf.Lerp(minIntensity, maxIntensity, t);
        float tintAmount = t * tintStrength;

        for (int i = 0; i < _targets.Length; i++)
        {
            var target = _targets[i];
            if (target.renderer == null)
                continue;

            target.renderer.GetPropertyBlock(_block);

            for (int j = 0; j < target.propertyIds.Length; j++)
            {
                if (target.useEmission)
                {
                    _block.SetColor(target.propertyIds[j], emissive);
                    continue;
                }

                Color original = target.originalColors[j];
                Color tinted = Color.Lerp(original, emissionColor, tintAmount);
                tinted.a = original.a;
                _block.SetColor(target.propertyIds[j], tinted);
            }

            target.renderer.SetPropertyBlock(_block);
        }
    }

    void ClearGlow()
    {
        if (_targets == null || _block == null)
            return;

        for (int i = 0; i < _targets.Length; i++)
        {
            var target = _targets[i];
            if (target.renderer == null)
                continue;

            target.renderer.GetPropertyBlock(_block);

            for (int j = 0; j < target.propertyIds.Length; j++)
                _block.SetColor(target.propertyIds[j], target.useEmission ? Color.black : target.originalColors[j]);

            target.renderer.SetPropertyBlock(_block);
        }
    }

    void BuildTargets()
    {
        _targets = null;

        if (targetRenderers == null || targetRenderers.Length == 0)
            return;

        var targets = new List<GlowTarget>(targetRenderers.Length);
        var names = new List<string>();

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var renderer = targetRenderers[i];
            if (renderer == null)
                continue;

            var materials = renderer.sharedMaterials;
            names.Clear();
            CollectColorProperties(materials, EmissionPropertyNames, names);

            bool useEmission = names.Count > 0;
            if (!useEmission)
                CollectColorProperties(materials, TintPropertyNames, names);

            if (names.Count == 0)
            {
                Debug.LogWarning($"InteractableGlowHint：{renderer.name} 的材質沒有可驅動的 Emission 或色彩屬性，發光提示對它無效。", renderer);
                continue;
            }

            var target = new GlowTarget
            {
                renderer = renderer,
                useEmission = useEmission,
                propertyIds = new int[names.Count],
            };

            if (!useEmission)
                target.originalColors = new Color[names.Count];

            for (int j = 0; j < names.Count; j++)
            {
                target.propertyIds[j] = Shader.PropertyToID(names[j]);
                if (!useEmission)
                    target.originalColors[j] = ReadColor(materials, names[j], target.propertyIds[j]);
            }

            if (useEmission)
                EnableEmissionKeyword(renderer);

            targets.Add(target);
        }

        _targets = targets.ToArray();
    }

    static void CollectColorProperties(Material[] materials, string[] candidates, List<string> results)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            if (HasColorProperty(materials, candidates[i]))
                results.Add(candidates[i]);
        }
    }

    static bool HasColorProperty(Material[] materials, string propertyName)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            var shader = materials[i] != null ? materials[i].shader : null;
            if (shader == null)
                continue;

            int index = shader.FindPropertyIndex(propertyName);
            if (index >= 0 && shader.GetPropertyType(index) == ShaderPropertyType.Color)
                return true;
        }

        return false;
    }

    static Color ReadColor(Material[] materials, string propertyName, int propertyId)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            var material = materials[i];
            if (material == null)
                continue;

            var shader = material.shader;
            if (shader == null)
                continue;

            int index = shader.FindPropertyIndex(propertyName);
            if (index >= 0 && shader.GetPropertyType(index) == ShaderPropertyType.Color)
                return material.GetColor(propertyId);
        }

        return Color.white;
    }

    /// <summary>URP / Standard 需要材質開啟 _EMISSION 關鍵字，PropertyBlock 的值才會生效。</summary>
    static void EnableEmissionKeyword(Renderer renderer)
    {
        var materials = renderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null)
                materials[i].EnableKeyword(EmissionKeyword);
        }
    }
}
