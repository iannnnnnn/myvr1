using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 掛在 XRSimpleInteractable 物件上，於 Console 輸出雷射 hover / Trigger 命中的目標名稱。
/// </summary>
[DisallowMultipleComponent]
public class InteractableRayDebugLog : MonoBehaviour
{
    [SerializeField] bool logHover = true;
    [SerializeField] bool logSelect = true;

    IXRSelectInteractable _select;
    IXRHoverInteractable _hover;

    void Awake()
    {
        _select = GetComponent<IXRSelectInteractable>();
        _hover = GetComponent<IXRHoverInteractable>();
    }

    void OnEnable()
    {
        if (_select != null && logSelect)
            _select.selectEntered.AddListener(OnSelectEntered);

        if (_hover != null && logHover)
        {
            _hover.hoverEntered.AddListener(OnHoverEntered);
            _hover.hoverExited.AddListener(OnHoverExited);
        }
    }

    void OnDisable()
    {
        if (_select != null && logSelect)
            _select.selectEntered.RemoveListener(OnSelectEntered);

        if (_hover != null && logHover)
        {
            _hover.hoverEntered.RemoveListener(OnHoverEntered);
            _hover.hoverExited.RemoveListener(OnHoverExited);
        }
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        Debug.Log($"[XR Ray] 指到 → {name}（{FormatInteractor(args.interactorObject)}）", this);
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        Debug.Log($"[XR Ray] 離開 → {name}（{FormatInteractor(args.interactorObject)}）", this);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        Debug.Log($"[XR Ray] Trigger 選取 → {name}（{FormatInteractor(args.interactorObject)}）", this);
    }

    static string FormatInteractor(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor)
    {
        if (interactor == null)
            return "未知 Interactor";

        if (interactor is Component component)
            return component.name;

        var t = interactor.transform;
        return t != null ? t.name : interactor.ToString();
    }
}
