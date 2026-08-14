using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class AnimalWander : MonoBehaviour
{
    [Header("動物的活動區域")]
    [SerializeField] private BoxCollider wanderArea;

    [Header("這隻動物會不會攻擊")]
    [SerializeField] private bool canAttack;

    [Header("這隻動物會不會逃跑")]
    [SerializeField] private bool canFlee;

    [Header("移動速度")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float fleeSpeed = 2f;

    [Header("待機時間")]
    [SerializeField] private float idleSeconds = 3f;

    [Header("尋找 NavMesh 的範圍")]
    [SerializeField] private float navMeshSearchRadius = 2f;

    private NavMeshAgent agent;
    private Animator animator;

    // true = 動物正在逃跑
    private bool isFleeing;

    private void Start()
    {
        // 取得 NavMeshAgent
        agent = GetComponent<NavMeshAgent>();

        // 取得 Animator
        animator = GetComponent<Animator>();

        // 檢查有沒有設定活動區域
        if (wanderArea == null)
        {
            Debug.LogError(
                gameObject.name +
                " 沒有設定 Wander Area！"
            );

            return;
        }

        // 開始平常的隨機活動
        StartCoroutine(WanderLoop());
    }

    private IEnumerator WanderLoop()
    {
        // 沒有逃跑時持續隨機活動
        while (!isFleeing)
        {
            int randomNumber = Random.Range(0, 100);

            if (randomNumber < 30)
            {
                // 30% 待機
                yield return DoIdle();
            }
            else if (
                canAttack &&
                randomNumber >= 95
            )
            {
                // 可以攻擊的動物
                // 有 5% 機率攻擊
                yield return DoAttack();
            }
            else
            {
                // 其他情況走路
                yield return DoWalk();
            }
        }
    }

    private IEnumerator DoIdle()
    {
        StopMoving();

        yield return new WaitForSeconds(
            idleSeconds
        );
    }

    private IEnumerator DoWalk()
    {
        // 尋找 Cube 裡可以走的位置
        bool foundPoint =
            TryGetRandomWalkablePoint(
                out Vector3 destination
            );

        // 找不到位置就這次先不走
        if (!foundPoint)
        {
            yield return null;
            yield break;
        }

        // 設定走路速度
        agent.speed = walkSpeed;

        // 關閉跑步動畫
        animator.SetBool(
            "IsRunning",
            false
        );

        // 開啟走路動畫
        animator.SetBool(
            "IsWalking",
            true
        );

        // 允許 NavMeshAgent 移動
        agent.isStopped = false;

        // 前往隨機位置
        agent.SetDestination(
            destination
        );

        // 等待 Agent 計算路線
        while (agent.pathPending)
        {
            yield return null;
        }

        // 等待動物走到目的地
        while (
            agent.hasPath &&
            agent.remainingDistance >
            agent.stoppingDistance + 0.05f
        )
        {
            yield return null;
        }

        // 到達後停止
        StopMoving();
    }

    private IEnumerator DoAttack()
    {
        StopMoving();

        // 播放攻擊動畫
        animator.SetTrigger(
            "IsAttack"
        );

        yield return null;

        // 等待進入 Attack 動畫
        while (
            !animator
                .GetCurrentAnimatorStateInfo(0)
                .IsTag("Attack")
        )
        {
            yield return null;
        }

        // 等待 Attack 動畫結束
        while (
            animator
                .GetCurrentAnimatorStateInfo(0)
                .IsTag("Attack")
        )
        {
            yield return null;
        }
    }

    private bool TryGetRandomWalkablePoint(
        out Vector3 destination
    )
    {
        // 最多嘗試 30 次
        for (int i = 0; i < 30; i++)
        {
            // BoxCollider 的一半大小
            Vector3 halfSize =
                wanderArea.size * 0.5f;

            // 稍微離 Cube 邊緣一點
            float margin = 0.05f;

            // 在 Cube 內隨機 X
            float randomX = Random.Range(
                wanderArea.center.x
                    - halfSize.x
                    + margin,

                wanderArea.center.x
                    + halfSize.x
                    - margin
            );

            // 在 Cube 內隨機 Z
            float randomZ = Random.Range(
                wanderArea.center.z
                    - halfSize.z
                    + margin,

                wanderArea.center.z
                    + halfSize.z
                    - margin
            );

            // 這是 Cube 自己的 Local 座標
            Vector3 localPosition =
                new Vector3(
                    randomX,
                    wanderArea.center.y,
                    randomZ
                );

            // Local 座標轉成世界座標
            Vector3 worldPosition =
                wanderArea.transform
                    .TransformPoint(
                        localPosition
                    );

            // 找這個位置附近的 NavMesh
            bool foundPosition =
                NavMesh.SamplePosition(
                    worldPosition,
                    out NavMeshHit hit,
                    navMeshSearchRadius,
                    agent.areaMask
                );

            // 附近沒有 NavMesh
            if (!foundPosition)
            {
                continue;
            }

            // SamplePosition 找到的位置
            // 如果跑到 Cube 外面就不要
            if (
                !IsInsideWanderArea(
                    hit.position
                )
            )
            {
                continue;
            }

            // 找到成功的位置
            destination = hit.position;

            return true;
        }

        // 30 次都失敗
        destination = transform.position;

        return false;
    }

    private bool IsInsideWanderArea(
        Vector3 worldPosition
    )
    {
        // 世界座標轉成 Cube Local 座標
        Vector3 localPosition =
            wanderArea.transform
                .InverseTransformPoint(
                    worldPosition
                );

        Vector3 halfSize =
            wanderArea.size * 0.5f;

        // 留一點點邊界
        float margin = 0.05f;

        float minX =
            wanderArea.center.x
            - halfSize.x
            + margin;

        float maxX =
            wanderArea.center.x
            + halfSize.x
            - margin;

        float minZ =
            wanderArea.center.z
            - halfSize.z
            + margin;

        float maxZ =
            wanderArea.center.z
            + halfSize.z
            - margin;

        // 判斷位置是否還在 Cube 裡
        return
            localPosition.x >= minX &&
            localPosition.x <= maxX &&
            localPosition.z >= minZ &&
            localPosition.z <= maxZ;
    }

    // 外部 Trigger 呼叫這個
    public void StartFlee(
        Transform escapePoint,
        bool disappearAfterArrival
    )
    {
        // 沒有勾 Can Flee
        if (!canFlee)
        {
            return;
        }

        // 已經在逃跑
        // 或沒有逃跑目的地
        if (
            isFleeing ||
            escapePoint == null
        )
        {
            return;
        }

        isFleeing = true;

        // 停止原本所有活動
        StopAllCoroutines();

        // 開始逃跑
        StartCoroutine(
            FleeToPoint(
                escapePoint,
                disappearAfterArrival
            )
        );
    }

    private IEnumerator FleeToPoint(
        Transform escapePoint,
        bool disappearAfterArrival
    )
    {
        // 停止原本移動
        StopMoving();

        // 尋找逃跑點附近的 NavMesh
        bool foundEscapePosition =
            NavMesh.SamplePosition(
                escapePoint.position,
                out NavMeshHit hit,
                navMeshSearchRadius,
                agent.areaMask
            );

        if (!foundEscapePosition)
        {
            Debug.LogError(
                gameObject.name +
                " 的逃跑點附近沒有 NavMesh！"
            );

            yield break;
        }

        Vector3 destination =
            hit.position;

        // 設定逃跑速度
        agent.speed = fleeSpeed;

        // 關閉走路動畫
        animator.SetBool(
            "IsWalking",
            false
        );

        // 開啟跑步動畫
        animator.SetBool(
            "IsRunning",
            true
        );

        // 開始移動
        agent.isStopped = false;

        // 設定目的地
        agent.SetDestination(
            destination
        );

        bool pathHasStarted = false;

        float retryTimer = 0f;

        while (true)
        {
            if (!agent.pathPending)
            {
                // 曾經成功建立完整路線
                if (
                    agent.hasPath &&
                    agent.pathStatus ==
                    NavMeshPathStatus.PathComplete
                )
                {
                    pathHasStarted = true;
                }

                // 計算水平距離
                Vector3 animalPosition =
                    transform.position;

                Vector3 targetPosition =
                    destination;

                animalPosition.y = 0f;
                targetPosition.y = 0f;

                float distanceToTarget =
                    Vector3.Distance(
                        animalPosition,
                        targetPosition
                    );

                bool closeEnough =
                    distanceToTarget <=
                    agent.stoppingDistance
                    + 0.2f;

                bool hasStopped =
                    agent.velocity
                        .sqrMagnitude
                    < 0.01f;

                // 確定真的抵達
                if (
                    pathHasStarted &&
                    closeEnough &&
                    hasStopped
                )
                {
                    break;
                }

                bool noPath =
                    !agent.hasPath;

                bool invalidPath =
                    agent.pathStatus ==
                    NavMeshPathStatus
                        .PathInvalid;

                bool stuckOnPartialPath =
                    agent.pathStatus ==
                    NavMeshPathStatus
                        .PathPartial
                    &&
                    hasStopped;

                // 路線出問題時重新嘗試
                if (
                    noPath ||
                    invalidPath ||
                    stuckOnPartialPath
                )
                {
                    retryTimer +=
                        Time.deltaTime;

                    if (
                        retryTimer >= 0.5f
                    )
                    {
                        agent.isStopped =
                            false;

                        agent.SetDestination(
                            destination
                        );

                        retryTimer = 0f;
                    }
                }
                else
                {
                    retryTimer = 0f;
                }
            }

            yield return null;
        }

        // 到達逃跑位置
        StopMoving();

        // 關閉跑步動畫
        animator.SetBool(
            "IsRunning",
            false
        );

        // 如果設定抵達後消失
        if (disappearAfterArrival)
        {
            gameObject.SetActive(
                false
            );
        }

        // 沒勾消失的話
        // 動物就停在逃跑位置
    }

    private void StopMoving()
    {
        if (
            agent != null &&
            agent.isOnNavMesh
        )
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (animator != null)
        {
            animator.SetBool(
                "IsWalking",
                false
            );

            animator.SetBool(
                "IsRunning",
                false
            );
        }
    }
}