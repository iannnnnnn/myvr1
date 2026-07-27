using System;

/// <summary>
/// 全域靜態事件中樞：單向廣播碳排／預算變更。
/// 無狀態、無 MonoBehaviour、熱路徑零 GC Alloc。
/// 訂閱端必須在 OnDisable / OnDestroy 以 -= 解除，避免靜態事件殭屍參考。
/// </summary>
public static class EnvironmentEventHub
{
    public static event Action<float> OnCarbonChanged;
    public static event Action<float> OnBudgetChanged;

    public static void RaiseCarbonChanged(float newLevel)
    {
        // 本地快照：避免 Invoke 期間 -= 造成競態漏播或 NRE
        Action<float> handler = OnCarbonChanged;
        if (handler != null)
            handler.Invoke(newLevel);
    }

    public static void RaiseBudgetChanged(float newBudget)
    {
        Action<float> handler = OnBudgetChanged;
        if (handler != null)
            handler.Invoke(newBudget);
    }

    /// <summary>場景卸載或測試重置時清空所有訂閱，防止跨場景洩漏。</summary>
    public static void ClearAll()
    {
        OnCarbonChanged = null;
        OnBudgetChanged = null;
    }
}
