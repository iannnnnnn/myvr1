using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;

/// <summary>
/// VR 暫停選單：Menu 鍵／Escape 開關。
/// 不改 Time.timeScale，只停 Locomotion 與世界互動，保留 XR UI 射線點擊。
/// </summary>
public sealed class PauseMenuController : MonoBehaviour
{
    const string DefaultReturnScene = "S1_TwoFutures";

    [Header("UI")]
    [SerializeField] Canvas _pauseCanvas;
    [SerializeField] Transform _panelRoot;
    [SerializeField] float _panelDistance = 1.6f;
    [SerializeField] float _panelHeightOffset = -0.1f;

    [Header("XR")]
    [SerializeField] Transform _xrOrigin;
    [SerializeField] GameObject _locomotionRoot;
    [SerializeField] XRBaseInteractor[] _worldInteractors;

    [Header("Scenes")]
    [SerializeField] string _returnSceneName = DefaultReturnScene;

    [Header("Input")]
    [SerializeField] float _toggleCooldown = 0.4f;

    bool _isPaused;
    bool _wasLocomotionActive;
    bool _interactionStateCached;
    bool _isLoadingScene;
    float _nextToggleTime;
    bool _prevLeftMenu;
    bool _prevRightMenu;
    Camera _mainCamera;
    InteractionLayerMask[] _savedInteractionLayers;

    public bool IsPaused => _isPaused;

    void Awake()
    {
        ResolveReferences();

        if (_pauseCanvas != null)
            _pauseCanvas.enabled = false;

        _isPaused = false;
    }

    void Update()
    {
        if (_isLoadingScene)
            return;

        bool left = IsMenuPressed(XRNode.LeftHand);
        bool right = IsMenuPressed(XRNode.RightHand);
        bool edge = (left && !_prevLeftMenu) || (right && !_prevRightMenu);
        _prevLeftMenu = left;
        _prevRightMenu = right;

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Escape))
            edge = true;
#endif

        if (!edge || Time.unscaledTime < _nextToggleTime)
            return;

        _nextToggleTime = Time.unscaledTime + _toggleCooldown;

        if (_isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    /// <summary>供 UI Button OnClick 綁定。</summary>
    public void ResumeGame()
    {
        if (!_isPaused)
            return;

        RestoreGameplay();

        if (_pauseCanvas != null)
            _pauseCanvas.enabled = false;

        _isPaused = false;
        _nextToggleTime = Time.unscaledTime + _toggleCooldown;
    }

    /// <summary>供 UI Button OnClick 綁定。</summary>
    public void RestartScene()
    {
        if (_isLoadingScene)
            return;

        _isLoadingScene = true;
        if (_isPaused)
            RestoreGameplay();

        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex);
    }

    /// <summary>供 UI Button OnClick 綁定。</summary>
    public void ReturnToS1()
    {
        if (_isLoadingScene)
            return;

        _isLoadingScene = true;
        if (_isPaused)
            RestoreGameplay();

        string scene = string.IsNullOrEmpty(_returnSceneName) ? DefaultReturnScene : _returnSceneName;
        SceneManager.LoadScene(scene);
    }

    void PauseGame()
    {
        if (_isPaused)
            return;

        if (UIManager.Instance != null && UIManager.Instance.IsShowing)
            UIManager.Instance.HidePopup();

        CacheGameplayState();
        ApplyPausedGameplay();
        PlacePanelInFrontOfCamera();

        if (_pauseCanvas != null)
        {
            if (_pauseCanvas.worldCamera == null && _mainCamera != null)
                _pauseCanvas.worldCamera = _mainCamera;

            _pauseCanvas.enabled = true;
        }

        _isPaused = true;
    }

    void ResolveReferences()
    {
        if (_panelRoot == null)
            _panelRoot = transform;

        if (_xrOrigin == null)
        {
            XROrigin origin = FindFirstObjectByType<XROrigin>();
            if (origin != null)
                _xrOrigin = origin.transform;
        }

        if (_locomotionRoot == null && _xrOrigin != null)
        {
            Transform locomotion = _xrOrigin.Find("Locomotion");
            if (locomotion != null)
                _locomotionRoot = locomotion.gameObject;
        }

        if (_worldInteractors == null || _worldInteractors.Length == 0)
        {
            _worldInteractors = FindObjectsByType<XRBaseInteractor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        }

        if (_mainCamera == null)
            _mainCamera = Camera.main;
    }

    void CacheGameplayState()
    {
        _wasLocomotionActive = _locomotionRoot != null && _locomotionRoot.activeSelf;

        if (_worldInteractors == null)
        {
            _interactionStateCached = false;
            return;
        }

        int count = _worldInteractors.Length;
        if (_savedInteractionLayers == null || _savedInteractionLayers.Length != count)
            _savedInteractionLayers = new InteractionLayerMask[count];

        for (int i = 0; i < count; i++)
        {
            XRBaseInteractor interactor = _worldInteractors[i];
            if (interactor != null)
                _savedInteractionLayers[i] = interactor.interactionLayers;
        }

        _interactionStateCached = true;
    }

    void ApplyPausedGameplay()
    {
        if (_locomotionRoot != null)
            _locomotionRoot.SetActive(false);

        if (!_interactionStateCached || _worldInteractors == null)
            return;

        for (int i = 0; i < _worldInteractors.Length; i++)
        {
            XRBaseInteractor interactor = _worldInteractors[i];
            if (interactor == null)
                continue;

            // 清掉世界互動層；UI 走 XRUIInputModule，不受此影響
            interactor.interactionLayers = 0;
        }
    }

    void RestoreGameplay()
    {
        if (_locomotionRoot != null)
            _locomotionRoot.SetActive(_wasLocomotionActive);

        if (!_interactionStateCached || _worldInteractors == null || _savedInteractionLayers == null)
            return;

        int count = _worldInteractors.Length;
        int savedCount = _savedInteractionLayers.Length;
        int n = count < savedCount ? count : savedCount;

        for (int i = 0; i < n; i++)
        {
            XRBaseInteractor interactor = _worldInteractors[i];
            if (interactor != null)
                interactor.interactionLayers = _savedInteractionLayers[i];
        }
    }

    void PlacePanelInFrontOfCamera()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;
        if (_mainCamera == null || _panelRoot == null)
            return;

        Transform cam = _mainCamera.transform;
        Vector3 forward = cam.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = cam.forward;
        else
            forward.Normalize();

        _panelRoot.position = cam.position + forward * _panelDistance + Vector3.up * _panelHeightOffset;

        Vector3 toCamera = cam.position - _panelRoot.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.0001f)
            return;

        _panelRoot.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
    }

    static bool IsMenuPressed(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        bool pressed;
        return device.TryGetFeatureValue(CommonUsages.menuButton, out pressed) && pressed;
    }
}
