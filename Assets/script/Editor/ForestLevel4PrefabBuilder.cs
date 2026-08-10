using UnityEditor;
using UnityEngine;

/// <summary>
/// 把樹木／澆水壺模型正式嵌進 Prefab，讓 Scene 編輯模式也能看到。
/// 選單：Tools / Forest Level4 / Embed Models Into Prefabs
/// </summary>
public static class ForestLevel4PrefabBuilder
{
    const string GrowableTreePath = "Assets/Prefabs/ForestLevel4/GrowableTree.prefab";
    const string WateringCanPath = "Assets/Prefabs/ForestLevel4/WateringCan_XR.prefab";

    static readonly string[] StagePrefabPaths =
    {
        "Assets/GreenForest/Prefabs/Smalltree.prefab",
        "Assets/GreenForest/Prefabs/Oak2.prefab",
        "Assets/GreenForest/Prefabs/Oak3.prefab",
    };

    const string WateringCanVisualPath = "Assets/GardenTools/Watering Can/WateringCanPrefab.prefab";

    static readonly string[] MaterialPaths =
    {
        "Assets/GreenForest/Materials/leav.mat",
        "Assets/GreenForest/Materials/PineTreeBrunch.mat",
        "Assets/GreenForest/Materials/Rocks.mat",
        "Assets/GreenForest/Materials/Stones.mat",
        "Assets/GreenForest/Materials/StonesAO.mat",
        "Assets/GreenForest/Materials/Terrain.mat",
        "Assets/GreenForest/Materials/TreeBrunches.mat",
        "Assets/GreenForest/Materials/Trees.mat",
        "Assets/GardenTools/Watering Can/Materials/Watering Can.mat",
        "Assets/GardenTools/Watering Can/Materials/No Name.mat",
    };

    [InitializeOnLoadMethod]
    static void ScheduleMaterialUpgrade()
    {
        EditorApplication.delayCall += UpgradeMaterialsToUrp;
    }

    [MenuItem("Tools/Forest Level4/Fix Pink Materials (URP)")]
    public static void UpgradeMaterialsToUrp()
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("找不到 Universal Render Pipeline/Lit Shader。請確認 URP 套件已啟用。");
            return;
        }

        int upgraded = 0;
        foreach (string path in MaterialPaths)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                continue;

            Texture mainTexture = material.HasProperty("_MainTex")
                ? material.GetTexture("_MainTex")
                : null;
            Texture normalMap = material.HasProperty("_BumpMap")
                ? material.GetTexture("_BumpMap")
                : null;
            Color color = material.HasProperty("_Color")
                ? material.GetColor("_Color")
                : Color.white;
            float metallic = material.HasProperty("_Metallic")
                ? material.GetFloat("_Metallic")
                : 0f;
            float smoothness = material.HasProperty("_Glossiness")
                ? material.GetFloat("_Glossiness")
                : 0.5f;
            float cutoff = material.HasProperty("_Cutoff")
                ? material.GetFloat("_Cutoff")
                : 0.5f;

            material.shader = urpLit;
            material.SetTexture("_BaseMap", mainTexture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);

            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
            }

            bool isFoliage =
                material.name.Contains("leav") ||
                material.name.Contains("Brunch");
            if (isFoliage)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", cutoff);
                material.SetFloat("_Cull", 0f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            EditorUtility.SetDirty(material);
            upgraded++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Forest Level4：已將 {upgraded} 個材質轉成 URP/Lit。");
    }

    [MenuItem("Tools/Forest Level4/Embed Models Into Prefabs")]
    public static void EmbedModelsIntoPrefabs()
    {
        UpgradeMaterialsToUrp();
        EmbedGrowableTree();
        EmbedWateringCan();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "Forest Level4",
                "已把樹木與澆水壺模型嵌進 Prefab。\n請回到 ForestBasic，Scene 視窗現在應能直接看到模型。",
                "OK");
        }
        else
        {
            Debug.Log("Forest Level4: Embed Models Into Prefabs 完成。");
        }
    }

    static void EmbedGrowableTree()
    {
        var root = PrefabUtility.LoadPrefabContents(GrowableTreePath);
        try
        {
            // 清掉舊的 Stage_* 子物件
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i);
                if (child.name.StartsWith("Stage_"))
                    Object.DestroyImmediate(child.gameObject);
            }

            var stages = new GameObject[StagePrefabPaths.Length];
            for (int i = 0; i < StagePrefabPaths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StagePrefabPaths[i]);
                if (prefab == null)
                {
                    Debug.LogError($"找不到階段 Prefab：{StagePrefabPaths[i]}");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.name = $"Stage_{i}_{prefab.name}";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                instance.SetActive(i == 0);
                stages[i] = instance;
            }

            var tree = root.GetComponent<WaterableTree>();
            if (tree != null)
            {
                var so = new SerializedObject(tree);
                so.FindProperty("stageRoots").arraySize = stages.Length;
                for (int i = 0; i < stages.Length; i++)
                    so.FindProperty("stageRoots").GetArrayElementAtIndex(i).objectReferenceValue = stages[i];

                var stagePrefabs = so.FindProperty("stagePrefabs");
                stagePrefabs.arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, GrowableTreePath);
            Debug.Log($"已更新 {GrowableTreePath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void EmbedWateringCan()
    {
        var root = PrefabUtility.LoadPrefabContents(WateringCanPath);
        try
        {
            var existing = root.transform.Find("Visual");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WateringCanVisualPath);
            if (visualPrefab == null)
            {
                Debug.LogError($"找不到澆水壺模型：{WateringCanVisualPath}");
                return;
            }

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            var rb = visual.GetComponent<Rigidbody>();
            if (rb != null)
                Object.DestroyImmediate(rb);

            foreach (var col in visual.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(col);

            var can = root.GetComponent<WateringCan>();
            if (can != null)
            {
                var so = new SerializedObject(can);
                so.FindProperty("visualPrefab").objectReferenceValue = null;
                var spout = root.transform.Find("Spout");
                if (spout != null)
                    so.FindProperty("spout").objectReferenceValue = spout;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (root.GetComponent<ToolFloatingDisplay>() == null)
                root.AddComponent<ToolFloatingDisplay>();
            if (root.GetComponent<ToolGrabController>() == null)
                root.AddComponent<ToolGrabController>();

            PrefabUtility.SaveAsPrefabAsset(root, WateringCanPath);
            Debug.Log($"已更新 {WateringCanPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
