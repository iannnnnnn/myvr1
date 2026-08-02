using UnityEngine;

public class HintController : MonoBehaviour
{
    [SerializeField] Canvas hintCanvas;
    [SerializeField] float firstShowDelay = 0.5f;
    [SerializeField] bool autoHide = false;
    [SerializeField] float showDuration = 5f;
    [SerializeField] float remindAfterIdle = 10f;

    [Header("Blink")]
    [SerializeField] bool blinkWhileVisible = true;
    [SerializeField] float blinkSpeed = 3f;
    [SerializeField] float blinkMinAlpha = 0.25f;
    [SerializeField] float blinkMaxAlpha = 1f;

    float showAt;
    float hideAt;
    bool remindQueued = true;
    bool interacted;
    bool isShowing;
    CanvasGroup _canvasGroup;

    void Awake()
    {
        if (hintCanvas == null)
            return;

        _canvasGroup = hintCanvas.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = hintCanvas.gameObject.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        SetVisible(false);
        showAt = Time.time + firstShowDelay;
        hideAt = -1f;
    }

    public void MarkInteracted()
    {
        interacted = true;
        SetVisible(false);
        showAt = -1f;
        hideAt = -1f;
    }

    void Update()
    {
        if (interacted)
            return;

        float now = Time.time;

        if (showAt > 0f && now >= showAt)
        {
            SetVisible(true);
            hideAt = autoHide ? now + showDuration : -1f;
            showAt = -1f;
        }
        else if (autoHide && hideAt > 0f && now >= hideAt)
        {
            SetVisible(false);
            hideAt = -1f;

            if (remindQueued)
            {
                remindQueued = false;
                showAt = now + remindAfterIdle;
            }
        }

        if (isShowing && blinkWhileVisible && _canvasGroup != null)
        {
            float t = (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f;
            _canvasGroup.alpha = Mathf.Lerp(blinkMinAlpha, blinkMaxAlpha, t);
        }
    }

    void SetVisible(bool on)
    {
        isShowing = on;

        if (hintCanvas != null)
            hintCanvas.enabled = on;

        if (_canvasGroup != null)
            _canvasGroup.alpha = on ? blinkMaxAlpha : 0f;
    }
}
