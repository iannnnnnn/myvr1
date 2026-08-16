using System.Collections.Generic;
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

        RegisterTrees();
    }

    void RegisterTrees()
    {
        var list = new List<WaterableTree>();
        var zones = GetComponentsInChildren<PlantingZone>(true);
        for (int z = 0; z < zones.Length; z++)
        {
            var zone = zones[z];
            if (zone == null || zone.Trees == null)
                continue;
            for (int i = 0; i < zone.Trees.Length; i++)
            {
                if (zone.Trees[i] != null)
                    list.Add(zone.Trees[i]);
            }
        }

        if (timeLapse == null)
            timeLapse = FindFirstObjectByType<ForestTimeLapseController>();
        if (timeLapse != null)
            timeLapse.SetTargetTrees(list.ToArray());
    }
}
