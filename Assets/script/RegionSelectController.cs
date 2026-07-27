using UnityEngine;
using UnityEngine.SceneManagement;
public class RegionSelectController : MonoBehaviour
{
    [SerializeField] string citySceneName = "S3_City";
    [SerializeField] string forestSceneName = "S3_Forest";
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