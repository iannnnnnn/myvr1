using System.Collections;
using UnityEngine;

/// <summary>
/// 砍樹控制器
///
/// 流程：
/// 1. AxeBladeHit 進入 AxeHitTrigger
/// 2. 判斷斧頭所在位置
/// 3. 隱藏 Tree_Complete
/// 4. 顯示 Tree_Cut
/// 5. 啟用 tree1001 的 Rigidbody
/// 6. 讓 tree1001 往斧頭接觸位置的反方向倒下
/// </summary>
public class TreeChopController : MonoBehaviour
{
    [Header("場景物件")]

    [Tooltip("完整樹木 Tree_Complete")]
    [SerializeField]
    private GameObject treeComplete;

    [Tooltip("砍斷後的樹 Tree_Cut")]
    [SerializeField]
    private GameObject treeCut;


    [Header("上半部物理")]

    [Tooltip("tree1001 上方 Rigidbody")]
    [SerializeField]
    private Rigidbody upperTreeRigidbody;

    [Tooltip("下半部 tree1002；倒下時忽略彼此碰撞，避免卡住")]
    [SerializeField]
    private GameObject lowerTree;


    [Header("倒下設定")]

    [Tooltip("斧頭 Tag")]
    [SerializeField]
    private string axeTag = "Axe";

    [Tooltip("倒下力度")]
    [SerializeField]
    private float fallForce = 12f;

    [Tooltip("施力點相對上半部中心的高度")]
    [SerializeField]
    private float forceHeight = 1.5f;


    // 避免同一棵樹被多次觸發
    private bool hasFallen = false;


    private void OnTriggerEnter(Collider other)
    {
        // 已經倒過就不再處理
        if (hasFallen)
        {
            return;
        }

        Debug.Log(
            "有物體進入 AxeHitTrigger：" +
            other.name
        );

        // 斧刃可能是子 Collider，其 Rigidbody 與斧頭在同一物件上的 Tag
        if (!IsAxe(other))
        {
            Debug.Log(
                "進入的物體不是 Axe，Tag = " +
                other.tag
            );

            return;
        }

        // 先檢查設定再設 hasFallen，否則 Inspector 漏接時會卡住
        if (!HasRequiredReferences())
        {
            return;
        }

        Debug.Log("成功觸發斧頭");

        hasFallen = true;

        // 計算倒下方向
        Vector3 fallDirection =
            CalculateFallDirection(
                other.transform.position
            );

        StartCoroutine(
            FallTree(fallDirection)
        );
    }


    /// <summary>
    /// 判斷進入觸發器的 Collider 是否屬於斧頭
    /// </summary>
    private bool IsAxe(Collider other)
    {
        if (other.CompareTag(axeTag))
        {
            return true;
        }

        // 斧刃在子物件時，Tag 通常設在 Rigidbody 所在的根物件
        Rigidbody attached = other.attachedRigidbody;

        if (attached != null &&
            attached.CompareTag(axeTag))
        {
            return true;
        }

        return other.transform.root.CompareTag(axeTag);
    }


    /// <summary>
    /// 檢查 Inspector 是否已指定必要物件
    /// </summary>
    private bool HasRequiredReferences()
    {
        if (treeComplete == null)
        {
            Debug.LogError(
                "Tree Complete 尚未指定"
            );

            return false;
        }

        if (treeCut == null)
        {
            Debug.LogError(
                "Tree Cut 尚未指定"
            );

            return false;
        }

        if (upperTreeRigidbody == null)
        {
            Debug.LogError(
                "tree1001 Rigidbody 尚未指定"
            );

            return false;
        }

        return true;
    }


    /// <summary>
    /// 讓上半部樹倒下時不會被下半部擋住
    /// </summary>
    private void IgnoreLowerTreeCollision()
    {
        if (lowerTree == null)
        {
            return;
        }

        Collider[] upperColliders =
            upperTreeRigidbody.GetComponentsInChildren<Collider>(true);

        Collider[] lowerColliders =
            lowerTree.GetComponentsInChildren<Collider>(true);

        foreach (Collider upper in upperColliders)
        {
            if (upper == null || upper.isTrigger)
            {
                continue;
            }

            foreach (Collider lower in lowerColliders)
            {
                if (lower == null || lower.isTrigger)
                {
                    continue;
                }

                Physics.IgnoreCollision(upper, lower, true);
            }
        }
    }


    /// <summary>
    /// 計算樹倒下方向
    ///
    /// 斧頭在左側
    /// 樹往右側倒
    ///
    /// 斧頭在前方
    /// 樹往後方倒
    /// </summary>
    private Vector3 CalculateFallDirection(
        Vector3 axePosition
    )
    {
        // Trigger 的位置當成樹幹中心
        Vector3 treeCenter =
            transform.position;

        // 從斧頭指向樹中心
        // 這個方向就是斧頭所在位置的反方向
        Vector3 direction =
            treeCenter - axePosition;

        // 只要水平倒下
        direction.y = 0f;

        // 避免距離太小造成方向不穩
        if (direction.sqrMagnitude < 0.001f)
        {
            direction =
                transform.forward;
        }

        direction.Normalize();

        return direction;
    }


    /// <summary>
    /// 執行倒下
    /// </summary>
    private IEnumerator FallTree(
        Vector3 fallDirection
    )
    {
        // 隱藏完整樹
        treeComplete.SetActive(false);

        // 顯示砍斷後的樹
        treeCut.SetActive(true);


        // 倒下時上半與下半不再互撞
        IgnoreLowerTreeCollision();


        // 關閉 Kinematic
        // 交給 Unity Physics 處理
        upperTreeRigidbody.isKinematic = false;

        upperTreeRigidbody.useGravity = true;


        // 清掉原本速度
        // 避免 Kinematic 狀態下殘留速度造成彈飛
#if UNITY_6000_0_OR_NEWER
        upperTreeRigidbody.linearVelocity =
            Vector3.zero;
#else
        upperTreeRigidbody.velocity =
            Vector3.zero;
#endif

        upperTreeRigidbody.angularVelocity =
            Vector3.zero;


        // 等待一次物理更新
        yield return new WaitForFixedUpdate();


        // 施力位置設在樹幹中心上方
        Vector3 forcePosition =
            upperTreeRigidbody.worldCenterOfMass
            + Vector3.up * forceHeight;


        // 在該處施力
        // 會比單純 AddForce 更有傾倒瞬間效果
        upperTreeRigidbody.AddForceAtPosition(
            fallDirection * fallForce,
            forcePosition,
            ForceMode.Impulse
        );


        Debug.Log(
            "Tree_Complete 已隱藏"
        );

        Debug.Log(
            "Tree_Cut 已顯示"
        );

        Debug.Log(
            "tree1001 開始倒下，方向：" +
            fallDirection
        );


        // 等待樹倒下
        yield return new WaitForSeconds(2.5f);


        // 停止剩餘線性與角速度
#if UNITY_6000_0_OR_NEWER
        upperTreeRigidbody.linearVelocity = Vector3.zero;
#else
        upperTreeRigidbody.velocity = Vector3.zero;
#endif

        upperTreeRigidbody.angularVelocity = Vector3.zero;


        // 將倒下後的樹固定在目前位置
        upperTreeRigidbody.isKinematic = true;

        Debug.Log(
            "tree1001 已停止物理運算並固定"
        );
    }
}
