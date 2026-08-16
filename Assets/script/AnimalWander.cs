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

    [Header("距離活動區邊緣的額外安全距離")]
    [SerializeField] private float edgePadding = 0.2f;

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

        // 沒有 NavMeshAgent
        if (agent == null)
        {
            Debug.LogError(
                gameObject.name +
                " 沒有 NavMeshAgent！"
            );

            return;
        }

        // 沒有設定活動區域
        if (wanderArea == null)
        {
            Debug.LogError(
                gameObject.name +
                " 沒有設定 Wander Area！"
            );

            return;
        }

        // 動物沒有站在 NavMesh 上
        if (!agent.isOnNavMesh)
        {
            Debug.LogError(
                gameObject.name +
                " 沒有站在 NavMesh 上！"
            );

            return;
        }

        // 開始平常活動
        StartCoroutine(WanderLoop());
    }


    private IEnumerator WanderLoop()
    {
        // 沒有逃跑時持續活動
        while (!isFleeing)
        {
            int randomNumber =
                Random.Range(0, 100);

            // 30% 機率待機
            if (randomNumber < 30)
            {
                yield return DoIdle();
            }

            // 可以攻擊的動物
            // 5% 機率攻擊
            else if (
                canAttack &&
                randomNumber >= 95
            )
            {
                yield return DoAttack();
            }

            // 其他情況走路
            else
            {
                yield return DoWalk();
            }
        }
    }


    private IEnumerator DoIdle()
    {
        // 停止移動
        StopMoving();

        // 等待
        yield return new WaitForSeconds(
            idleSeconds
        );
    }


    private IEnumerator DoWalk()
    {
        // 尋找活動區域內可以走的位置
        bool foundPoint =
            TryGetRandomWalkablePoint(
                out Vector3 destination
            );

        // 找不到位置就先不走
        if (!foundPoint)
        {
            StopMoving();

            yield return new WaitForSeconds(0.5f);

            yield break;
        }


        // 設定走路速度
        agent.speed =
            walkSpeed;

        // 允許 Agent 移動
        agent.isStopped =
            false;

        // 設定目的地
        agent.SetDestination(
            destination
        );


        // 等待 NavMeshAgent 計算路線
        while (agent.pathPending)
        {
            yield return null;
        }


        // 沒有完整路線
        if (
            !agent.hasPath ||
            agent.pathStatus !=
            NavMeshPathStatus.PathComplete
        )
        {
            StopMoving();

            yield break;
        }


        // ==========================
        // 卡住判斷
        // ==========================

        float stuckTimer =
            0f;

        // 幾秒幾乎沒移動就放棄目的地
        float stuckLimit =
            1.5f;

        // 多少速度以下視為沒有真的走
        float movingThreshold =
            0.05f;


        while (true)
        {
            // Agent 的實際移動速度
            float currentSpeed =
                agent.velocity.magnitude;


            // ==========================
            // 根據「實際速度」控制走路動畫
            // ==========================

            bool isActuallyMoving =
                currentSpeed >
                movingThreshold;


            if (animator != null)
            {
                animator.SetBool(
                    "IsWalking",
                    isActuallyMoving
                );

                animator.SetBool(
                    "IsRunning",
                    false
                );
            }


            // ==========================
            // 是否已經抵達
            // ==========================

            if (
                !agent.pathPending &&
                agent.remainingDistance <=
                agent.stoppingDistance + 0.1f
            )
            {
                break;
            }


            // ==========================
            // 路線失效
            // ==========================

            if (
                !agent.hasPath ||
                agent.pathStatus !=
                NavMeshPathStatus.PathComplete
            )
            {
                break;
            }


            // ==========================
            // 卡住偵測
            // ==========================

            if (!isActuallyMoving)
            {
                stuckTimer +=
                    Time.deltaTime;
            }
            else
            {
                stuckTimer =
                    0f;
            }


            // 1.5 秒都沒真正移動
            // 放棄這個目的地
            if (
                stuckTimer >=
                stuckLimit
            )
            {
                break;
            }


            yield return null;
        }


        // 停止
        StopMoving();

        // 稍微停一下
        // 避免馬上又選新位置
        yield return new WaitForSeconds(
            0.2f
        );
    }


    private IEnumerator DoAttack()
    {
        // 先停下來
        StopMoving();

        // 沒有 Animator 就不攻擊
        if (animator == null)
        {
            yield break;
        }

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
        // 預設位置
        destination =
            transform.position;

        // Box Collider 的 Local 一半大小
        Vector3 halfSize =
            wanderArea.size * 0.5f;


        // =========================
        // 計算安全邊界
        // =========================

        /*
         * agent.radius 是世界座標尺寸。
         *
         * wanderArea.size 是 Local 尺寸。
         *
         * 所以必須根據 WanderArea 的 Scale
         * 換算。
         */

        float worldMargin =
            agent.radius +
            edgePadding;

        Vector3 areaScale =
            wanderArea.transform.lossyScale;

        float scaleX =
            Mathf.Max(
                Mathf.Abs(areaScale.x),
                0.0001f
            );

        float scaleZ =
            Mathf.Max(
                Mathf.Abs(areaScale.z),
                0.0001f
            );

        // 世界距離轉 Local 距離
        float marginX =
            worldMargin /
            scaleX;

        float marginZ =
            worldMargin /
            scaleZ;


        // =========================
        // 檢查活動區是不是太小
        // =========================

        if (
            halfSize.x <= marginX ||
            halfSize.z <= marginZ
        )
        {
            Debug.LogError(
                gameObject.name +
                " 的 Wander Area 太小！"
            );

            return false;
        }


        // =========================
        // 隨機尋找可以走的位置
        // =========================

        // 最多嘗試 30 次
        for (int i = 0; i < 30; i++)
        {
            // 隨機 X
            float randomX =
                Random.Range(
                    wanderArea.center.x
                    - halfSize.x
                    + marginX,

                    wanderArea.center.x
                    + halfSize.x
                    - marginX
                );

            // 隨機 Z
            float randomZ =
                Random.Range(
                    wanderArea.center.z
                    - halfSize.z
                    + marginZ,

                    wanderArea.center.z
                    + halfSize.z
                    - marginZ
                );


            // =========================
            // Y 軸不使用 WanderArea 限制
            // =========================

            /*
             * 取得動物目前位置
             * 在 WanderArea Local 空間中的高度。
             *
             * X、Z：
             * 使用 WanderArea 隨機。
             *
             * Y：
             * 使用動物目前高度。
             *
             * 最後真正的地面高度
             * 交給 NavMesh.SamplePosition 找。
             */

            Vector3 animalLocalPosition =
                wanderArea.transform
                    .InverseTransformPoint(
                        transform.position
                    );


            Vector3 localPosition =
                new Vector3(
                    randomX,
                    animalLocalPosition.y,
                    randomZ
                );


            // Local 座標轉世界座標
            Vector3 worldPosition =
                wanderArea.transform
                    .TransformPoint(
                        localPosition
                    );


            // =========================
            // 尋找真正的 NavMesh 地面
            // =========================

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


            // =========================
            // 只檢查 X、Z
            // 不檢查 Y
            // =========================

            if (
                !IsInsideWanderArea(
                    hit.position
                )
            )
            {
                continue;
            }


            // 成功找到目的地
            destination =
                hit.position;

            return true;
        }


        // 30 次全部找不到
        return false;
    }


    private bool IsInsideWanderArea(
        Vector3 worldPosition
    )
    {
        // 世界座標
        // 轉成 WanderArea Local 座標
        Vector3 localPosition =
            wanderArea.transform
                .InverseTransformPoint(
                    worldPosition
                );


        // Collider Local 一半大小
        Vector3 halfSize =
            wanderArea.size * 0.5f;


        // =========================
        // 計算安全邊界
        // =========================

        float worldMargin =
            agent.radius +
            edgePadding;

        Vector3 areaScale =
            wanderArea.transform.lossyScale;


        float scaleX =
            Mathf.Max(
                Mathf.Abs(areaScale.x),
                0.0001f
            );

        float scaleZ =
            Mathf.Max(
                Mathf.Abs(areaScale.z),
                0.0001f
            );


        float marginX =
            worldMargin /
            scaleX;

        float marginZ =
            worldMargin /
            scaleZ;


        // =========================
        // X 範圍
        // =========================

        float minX =
            wanderArea.center.x
            - halfSize.x
            + marginX;

        float maxX =
            wanderArea.center.x
            + halfSize.x
            - marginX;


        // =========================
        // Z 範圍
        // =========================

        float minZ =
            wanderArea.center.z
            - halfSize.z
            + marginZ;

        float maxZ =
            wanderArea.center.z
            + halfSize.z
            - marginZ;


        // =========================
        // 只判斷 X、Z
        //
        // 完全不判斷 Y
        // =========================

        return
            localPosition.x >= minX &&
            localPosition.x <= maxX &&
            localPosition.z >= minZ &&
            localPosition.z <= maxZ;
    }


    // =========================
    // 外部 Trigger 呼叫逃跑
    // =========================

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

        isFleeing =
            true;

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


        // =========================
        // 找逃跑點附近 NavMesh
        // =========================

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


        // =========================
        // 設定逃跑
        // =========================

        agent.speed =
            fleeSpeed;


        if (animator != null)
        {
            animator.SetBool(
                "IsWalking",
                false
            );

            animator.SetBool(
                "IsRunning",
                true
            );
        }


        agent.isStopped =
            false;


        agent.SetDestination(
            destination
        );


        bool pathHasStarted =
            false;

        float retryTimer =
            0f;


        // =========================
        // 持續逃跑直到真正到達
        // =========================

        while (true)
        {
            if (!agent.pathPending)
            {
                // 曾經建立完整路線
                if (
                    agent.hasPath &&
                    agent.pathStatus ==
                    NavMeshPathStatus.PathComplete
                )
                {
                    pathHasStarted =
                        true;
                }


                // =========================
                // 距離只看水平距離
                // 不看 Y
                // =========================

                Vector3 animalPosition =
                    transform.position;

                Vector3 targetPosition =
                    destination;


                animalPosition.y =
                    0f;

                targetPosition.y =
                    0f;


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


                // 確定抵達
                if (
                    pathHasStarted &&
                    closeEnough &&
                    hasStopped
                )
                {
                    break;
                }


                // =========================
                // 判斷路線問題
                // =========================

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


                // =========================
                // 路線有問題
                // 每 0.5 秒重新嘗試
                // =========================

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


                        retryTimer =
                            0f;
                    }
                }
                else
                {
                    retryTimer =
                        0f;
                }
            }


            yield return null;
        }


        // =========================
        // 抵達
        // =========================

        StopMoving();


        if (animator != null)
        {
            animator.SetBool(
                "IsRunning",
                false
            );
        }


        // 如果設定抵達後消失
        if (disappearAfterArrival)
        {
            gameObject.SetActive(
                false
            );
        }
    }


    // =========================
    // 停止動物移動
    // =========================

    private void StopMoving()
    {
        if (
            agent != null &&
            agent.isOnNavMesh
        )
        {
            agent.isStopped =
                true;

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