using UnityEngine;

/// <summary>
/// 可澆水樹木：累積水量後在小／中／大階段間切換模型。
/// 優先使用 Prefab 內預先嵌好的 stageRoots；否則用 stagePrefabs。
/// ExecuteAlways：編輯模式也會預覽模型。
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class WaterableTree : MonoBehaviour
{
    [Header("Stages (Small / Medium / Large)")]
    [Tooltip("預先嵌在 Prefab 裡的各階段根物件（編輯模式可見）")]
    [SerializeField] GameObject[] stageRoots;

    [Tooltip("備援：僅在 stageRoots 為空時實例化")]
    [SerializeField] GameObject[] stagePrefabs;

    [Header("Growth")]
    [SerializeField] float waterPerStage = 1f;
    [SerializeField] int startingStage;

    int _stage;
    float _waterAccumulated;
    bool _maxLogged;

    public int CurrentStage => _stage;
    public int MaxStageIndex => StageCount - 1;
    public float WaterAccumulated => _waterAccumulated;
    public bool IsFullyGrown => StageCount > 0 && _stage >= StageCount - 1;

    int StageCount => stageRoots != null ? stageRoots.Length : 0;

    void OnEnable()
    {
        EnsureStages();
        _stage = Mathf.Clamp(startingStage, 0, Mathf.Max(0, StageCount - 1));
        ApplyStageVisuals();
    }

    void Awake()
    {
        if (!Application.isPlaying)
            return;

        EnsureStages();
        _stage = Mathf.Clamp(startingStage, 0, Mathf.Max(0, StageCount - 1));
        ApplyStageVisuals();
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
            stageRoots = System.Array.Empty<GameObject>();
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

        if (IsFullyGrown)
        {
            if (!_maxLogged)
            {
                _maxLogged = true;
                Debug.Log($"WaterableTree '{name}' 已達最大階段。", this);
            }
            return;
        }

        _waterAccumulated += amount;
        float threshold = Mathf.Max(0.01f, waterPerStage);

        while (!IsFullyGrown && _waterAccumulated >= threshold)
        {
            _waterAccumulated -= threshold;
            AdvanceStage();
        }

        if (IsFullyGrown)
            _waterAccumulated = 0f;
    }

    void AdvanceStage()
    {
        if (IsFullyGrown)
            return;

        _stage++;
        ApplyStageVisuals();
        Debug.Log($"WaterableTree '{name}' 成長至階段 {_stage}/{MaxStageIndex}。", this);
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
