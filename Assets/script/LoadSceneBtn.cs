using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneBtn : MonoBehaviour
{
    public string sceneName = "SampleScene";

    public void LoadTargetScene()
    {
        SceneManager.LoadScene(sceneName);
    }
}

