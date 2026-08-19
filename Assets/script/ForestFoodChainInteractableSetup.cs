using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class ForestFoodChainInteractableSetup : MonoBehaviour
{
    [Header("Groups")]
    [SerializeField] Transform groupA;
    [SerializeField] Sprite groupAPage1;
    [SerializeField] Sprite groupAPage2;
    [SerializeField] Vector3 groupAPopupOffset = new Vector3(0f, 0.6f, 0.8f);

    [SerializeField] Transform groupB;
    [SerializeField] Sprite groupBPage1;
    [SerializeField] Sprite groupBPage2;
    [SerializeField] Vector3 groupBPopupOffset = new Vector3(0f, 0.6f, 0.8f);

    [SerializeField] Transform groupC;
    [SerializeField] Sprite groupCPage1;
    [SerializeField] Sprite groupCPage2;
    [SerializeField] Vector3 groupCPopupOffset = new Vector3(0f, 0.6f, 0.8f);

    [Header("Glow")]
    [ColorUsage(true, true)]
    [SerializeField] Color glowColor = new Color(0.86773145f, 0.94716984f, 0.23411177f, 1f);
    [SerializeField] float pulseSpeed = 2f;
    [SerializeField] float minIntensity = 0.2f;
    [SerializeField] float maxIntensity = 2.5f;
    [SerializeField] bool startGlowing = true;
    [SerializeField] bool stopOnSelect = true;

    void Awake()
    {
        SetupGroup(groupA, groupAPage1, groupAPage2, groupAPopupOffset);
        SetupGroup(groupB, groupBPage1, groupBPage2, groupBPopupOffset);
        SetupGroup(groupC, groupCPage1, groupCPage2, groupCPopupOffset);
    }

    void SetupGroup(Transform target, Sprite page1, Sprite page2, Vector3 popupOffset)
    {
        if (target == null)
            return;

        var renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"ForestFoodChainInteractableSetup: {target.name} 沒有 Renderer，略過互動設定。", target);
            return;
        }

        var collider = GetOrAdd<BoxCollider>(target.gameObject);
        FitColliderToRenderers(target, renderers, collider);

        GetOrAdd<XRSimpleInteractable>(target.gameObject);

        var glow = GetOrAdd<InteractableGlowHint>(target.gameObject);
        glow.Configure(renderers, glowColor, pulseSpeed, minIntensity, maxIntensity, startGlowing, stopOnSelect);

        var popup = GetOrAdd<InteractableInfoPopup>(target.gameObject);
        popup.Configure(string.Empty, string.Empty, page1, page2, null, popupOffset, true);
    }

    static T GetOrAdd<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    static readonly Vector3 MaxColliderSize = new Vector3(15f, 15f, 15f);

    static void FitColliderToRenderers(Transform target, Renderer[] renderers, BoxCollider collider)
    {
        bool hasBounds = false;
        Vector3 min = default;
        Vector3 max = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            var bounds = renderer.bounds;
            Vector3 boundsMin = bounds.min;
            Vector3 boundsMax = bounds.max;
            Vector3[] corners =
            {
                new Vector3(boundsMin.x, boundsMin.y, boundsMin.z),
                new Vector3(boundsMin.x, boundsMin.y, boundsMax.z),
                new Vector3(boundsMin.x, boundsMax.y, boundsMin.z),
                new Vector3(boundsMin.x, boundsMax.y, boundsMax.z),
                new Vector3(boundsMax.x, boundsMin.y, boundsMin.z),
                new Vector3(boundsMax.x, boundsMin.y, boundsMax.z),
                new Vector3(boundsMax.x, boundsMax.y, boundsMin.z),
                new Vector3(boundsMax.x, boundsMax.y, boundsMax.z)
            };

            for (int j = 0; j < corners.Length; j++)
            {
                Vector3 localPoint = target.InverseTransformPoint(corners[j]);
                if (!hasBounds)
                {
                    min = localPoint;
                    max = localPoint;
                    hasBounds = true;
                    continue;
                }

                min = Vector3.Min(min, localPoint);
                max = Vector3.Max(max, localPoint);
            }
        }

        if (!hasBounds)
            return;

        Vector3 size = max - min;
        size = Vector3.Min(size, MaxColliderSize);

        collider.center = (min + max) * 0.5f;
        collider.size = size;

        if (size != (max - min))
            Debug.LogWarning($"ForestFoodChainInteractableSetup: {target.name} 的 Collider 被限制為 {size}（原始 {max - min} 太大）", target);
    }
}
