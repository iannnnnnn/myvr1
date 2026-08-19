using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

/// <summary>
/// 掛在射線互動器上，輸出第一個物理命中的 Collider，
/// 方便分辨目前打到的是地形、樹、還是可互動物件。
/// </summary>
[DisallowMultipleComponent]
public class PhysicsRayDebug : MonoBehaviour
{
    [SerializeField] Transform castOrigin;
    [SerializeField] float castDistance = 10f;
    [SerializeField] float sphereCastRadius = 0.1f;
    [SerializeField] LayerMask physicsMask = Physics.DefaultRaycastLayers;
    [SerializeField] QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
    [SerializeField] bool logHitExit = true;
    [SerializeField] bool drawDebugRay = true;

    CurveInteractionCaster _caster;
    Collider _lastCollider;

    void Awake()
    {
        _caster = GetComponent<CurveInteractionCaster>();
        if (castOrigin == null)
            castOrigin = transform;

        SyncFromCaster();
    }

    void Update()
    {
        SyncFromCaster();

        if (castOrigin == null)
            return;

        Vector3 origin = castOrigin.position;
        Vector3 direction = castOrigin.forward;

        if (drawDebugRay)
            Debug.DrawRay(origin, direction * castDistance, Color.magenta);

        bool hasHit = Physics.SphereCast(
            origin,
            sphereCastRadius,
            direction,
            out RaycastHit hit,
            castDistance,
            physicsMask,
            triggerInteraction);

        var currentCollider = hasHit ? hit.collider : null;
        if (currentCollider == _lastCollider)
            return;

        if (currentCollider != null)
        {
            string rootName = currentCollider.transform.root != null ? currentCollider.transform.root.name : currentCollider.name;
            Debug.Log(
                $"[Physics Ray] 命中 → {currentCollider.name} | root: {rootName} | layer: {LayerMask.LayerToName(currentCollider.gameObject.layer)} | distance: {hit.distance:F2}",
                currentCollider);
        }
        else if (logHitExit && _lastCollider != null)
        {
            Debug.Log($"[Physics Ray] 離開 → {_lastCollider.name}", this);
        }

        _lastCollider = currentCollider;
    }

    void SyncFromCaster()
    {
        if (_caster == null)
            return;

        castDistance = _caster.castDistance;
    }

    public void Configure(Transform origin, float radius, LayerMask mask, QueryTriggerInteraction triggerMode)
    {
        castOrigin = origin;
        sphereCastRadius = radius;
        physicsMask = mask;
        triggerInteraction = triggerMode;
    }
}
