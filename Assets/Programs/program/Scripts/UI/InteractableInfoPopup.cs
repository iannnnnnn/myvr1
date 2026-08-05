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
    [SerializeField] Sprite infoImage;
    [SerializeField] AudioClip infoAudioClip;

    [Header("Follow")]
    [Tooltip("彈窗相對此物件的本地偏移（預設在頭頂上方）")]
    [SerializeField] Vector3 popupOffset = new Vector3(0f, 1.6f, 0f);

    IXRSelectInteractable _interactable;

    void Awake()
    {
        _interactable = GetComponent<IXRSelectInteractable>();
        if (_interactable == null)
            Debug.LogWarning($"InteractableInfoPopup on {name} 找不到 IXRSelectInteractable，需要搭配 XR Grab/Simple Interactable 使用。", this);
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

        UIManager.Instance.ShowPopup(infoTitle, infoContent, infoImage, infoAudioClip, transform, popupOffset);
    }
}
