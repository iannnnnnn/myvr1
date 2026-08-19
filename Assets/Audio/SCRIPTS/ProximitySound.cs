using UnityEngine;

public class ProximitySound : MonoBehaviour
{
    public Transform player;

    public float maxDistance = 10f;
    public float maxVolume = 1f;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.loop = true;
        audioSource.Play();
    }

    void Update()
    {
        float distance = Vector3.Distance(
            transform.position,
            player.position
        );


        float volume = 1 - (distance / maxDistance);

        volume = Mathf.Clamp01(volume);

        audioSource.volume = volume * maxVolume;
    }
    private void OnTriggerEnter(Collider other)
{
    if (other.CompareTag("Player"))
    {
        audioSource.Play();
    }
}


}