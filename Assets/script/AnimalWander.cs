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

    // true 代表動物已經停止平常活動，正在逃跑
    private bool isFleeing;

    private void Start()
    {
        // Animator 和 NavMeshAgent 都在動物最外層
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        // 開始平常的隨機活動
        StartCoroutine(WanderLoop());
    }

    private IEnumerator WanderLoop()
    {
        // 還沒進入逃跑狀態時，持續選擇動作
        while (!isFleeing)
        {
            int randomNumber = Random.Range(0, 100);

            if (randomNumber < 30)
            {
                // 30% 待機
                yield return DoIdle();
            }
            else if (canAttack && randomNumber >= 95)
            {
                // 可以攻擊的動物有 5% 機率攻擊
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

        yield return new WaitForSeconds(idleSeconds);
    }

    private IEnumerator DoWalk()
    {
        bool foundPoint = TryGetRandomWalkablePoint(
            out Vector3 destination
        );

        if (!foundPoint)
        {
            yield return null;
            yield break;
        }

        agent.speed = walkSpeed;

        // 播放走路動畫
        animator.SetBool("IsWalking", true);

        agent.isStopped = false;
        agent.SetDestination(destination);

        // Agent 計算路線
        while (agent.pathPending)
        {
            yield return null;
        }

        // 等待動物抵達目的地
        while (
            agent.hasPath &&
            agent.remainingDistance >
            agent.stoppingDistance + 0.05f
        )
        {
            yield return null;
        }

        StopMoving();
    }

    private IEnumerator DoAttack()
    {
        StopMoving();

        animator.SetTrigger("IsAttack");

       
        yield return null;

     
        while (
            !animator
                .GetCurrentAnimatorStateInfo(0)
                .IsTag("Attack")
        )
        {
            yield return null;
        }


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
        Bounds areaBounds = wanderArea.bounds;

        // 最多尋找 20 次
        for (int i = 0; i < 20; i++)
        {
            float randomX = Random.Range(
                areaBounds.min.x,
                areaBounds.max.x
            );

            float randomZ = Random.Range(
                areaBounds.min.z,
                areaBounds.max.z
            );

            Vector3 randomPosition = new Vector3(
                randomX,
                transform.position.y,
                randomZ
            );

            // 確認隨機位置附近有 NavMesh
            bool foundPosition = NavMesh.SamplePosition(
                randomPosition,
                out NavMeshHit hit,
                navMeshSearchRadius,
                NavMesh.AllAreas
            );

            if (!foundPosition)
            {
                continue;
            }

            // 確認找到的位置仍在活動區域內
            bool insideArea =
                hit.position.x >= areaBounds.min.x &&
                hit.position.x <= areaBounds.max.x &&
                hit.position.z >= areaBounds.min.z &&
                hit.position.z <= areaBounds.max.z;

            if (insideArea)
            {
                destination = hit.position;
                return true;
            }
        }

        destination = transform.position;
        return false;
    }

    // 逃跑 Trigger 會呼叫這個函式
    public void StartFlee(
        Transform escapePoint,
        bool disappearAfterArrival
    )
    {
        // Can Flee 沒勾，就完全忽略逃跑指令
        if (!canFlee)
        {
            return;
        }

        // 已經在逃跑，或沒有指定目的地
        if (isFleeing || escapePoint == null)
        {
            return;
        }

        isFleeing = true;


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
        // 停止原本的隨機移動
        StopMoving();

        // 尋找逃跑點附近真正位於 NavMesh 上的位置
        bool foundEscapePosition = NavMesh.SamplePosition(
            escapePoint.position,
            out NavMeshHit hit,
            navMeshSearchRadius,
            agent.areaMask
        );

        // 逃跑點附近完全沒有 NavMesh
        if (!foundEscapePosition)
        {
            Debug.LogError(
                gameObject.name +
                " 的逃跑點附近沒有 NavMesh！"
            );

            yield break;
        }

        Vector3 destination = hit.position;

        // 使用逃跑速度
        agent.speed = fleeSpeed;


        animator.SetBool("IsRunning", true);

        // 允許 Agent 移動
        agent.isStopped = false;

        // 設定第一次目的地
        agent.SetDestination(destination);

        // 確認至少曾經成功建立過路線
        bool pathHasStarted = false;

        // 重新嘗試設定路線的計時器
        float retryTimer = 0f;

        while (true)
        {
            // Agent 沒有正在計算路線時才判斷
            if (!agent.pathPending)
            {
                // 只要曾經出現有效路線，就記錄下來
                if (
                    agent.hasPath &&
                    agent.pathStatus ==
                    NavMeshPathStatus.PathComplete
                )
                {
                    pathHasStarted = true;
                }

                // 計算動物和逃跑點之間的水平距離
                Vector3 animalPosition = transform.position;
                Vector3 targetPosition = destination;

                animalPosition.y = 0f;
                targetPosition.y = 0f;

                float distanceToTarget = Vector3.Distance(
                    animalPosition,
                    targetPosition
                );

                bool closeEnough =
                    distanceToTarget <=
                    agent.stoppingDistance + 0.2f;

                bool hasStopped =
                    agent.velocity.sqrMagnitude < 0.01f;


                if (
                    pathHasStarted &&
                    closeEnough &&
                    hasStopped
                )
                {
                    break;
                }

                // 判斷目前是否需要重新設定目的地
                bool noPath = !agent.hasPath;

                bool invalidPath =
                    agent.pathStatus ==
                    NavMeshPathStatus.PathInvalid;

                bool stuckOnPartialPath =
                    agent.pathStatus ==
                    NavMeshPathStatus.PathPartial &&
                    hasStopped;

                if (
                    noPath ||
                    invalidPath ||
                    stuckOnPartialPath
                )
                {
                    retryTimer += Time.deltaTime;

                    // 每 0.5 秒重新嘗試一次
                    if (retryTimer >= 0.5f)
                    {
                        agent.isStopped = false;
                        agent.SetDestination(destination);

                        retryTimer = 0f;
                    }
                }
                else
                {
                    retryTimer = 0f;
                }
            }

            // 下一幀繼續檢查
            yield return null;
        }

        // 確定抵達後才停止
        StopMoving();

        if (disappearAfterArrival)
        {
            gameObject.SetActive(false);
        }

        // 沒有選擇消失時，
        // 動物會留在逃跑點並保持 Idle
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
            animator.SetBool("IsWalking", false);
        }
    }
}