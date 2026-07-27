using UnityEngine;

public class HintController : MonoBehaviour
{
    [SerializeField] Canvas hintCanvas;
    [SerializeField] float firstShowDelay = 0.5f;
    [SerializeField] float showDuration = 5f;
    [SerializeField] float remindAfterIdle = 10f;

    float showAt;
    float hideAt;
    bool remindQueued = true;
    bool interacted;

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
            hideAt = now + showDuration;
            showAt = -1f;
        }
        else if (hideAt > 0f && now >= hideAt)
        {
            SetVisible(false);
            hideAt = -1f;

            if (remindQueued)
            {
                remindQueued = false;
                showAt = now + remindAfterIdle;
            }
        }
    }

    void SetVisible(bool on)
    {
        if (hintCanvas != null)
            hintCanvas.enabled = on;
    }
}