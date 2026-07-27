using System;
using UnityEngine;

/// <summary>
/// 以 1D 狀態向量維護選取狀態，並用增量內積維持剩餘預算與碳排（真正 O(1)）。
/// Category Mask 負責同組互斥，避免巢狀 if-else。
/// </summary>
public sealed class GameStateManager : MonoBehaviour
{
    [Header("Initial Resources")]
    [SerializeField] private float _initialBudget = 1000f;

    // S[i] ∈ {0,1}：選取狀態；C[i]：成本；E[i]：碳排
    private float[] _S;
    private float[] _C;
    private float[] _E;

    // 每個選項所屬的互斥組索引（-1 = 無互斥）
    private int[] _categoryOf;

    // categoryMasks[g] = 該組內所有選項的位元遮罩（選項數 ≤ 32 時使用）
    private int[] _categoryMasks;

    // 增量快取：等價於 S·C 與 S·E，避免每次 O(n) 重算
    private float _spentBudget;
    private float _totalCarbon;

    private int _optionCount;
    private bool _initialized;

    /// <summary>剩餘預算 = Initial − S·C</summary>
    public float RemainingBudget => _initialBudget - _spentBudget;

    /// <summary>最終碳排 = S·E</summary>
    public float TotalCarbon => _totalCarbon;

    public float InitialBudget => _initialBudget;
    public float SpentBudget => _spentBudget;
    public int OptionCount => _optionCount;
    public bool IsInitialized => _initialized;

    /// <summary>狀態變更後通知（UI / 數位孿生訂閱用）。勿在高頻路徑 new 任何東西。</summary>
    public event Action OnStateChanged;

    /// <summary>
    /// 以成本／碳排向量初始化狀態空間。
    /// categoryOf[i]：選項 i 的互斥組；同組只能選一個。-1 表示獨立可複選。
    /// </summary>
    public void Initialize(float[] costs, float[] emissions, int[] categoryOf, float initialBudget)
    {
        if (costs == null || emissions == null || categoryOf == null)
            throw new ArgumentNullException("costs / emissions / categoryOf must be non-null.");

        if (costs.Length != emissions.Length || costs.Length != categoryOf.Length)
            throw new ArgumentException("C, E, and categoryOf must share the same length.");

        if (costs.Length > 32)
            throw new ArgumentException("Category Mask uses Int32 bitfields; option count must be ≤ 32.");

        _optionCount = costs.Length;
        _initialBudget = initialBudget;

        // 一次配置，之後熱路徑不再 new（Zero-GC）
        _S = new float[_optionCount];
        _C = new float[_optionCount];
        _E = new float[_optionCount];
        _categoryOf = new int[_optionCount];

        Array.Copy(costs, _C, _optionCount);
        Array.Copy(emissions, _E, _optionCount);
        Array.Copy(categoryOf, _categoryOf, _optionCount);

        BuildCategoryMasks();

        _spentBudget = 0f;
        _totalCarbon = 0f;
        _initialized = true;

        OnStateChanged?.Invoke();
    }

    /// <summary>預先建立各互斥組的位元遮罩，讓組內清除為 O(1)。</summary>
    private void BuildCategoryMasks()
    {
        int maxCategory = -1;
        for (int i = 0; i < _optionCount; i++)
        {
            if (_categoryOf[i] > maxCategory)
                maxCategory = _categoryOf[i];
        }

        int groupCount = maxCategory + 1;
        _categoryMasks = groupCount > 0 ? new int[groupCount] : Array.Empty<int>();

        for (int i = 0; i < _optionCount; i++)
        {
            int g = _categoryOf[i];
            if (g < 0)
                continue;

            // 將選項 i 編入組 g 的遮罩
            _categoryMasks[g] |= (1 << i);
        }
    }

    /// <summary>唯讀查詢 S[i]（選取 = 1）。</summary>
    public float GetState(int index)
    {
        EnsureInitialized();
        ValidateIndex(index);
        return _S[index];
    }

    public bool IsSelected(int index) => GetState(index) > 0.5f;

