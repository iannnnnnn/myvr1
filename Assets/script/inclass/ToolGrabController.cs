using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// 通用工具抓取控制：抓取時停止展示，放開 Grip/Select 時正常放下。
/// 也可用 R 鍵或指定的 Input Action 強制放下。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
public class ToolGrabController : MonoBehaviour
{
    [Header("工具元件")]
    [FormerlySerializedAs("axeGrabInteractable")]
    [SerializeField] XRGrabInteractable grabInteractable;

    [FormerlySerializedAs("axeRigidbody")]
    [SerializeField] Rigidbody toolRigidbody;

    [FormerlySerializedAs("axeFloatingRotation")]
    [SerializeField] ToolFloatingDisplay floatingDisplay;

    [Header("右手握持")]
    [Tooltip("只允許右手 Interactor 抓取")]
    [SerializeField] bool requireRightHand = true;

    [Tooltip("工具抓在右手後，相對右手的位置")]
    [SerializeField] Vector3 heldLocalPosition = new Vector3(0.02f, -0.04f, 0.08f);

    [Tooltip("工具抓在右手後，相對右手的旋轉")]
    [SerializeField] Vector3 heldLocalEulerAngles = new Vector3(0f, 90f, -25f);

    [Tooltip("抓取後鎖在右手，停用 XR 拖拉位移／旋轉；Activate 事件仍可使用")]
    [SerializeField] bool lockToRightHandWhileSelected = true;

    [Header("放下設定")]
    [Tooltip("VR 可指定 Secondary Button 等 Input Action；不要使用 Activate，以免和澆水衝突")]
    [SerializeField] InputActionReference releaseAction;

    [SerializeField] bool useGravityWhenReleased = true;
    [SerializeField] bool restartDisplayWhenReleased;

    bool _releaseActionEnabledHere;
    Transform _heldHand;
    bool _previousTrackPosition;
    bool _previousTrackRotation;
    bool _previousTrackScale;

    protected virtual void Awake()
    {
        CacheComponents();
        ConfigureAttachPose();
    }

    protected virtual void OnEnable()
    {
        CacheComponents();
        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);

        if (releaseAction != null && releaseAction.action != null)
        {
            _releaseActionEnabledHere = !releaseAction.action.enabled;
            if (_releaseActionEnabledHere)
                releaseAction.action.Enable();
            releaseAction.action.performed += OnReleasePerformed;
        }
    }

    protected virtual void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        if (releaseAction != null && releaseAction.action != null)
        {
            releaseAction.action.performed -= OnReleasePerformed;
            if (_releaseActionEnabledHere)
                releaseAction.action.Disable();
        }
    }

    protected virtual void Update()
    {
        if (grabInteractable != null &&
            grabInteractable.isSelected &&
            Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            ReleaseTool();
        }
    }

    protected virtual void LateUpdate()
    {
        if (!lockToRightHandWhileSelected ||
            _heldHand == null ||
            grabInteractable == null ||
            !grabInteractable.isSelected)
        {
            return;
        }

        transform.SetPositionAndRotation(
            _heldHand.TransformPoint(heldLocalPosition),
            _heldHand.rotation * Quaternion.Euler(heldLocalEulerAngles));
    }

    void CacheComponents()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();
        if (toolRigidbody == null)
            toolRigidbody = GetComponent<Rigidbody>();
        if (floatingDisplay == null)
            floatingDisplay = GetComponent<ToolFloatingDisplay>();
    }

    void ConfigureAttachPose()
    {
        var attach = transform.Find("ToolAttach");
        if (attach == null)
        {
            var go = new GameObject("ToolAttach");
            attach = go.transform;
            attach.SetParent(transform, false);
        }

        Quaternion desiredRotation = Quaternion.Euler(heldLocalEulerAngles);
        Quaternion inverseRotation = Quaternion.Inverse(desiredRotation);
        attach.localRotation = inverseRotation;
        attach.localPosition = -(inverseRotation * heldLocalPosition);

        grabInteractable.attachTransform = attach;
        grabInteractable.useDynamicAttach = false;
        grabInteractable.matchAttachPosition = true;
        grabInteractable.matchAttachRotation = true;
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (requireRightHand &&
            args.interactorObject.handedness != InteractorHandedness.Right)
        {
            var manager = grabInteractable.interactionManager;
            if (manager != null)
                manager.SelectExit(args.interactorObject, grabInteractable);
            return;
        }

        floatingDisplay?.StopDisplayEffect();
        _heldHand = FindRightHandAnchor(args.interactorObject.transform);

        if (toolRigidbody != null)
            toolRigidbody.useGravity = false;

        if (lockToRightHandWhileSelected)
        {
            _previousTrackPosition = grabInteractable.trackPosition;
            _previousTrackRotation = grabInteractable.trackRotation;
            _previousTrackScale = grabInteractable.trackScale;
            grabInteractable.trackPosition = false;
            grabInteractable.trackRotation = false;
            grabInteractable.trackScale = false;
        }
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (lockToRightHandWhileSelected)
        {
            grabInteractable.trackPosition = _previousTrackPosition;
            grabInteractable.trackRotation = _previousTrackRotation;
            grabInteractable.trackScale = _previousTrackScale;
        }
        _heldHand = null;

        if (toolRigidbody != null)
            toolRigidbody.useGravity = useGravityWhenReleased;

        if (restartDisplayWhenReleased)
            floatingDisplay?.RestartDisplayEffect();
    }

    static Transform FindRightHandAnchor(Transform interactor)
    {
        Transform current = interactor;
        while (current != null)
        {
            if (current.name.Contains("Right Controller"))
                return current;
            current = current.parent;
        }

        return interactor;
    }

    void OnReleasePerformed(InputAction.CallbackContext context)
    {
        ReleaseTool();
    }

    /// <summary>強制解除所有 XR 選取，讓工具從手上放下。</summary>
    public void ReleaseTool()
    {
        if (grabInteractable == null || !grabInteractable.isSelected)
            return;

        var manager = grabInteractable.interactionManager;
        if (manager == null)
            return;

        var selecting = grabInteractable.interactorsSelecting;
        for (int i = selecting.Count - 1; i >= 0; i--)
            manager.SelectExit(selecting[i], grabInteractable);
    }
}
