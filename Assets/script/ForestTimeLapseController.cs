using UnityEngine;

/// <summary>
/// 森林縮時：種植區進度條滿、整區長大一階後，播太陽日夜（2 秒 × 3 輪）並推進年份。
/// </summary>
[DisallowMultipleComponent]
public class ForestTimeLapseController : MonoBehaviour
{
    [SerializeField] SunDayNightCycle sunCycle;

    [Header("Goal")]
    [Tooltip("要監聽的種植區；有值時優先用區事件（一區長大只觸發一次縮時）")]
    [SerializeField] PlantingZone[] targetZones;

    [Tooltip("要監聽的樹；targetZones 為空時才用。留空則自動找場景內所有 WaterableTree")]
    [SerializeField] WaterableTree[] targetTrees;

    [Tooltip("本回合需要幾次「階段前進」才觸發縮時")]
    [SerializeField] int advancesPerTimeLapse = 1;

    [Tooltip("false=條滿立刻長大（第四關進度條模式）；true=延到第一次日出")]
    [SerializeField] bool deferTreeGrowthToFirstSunrise;

    [Header("Year")]
    [SerializeField] int currentYear;
    [SerializeField] int yearStep = 5;
    [SerializeField] int maxYear = 30;

    [Header("Debug")]
    [SerializeField] bool logProgress = true;

    int _advancesThisRound;

    public int CurrentYear => currentYear;
    public bool IsTimeLapsing => sunCycle != null && sunCycle.IsPlaying;

    void Awake()
    {
        if (sunCycle == null)
            sunCycle = GetComponent<SunDayNightCycle>();
        if (sunCycle == null)
            sunCycle = FindFirstObjectByType<SunDayNightCycle>();
    }

    void OnEnable()
    {
        ResolveTargets();
        ApplyDeferGrowthFlags();
        Subscribe(true);
    }

    void OnDisable()
    {
        Subscribe(false);
    }

    void OnValidate()
    {
        advancesPerTimeLapse = Mathf.Max(1, advancesPerTimeLapse);
        yearStep = Mathf.Max(1, yearStep);
        maxYear = Mathf.Max(yearStep, maxYear);
        currentYear = Mathf.Clamp(currentYear, 0, maxYear);
    }

    [ContextMenu("Debug/Play Time-Lapse Now")]
    public void DebugPlayTimeLapseNow()
    {
        if (!Application.isPlaying)
            return;
        TryStartTimeLapse();
    }

    public void SetTargetZones(PlantingZone[] zones)
    {
        Subscribe(false);
        targetZones = zones ?? System.Array.Empty<PlantingZone>();
        deferTreeGrowthToFirstSunrise = false;
        ResolveTargets();
        ApplyDeferGrowthFlags();
        if (isActiveAndEnabled)
            Subscribe(true);
    }

    public void SetTargetTrees(WaterableTree[] trees)
    {
        Subscribe(false);
        targetTrees = trees ?? System.Array.Empty<WaterableTree>();
        deferTreeGrowthToFirstSunrise = false;
        ApplyDeferGrowthFlags();
        if (isActiveAndEnabled)
            Subscribe(true);
    }

    bool UseZones => targetZones != null && targetZones.Length > 0;

    void ResolveTargets()
    {
        if (UseZones)
            return;

        if (targetZones == null || targetZones.Length == 0)
        {
            var zones = FindObjectsByType<PlantingZone>(FindObjectsSortMode.None);
            if (zones != null && zones.Length > 0)
            {
                targetZones = zones;
                return;
            }
        }

        if (targetTrees != null && targetTrees.Length > 0)
            return;

        targetTrees = FindObjectsByType<WaterableTree>(FindObjectsSortMode.None);
    }

