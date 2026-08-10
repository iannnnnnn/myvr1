using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 澆水壺：握持後按 Activate（Trigger）從壺嘴短距偵測並對 WaterableTree 澆水。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
[ExecuteAlways]
public class WateringCan : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("備援：僅在沒有 Visual 子物件時，Awake 才實例化。建議用選單嵌進 Prefab。")]
    [SerializeField] GameObject visualPrefab;

    [Header("Spout")]
    [SerializeField] Transform spout;
    [SerializeField] Vector3 spoutLocalPosition = new Vector3(0.15f, 0.1f, 0.35f);
    [SerializeField] ParticleSystem waterParticles;

    [Header("Cast")]
    [SerializeField] float castRadius = 0.3f;
    [SerializeField] float castDistance = 1.25f;
    [SerializeField] LayerMask hitMask = ~0;

    [Header("Watering")]
    [SerializeField] float waterPerSecond = 0.75f;
    [SerializeField] float tickInterval = 0.2f;

    XRGrabInteractable _grab;
    bool _isWatering;
    float _nextTickTime;
    readonly RaycastHit[] _hits = new RaycastHit[8];
    readonly Collider[] _overlap = new Collider[8];

    void OnEnable()
    {
        if (_grab == null)
            _grab = GetComponent<XRGrabInteractable>();

        EnsureVisual();
        EnsureSpout();

        if (!Application.isPlaying)
            return;

        EnsureWaterParticles();
        SetParticlesPlaying(false);

        if (_grab == null)
            return;

        _grab.activated.AddListener(OnActivated);
        _grab.deactivated.AddListener(OnDeactivated);
        _grab.selectExited.AddListener(OnSelectExited);
    }

    void Awake()
    {
        if (!Application.isPlaying)
            return;

        _grab = GetComponent<XRGrabInteractable>();
        EnsureVisual();
        EnsureSpout();
        EnsureWaterParticles();
        SetParticlesPlaying(false);
    }

    void EnsureVisual()
    {
        var existing = transform.Find("Visual");
        if (existing != null)
        {
            StripGrabConflicts(existing.gameObject);
            return;
        }

        if (visualPrefab == null)
            return;

        GameObject visual;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            visual = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(visualPrefab, transform);
        else
#endif
            visual = Instantiate(visualPrefab, transform);

        visual.name = "Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        StripGrabConflicts(visual);
    }

    static void StripGrabConflicts(GameObject visual)
    {
        var rb = visual.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (Application.isPlaying)
                Object.Destroy(rb);
            else
                Object.DestroyImmediate(rb);
        }

        foreach (var col in visual.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying)
                col.enabled = false;
            else
                Object.DestroyImmediate(col);
        }
    }

    void EnsureSpout()
    {
        if (spout != null)
            return;

        var existing = transform.Find("Spout");
        if (existing != null)
        {
            spout = existing;
            return;
        }

        var go = new GameObject("Spout");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = spoutLocalPosition;
        go.transform.localRotation = Quaternion.identity;
        spout = go.transform;
    }

    void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (_grab == null)
            return;

        _grab.activated.RemoveListener(OnActivated);
        _grab.deactivated.RemoveListener(OnDeactivated);
        _grab.selectExited.RemoveListener(OnSelectExited);
        StopWatering();
    }

    void Update()
    {
        if (!Application.isPlaying || !_isWatering)
            return;

        if (Time.time < _nextTickTime)
            return;

        _nextTickTime = Time.time + Mathf.Max(0.05f, tickInterval);
        float amount = waterPerSecond * tickInterval;
        TryWater(amount);
    }

    void OnActivated(ActivateEventArgs args)
    {
        if (!_grab.isSelected)
            return;

        _isWatering = true;
        _nextTickTime = Time.time;
        SetParticlesPlaying(true);
    }

    void OnDeactivated(DeactivateEventArgs args) => StopWatering();

    void OnSelectExited(SelectExitEventArgs args) => StopWatering();

    void StopWatering()
    {
        _isWatering = false;
        SetParticlesPlaying(false);
    }

    void TryWater(float amount)
    {
        Vector3 origin = spout.position;
        Vector3 direction = spout.forward;
        WaterableTree best = null;
        float bestDist = float.MaxValue;

        int castCount = Physics.SphereCastNonAlloc(
            origin,
            castRadius,
            direction,
            _hits,
            castDistance,
            hitMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < castCount; i++)
            ConsiderHit(_hits[i].collider, _hits[i].distance, ref best, ref bestDist);

        // 壺嘴已在樹 Collider 內時 SphereCast 會漏掉，用 Overlap 補上
        Vector3 overlapCenter = origin + direction * (castDistance * 0.5f);
        float overlapRadius = castRadius + castDistance * 0.5f;
        int overlapCount = Physics.OverlapSphereNonAlloc(
            overlapCenter,
            overlapRadius,
            _overlap,
            hitMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < overlapCount; i++)
        {
            var col = _overlap[i];
            if (col == null)
                continue;
            float dist = Vector3.Distance(origin, col.ClosestPoint(origin));
            ConsiderHit(col, dist, ref best, ref bestDist);
        }

        if (best != null)
            best.AddWater(amount);
    }

    void ConsiderHit(Collider col, float distance, ref WaterableTree best, ref float bestDist)
    {
        if (col == null)
            return;
        if (col.transform.IsChildOf(transform))
            return;

        var tree = col.GetComponentInParent<WaterableTree>();
        if (tree == null)
            return;

        if (distance < bestDist)
        {
            bestDist = distance;
            best = tree;
        }
    }

    void EnsureWaterParticles()
    {
        if (waterParticles != null)
            return;

        var go = new GameObject("WaterSpray");
        go.transform.SetParent(spout, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        waterParticles = go.AddComponent<ParticleSystem>();
        var main = waterParticles.main;
        main.loop = true;
        main.startLifetime = 0.45f;
        main.startSpeed = 2.5f;
        main.startSize = 0.04f;
        main.startColor = new Color(0.55f, 0.75f, 1f, 0.85f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        main.playOnAwake = false;

        var emission = waterParticles.emission;
        emission.rateOverTime = 40f;

        var shape = waterParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.02f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    void SetParticlesPlaying(bool play)
    {
        if (waterParticles == null)
            return;

        if (play)
        {
            if (!waterParticles.isPlaying)
                waterParticles.Play(true);
        }
        else if (waterParticles.isPlaying)
        {
            waterParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Transform t = spout != null ? spout : transform;
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.35f);
        Vector3 end = t.position + t.forward * castDistance;
        Gizmos.DrawWireSphere(t.position, castRadius);
        Gizmos.DrawWireSphere(end, castRadius);
        Gizmos.DrawLine(t.position, end);
    }

    void OnValidate()
    {
        castRadius = Mathf.Max(0.05f, castRadius);
        castDistance = Mathf.Max(0.1f, castDistance);
        waterPerSecond = Mathf.Max(0.01f, waterPerSecond);
        tickInterval = Mathf.Max(0.05f, tickInterval);
    }
#endif
}
