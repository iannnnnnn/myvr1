using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 種植小區：約 5 棵同種樹共用一條進度條。
/// 澆到區內任一棵（或區碰撞）→ 整區進度增加；條滿 → 區內樹一起長大一階。
/// </summary>
[DisallowMultipleComponent]
public class PlantingZone : MonoBehaviour
{
    public event Action<float> ProgressChanged;
    public event Action<PlantingZone> ZoneGrewOneStage;

    [Header("Trees")]
    [SerializeField] WaterableTree[] trees;
    [SerializeField] GameObject treePrefab;
    [SerializeField] int treeCount = 5;
    [SerializeField] float clusterRadius = 2.2f;

    [Header("Zone Water")]
    [SerializeField] float waterPerStage = 1f;
    [SerializeField] Image progressFill;
    [SerializeField] bool createProgressUiIfMissing = true;
    [SerializeField] Vector3 progressLocalPosition = new Vector3(0f, 3.5f, 0f);

    [Header("Hit Volume")]
    [SerializeField] bool ensureZoneCollider = true;
    [SerializeField] float zoneColliderRadius = 3.5f;

    float _waterAccumulated;

    public WaterableTree[] Trees => trees;
    public WaterableTree Tree => trees != null && trees.Length > 0 ? trees[0] : null;
    public float Progress01 =>
        IsZoneFullyGrown
            ? 1f
            : Mathf.Clamp01(_waterAccumulated / Mathf.Max(0.01f, waterPerStage));
    public bool IsZoneFullyGrown
    {
        get
        {
            if (trees == null || trees.Length == 0)
                return true;
            for (int i = 0; i < trees.Length; i++)
            {
                if (trees[i] != null && !trees[i].IsFullyGrown)
                    return false;
            }
            return true;
        }
    }

    void Awake()
    {
        CollectTreesIfNeeded();
        ConfigureTrees();
        EnsureZoneCollider();
        EnsureProgressUi();
        NotifyProgress();
    }

    void OnEnable()
    {
        CollectTreesIfNeeded();
        ConfigureTrees();
        EnsureProgressUi();
        NotifyProgress();
    }

    void OnValidate()
    {
        treeCount = Mathf.Clamp(treeCount, 1, 12);
        clusterRadius = Mathf.Max(0.5f, clusterRadius);
        waterPerStage = Mathf.Max(0.01f, waterPerStage);
        zoneColliderRadius = Mathf.Max(0.5f, zoneColliderRadius);
    }

    /// <summary>澆水壺呼叫：一次加到整區進度。</summary>
    public void AddWater(float amount)
    {
        if (!Application.isPlaying || amount <= 0f)
            return;

        if (IsZoneFullyGrown)
        {
            NotifyProgress();
            return;
        }

        CollectTreesIfNeeded();
        float threshold = Mathf.Max(0.01f, waterPerStage);
        _waterAccumulated += amount;

        while (_waterAccumulated >= threshold && !IsZoneFullyGrown)
        {
            _waterAccumulated = threshold;
            NotifyProgress();

            _waterAccumulated = 0f;
            GrowAllTreesOneStage();
            ZoneGrewOneStage?.Invoke(this);
        }

        NotifyProgress();
    }

    public void BindProgressFill(Image fill)
    {
        progressFill = fill;
        EnsureProgressUi();
        NotifyProgress();
    }

#if UNITY_EDITOR
    [ContextMenu("Populate Trees In Zone (Edit Mode)")]
    public void EditorPopulateTrees()
    {
        if (treePrefab == null)
        {
            Debug.LogWarning("PlantingZone: 請先指定 treePrefab。", this);
            return;
        }

        PopulateTreesEditMode();
        EnsureZoneCollider();
        EnsureProgressUi();
        UnityEditor.EditorUtility.SetDirty(this);
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    void GrowAllTreesOneStage()
    {
        if (trees == null)
            return;

        for (int i = 0; i < trees.Length; i++)
        {
            var tree = trees[i];
            if (tree == null || tree.IsFullyGrown)
                continue;
            tree.AdvanceStagesOnWater = false;
            tree.AdvanceOneStage();
        }
    }

    void CollectTreesIfNeeded()
    {
        if (trees != null && trees.Length > 0)
        {
            bool any = false;
            for (int i = 0; i < trees.Length; i++)
            {
                if (trees[i] != null)
                {
                    any = true;
                    break;
                }
            }
            if (any)
                return;
        }

        trees = GetComponentsInChildren<WaterableTree>(true);
    }

    void ConfigureTrees()
    {
        if (trees == null)
            return;

        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i] != null)
                trees[i].AdvanceStagesOnWater = false;
        }
    }

    void EnsureZoneCollider()
    {
        if (!ensureZoneCollider)
            return;

        var sphere = GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.center = new Vector3(0f, 1f, 0f);
        sphere.radius = zoneColliderRadius;
    }

#if UNITY_EDITOR
    void PopulateTreesEditMode()
    {
        // 清掉舊樹（保留 ProgressCanvas）
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (child.name == "ProgressCanvas")
                continue;
            if (child.GetComponent<WaterableTree>() != null || child.name.StartsWith("GrowableTree"))
                DestroyImmediate(child);
        }

        trees = new WaterableTree[treeCount];
        for (int i = 0; i < treeCount; i++)
        {
            float angle = (Mathf.PI * 2f * i) / treeCount;
            Vector3 pos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * clusterRadius;

            GameObject treeGo;
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(treePrefab))
                treeGo = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(treePrefab, transform);
            else
                treeGo = Instantiate(treePrefab, transform);

            treeGo.name = $"{treePrefab.name}_{i + 1}";
            treeGo.transform.localPosition = pos;
            treeGo.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg + 180f, 0f);
            treeGo.transform.localScale = Vector3.one;

            var tree = treeGo.GetComponent<WaterableTree>();
            if (tree != null)
                tree.AdvanceStagesOnWater = false;
            trees[i] = tree;
        }
    }
#endif

    void EnsureProgressUi()
    {
        if (progressFill == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].type == Image.Type.Filled)
                {
                    progressFill = images[i];
                    break;
                }
            }
        }

        if (progressFill != null || !createProgressUiIfMissing)
            return;

        var canvasGo = new GameObject("ProgressCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = progressLocalPosition;
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = Vector3.one * 0.02f;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<BillboardFacingCamera>();

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1.6f, 0.28f);

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bgGo.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.14f, 0.85f);

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(canvasGo.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0.04f, 0.18f);
        fillRt.anchorMax = new Vector2(0.96f, 0.82f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        progressFill = fillGo.GetComponent<Image>();
        progressFill.color = new Color(0.25f, 0.75f, 1f, 0.95f);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;
    }

    void NotifyProgress()
    {
        float p = Progress01;
        if (progressFill != null)
            progressFill.fillAmount = p;
        ProgressChanged?.Invoke(p);
    }
}
