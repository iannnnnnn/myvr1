using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 掛在可互動物件上：被選取（點擊/Trigger）時呼叫共用的 UIManager 彈窗，
/// 顯示這個物件自己的標題／說明／圖片／語音。
/// 彈窗會跟隨此物件移動。
/// 透過 selectEntered 事件訂閱，OnDisable 必須 -=，避免事件殭屍參考。
/// </summary>
public class InteractableInfoPopup : MonoBehaviour
{
    [Header("Popup Content")]
    [SerializeField] string infoTitle;
    [TextArea(2, 6)]
    [SerializeField] string infoContent;
    [Tooltip("第 1 頁 PNG（介紹內容已畫在圖裡時，只需指定圖片）")]
    [SerializeField] Sprite infoImage;
    [Tooltip("第 2 頁 PNG，可留空")]
    [SerializeField] Sprite infoImagePage2;
    [SerializeField] AudioClip infoAudioClip;

    public enum PopupAnchorMode
    {
        BoundsBottom,
        BoundsCenter,
        BoundsTop,
        TransformOrigin,
    }

    [Header("Follow")]
    [Tooltip("世界公尺：x 為玩家視角右方、y 為離基準點的高度、z 為朝玩家拉近的距離")]
    [SerializeField] Vector3 popupOffset = new Vector3(0f, 1.6f, 0f);
    [Tooltip("計算 y 高度時的基準點；BoundsBottom 代表模型底部（離地高度）")]
    [SerializeField] PopupAnchorMode anchorMode = PopupAnchorMode.BoundsBottom;
    [Tooltip("同一物種散佈成多叢時，卡片只對準射線瞄到的那一叢，而不是全部的中心")]
    [SerializeField] bool anchorToNearestChild;

    IXRSelectInteractable _interactable;
    Transform _anchor;

    void Awake()
    {
        _interactable = GetComponent<IXRSelectInteractable>();
        if (_interactable == null)
            Debug.LogWarning($"InteractableInfoPopup on {name} 找不到 IXRSelectInteractable，需要搭配 XR Grab/Simple Interactable 使用。", this);

        if (GetComponent<InteractableRayDebugLog>() == null)
            gameObject.AddComponent<InteractableRayDebugLog>();
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
        if (UIManager.Instance == null)
        {
            Debug.LogWarning("場景中找不到 UIManager，無法顯示資訊框。", this);
            return;
        }

        UIManager.Instance.ShowPopup(infoTitle, infoContent, infoImage, infoImagePage2, infoAudioClip, ResolveAnchor(args.interactorObject?.transform), popupOffset);
    }

    /// <summary>
    /// 產生一個跟著物件移動的基準點，避免群組物件縮放後 transform 原點偏離可見模型。
    /// </summary>
    Transform ResolveAnchor(Transform interactor)
    {
        if (anchorMode == PopupAnchorMode.TransformOrigin)
            return transform;

        if (!TryGetAnchorBounds(interactor, out Bounds bounds))
            return transform;

        float y = anchorMode switch
        {
            PopupAnchorMode.BoundsTop => bounds.max.y,
            PopupAnchorMode.BoundsCenter => bounds.center.y,
            _ => bounds.min.y,
        };

        if (_anchor == null)
        {
            var anchorObject = new GameObject("PopupAnchor");
            anchorObject.transform.SetParent(transform, false);
            _anchor = anchorObject.transform;
        }

        _anchor.position = new Vector3(bounds.center.x, y, bounds.center.z);
        return _anchor;
    }

    bool TryGetAnchorBounds(Transform interactor, out Bounds bounds)
    {
        if (anchorToNearestChild && TryGetAimedChildBounds(interactor, out bounds))
            return true;

        return TryEncapsulateRenderers(transform, out bounds);
    }

    /// <summary>挑出射線最接近的那一叢，讓卡片停在玩家真正指到的位置。</summary>
    bool TryGetAimedChildBounds(Transform interactor, out Bounds bounds)
    {
        bounds = default;

        Vector3 origin = interactor != null ? interactor.position
            : Camera.main != null ? Camera.main.transform.position
            : transform.position;
        Vector3 direction = interactor != null ? interactor.forward : Vector3.zero;
        bool useRay = direction.sqrMagnitude > 0.0001f;
        if (useRay)
            direction.Normalize();

        bool found = false;
        float best = float.MaxValue;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == _anchor || !TryEncapsulateRenderers(child, out Bounds childBounds))
                continue;

            float score = useRay
                ? DistanceToRay(childBounds.center, origin, direction)
                : Vector3.Distance(childBounds.center, origin);

            if (score >= best)
                continue;

            best = score;
            bounds = childBounds;
            found = true;
        }

        return found;
    }

    static bool TryEncapsulateRenderers(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderers[i].bounds);
        }

        return hasBounds;
    }

    static float DistanceToRay(Vector3 point, Vector3 origin, Vector3 direction)
    {
        float along = Mathf.Max(0f, Vector3.Dot(point - origin, direction));
        return Vector3.Distance(point, origin + direction * along);
    }

    public void Configure(string title, string content, Sprite imagePage1, Sprite imagePage2, AudioClip audioClip, Vector3 followOffset, bool anchorToAimedGroupMember = false)
    {
        infoTitle = title;
        infoContent = content;
        infoImage = imagePage1;
        infoImagePage2 = imagePage2;
        infoAudioClip = audioClip;
        popupOffset = followOffset;
        anchorToNearestChild = anchorToAimedGroupMember;
    }
}
