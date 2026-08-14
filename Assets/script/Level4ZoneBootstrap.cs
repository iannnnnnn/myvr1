using UnityEngine;

/// <summary>
/// 已改為編輯模式寫入場景。若場景裡還留著此元件，不會在 Play 時重建。
/// 請用 Tools / Forest Level4 / Build Three Zones + Progress Bars。
/// </summary>
[DisallowMultipleComponent]
public class Level4ZoneBootstrap : MonoBehaviour
{
    [SerializeField] ForestTimeLapseController timeLapse;

    void Awake()
    {
        if (transform.Find("Zone_Oak") == null)
        {
            Debug.LogWarning(
                "Level4ZoneBootstrap: 場景尚無 Zone_Oak。請在編輯模式執行 Tools → Forest Level4 → Build Three Zones + Progress Bars。",
                this);
            return;
        }

        RegisterZones();
    }

    void RegisterZones()
    {
        var zones = GetComponentsInChildren<PlantingZone>(true);
        if (timeLapse == null)
            timeLapse = FindFirstObjectByType<ForestTimeLapseController>();
        if (timeLapse != null)
            timeLapse.SetTargetZones(zones);
    }
}
