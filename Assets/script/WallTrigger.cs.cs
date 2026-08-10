using UnityEngine;

public class WallTrigger : MonoBehaviour
{
    [Header("這個 Trigger 要開啟的牆")]
    [SerializeField] private GameObject targetWall;

    private bool activated = false;

    private void OnTriggerEnter(Collider other)
    {
        // 已經觸發過就不再執行
        if (activated)
            return;

        // 確認進來的是玩家
        CharacterController player =
            other.GetComponentInParent<CharacterController>();

        if (player == null)
            return;

        activated = true;

        // 開啟指定的隱形牆
        if (targetWall != null)
        {
            targetWall.SetActive(true);
        }
    }
}