    void ApplyDeferGrowthFlags()
    {
        // 種植區模式：樹不自己長大，由 PlantingZone 統一推進
        if (UseZones)
        {
            for (int z = 0; z < targetZones.Length; z++)
            {
                var zone = targetZones[z];
                if (zone == null || zone.Trees == null)
                    continue;
                for (int i = 0; i < zone.Trees.Length; i++)
                {
                    if (zone.Trees[i] != null)
                        zone.Trees[i].AdvanceStagesOnWater = false;
                }
            }
            return;
        }

        if (targetTrees == null)
            return;

        for (int i = 0; i < targetTrees.Length; i++)
        {
            if (targetTrees[i] != null)
                targetTrees[i].AdvanceStagesOnWater = !deferTreeGrowthToFirstSunrise;
        }
    }

    void Subscribe(bool subscribe)
    {
        if (UseZones)
        {
            for (int i = 0; i < targetZones.Length; i++)
            {
                var zone = targetZones[i];
                if (zone == null)
                    continue;
                zone.ZoneGrewOneStage -= OnZoneGrew;
                if (subscribe)
                    zone.ZoneGrewOneStage += OnZoneGrew;
            }
            return;
        }

        if (targetTrees == null)
            return;

        for (int i = 0; i < targetTrees.Length; i++)
        {
            var tree = targetTrees[i];
            if (tree == null)
                continue;

            tree.GrowthQueued -= OnTreeGrowthQueued;
            tree.StageAdvanced -= OnTreeStageAdvanced;

            if (!subscribe)
                continue;

            if (deferTreeGrowthToFirstSunrise)
                tree.GrowthQueued += OnTreeGrowthQueued;
            else
                tree.StageAdvanced += OnTreeStageAdvanced;
        }
    }

    void OnZoneGrew(PlantingZone zone)
    {
        RegisterAdvance(zone != null ? zone.name : "zone");
    }

    void OnTreeGrowthQueued(WaterableTree tree)
    {
        RegisterAdvance(tree != null ? tree.name : "tree");
    }

    void OnTreeStageAdvanced(WaterableTree tree, int newStage)
    {
        RegisterAdvance(tree != null ? $"{tree.name} stage {newStage}" : "tree");
    }

    void RegisterAdvance(string label)
    {
        if (currentYear >= maxYear)
            return;

        if (sunCycle != null && sunCycle.IsPlaying)
            return;

        _advancesThisRound++;
        if (logProgress)
            Debug.Log(
                $"ForestTimeLapse: {label}（本回合 {_advancesThisRound}/{advancesPerTimeLapse}）",
                this);

        if (_advancesThisRound < advancesPerTimeLapse)
            return;

        TryStartTimeLapse();
    }

    void TryStartTimeLapse()
    {
        if (currentYear >= maxYear)
            return;

        if (sunCycle == null)
        {
            Debug.LogWarning("ForestTimeLapseController: 缺少 SunDayNightCycle。", this);
            if (deferTreeGrowthToFirstSunrise)
                GrowTreesOnFirstSunrise();
            AdvanceYear();
            return;
        }

        if (sunCycle.IsPlaying)
            return;

        _advancesThisRound = 0;
        if (logProgress)
            Debug.Log($"ForestTimeLapse: 開始縮時 2s×3（目前 {currentYear} 年）", this);

        sunCycle.PlayCycles(3, deferTreeGrowthToFirstSunrise ? OnFirstSunrise : null, OnTimeLapseComplete);
    }

    void OnFirstSunrise()
    {
        if (logProgress)
            Debug.Log("ForestTimeLapse: 第一次日出 → 樹木長大", this);

        GrowTreesOnFirstSunrise();
    }

    void GrowTreesOnFirstSunrise()
    {
        if (!deferTreeGrowthToFirstSunrise)
            return;

        if (targetTrees == null)
            return;

        for (int i = 0; i < targetTrees.Length; i++)
        {
            var tree = targetTrees[i];
            if (tree != null && tree.QueuedAdvances > 0)
                tree.ApplyQueuedAdvance();
        }
    }

    void OnTimeLapseComplete()
    {
        AdvanceYear();
        if (logProgress)
            Debug.Log($"ForestTimeLapse: 縮時結束 → 第 {currentYear} 年", this);
    }

    void AdvanceYear()
    {
        currentYear = Mathf.Min(maxYear, currentYear + yearStep);
    }
}
