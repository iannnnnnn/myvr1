using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneFlowController : MonoBehaviour
{
    [SerializeField] string nextSceneName = "S2_Question";
    bool isLeaving;
    public void GoToNextScene()
    {
        if (isLeaving)
            return;
        isLeaving = true;
        SceneManager.LoadScene(nextSceneName);
    }
}