using UnityEngine;
using System.Collections;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    private AudioSource audioSource;
    private Coroutine fadeCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
    }

    public void PlayBGM(AudioClip newClip, float fadeTime = 1f)
    {
        if (audioSource.clip == newClip && audioSource.isPlaying)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(ChangeBGM(newClip, fadeTime));
    }

    IEnumerator ChangeBGM(AudioClip newClip, float fadeTime)
    {
        // 淡出目前音樂
        float startVolume = audioSource.volume;

        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            audioSource.volume = Mathf.Lerp(
                startVolume,
                0,
                t / fadeTime
            );

            yield return null;
        }

        audioSource.volume = 0;

        // 更換音樂
        audioSource.clip = newClip;
        audioSource.Play();

        // 淡入新音樂
        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            audioSource.volume = Mathf.Lerp(
                0,
                1,
                t / fadeTime
            );

            yield return null;
        }

        audioSource.volume = 1;
    }
}