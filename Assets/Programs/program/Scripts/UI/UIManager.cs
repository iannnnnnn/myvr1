using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using TMPro;

/// <summary>
/// 單一共用 UI 彈窗：只負責圖文／語音呈現。
/// 連續觸發時 Stop + 覆蓋內容，不做 Close/Open，避免 Canvas 重建與語音重疊。
/// 關閉支援 Meta Quest / HTC VIVE 控制器 Trigger／Primary 點擊（XR InputDevices，Update 零配置）。
/// </summary>
public sealed class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Shared Popup")]
    [SerializeField] private Canvas _popupCanvas;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _contentText;
    [SerializeField] private Image _image;
    [SerializeField] private AudioSource _audioSource;

    [Header("VR Dismiss")]
    [SerializeField] private float _dismissCooldown = 0.5f;
    [SerializeField] private bool _allowControllerDismiss = true;

    private bool _isShowing;
    private float _nextDismissTime;
    private bool _prevLeftClick;
    private bool _prevRightClick;

    public bool IsShowing => _isShowing;
    public bool IsVoicePlaying => _audioSource != null && _audioSource.isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        // 以 Canvas.enabled 顯隱，避免 SetActive 造成 UI 重建尖峰
        if (_popupCanvas != null)
            _popupCanvas.enabled = false;

        _isShowing = false;
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
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();

        if (_titleText != null)
            _titleText.text = title ?? string.Empty;

        if (_contentText != null)
            _contentText.text = content ?? string.Empty;

        if (_image != null)
        {
            _image.sprite = img;
            _image.enabled = img != null;
        }

        if (_audioSource != null)
        {
            _audioSource.clip = clip;
            if (clip != null)
                _audioSource.Play();
        }

        if (_popupCanvas != null && !_popupCanvas.enabled)
            _popupCanvas.enabled = true;

        _isShowing = true;
        // 稍延遲才允許關閉，避免同一幀 Trigger 按下立刻關掉剛打開的面板
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

        _isShowing = false;
    }

    private void Update()
    {
        if (!_isShowing || !_allowControllerDismiss)
            return;

        bool left = IsControllerClicked(XRNode.LeftHand);
        bool right = IsControllerClicked(XRNode.RightHand);

        // 邊緣觸發：只在按下瞬間關閉，長按不連發
        bool edge = (left && !_prevLeftClick) || (right && !_prevRightClick);
        _prevLeftClick = left;
        _prevRightClick = right;

        if (!edge || Time.time < _nextDismissTime)
            return;

        _nextDismissTime = Time.time + _dismissCooldown;
        HidePopup();
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
