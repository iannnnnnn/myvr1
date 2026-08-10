using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 當 XR Origin 進入斧頭觸發區時，
/// 停止斧頭的展示旋轉，
/// 並讓右手 Near-Far Interactor 自動抓住斧頭。
/// 
/// 適用於 XR Interaction Toolkit 3.x。
/// </summary>
public class AxeAutoPickupTrigger : MonoBehaviour
{
    [Header("玩家設定")]

    [Tooltip("場景中的 XR Origin 根物件")]
    [SerializeField]
    private Transform xrOriginRoot;

    [Header("右手設定")]

    [Tooltip("右手控制器底下的 Near-Far Interactor")]
    [SerializeField]
    private NearFarInteractor rightHandInteractor;

    [Header("斧頭設定")]

    [Tooltip("斧頭物件上的 XR Grab Interactable")]
    [SerializeField]
    private XRGrabInteractable axeGrabInteractable;

    [Tooltip("斧頭物件上的 Rigidbody")]
    [SerializeField]
    private Rigidbody axeRigidbody;

    [Tooltip("控制斧頭展示旋轉的程式")]
    [SerializeField]
    private AxeFloatingRotation axeFloatingRotation;

    [Header("觸發設定")]

    [Tooltip("成功拿到斧頭後是否關閉觸發區")]
    [SerializeField]
    private bool disableTriggerAfterPickup = true;

    [Tooltip("切換物理狀態後等待多久才執行抓取")]
    [SerializeField]
    private float pickupDelay = 0.05f;

    // 避免同一個觸發區重複執行
    private bool hasPickedUp = false;

    // 儲存 AxeTriggerArea 上的 Collider
    private Collider triggerCollider;

    private void Awake()
    {
        // 取得此物件上的 Collider
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider == null)
        {
            Debug.LogError(
                "AxeTriggerArea 沒有 Collider，請加入 Sphere Collider。"
            );

            return;
        }

        // 確保此 Collider 為觸發區
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 已觸發過，不再重複執行
        if (hasPickedUp)
        {
            return;
        }

        // 尚未指定 XR Origin
        if (xrOriginRoot == null)
        {
            Debug.LogError(
                "AxeAutoPickupTrigger 尚未指定 XR Origin Root。"
            );

            return;
        }

        // 判斷進入 Trigger 的 Collider
        // 是否為 XR Origin 本身或其子物件
        bool belongsToXROrigin =
            other.transform == xrOriginRoot ||
            other.transform.IsChildOf(xrOriginRoot);

        if (!belongsToXROrigin)
        {
            return;
        }

        hasPickedUp = true;

        StartCoroutine(PickupAxeWithRightHand());
    }

    /// <summary>
    /// 停止斧頭展示狀態，
    /// 並由右手 Near-Far Interactor 自動選取斧頭。
    /// </summary>
    private IEnumerator PickupAxeWithRightHand()
    {
        // 檢查必要物件是否有指定
        if (rightHandInteractor == null)
        {
            Debug.LogError(
                "尚未指定右手 Near-Far Interactor。"
            );

            hasPickedUp = false;
            yield break;
        }

        if (axeGrabInteractable == null)
        {
            Debug.LogError(
                "尚未指定斧頭的 XR Grab Interactable。"
            );

            hasPickedUp = false;
            yield break;
        }

        if (axeRigidbody == null)
        {
            Debug.LogError(
                "尚未指定斧頭的 Rigidbody。"
            );

            hasPickedUp = false;
            yield break;
        }

        // 停止斧頭旋轉與懸浮
        if (axeFloatingRotation != null)
        {
            axeFloatingRotation.StopDisplayEffect();
        }

        // 取得斧頭本身的 Transform
        Transform axeTransform = axeGrabInteractable.transform;

        // 將斧頭移出 AxeDisplayRoot
        // 避免斧頭拿到右手後仍受到父物件旋轉影響
        axeTransform.SetParent(null, true);

        // 清除原本可能殘留的物理速度
#if UNITY_6000_0_OR_NEWER
        axeRigidbody.linearVelocity = Vector3.zero;
#else
        axeRigidbody.velocity = Vector3.zero;
#endif

        axeRigidbody.angularVelocity = Vector3.zero;

        // 將斧頭交回 XR Interaction Toolkit 控制
        axeRigidbody.isKinematic = false;

        // 斧頭目前要直接吸附到右手
        // 手持期間先關閉重力
        axeRigidbody.useGravity = false;

        // 讓移動和旋轉更平順
        axeRigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;

        // 避免快速揮動斧頭時穿過樹木
        axeRigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        // 等待一個影格，讓 Rigidbody 與階層更新
        yield return null;

        // 額外等待少量時間
        if (pickupDelay > 0f)
        {
            yield return new WaitForSeconds(pickupDelay);
        }

        // 取得右手所使用的 XR Interaction Manager
        XRInteractionManager interactionManager =
            rightHandInteractor.interactionManager;

        if (interactionManager == null)
        {
            Debug.LogError(
                "右手 Near-Far Interactor 沒有連接 XR Interaction Manager。"
            );

            hasPickedUp = false;
            yield break;
        }

        // 將元件轉成新版 XRI 使用的選取介面
        IXRSelectInteractor selectInteractor =
            rightHandInteractor;

        IXRSelectInteractable selectInteractable =
            axeGrabInteractable;

        // 確認斧頭目前沒有被其他手或 Interactor 抓住
        if (!axeGrabInteractable.isSelected)
        {
            // 新版 XRI 對外公開的 SelectEnter
            // 使用兩個參數
            interactionManager.SelectEnter(
                selectInteractor,
                selectInteractable
            );
        }

        // 檢查是否成功被右手選取
        if (axeGrabInteractable.isSelected)
        {
            Debug.Log(
                "XR Origin 已進入觸發區，斧頭已自動交到右手。"
            );

            // 成功後關閉觸發區
            if (disableTriggerAfterPickup &&
                triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
        }
        else
        {
            Debug.LogWarning(
                "已執行自動抓取，但斧頭尚未被選取。請檢查 Interaction Layer Mask。"
            );

            hasPickedUp = false;
        }
    }

    /// <summary>
    /// 重設觸發狀態。
    /// 關卡重新開始時可以呼叫。
    /// </summary>
    public void ResetPickup()
    {
        hasPickedUp = false;

        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
    }
}