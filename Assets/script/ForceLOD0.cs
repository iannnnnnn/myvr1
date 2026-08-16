using UnityEngine;

public class ForceLOD0 : MonoBehaviour
{
    private LODGroup lodGroup;

    private void Awake()
    {
        lodGroup = GetComponent<LODGroup>();

        if (lodGroup != null)
        {
            // 永遠使用 LOD0
            lodGroup.ForceLOD(0);
        }
    }
}