using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// 澆水壺：拿取為開關（Grip 按一下拿起、再按一下放下）；
/// 澆水仍為按住 Activate（Trigger）噴水。
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
    readonly RaycastHit[] _hits = new RaycastHit[16];
    readonly Collider[] _overlap = new Collider[16];
    readonly Dictionary<XRBaseInputInteractor, XRBaseInputInteractor.InputTriggerType> _savedSelectTriggers
        = new Dictionary<XRBaseInputInteractor, XRBaseInputInteractor.InputTriggerType>();

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

        _grab.hoverEntered.AddListener(OnHoverEntered);
        _grab.hoverExited.AddListener(OnHoverExited);
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

        _grab.hoverEntered.RemoveListener(OnHoverEntered);
        _grab.hoverExited.RemoveListener(OnHoverExited);
        _grab.activated.RemoveListener(OnActivated);
        _grab.deactivated.RemoveListener(OnDeactivated);
        _grab.selectExited.RemoveListener(OnSelectExited);
        RestoreAllSelectTriggers();
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

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        // 只有指向／靠近這把壺時，暫時把該手的 Select 改成 Sticky（按一下拿、再按一下放）
        if (args.interactorObject is not XRBaseInputInteractor inputInteractor)
            return;

        if (!_savedSelectTriggers.ContainsKey(inputInteractor))
            _savedSelectTriggers[inputInteractor] = inputInteractor.selectActionTrigger;

        inputInteractor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Sticky;
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (args.interactorObject is not XRBaseInputInteractor inputInteractor)
            return;

        if (args.interactorObject is IXRSelectInteractor selectInteractor &&
            _grab != null &&
            _grab.interactorsSelecting.Contains(selectInteractor))
            return;

        RestoreSelectTrigger(inputInteractor);
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

    void OnSelectExited(SelectExitEventArgs args)
    {
        StopWatering();

        if (args.interactorObject is XRBaseInputInteractor inputInteractor)
            RestoreSelectTrigger(inputInteractor);
    }

    void StopWatering()
    {
        _isWatering = false;
        SetParticlesPlaying(false);
    }

    void RestoreSelectTrigger(XRBaseInputInteractor inputInteractor)
    {
        if (inputInteractor == null)
            return;

        if (_savedSelectTriggers.TryGetValue(inputInteractor, out var previous))
        {
            inputInteractor.selectActionTrigger = previous;
            _savedSelectTriggers.Remove(inputInteractor);
        }
    }

    void RestoreAllSelectTriggers()
    {
        foreach (var pair in _savedSelectTriggers)
        {
            if (pair.Key != null)
                pair.Key.selectActionTrigger = pair.Value;
        }

        _savedSelectTriggers.Clear();
    }

    void TryWater(float amount)
    {
        Vector3 origin = spout.position;
        Vector3 direction = spout.forward;
        PlantingZone bestZone = null;
        WaterableTree bestTree = null;
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
            ConsiderHit(_hits[i].collider, _hits[i].distance, ref bestZone, ref bestTree, ref bestDist);

        // 壺嘴已在 Collider 內時 SphereCast 會漏掉，用 Overlap 補上
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
            ConsiderHit(col, dist, ref bestZone, ref bestTree, ref bestDist);
        }

        // 優先整區澆水（一區多棵共用進度）
        if (bestZone != null)
            bestZone.AddWater(amount);
        else if (bestTree != null)
            bestTree.AddWater(amount);
    }

    void ConsiderHit(
        Collider col,
        float distance,
        ref PlantingZone bestZone,
        ref WaterableTree bestTree,
        ref float bestDist)
    {
        if (col == null)
            return;
        if (col.transform.IsChildOf(transform))
            return;

        var zone = col.GetComponentInParent<PlantingZone>();
        if (zone != null)
        {
            if (distance < bestDist)
            {
                bestDist = distance;
                bestZone = zone;
                bestTree = null;
            }
            return;
        }

        var tree = col.GetComponentInParent<WaterableTree>();
        if (tree == null)
            return;

        // 樹若在種植區內，改算該區
        zone = tree.GetComponentInParent<PlantingZone>();
        if (zone != null)
        {
            if (distance < bestDist)
            {
                bestDist = distance;
                bestZone = zone;
                bestTree = null;
            }
            return;
        }

        if (distance < bestDist)
        {
            bestDist = distance;
            bestZone = null;
            bestTree = tree;
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
