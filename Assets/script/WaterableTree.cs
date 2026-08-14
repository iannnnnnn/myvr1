using System;
using UnityEngine;

/// <summary>
/// 可澆水樹木：累積水量，進度條達標後才長大一階（小／中／大）。
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class WaterableTree : MonoBehaviour
{
    /// <summary>階段前進時（newStage）。</summary>
    public event Action<WaterableTree, int> StageAdvanced;

    /// <summary>到達最大階段時。</summary>
    public event Action<WaterableTree> BecameFullyGrown;

    /// <summary>澆水達標並排隊一階成長（尚未套用外觀）時。</summary>
    public event Action<WaterableTree> GrowthQueued;

    /// <summary>進度 0～1 變化（供進度條）。</summary>
    public event Action<float> ProgressChanged;

    [Header("Stages (Small / Medium / Large)")]
    [Tooltip("預先嵌在 Prefab 裡的各階段根物件（編輯模式可見）")]
    [SerializeField] GameObject[] stageRoots;

    [Tooltip("備援：僅在 stageRoots 為空時實例化")]
    [SerializeField] GameObject[] stagePrefabs;

    [Header("Growth")]
    [SerializeField] float waterPerStage = 1f;
    [SerializeField] int startingStage;

    [Tooltip("true=進度滿立刻長大；false=只排隊，等外部套用")]
    [SerializeField] bool advanceStagesOnWater = true;

    int _stage;
    float _waterAccumulated;
    int _queuedAdvances;
    bool _maxLogged;

    public int CurrentStage => _stage;
    public int MaxStageIndex => StageCount - 1;
    public float WaterAccumulated => _waterAccumulated;
    public float WaterPerStage => Mathf.Max(0.01f, waterPerStage);
    public int QueuedAdvances => _queuedAdvances;
    public bool IsFullyGrown => StageCount > 0 && _stage >= StageCount - 1;

    /// <summary>目前這一階的澆水進度 0～1。</summary>
    public float Progress01
    {
        get
        {
            if (IsFullyGrown && _queuedAdvances <= 0)
                return 1f;
            return Mathf.Clamp01(_waterAccumulated / WaterPerStage);
        }
    }

    public bool AdvanceStagesOnWater
    {
        get => advanceStagesOnWater;
        set => advanceStagesOnWater = value;
    }

    int StageCount => stageRoots != null ? stageRoots.Length : 0;

    int RemainingStages
    {
        get
        {
            if (StageCount <= 0)
                return 0;
            return Mathf.Max(0, StageCount - 1 - _stage - _queuedAdvances);
        }
    }

    void OnEnable()
    {
        EnsureStages();
        _stage = Mathf.Clamp(startingStage, 0, Mathf.Max(0, StageCount - 1));
        ApplyStageVisuals();
        NotifyProgress();
    }

    void Awake()
    {
        if (!Application.isPlaying)
            return;

        EnsureStages();
        _stage = Mathf.Clamp(startingStage, 0, Mathf.Max(0, StageCount - 1));
        ApplyStageVisuals();
        NotifyProgress();
    }

    void EnsureStages()
    {
        if (HasValidStageRoots())
            return;

        if (TryCollectStageChildren())
            return;

        if (stagePrefabs == null || stagePrefabs.Length == 0)
        {
            if (Application.isPlaying)
                Debug.LogWarning($"WaterableTree on {name}: 未設定 stageRoots 或 stagePrefabs。", this);
            stageRoots = Array.Empty<GameObject>();
            return;
        }

        stageRoots = new GameObject[stagePrefabs.Length];
        for (int i = 0; i < stagePrefabs.Length; i++)
        {
            var prefab = stagePrefabs[i];
            if (prefab == null)
            {
                Debug.LogWarning($"WaterableTree on {name}: stagePrefabs[{i}] 為空。", this);
                continue;
            }

            GameObject instance;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform);
            else
#endif
                instance = Instantiate(prefab, transform);

            instance.name = $"Stage_{i}_{prefab.name}";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(i == startingStage);
            stageRoots[i] = instance;
        }
    }

    bool HasValidStageRoots()
    {
        if (stageRoots == null || stageRoots.Length == 0)
            return false;

        for (int i = 0; i < stageRoots.Length; i++)
        {
            if (stageRoots[i] != null)
                return true;
        }

        return false;
    }

    bool TryCollectStageChildren()
    {
        var found = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i).gameObject;
            if (child.name.StartsWith("Stage_"))
                found.Add(child);
        }

        if (found.Count == 0)
            return false;

        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        stageRoots = found.ToArray();
        return true;
    }

    public void AddWater(float amount)
    {
        if (!Application.isPlaying)
            return;

        if (amount <= 0f || StageCount == 0)
            return;

        if (IsFullyGrown && _queuedAdvances <= 0)
        {
            if (!_maxLogged)
            {
                _maxLogged = true;
                Debug.Log($"WaterableTree '{name}' 已達最大階段。", this);
            }

            NotifyProgress();
            return;
        }

        _waterAccumulated += amount;
        float threshold = WaterPerStage;

        while (_waterAccumulated >= threshold && RemainingStages > 0)
        {
            // 先讓進度條到滿，再成長並清空
            _waterAccumulated = threshold;
            NotifyProgress();

            _waterAccumulated = 0f;

            if (advanceStagesOnWater)
                AdvanceStage();
            else
                QueueAdvance();
        }

        if (IsFullyGrown && _queuedAdvances <= 0)
            _waterAccumulated = 0f;

        NotifyProgress();
    }

    void QueueAdvance()
    {
        if (RemainingStages <= 0)
            return;

        _queuedAdvances++;
        Debug.Log($"WaterableTree '{name}' 排隊成長（queued={_queuedAdvances}）。", this);
        GrowthQueued?.Invoke(this);
    }

    public bool ApplyQueuedAdvance()
    {
        if (!Application.isPlaying || _queuedAdvances <= 0 || IsFullyGrown)
            return false;

        _queuedAdvances--;
        AdvanceStage();
        NotifyProgress();
        return true;
    }

    public bool AdvanceOneStage()
    {
        if (!Application.isPlaying || IsFullyGrown)
            return false;

        AdvanceStage();
        NotifyProgress();
        return true;
    }

    void AdvanceStage()
    {
        if (IsFullyGrown)
            return;

        _stage++;
        ApplyStageVisuals();
        Debug.Log($"WaterableTree '{name}' 成長至階段 {_stage}/{MaxStageIndex}。", this);

        StageAdvanced?.Invoke(this, _stage);

        if (IsFullyGrown)
            BecameFullyGrown?.Invoke(this);
    }

    void ApplyStageVisuals()
    {
        if (stageRoots == null)
            return;

        for (int i = 0; i < stageRoots.Length; i++)
        {
            if (stageRoots[i] != null)
                stageRoots[i].SetActive(i == _stage);
        }
    }

    void NotifyProgress()
    {
        ProgressChanged?.Invoke(Progress01);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        waterPerStage = Mathf.Max(0.01f, waterPerStage);
        if (stageRoots != null && stageRoots.Length > 0)
            startingStage = Mathf.Clamp(startingStage, 0, stageRoots.Length - 1);
        else if (stagePrefabs != null && stagePrefabs.Length > 0)
            startingStage = Mathf.Clamp(startingStage, 0, stagePrefabs.Length - 1);
    }
#endif
}
