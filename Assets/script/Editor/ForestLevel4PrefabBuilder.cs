using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 把樹木／澆水壺模型正式嵌進 Prefab，讓 Scene 編輯模式也能看到。
/// 選單：Tools / Forest Level4 / Embed Models Into Prefabs
/// </summary>
public static class ForestLevel4PrefabBuilder
{
    const string GrowableTreePath = "Assets/Prefabs/ForestLevel4/GrowableTree.prefab";
    const string WateringCanPath = "Assets/Prefabs/ForestLevel4/WateringCan_XR.prefab";
    const string ForestBasicScene = "Assets/Scenes/ForestBasic.unity";
    const string ConvertStationFlagPath = "Temp/convert_watering_station.flag";

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
        EditorApplication.delayCall += TryConsumeConvertStationFlag;
        AssemblyReloadEvents.afterAssemblyReload += () =>
            EditorApplication.delayCall += TryConsumeConvertStationFlag;
    }

    static void TryConsumeConvertStationFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string flagPath = Path.GetFullPath(ConvertStationFlagPath);
        if (!File.Exists(flagPath))
            return;

        try
        {
            File.Delete(flagPath);
        }
        catch
        {
            return;
        }

        ConvertSceneWateringCanToStationButton();
    }

    [MenuItem("Tools/Forest Level4/Convert Watering Can To Station Button")]
    public static void ConvertSceneWateringCanToStationButton()
    {
        var scene = EditorSceneManager.OpenScene(ForestBasicScene, OpenSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WateringCanPath);
        if (prefab == null)
        {
            Debug.LogError("找不到 WateringCan_XR prefab。");
            return;
        }

        GameObject stationGo = null;
        var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == "WateringCan_XR")
            {
                stationGo = all[i].gameObject;
                break;
            }
        }

        if (stationGo == null)
        {
            Debug.LogError("ForestBasic 找不到 WateringCan_XR。");
            return;
        }

        // 關掉可抓／澆水元件（場景覆寫）
        var grab = stationGo.GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.enabled = false;

        var watering = stationGo.GetComponent<WateringCan>();
        if (watering != null)
            watering.enabled = false;

        var toolGrab = stationGo.GetComponent<ToolGrabController>();
        if (toolGrab != null)
            toolGrab.enabled = false;

        var rb = stationGo.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (stationGo.GetComponent<XRSimpleInteractable>() == null)
            stationGo.AddComponent<XRSimpleInteractable>();

        var station = stationGo.GetComponent<WateringCanStationButton>();
        if (station == null)
            station = stationGo.AddComponent<WateringCanStationButton>();

        var so = new SerializedObject(station);
        so.FindProperty("heldCanPrefab").objectReferenceValue = prefab;
        so.FindProperty("heldLocalScale").vector3Value = new Vector3(0.015f, 0.015f, 0.025f);

        var spray = stationGo.transform.Find("Spout/SprayWater")
                     ?? stationGo.transform.Find("SprayWater");
        if (spray != null)
            so.FindProperty("waterParticleTemplate").objectReferenceValue =
                spray.GetComponent<ParticleSystem>();

        so.FindProperty("buttonScaleMultiplier").floatValue = 1.75f;
        so.FindProperty("applyScaleOnAwake").boolValue = false;
        so.FindProperty("enableGlow").boolValue = true;
        so.FindProperty("addPulseLight").boolValue = true;
        so.FindProperty("convertGrabToButtonOnAwake").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        var floating = stationGo.GetComponent<ToolFloatingDisplay>();
        if (floating != null)
        {
            var fso = new SerializedObject(floating);
            fso.FindProperty("rotateOnStart").boolValue = true;
            fso.FindProperty("enableFloating").boolValue = true;
            fso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(floating);
        }

        // 按鈕站放大（約 1.75x 原場景尺度）
        Undo.RecordObject(stationGo.transform, "Scale watering station");
        stationGo.transform.localScale = new Vector3(0.026f, 0.026f, 0.044f);

        EditorUtility.SetDirty(station);
        EditorUtility.SetDirty(stationGo);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("Forest Level4: WateringCan_XR 已轉成漂浮按鈕站（按一下生成右手壺）。");
        if (!Application.isBatchMode)
            EditorUtility.DisplayDialog(
                "Forest Level4",
                "場景灑水壺已改成按鈕站：\n放大＋發光漂浮\n按一下 → 右手拿到可澆水壺\n再按 → 收回",
                "OK");
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
