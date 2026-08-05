using UnityEngine;
using UnityEngine.SceneManagement;
public class RegionSelectController : MonoBehaviour
{
    [SerializeField] string citySceneName = "City01";
    [SerializeField] string forestSceneName = "ForestBasic";
    bool isLeaving;
    public void SelectCity()
    {
        Load(citySceneName);
    }
    public void SelectForest()
    {
        Load(forestSceneName);
    }
    void Load(string sceneName)
    {
        if (isLeaving)
            return;
        isLeaving = true;
        SceneManager.LoadScene(sceneName);
    }
}