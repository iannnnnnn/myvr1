using UnityEngine;

/// <summary>
/// 讓可互動物件的材質產生呼吸式發光（Emission 強度隨時間脈動），
/// 提示玩家「這裡可以點選」。
/// 使用 MaterialPropertyBlock 修改，不會複製材質、不產生逐幀 GC。
/// 材質需先開啟 Emission（Standard / URP Lit 皆可）。
/// </summary>
[DisallowMultipleComponent]
public class InteractableGlowHint : MonoBehaviour
{
    [SerializeField] Renderer[] targetRenderers;

    [Header("Glow Color")]
    [ColorUsage(true, true)]
    [SerializeField] Color emissionColor = Color.cyan;

    [Header("Pulse")]
    [SerializeField] float pulseSpeed = 2f;
    [SerializeField] float minIntensity = 0.2f;
    [SerializeField] float maxIntensity = 2.5f;

    [Header("State")]
    [SerializeField] bool glowing = true;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    MaterialPropertyBlock _block;

    void Awake()
    {
        _block = new MaterialPropertyBlock();

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        if (!glowing)
            return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
        ApplyEmission(emissionColor * intensity);
    }

    /// <summary>外部呼叫：例如玩家點選後關閉發光提示。</summary>
    public void SetGlowing(bool on)
    {
        glowing = on;
        if (!on)
            ApplyEmission(Color.black);
    }

    void ApplyEmission(Color color)
    {
        if (targetRenderers == null)
            return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(_block);
            _block.SetColor(EmissionColorId, color);
            r.SetPropertyBlock(_block);
        }
    }
}
