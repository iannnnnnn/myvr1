using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 縮時用太陽日夜：只旋轉 Directional Light 並調整 intensity／color。
/// 預設每輪 2 秒，可連播多次；第一次日出可回呼。
/// </summary>
[DisallowMultipleComponent]
public class SunDayNightCycle : MonoBehaviour
{
    [SerializeField] Light sun;
    [SerializeField] float cycleDuration = 2f;
    [SerializeField] int loopCount = 3;

    [Tooltip("單輪正規化時間點：到達此值視為「日出」（約清晨→午前）")]
    [SerializeField] [Range(0.05f, 0.5f)] float sunriseNormalizedTime = 0.2f;

    [Header("Day (rest pose)")]
    [SerializeField] float daySunX = 50f;
    [SerializeField] float daySunY = -30f;
    [SerializeField] float dayIntensity = 1f;
    [SerializeField] Color dayColor = new Color(1f, 0.956f, 0.839f);

    [Header("Keyframes (0=start dawn … 1=end next dawn)")]
    [SerializeField] AnimationCurve sunXOverCycle = new AnimationCurve(
        new Keyframe(0f, 5f),
        new Keyframe(0.25f, 50f),
        new Keyframe(0.45f, 10f),
        new Keyframe(0.55f, -20f),
        new Keyframe(0.7f, -80f),
        new Keyframe(0.85f, -120f),
        new Keyframe(1f, 5f));

    [SerializeField] AnimationCurve intensityOverCycle = new AnimationCurve(
        new Keyframe(0f, 0.55f),
        new Keyframe(0.25f, 1.1f),
        new Keyframe(0.5f, 0.45f),
        new Keyframe(0.7f, 0.08f),
        new Keyframe(0.85f, 0.05f),
        new Keyframe(1f, 0.55f));

    [SerializeField] Gradient colorOverCycle;

    public bool IsPlaying { get; private set; }

    Coroutine _routine;

    void Awake()
    {
        if (sun == null)
            sun = FindDirectionalLight();

        EnsureCurves();
        EnsureGradient();
        ApplyDayRestPose();
    }

    void OnValidate()
    {
        cycleDuration = Mathf.Max(0.1f, cycleDuration);
        loopCount = Mathf.Max(1, loopCount);
        EnsureCurves();
        EnsureGradient();
    }

    public void PlayCycle(Action onComplete = null)
    {
        PlayCycles(loopCount, null, onComplete);
    }

    public void PlayCycles(int loops, Action onFirstSunrise = null, Action onComplete = null)
    {
        if (!isActiveAndEnabled)
        {
            onComplete?.Invoke();
            return;
        }

        if (sun == null)
            sun = FindDirectionalLight();

        if (sun == null)
        {
            Debug.LogWarning("SunDayNightCycle: 找不到 Directional Light。", this);
            onComplete?.Invoke();
            return;
        }

        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(CycleRoutine(Mathf.Max(1, loops), onFirstSunrise, onComplete));
    }

    public void StopCycleAndReset()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        IsPlaying = false;
        ApplyDayRestPose();
    }

    IEnumerator CycleRoutine(int loops, Action onFirstSunrise, Action onComplete)
    {
        IsPlaying = true;
        EnsureCurves();
        EnsureGradient();

        float duration = Mathf.Max(0.1f, cycleDuration);
        bool sunriseFired = false;

        for (int loop = 0; loop < loops; loop++)
        {
            float t = 0f;
            bool crossedSunriseThisLoop = false;

            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float sample = Mathf.Clamp01(t);
                ApplyCycleSample(sample);

                if (!sunriseFired && !crossedSunriseThisLoop && sample >= sunriseNormalizedTime)
                {
                    crossedSunriseThisLoop = true;
                    if (loop == 0)
                    {
                        sunriseFired = true;
                        onFirstSunrise?.Invoke();
                    }
                }

                yield return null;
            }

            ApplyCycleSample(1f);
        }

        ApplyDayRestPose();
        IsPlaying = false;
        _routine = null;
        onComplete?.Invoke();
    }

    void ApplyCycleSample(float t)
    {
        if (sun == null)
            return;

        float x = sunXOverCycle.Evaluate(t);
        sun.transform.rotation = Quaternion.Euler(x, daySunY, 0f);
        sun.intensity = Mathf.Max(0f, intensityOverCycle.Evaluate(t));
        sun.color = colorOverCycle.Evaluate(t);
    }

    void ApplyDayRestPose()
    {
        if (sun == null)
            return;

        sun.transform.rotation = Quaternion.Euler(daySunX, daySunY, 0f);
        sun.intensity = dayIntensity;
        sun.color = dayColor;
    }

    void EnsureCurves()
    {
        if (sunXOverCycle == null || sunXOverCycle.length < 2)
        {
            sunXOverCycle = new AnimationCurve(
                new Keyframe(0f, 5f),
                new Keyframe(0.25f, 50f),
                new Keyframe(0.45f, 10f),
                new Keyframe(0.55f, -20f),
                new Keyframe(0.7f, -80f),
                new Keyframe(0.85f, -120f),
                new Keyframe(1f, 5f));
        }

        if (intensityOverCycle == null || intensityOverCycle.length < 2)
        {
            intensityOverCycle = new AnimationCurve(
                new Keyframe(0f, 0.55f),
                new Keyframe(0.25f, 1.1f),
                new Keyframe(0.5f, 0.45f),
                new Keyframe(0.7f, 0.08f),
                new Keyframe(0.85f, 0.05f),
                new Keyframe(1f, 0.55f));
        }
    }

    void EnsureGradient()
    {
        if (colorOverCycle != null && colorOverCycle.colorKeys != null && colorOverCycle.colorKeys.Length > 1)
            return;

        colorOverCycle = new Gradient();
        colorOverCycle.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.75f, 0.55f), 0f),
                new GradientColorKey(dayColor, 0.25f),
                new GradientColorKey(new Color(1f, 0.55f, 0.35f), 0.5f),
                new GradientColorKey(new Color(0.25f, 0.3f, 0.55f), 0.7f),
                new GradientColorKey(new Color(0.15f, 0.18f, 0.35f), 0.85f),
                new GradientColorKey(new Color(1f, 0.75f, 0.55f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            });
    }

    static Light FindDirectionalLight()
    {
        var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Directional)
                return lights[i];
        }

        return null;
    }
}
