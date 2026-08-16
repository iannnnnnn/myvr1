using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 實驗用：半透明球跟著相機，從內部看可模擬簡易霧霾。
/// 用法：掛在任意物件上 → Play；或手動指定 Sphere / Camera。
/// </summary>
[DisallowMultipleComponent]
public class CameraHazeSphere : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] Transform hazeSphere;

    [Header("外觀")]
    [SerializeField] Color hazeColor = new Color(0.65f, 0.62f, 0.55f, 0.18f);
    [SerializeField] float radius = 8f;
    [Tooltip("球心相對相機的偏移（通常保持 0）")]
    [SerializeField] Vector3 localOffset = Vector3.zero;

    [Header("行為")]
    [SerializeField] bool createSphereIfMissing = true;
    [SerializeField] bool followRotation = true;
    [SerializeField] bool disableColliders = true;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int CullId = Shader.PropertyToID("_Cull");
    static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
    static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

    Material _runtimeMaterial;
    Renderer _renderer;

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (hazeSphere == null && createSphereIfMissing)
            hazeSphere = CreateHazeSphere().transform;

        if (hazeSphere != null)
        {
            _renderer = hazeSphere.GetComponent<Renderer>();
            ApplyAppearance();
            if (disableColliders)
            {
                foreach (var col in hazeSphere.GetComponentsInChildren<Collider>())
                    col.enabled = false;
            }
        }
    }

    void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null || hazeSphere == null)
            return;

        Transform cam = targetCamera.transform;
        hazeSphere.position = cam.TransformPoint(localOffset);
        if (followRotation)
            hazeSphere.rotation = cam.rotation;

        hazeSphere.localScale = Vector3.one * (radius * 2f);
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
            return;
        ApplyAppearance();
    }

    void OnDestroy()
    {
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
    }

    public void SetHazeColor(Color color)
    {
        hazeColor = color;
        ApplyAppearance();
    }

    public void SetRadius(float value)
    {
        radius = Mathf.Max(0.1f, value);
    }

    void ApplyAppearance()
    {
        if (hazeSphere == null)
            return;

        if (_renderer == null)
            _renderer = hazeSphere.GetComponent<Renderer>();
        if (_renderer == null)
            return;

        if (_runtimeMaterial == null)
            _runtimeMaterial = CreateHazeMaterial();

        if (_renderer.sharedMaterial != _runtimeMaterial)
            _renderer.sharedMaterial = _runtimeMaterial;

        if (_runtimeMaterial.HasProperty(BaseColorId))
            _runtimeMaterial.SetColor(BaseColorId, hazeColor);
        else if (_runtimeMaterial.HasProperty(ColorId))
            _runtimeMaterial.SetColor(ColorId, hazeColor);

        hazeSphere.localScale = Vector3.one * (radius * 2f);
    }

    GameObject CreateHazeSphere()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "HazeSphere";
        go.transform.SetParent(transform, false);

        var col = go.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        return go;
    }

    Material CreateHazeMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        var mat = new Material(shader)
        {
            name = "CameraHaze_Runtime",
            hideFlags = HideFlags.HideAndDontSave
        };

        // 透明 + 正面剔除：從球內才看得到霧
        if (mat.HasProperty(SurfaceId))
            mat.SetFloat(SurfaceId, 1f); // Transparent
        if (mat.HasProperty(CullId))
            mat.SetFloat(CullId, (float)CullMode.Front);
        if (mat.HasProperty(ZWriteId))
            mat.SetFloat(ZWriteId, 0f);
        if (mat.HasProperty(SrcBlendId))
            mat.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
        if (mat.HasProperty(DstBlendId))
            mat.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);

        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetColor(BaseColorId, hazeColor);

        return mat;
    }
}
