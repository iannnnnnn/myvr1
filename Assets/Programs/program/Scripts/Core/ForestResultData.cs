/// <summary>
/// 森林關卡結果載體（Value Type）。
/// Scene 轉場時以值複製傳遞，不經 Heap 配置、無 GC。
/// treeTypeId：0 未選、1 快速生長、2 原生樹種。
/// </summary>
public struct ForestResultData
{
    public int treeTypeId;
    public float recoveryRate;
    public int carbonAbsorbed;

    public ForestResultData(int treeTypeId, float recoveryRate, int carbonAbsorbed)
    {
        this.treeTypeId = treeTypeId;
        this.recoveryRate = recoveryRate;
        this.carbonAbsorbed = carbonAbsorbed;
    }

    public static ForestResultData None => new ForestResultData(0, 0f, 0);

    public bool HasSelection => treeTypeId > 0;
}
