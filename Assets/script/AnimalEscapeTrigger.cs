using UnityEngine;

public class AnimalEscapeTrigger : MonoBehaviour
{
    [Header("要逃跑的動物")]
    [SerializeField] private AnimalWander[] animals;

    [Header("逃跑目的地")]
    [SerializeField] private Transform escapePoint;

    [Header("抵達後是否消失")]
    [SerializeField] private bool disappearAfterArrival = true;

    [Header("是否只能觸發一次")]
    [SerializeField] private bool triggerOnlyOnce = true;

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        // 已觸發過，而且設定只能觸發一次
        if (triggerOnlyOnce && hasTriggered)
        {
            return;
        }

        

        hasTriggered = true;

        // 通知所有第二區動物開始逃跑
        foreach (AnimalWander animal in animals)
        {
            if (animal != null)
            {
                animal.StartFlee(
                    escapePoint,
                    disappearAfterArrival
                );
            }
        }
    }
}