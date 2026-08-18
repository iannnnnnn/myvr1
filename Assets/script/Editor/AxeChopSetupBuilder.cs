using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 建立砍樹所需資產與場景結構。
/// 選單：Tools / Axe Chop
/// </summary>
public static class AxeChopSetupBuilder
{
    const string AxeVisualPath = "Assets/Stylized Tools/Prefabs/axe.prefab";
    const string AxeMaterialPath = "Assets/Stylized Tools/Materials/mat_axe.mat";
    const string HeldAxePrefabPath = "Assets/Prefabs/Axe/Axe_XR.prefab";

    const string TreeCompletePath = "Assets/tree/tree1_com.fbx";
    const string TreeUpperPath = "Assets/tree/tree1001.fbx";
    const string TreeLowerPath = "Assets/tree/tree1002.fbx";

    const string LeavesMaterialPath = "Assets/Material/leaves.mat";
    const string BranchNameToken = "branch";

    // 觸發半徑相對樹幹的外擴倍率，太大會在斧頭還沒碰到時就砍倒
    const float TriggerRadiusMargin = 1.15f;

    const string AxeTag = "Axe";
    const string ChopTreeRootName = "ChopTree";

    // New Scene 舊有斧頭上調好的握持姿態
    static readonly Vector3 DefaultHeldLocalPosition = new Vector3(0.02f, -0.02f, 0f);
    static readonly Vector3 DefaultHeldLocalEulerAngles = new Vector3(0f, -90f, -90f);

    [MenuItem("Tools/Axe Chop/Build All (Prefab + Scene)")]
    public static void BuildAll()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var stationAxe = scene.IsValid() ? FindStationAxe(scene) : null;

        ReadHeldPose(stationAxe, out Vector3 heldPosition, out Vector3 heldEulerAngles);

