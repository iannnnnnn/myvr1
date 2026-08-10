using System.Collections.Generic;
using UnityEngine;

public class BoyGlowTriggerController : MonoBehaviour
{
    [Header("要控制顯示與隱藏的 Glow 物件")]
    [SerializeField]
    private GameObject boyGlowObject;

    [Header("玩家標籤")]
    [SerializeField]
    private string playerTag = "Player";

    /*
        記錄目前位於觸發區內的玩家碰撞物件

        XR Origin 可能同時有 Character Controller
        手部碰撞器或其他子物件碰撞器

        使用 HashSet 可以避免其中一個碰撞器先離開時
        就立刻錯誤地重新顯示 Glow
    */
    private readonly HashSet<Collider> playerCollidersInside =
        new HashSet<Collider>();

    private void Start()
    {
        /*
            場景開始時讓 Glow 顯示
        */
        SetGlowVisible(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        /*
            判斷碰撞物件本身或其根物件
            是否屬於 Player
        */
        if (!IsPlayer(other))
        {
            return;
        }

        /*
            將進入的玩家碰撞器加入紀錄
        */
        playerCollidersInside.Add(other);

        /*
            玩家進入範圍後關閉 Glow
        */
        SetGlowVisible(false);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        /*
            移除已離開的玩家碰撞器
        */
        playerCollidersInside.Remove(other);

        /*
            確認所有玩家碰撞器都已離開後
            才重新顯示 Glow
        */
        if (playerCollidersInside.Count == 0)
        {
            SetGlowVisible(true);
        }
    }

    /*
        判斷是否為玩家

        支援 Collider 位於 XR Origin 根物件
        或 XR Origin 子物件的情況
    */
    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            return true;
        }

        Transform rootTransform = other.transform.root;

        if (rootTransform != null &&
            rootTransform.CompareTag(playerTag))
        {
            return true;
        }

        return false;
    }

    /*
        統一控制 Glow 顯示或隱藏
    */
    private void SetGlowVisible(bool visible)
    {
        if (boyGlowObject == null)
        {
            Debug.LogWarning(
                "尚未指定 boy001_Glow 物件",
                this
            );

            return;
        }

        boyGlowObject.SetActive(visible);
    }

    /*
        當此觸發區被停用時
        清除殘留的碰撞器紀錄
    */
    private void OnDisable()
    {
        playerCollidersInside.Clear();
    }
}