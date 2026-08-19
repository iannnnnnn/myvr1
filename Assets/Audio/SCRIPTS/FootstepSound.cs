using UnityEngine;

public class FootstepSound : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip[] footstepSounds;

    public void PlayFootstep()
    {
        if (footstepSounds == null || footstepSounds.Length == 0)
        {
            Debug.LogWarning("沒有設定腳步音效！");
            return;
        }

        if (audioSource == null)
        {
            Debug.LogWarning("沒有設定 Audio Source！");
            return;
        }

        int randomIndex = Random.Range(0, footstepSounds.Length);

        audioSource.PlayOneShot(footstepSounds[randomIndex]);
    }
}