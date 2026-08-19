using UnityEngine;

public class SceneBGM : MonoBehaviour
{
    public AudioClip bgm;

    void Start()
    {
        BGMManager.Instance.PlayBGM(bgm);
    }
}