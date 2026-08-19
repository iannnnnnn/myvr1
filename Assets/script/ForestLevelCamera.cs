using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 關卡圖示：浮空 Image，被 XR 雷射選取後通知 ForestLevelSelectController。
/// </summary>
[DisallowMultipleComponent]
public class ForestLevelCamera : MonoBehaviour
{
    [SerializeField] int levelIndex;
    [SerializeField] Transform firstPersonPoint;
    [SerializeField] ForestLevelSelectController controller;

    [Header("Icon")]
    [SerializeField] Image iconImage;
    [SerializeField] Color iconColor = Color.white;
    [SerializeField] Color iconPulseColor = new Color(0.45f, 0.95f, 1f, 1f);
    [SerializeField] float pulseSpeed = 2f;

    public int LevelIndex => levelIndex;
    public Transform FirstPersonPoint => firstPersonPoint != null ? firstPersonPoint : transform;

    IXRSelectInteractable _selectInteractable;
    IXRHoverInteractable _hoverInteractable;

    void Awake()
    {
        if (firstPersonPoint == null)
        {
            var child = transform.Find("FirstPersonPoint");
            if (child != null)
                firstPersonPoint = child;
        }

        if (controller == null)
            controller = FindFirstObjectByType<ForestLevelSelectController>();

        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);

        EnsureEasyHitCollider();

        _selectInteractable = GetComponent<IXRSelectInteractable>();
        _hoverInteractable = GetComponent<IXRHoverInteractable>();
        if (_selectInteractable == null)
            Debug.LogWarning($"ForestLevelCamera on {name} 需要搭配 XR Simple Interactable。", this);
    }

    void EnsureEasyHitCollider()
    {
        // Near-Far 用 ConeCast；加大碰撞體，遠距也較好指到
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            box.size = new Vector3(20f, 20f, 20f);
            box.center = Vector3.zero;
            box.isTrigger = false;
            return;
        }

        var sphere = GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = gameObject.AddComponent<SphereCollider>();
        sphere.radius = 10f;
        sphere.center = Vector3.zero;
        sphere.isTrigger = false;
    }

    void OnEnable()
    {
        if (_selectInteractable != null)
            _selectInteractable.selectEntered.AddListener(OnSelectEntered);

        if (_hoverInteractable != null)
        {
            _hoverInteractable.hoverEntered.AddListener(OnHoverEntered);
            _hoverInteractable.hoverExited.AddListener(OnHoverExited);
        }
    }

    void OnDisable()
    {
        if (_selectInteractable != null)
            _selectInteractable.selectEntered.RemoveListener(OnSelectEntered);

        if (_hoverInteractable != null)
        {
            _hoverInteractable.hoverEntered.RemoveListener(OnHoverEntered);
            _hoverInteractable.hoverExited.RemoveListener(OnHoverExited);
        }
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        Debug.Log($"[XR Ray] 指到 → {name}（關卡 {levelIndex + 1}）", this);
        pulseSpeed = 6f;
        if (iconImage != null)
            iconImage.transform.localScale = Vector3.one * 1.15f;
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        Debug.Log($"[XR Ray] 離開 → {name}（關卡 {levelIndex + 1}）", this);
        pulseSpeed = 2f;
        if (iconImage != null)
            iconImage.transform.localScale = Vector3.one;
    }

    void Update()
    {
        if (iconImage == null)
            return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        iconImage.color = Color.Lerp(iconColor, iconPulseColor, t);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        Debug.Log($"[XR Ray] Trigger 選取 → {name}（關卡 {levelIndex + 1}）", this);
        if (controller != null)
            controller.SelectLevel(levelIndex);
    }
}
