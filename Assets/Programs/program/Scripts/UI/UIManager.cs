using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using TMPro;

/// <summary>
/// 單一共用 UI 彈窗：只負責圖文／語音呈現。
/// 若有 Info Image（介紹內容已畫在 PNG 裡），改以整張圖當世界空間卡片，
/// 隱藏標題／內文／深色面板，並把 PNG 黑底扣掉，讓卡片像場景裡的物件。
/// 支援兩頁 PNG：上一頁／下一頁按鈕，Trigger 在還有下一頁時先翻頁，最後一頁再關閉。
/// 連續觸發時 Stop + 覆蓋內容，不做 Close/Open，避免 Canvas 重建與語音重疊。
/// 關閉支援 Meta Quest / HTC VIVE 控制器 Trigger／Primary 點擊。
/// 顯示時可跟隨指定物件，並面向攝影機。
/// </summary>
public sealed class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Shared Popup")]
    [SerializeField] private Canvas _popupCanvas;
    [SerializeField] private Image _panelImage;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _contentText;
    [SerializeField] private Image _image;
    [SerializeField] private AudioSource _audioSource;

    [Header("Paging")]
    [SerializeField] private Button _prevButton;
    [SerializeField] private Button _nextButton;

    [Header("Image Card")]
    [Tooltip("有 Info Image 時，把 PNG 當成整張卡片（標題／內文已含在圖裡）")]
    [SerializeField] private bool _useImageAsCard = true;
    [Tooltip("把 PNG 接近黑色的底去掉，讓卡片浮在場景中")]
    [SerializeField] private Material _chromaKeyMaterial;
    [SerializeField] private Vector2 _imageCardPadding = new Vector2(12f, 72f);

    [Header("Follow Target")]
    [SerializeField] private Vector3 _defaultFollowOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private bool _faceCamera = true;

    [Header("VR Dismiss")]
    [SerializeField] private float _dismissCooldown = 0.5f;
    [SerializeField] private bool _allowControllerDismiss = true;

    private bool _isShowing;
    private float _nextDismissTime;
    private bool _prevLeftClick;
    private bool _prevRightClick;
    private Transform _followTarget;
    private Vector3 _followOffset;
    private Transform _popupRoot;
    private Camera _mainCamera;

    private string _title;
    private string _content;
    private Sprite _page1;
    private Sprite _page2;
    private int _pageIndex;

    public bool IsShowing => _isShowing;
    public bool IsVoicePlaying => _audioSource != null && _audioSource.isPlaying;

    private int PageCount
    {
        get
        {
            int n = 0;
            if (_page1 != null) n++;
            if (_page2 != null) n++;
            return n;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        _popupRoot = transform;

        // 以 Canvas.enabled 顯隱，避免 SetActive 造成 UI 重建尖峰
        if (_popupCanvas != null)
            _popupCanvas.enabled = false;

        _isShowing = false;
        RefreshPagerButtons();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 顯示／覆蓋共用彈窗。正在播語音或已顯示時：先 Stop 舊語音，再直接覆寫圖文與 clip。
    /// </summary>
    public void ShowPopup(string title, string content, Sprite img, AudioClip clip)
    {
        ShowPopup(title, content, img, null, clip, null, _defaultFollowOffset);
    }

    /// <summary>
    /// 顯示彈窗並跟隨指定物件（offset 為物件本地空間偏移）。
    /// </summary>
    public void ShowPopup(string title, string content, Sprite img, AudioClip clip, Transform followTarget, Vector3 followOffset)
    {
        ShowPopup(title, content, img, null, clip, followTarget, followOffset);
    }

    /// <summary>
    /// 顯示彈窗，可帶第二頁 PNG。
    /// </summary>
    public void ShowPopup(string title, string content, Sprite img, Sprite img2, AudioClip clip, Transform followTarget, Vector3 followOffset)
    {
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();

        _title = title ?? string.Empty;
        _content = content ?? string.Empty;
        _page1 = img;
        _page2 = img2;
        _pageIndex = 0;

        ApplyCurrentPage();

        if (_audioSource != null)
        {
            _audioSource.clip = clip;
            if (clip != null)
                _audioSource.Play();
        }

        _followTarget = followTarget;
        _followOffset = followOffset;
        if (_followTarget != null)
            UpdateFollowTransform();

        if (_popupCanvas != null)
        {
            if (_popupCanvas.worldCamera == null && Camera.main != null)
                _popupCanvas.worldCamera = Camera.main;

            if (!_popupCanvas.enabled)
                _popupCanvas.enabled = true;
        }

        _isShowing = true;
        // 稍延遲才允許關閉／翻頁，避免同一幀 Trigger 按下立刻關掉剛打開的面板
        _nextDismissTime = Time.time + _dismissCooldown;
    }

    /// <summary>供 XR Ray／UI Button OnClick 綁定（Quest 與 VIVE 皆適用）。</summary>
    public void HidePopup()
    {
        if (!_isShowing)
            return;

        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();

        if (_popupCanvas != null)
            _popupCanvas.enabled = false;

        _followTarget = null;
        _isShowing = false;
        _pageIndex = 0;
        RefreshPagerButtons();
    }

    /// <summary>供 UI Button OnClick 綁定。</summary>
    public void NextPage()
    {
        if (!_isShowing || _pageIndex + 1 >= PageCount)
            return;

        _pageIndex++;
        ApplyCurrentPage();
    }

    /// <summary>供 UI Button OnClick 綁定。</summary>
    public void PrevPage()
    {
        if (!_isShowing || _pageIndex <= 0)
            return;

        _pageIndex--;
        ApplyCurrentPage();
    }

    private void LateUpdate()
    {
        if (!_isShowing || _followTarget == null)
            return;

        UpdateFollowTransform();
    }

    private void Update()
    {
        if (!_isShowing || !_allowControllerDismiss)
            return;

        bool left = IsControllerClicked(XRNode.LeftHand);
        bool right = IsControllerClicked(XRNode.RightHand);

        // 邊緣觸發：只在按下瞬間關閉／翻頁，長按不連發
        bool edge = (left && !_prevLeftClick) || (right && !_prevRightClick);
        _prevLeftClick = left;
        _prevRightClick = right;

        if (!edge || Time.time < _nextDismissTime)
            return;

        _nextDismissTime = Time.time + _dismissCooldown;

        // 還有下一頁就先翻頁，最後一頁才關閉
        if (_pageIndex + 1 < PageCount)
            NextPage();
        else
            HidePopup();
    }

    private Sprite GetCurrentSprite()
    {
        if (_pageIndex == 1 && _page2 != null)
            return _page2;
        return _page1;
    }

    private void ApplyCurrentPage()
    {
        Sprite img = GetCurrentSprite();
        bool imageAsCard = _useImageAsCard && img != null;
        ApplyLayout(imageAsCard);

        if (_titleText != null)
        {
            _titleText.text = _title ?? string.Empty;
            _titleText.gameObject.SetActive(!imageAsCard && !string.IsNullOrEmpty(_title));
        }

        if (_contentText != null)
        {
            _contentText.text = _content ?? string.Empty;
            _contentText.gameObject.SetActive(!imageAsCard && !string.IsNullOrEmpty(_content));
        }

        if (_image != null)
        {
            _image.sprite = img;
            _image.enabled = img != null;
            _image.preserveAspect = true;
            _image.material = imageAsCard ? _chromaKeyMaterial : null;
        }

        RefreshPagerButtons();
    }

    private void RefreshPagerButtons()
    {
        bool multi = _isShowing && PageCount > 1;

        if (_prevButton != null)
        {
            _prevButton.gameObject.SetActive(multi);
            _prevButton.interactable = multi && _pageIndex > 0;
        }

        if (_nextButton != null)
        {
            _nextButton.gameObject.SetActive(multi);
            _nextButton.interactable = multi && _pageIndex + 1 < PageCount;
        }
    }

    private void ApplyLayout(bool imageAsCard)
    {
        if (_panelImage != null)
        {
            _panelImage.enabled = !imageAsCard;
            _panelImage.color = imageAsCard
                ? new Color(0f, 0f, 0f, 0f)
                : new Color(0.08f, 0.1f, 0.14f, 0.92f);
        }

        if (_image == null)
            return;

        RectTransform rt = _image.rectTransform;
        if (imageAsCard)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(_imageCardPadding.x, _imageCardPadding.y);
            rt.offsetMax = new Vector2(-_imageCardPadding.x, -12f);
        }
        else
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 40f);
            rt.sizeDelta = new Vector2(180f, 120f);
        }
    }

    /// <summary>
    /// 偏移以世界公尺計算，不受目標物件縮放影響：
    /// x 為玩家視角的右方、y 為世界上方、z 為朝玩家拉近的距離。
    /// </summary>
    private void UpdateFollowTransform()
    {
        if (_followTarget == null || _popupRoot == null)
            return;

        if (_mainCamera == null)
            _mainCamera = Camera.main;

        Vector3 basePosition = _followTarget.position;
        Vector3 towardCamera = ResolveTowardCameraDirection(basePosition);
        Vector3 right = Vector3.Cross(Vector3.up, towardCamera);

        _popupRoot.position = basePosition
            + right * _followOffset.x
            + Vector3.up * _followOffset.y
            + towardCamera * _followOffset.z;

        if (!_faceCamera || _mainCamera == null)
            return;

        Vector3 toCamera = _mainCamera.transform.position - _popupRoot.position;
        if (toCamera.sqrMagnitude < 0.0001f)
            return;

        // Canvas 正面朝向攝影機（forward 背對鏡頭）；IgnoreReversedGraphics 已關，Close 仍可點
        _popupRoot.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
    }

    /// <summary>水平方向的「由物件指向玩家」；玩家幾乎站在基準點上時改用視線方向。</summary>
    private Vector3 ResolveTowardCameraDirection(Vector3 basePosition)
    {
        if (_mainCamera != null)
        {
            Vector3 toCamera = _mainCamera.transform.position - basePosition;
            Vector3 flat = new Vector3(toCamera.x, 0f, toCamera.z);
            if (flat.sqrMagnitude > 0.01f)
                return flat.normalized;

            Vector3 look = _mainCamera.transform.forward;
            Vector3 lookFlat = new Vector3(look.x, 0f, look.z);
            if (lookFlat.sqrMagnitude > 0.0001f)
                return lookFlat.normalized;
        }

        Vector3 fallback = new Vector3(_followTarget.forward.x, 0f, _followTarget.forward.z);
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    /// <summary>
    /// Quest（Oculus Touch）與 VIVE Controller 共用 CommonUsages。
    /// GetDeviceAtXRNode 無 List 配置，符合 Zero-GC。
    /// </summary>
    private static bool IsControllerClicked(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        bool pressed;
        if (device.TryGetFeatureValue(CommonUsages.triggerButton, out pressed) && pressed)
            return true;

        if (device.TryGetFeatureValue(CommonUsages.primaryButton, out pressed) && pressed)
            return true;

        return false;
    }
}
