using System.Collections;
using UnityEngine;

/// <summary>
/// 延遲指定秒數後淡入顯示 UI。
/// </summary>
public class DelayedShow : MonoBehaviour
{
    [SerializeField] GameObject target;
    [SerializeField] float delaySeconds = 3f;
    [SerializeField] float fadeDuration = 1f;
    [SerializeField] bool hideOnStart = true;

    Canvas _selfCanvas;
    CanvasGroup _canvasGroup;

    void Awake()
    {
        if (target == null)
            target = gameObject;

        if (target == gameObject)
            _selfCanvas = GetComponent<Canvas>();

        _canvasGroup = target.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = target.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        if (hideOnStart)
            SetAlpha(0f, interactable: false);

        StopAllCoroutines();
        StartCoroutine(ShowAfterDelay());
    }

    IEnumerator ShowAfterDelay()
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        if (_selfCanvas != null && !_selfCanvas.enabled)
            _selfCanvas.enabled = true;

        if (target != null && !target.activeSelf)
            target.SetActive(true);

        if (fadeDuration <= 0f)
        {
            SetAlpha(1f, interactable: true);
            yield break;
        }

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / fadeDuration);
            SetAlpha(a, interactable: false);
            yield return null;
        }

        SetAlpha(1f, interactable: true);
    }

    void SetAlpha(float alpha, bool interactable)
    {
        if (_canvasGroup == null)
            return;

        _canvasGroup.alpha = alpha;
        _canvasGroup.interactable = interactable && alpha >= 0.99f;
        _canvasGroup.blocksRaycasts = interactable && alpha >= 0.99f;
    }
}