        var prefab = BuildHeldAxePrefab(heldPosition, heldEulerAngles);
        SetupCurrentScene(prefab);
    }

    [MenuItem("Tools/Axe Chop/Build Held Axe Prefab")]
    public static void BuildHeldAxePrefabMenu()
    {
        BuildHeldAxePrefab();
    }

    [MenuItem("Tools/Axe Chop/Setup Current Scene")]
    public static void SetupCurrentSceneMenu()
    {
        SetupCurrentScene(AssetDatabase.LoadAssetAtPath<GameObject>(HeldAxePrefabPath));
    }

    [MenuItem("Tools/Axe Chop/Apply Branch Leaves Material")]
    public static void ApplyBranchMaterialMenu()
    {
        var root = GameObject.Find(ChopTreeRootName);
        if (root == null)
        {
            Debug.LogWarning($"Axe Chop：場景中找不到 {ChopTreeRootName}。");
            return;
        }

        ApplyBranchMaterial(root);
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    public static GameObject BuildHeldAxePrefab()
    {
        return BuildHeldAxePrefab(DefaultHeldLocalPosition, DefaultHeldLocalEulerAngles);
    }

    /// <summary>
    /// 產生手上斧頭 Prefab：可抓、可碰撞、帶 Axe Tag。
    /// </summary>
    public static GameObject BuildHeldAxePrefab(Vector3 heldPosition, Vector3 heldEulerAngles)
    {
        var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AxeVisualPath);
        if (visualPrefab == null)
        {
            Debug.LogError($"Axe Chop：找不到斧頭模型 {AxeVisualPath}");
            return null;
        }

        EnsureFolder("Assets/Prefabs/Axe");

        var root = new GameObject("Axe_XR");
        try
        {
            SetTag(root, AxeTag);

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            foreach (var rb in visual.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(rb);
            foreach (var col in visual.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(col);

            RepairMissingMaterials(visual);

            if (!TryGetLocalBounds(root, out Bounds bounds))
            {
                Debug.LogError("Axe Chop：斧頭模型沒有 Renderer，無法計算碰撞範圍。");
                Object.DestroyImmediate(root);
                return null;
            }

            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
            box.isTrigger = false;

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 2f;
            rigidbody.useGravity = false;
            rigidbody.isKinematic = false;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            root.AddComponent<XRGrabInteractable>();

            var toolGrab = root.AddComponent<ToolGrabController>();
            WriteHeldPose(toolGrab, heldPosition, heldEulerAngles);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, HeldAxePrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Axe Chop：已建立手上斧頭 Prefab {HeldAxePrefabPath}" +
                $"（長度 {bounds.size}、握持位置 {heldPosition}、握持旋轉 {heldEulerAngles}）");
            return saved;
        }
        finally
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// 把目前場景的漂浮斧頭改成按鈕站，並建立可砍的樹。
    /// </summary>
    public static void SetupCurrentScene(GameObject heldAxePrefab)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("Axe Chop：沒有開啟的場景。");
            return;
        }

        GameObject stationAxe = FindStationAxe(scene);
        if (stationAxe == null)
            Debug.LogWarning("Axe Chop：場景中找不到名稱含 axe 的漂浮斧頭，將只建立樹木。");
        else
            ConfigureStationAxe(stationAxe, heldAxePrefab);

        BuildChopTree(stationAxe);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Axe Chop：場景設定完成，請記得存檔。");
    }

    static GameObject FindStationAxe(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name.ToLowerInvariant().Contains("axe"))
                return root;
        }

        return null;
    }

    static void ConfigureStationAxe(GameObject stationAxe, GameObject heldAxePrefab)
    {
        RepairMissingMaterials(stationAxe);

        // 舊的自動上手流程已由按鈕站取代
        var legacyGrab = stationAxe.GetComponent<ToolGrabController>();
        if (legacyGrab != null)
            Object.DestroyImmediate(legacyGrab);

        var legacyInteractable = stationAxe.GetComponent<XRGrabInteractable>();
        if (legacyInteractable != null)
            Object.DestroyImmediate(legacyInteractable);

        // 指向選取需要碰撞體
        if (stationAxe.GetComponentInChildren<Collider>(true) == null &&
            TryGetLocalBounds(stationAxe, out Bounds bounds))
        {
            var box = stationAxe.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }

        if (stationAxe.GetComponent<XRSimpleInteractable>() == null)
            stationAxe.AddComponent<XRSimpleInteractable>();

        var station = stationAxe.GetComponent<AxeStationButton>();
        if (station == null)
            station = stationAxe.AddComponent<AxeStationButton>();

        if (heldAxePrefab != null)
        {
            var so = new SerializedObject(station);
            so.FindProperty("heldAxePrefab").objectReferenceValue = heldAxePrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log($"Axe Chop：{stationAxe.name} 已設定為斧頭按鈕站。");
    }

    static void BuildChopTree(GameObject stationAxe)
    {
        var completeModel = AssetDatabase.LoadAssetAtPath<GameObject>(TreeCompletePath);
        var upperModel = AssetDatabase.LoadAssetAtPath<GameObject>(TreeUpperPath);
        var lowerModel = AssetDatabase.LoadAssetAtPath<GameObject>(TreeLowerPath);

        if (completeModel == null || upperModel == null || lowerModel == null)
        {
            Debug.LogError("Axe Chop：找不到 Assets/tree 下的樹木模型。");
            return;
        }

        var existing = GameObject.Find(ChopTreeRootName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        var root = new GameObject(ChopTreeRootName);

        var complete = (GameObject)PrefabUtility.InstantiatePrefab(completeModel, root.transform);
        complete.name = "Tree_Complete";
        ResetLocal(complete.transform);
        AddMeshColliders(complete, false);

        var cut = new GameObject("Tree_Cut");
        cut.transform.SetParent(root.transform, false);

        var upper = (GameObject)PrefabUtility.InstantiatePrefab(upperModel, cut.transform);
        upper.name = "Tree_Upper";
        ResetLocal(upper.transform);

        var lower = (GameObject)PrefabUtility.InstantiatePrefab(lowerModel, cut.transform);
        lower.name = "Tree_Lower";
        ResetLocal(lower.transform);
        AddMeshColliders(lower, false);

        TryGetLocalBounds(upper, out Bounds upperBounds);
        TryGetLocalBounds(complete, out Bounds treeBounds);
        TryGetLocalBounds(lower, out Bounds trunkBounds);

        float cutHeight = upperBounds.size.y > 0f ? upperBounds.min.y : treeBounds.size.y * 0.4f;

        // 只量下半樹（純樹幹），避免被樹冠寬度撐大
        float trunkWidth = Mathf.Max(trunkBounds.size.x, trunkBounds.size.z);
        float trunkRadius = trunkWidth > 0f
            ? trunkWidth * 0.5f * TriggerRadiusMargin
            : Mathf.Max(0.05f, treeBounds.size.y * 0.02f);

        // 貼著樹幹的膠囊；用整個樹冠當碰撞體會在倒下時卡住下半樹
        var upperCapsule = upper.AddComponent<CapsuleCollider>();
        upperCapsule.direction = 1;
        upperCapsule.radius = trunkRadius;
        upperCapsule.height = Mathf.Max(upperBounds.size.y, trunkRadius * 2f);
        upperCapsule.center = new Vector3(
            trunkBounds.center.x,
            cutHeight + upperCapsule.height * 0.5f,
            trunkBounds.center.z);

        var upperBody = upper.AddComponent<Rigidbody>();
        upperBody.isKinematic = true;
        upperBody.useGravity = false;
        upperBody.interpolation = RigidbodyInterpolation.Interpolate;
        upperBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        ApplyBranchMaterial(root);

        cut.SetActive(false);

        var trigger = new GameObject("AxeHitTrigger");
        trigger.transform.SetParent(root.transform, false);
        trigger.transform.localPosition = new Vector3(trunkBounds.center.x, cutHeight, trunkBounds.center.z);

        var triggerCollider = trigger.AddComponent<CapsuleCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.direction = 1;
        triggerCollider.radius = trunkRadius;
        triggerCollider.height = trunkRadius * 2f;

        var chop = trigger.AddComponent<TreeChopController>();
        var so = new SerializedObject(chop);
        so.FindProperty("treeComplete").objectReferenceValue = complete;
        so.FindProperty("treeCut").objectReferenceValue = cut;
        so.FindProperty("upperTreeRigidbody").objectReferenceValue = upperBody;
        so.FindProperty("lowerTree").objectReferenceValue = lower;
        so.ApplyModifiedPropertiesWithoutUndo();

        root.transform.position = ResolveTreePosition(stationAxe);

        Debug.Log(
            $"Axe Chop：已建立 {ChopTreeRootName}，樹高 {treeBounds.size.y:F2}、樹幹寬 {trunkWidth:F2}、" +
            $"切斷高度 {cutHeight:F2}、觸發半徑 {trunkRadius:F2}。");
    }

    /// <summary>
    /// 把樹放在斧頭站點前方的地面上。
    /// </summary>
    static Vector3 ResolveTreePosition(GameObject stationAxe)
    {
        Vector3 basePosition = stationAxe != null
            ? stationAxe.transform.position + stationAxe.transform.forward * 3f
            : new Vector3(3f, 0f, 3f);

        var terrain = Terrain.activeTerrain;
        basePosition.y = terrain != null
            ? terrain.SampleHeight(basePosition) + terrain.transform.position.y
            : 0f;

        return basePosition;
    }

    /// <summary>
    /// 樹枝節點（branch1-*）改用 Material/leaves。
    /// </summary>
    static void ApplyBranchMaterial(GameObject target)
    {
        var leaves = AssetDatabase.LoadAssetAtPath<Material>(LeavesMaterialPath);
        if (leaves == null)
        {
            Debug.LogWarning($"Axe Chop：找不到樹葉材質 {LeavesMaterialPath}");
            return;
        }

        int replaced = 0;
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (!IsBranch(renderer.transform))
                continue;

            var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = leaves;

            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
            replaced++;
        }

        Debug.Log($"Axe Chop：{replaced} 個樹枝 Renderer 已改用 {leaves.name}。");
    }

    static bool IsBranch(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.name.ToLowerInvariant().Contains(BranchNameToken))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 沿用場景斧頭上調好的握持姿態，沒有時採用 New Scene 的原設定。
    /// </summary>
    static void ReadHeldPose(GameObject stationAxe, out Vector3 position, out Vector3 eulerAngles)
    {
        position = DefaultHeldLocalPosition;
        eulerAngles = DefaultHeldLocalEulerAngles;

        if (stationAxe == null)
            return;

        var legacy = stationAxe.GetComponent<ToolGrabController>();
        if (legacy == null)
            return;

        var so = new SerializedObject(legacy);
        position = so.FindProperty("heldLocalPosition").vector3Value;
        eulerAngles = so.FindProperty("heldLocalEulerAngles").vector3Value;

        Debug.Log($"Axe Chop：沿用 {stationAxe.name} 的握持姿態 {position} / {eulerAngles}。");
    }

    static void WriteHeldPose(ToolGrabController toolGrab, Vector3 position, Vector3 eulerAngles)
    {
        var so = new SerializedObject(toolGrab);
        so.FindProperty("heldLocalPosition").vector3Value = position;
        so.FindProperty("heldLocalEulerAngles").vector3Value = eulerAngles;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 場景舊有的材質覆寫可能已被刪除，缺少材質時會顯示成粉紅色。
    /// </summary>
    static void RepairMissingMaterials(GameObject target)
    {
        var fallback = AssetDatabase.LoadAssetAtPath<Material>(AxeMaterialPath);
        if (fallback == null)
        {
            Debug.LogWarning($"Axe Chop：找不到斧頭材質 {AxeMaterialPath}");
            return;
        }

        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null)
                    continue;

                materials[i] = fallback;
                changed = true;
            }

            if (!changed)
                continue;

            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
            Debug.Log($"Axe Chop：{renderer.name} 遺失的材質已改用 {fallback.name}。");
        }
    }

    static void AddMeshColliders(GameObject target, bool convex)
    {
        foreach (var filter in target.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            if (filter.GetComponent<Collider>() != null)
                continue;

            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = convex;
        }
    }

    static bool TryGetLocalBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialised = false;
        Matrix4x4 worldToLocal = root.transform.worldToLocalMatrix;

        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            Bounds world = renderer.bounds;
            Vector3 localCenter = worldToLocal.MultiplyPoint3x4(world.center);
            Vector3 localExtents = worldToLocal.MultiplyVector(world.extents);
            localExtents = new Vector3(
                Mathf.Abs(localExtents.x),
                Mathf.Abs(localExtents.y),
                Mathf.Abs(localExtents.z));

            var localBounds = new Bounds(localCenter, localExtents * 2f);
            if (!initialised)
            {
                bounds = localBounds;
                initialised = true;
            }
            else
            {
                bounds.Encapsulate(localBounds);
            }
        }

        return initialised;
    }

    static void ResetLocal(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    static void SetTag(GameObject target, string tag)
    {
        try
        {
            target.tag = tag;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"Axe Chop：專案沒有 Tag「{tag}」，請先在 Tags & Layers 建立。");
        }
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folder);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