    /// <summary>
    /// 設定選項選取狀態。同組互斥：選中時先以 Category Mask 清除同組其他位，再寫入新狀態。
    /// 預算／碳排以增量更新，維持 O(1)。
    /// </summary>
    public bool TrySetSelection(int index, bool selected)
    {
        EnsureInitialized();
        ValidateIndex(index);

        float target = selected ? 1f : 0f;
        if (Mathf.Approximately(_S[index], target))
            return false;

        if (selected)
        {
            int g = _categoryOf[index];
            if (g >= 0)
                ClearCategoryExcept(g, index);
        }

        ApplyDelta(index, target - _S[index]);
        OnStateChanged?.Invoke();
        return true;
    }

    /// <summary>切換選取；已選則取消，未選則選中（含互斥）。</summary>
    public bool TryToggle(int index)
    {
        EnsureInitialized();
        ValidateIndex(index);
        return TrySetSelection(index, !IsSelected(index));
    }

    /// <summary>
    /// 以 Category Mask 一次清除組內所有選項（可選擇保留 keepIndex）。
    /// 迴圈只掃位元集合，不做巢狀業務 if-else。
    /// </summary>
    private void ClearCategoryExcept(int category, int keepIndex)
    {
        int mask = _categoryMasks[category];
        // 去掉要保留的位，其餘同組位全部清零
        mask &= ~(1 << keepIndex);

        while (mask != 0)
        {
            int bit = mask & -mask;          // 最低設置位
            int i = TrailingZeroCount(bit);  // 選項索引
            if (_S[i] > 0.5f)
                ApplyDelta(i, -_S[i]);
            mask ^= bit;
        }
    }

    /// <summary>
    /// 增量更新：Δ(S·C) = Δs · C[i]，Δ(S·E) = Δs · E[i]。
    /// 等價於完整內積，但複雜度為 O(1)。
    /// </summary>
    private void ApplyDelta(int index, float deltaS)
    {
        if (Mathf.Approximately(deltaS, 0f))
            return;

        _S[index] += deltaS;
        _spentBudget += deltaS * _C[index];
        _totalCarbon += deltaS * _E[index];
    }

    /// <summary>完整重算內積（僅除錯／校驗用，熱路徑請用快取屬性）。</summary>
    public void RecalculateDotProducts(out float spentBudget, out float totalCarbon)
    {
        EnsureInitialized();
        spentBudget = 0f;
        totalCarbon = 0f;
        for (int i = 0; i < _optionCount; i++)
        {
            spentBudget += _S[i] * _C[i];
            totalCarbon += _S[i] * _E[i];
        }
    }

    /// <summary>重置所有選取狀態與快取。</summary>
    public void ResetAll()
    {
        EnsureInitialized();
        Array.Clear(_S, 0, _optionCount);
        _spentBudget = 0f;
        _totalCarbon = 0f;
        OnStateChanged?.Invoke();
    }

    /// <summary>將目前狀態向量複製到呼叫端緩衝（呼叫端預配置，避免 GC）。</summary>
    public void CopyStateTo(float[] destination)
    {
        EnsureInitialized();
        if (destination == null || destination.Length < _optionCount)
            throw new ArgumentException("destination must be non-null and length >= OptionCount.");
        Array.Copy(_S, destination, _optionCount);
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Call Initialize(...) before using GameStateManager.");
    }

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)_optionCount)
            throw new ArgumentOutOfRangeException(nameof(index));
    }

    /// <summary>不分配的 trailing-zero 計數（選項索引 = 位元位置）。</summary>
    private static int TrailingZeroCount(int value)
    {
        // value 保證為單一 bit；De Bruijn 風格也可，此處用簡單位移即可且無 GC
        int count = 0;
        while ((value & 1) == 0)
        {
            value >>= 1;
            count++;
        }
        return count;
    }

    private void OnDestroy()
    {
        // 防止訂閱者成為殭屍參考造成記憶體洩漏
        OnStateChanged = null;
    }
}
