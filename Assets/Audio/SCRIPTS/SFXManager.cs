using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance;

    public AudioSource audioSource;

    public AudioClip click;
    public AudioClip close;
    public AudioClip success;
    public AudioClip appearance;
  

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlayButtonClick()
    {
        audioSource.PlayOneShot(click);
    }

    public void PlayConfirm()
    {
        audioSource.PlayOneShot(close);
    }

    public void PlayCancel()
    {
        audioSource.PlayOneShot(success);
    }

    public void PlayDoor()
    {
        audioSource.PlayOneShot(appearance);
    }

    
}