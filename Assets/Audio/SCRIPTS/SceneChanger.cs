using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    public void LoadGameScene()
    {
        SceneManager.LoadScene("SampleScene");
    }
}