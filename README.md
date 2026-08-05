# myvr1 — VR 場景流程說明

Unity VR 專案（URP / XR Interaction Toolkit）。目前主要流程：選擇未來 → 選區域 → 進入城市或森林場景。

建議 Unity 版本：**6000.5.x**（與 `ProjectSettings/ProjectVersion.txt` 一致）。

---

## 場景流程

```
S1_TwoFutures  →  S2_RegionSelect  →  City01
                                   ↘  ForestBasic
```

| 場景 | 路徑 | 說明 |
|------|------|------|
| S1 | `Assets/Scenes/S1_TwoFutures.unity` | 開場／兩種未來選擇 |
| S2 | `Assets/Scenes/S2_RegionSelect.unity` | 選城市或森林 |
| 城市 | `Assets/Scenes/City01.unity` | 城市關 |
| 森林 | `Assets/Scenes/ForestBasic.unity` | 森林關（地形、互動物、InfoPopup） |

Build Settings 已啟用：`S1`、`S2`、`City01`、`ForestBasic`。

區域跳轉腳本：`Assets/Script/RegionSelectController.cs`  
- 城市 → `City01`  
- 森林 → `ForestBasic`

---

## 本機需要的素材包（未進 git）

`jiafu` 分支為控制體積，**大型 Asset Store 素材包未上傳**。若 `ForestBasic` 出現 Missing Prefab／粉紅材質，請在本機補齊並放回對應路徑：

| 資料夾 | 用途 |
|--------|------|
| `Assets/ForestAnimals/` | 熊等動物模型 |
| `Assets/Tree_Packs/` | URP 樹模型 |
| `Assets/GreenForest/` | 森林相關資源／舊 PostProcessing |
| `Assets/ALP_Assets/` | 地形貼圖等 |
| `Assets/NatureManufacture Assets/` | 自然場景素材 |
| `Assets/Grass And Flowers Pack 1/` | 草與花 |
| `Assets/Blue Polygon/` | 場景物件 |

已上傳的場景／腳本／`New Terrain 6.asset` 可直接用；缺的是模型與貼圖本體。

---

## ForestBasic 重點

### XR 移動（Simulator／頭顯）

選 `XR Origin (XR Rig)` → `Locomotion`，確認開啟：

- **Move**（走路）
- **Gravity**（貼地）
- **Turn**（轉向）

地形需有 **Terrain Collider**（預設通常有）。

### 可點物件（熊等）

必要元件：

1. **Box Collider**（包住模型，否則雷射點不到）
2. **XR Simple Interactable**
3. **Interactable Glow Hint**（呼吸發光）
4. **Interactable Info Popup**（點擊開資訊）

場景需有 **InfoPopup** Prefab：  
`Assets/Programs/program/Prefabs/UI/InfoPopup.prefab`

詳細步驟見：  
`Assets/Programs/program/Docs/InteractableGlowAndInfoPopup.md`

### InfoPopup 調整

| 要改的 | 在哪裡 |
|--------|--------|
| 相對動物高度／位置 | 物件上 `Interactable Info Popup` → **Popup Offset** |
| 文字清晰度 | `InfoPopup` → `PopupCanvas` → **Canvas Scaler** → **Dynamic Pixels Per Unit**（建議 30～50） |
| 關閉方式 | 點 Close，或再按 Trigger／Primary |

彈窗會跟隨被點物件，並面向攝影機。

---

## 主要腳本

| 腳本 | 路徑 | 用途 |
|------|------|------|
| `RegionSelectController` | `Assets/Script/` | S2 進城市／森林 |
| `UIManager` | `Assets/Programs/program/Scripts/UI/` | 共用 InfoPopup |
| `InteractableInfoPopup` | 同上 | 掛在可點物件上 |
| `InteractableGlowHint` | `Assets/Script/` | 發光提示 |
| `AnimalWander` | `Assets/Script/` | 動物巡邏 |

---

## 傳地形給另一台電腦

高度／貼圖／草樹實例在 **TerrainData**，不在 scene  alone：

1. Scene 檔（如 `ForestBasic.unity`）+ `.meta`
2. TerrainData（如 `Assets/New Terrain 6.asset`）+ `.meta`
3. 有新增的 `.terrainlayer`、貼圖、Prefab 也要一起傳

`.meta` 的 GUID 必須保留，路徑建議一致。

---

## 分支說明

- 工作分支：`jiafu`
- 推送時可跳過大型素材包，只同步場景、腳本、設定與 TerrainData

```bash
git checkout jiafu
git pull origin jiafu
```

---

## 常見問題

**S2 點森林沒反應**  
確認 Build Settings 有勾 `ForestBasic`，且場景名與 `RegionSelectController` 的 `forestSceneName` 一致。

**熊點不到**  
檢查 Box Collider、XR Simple Interactable；Colliders 列表不要留空參考。

**InfoPopup 糊**  
提高 Dynamic Pixels Per Unit。

**Close 點不到**  
用雷射指到按鈕再按 Trigger；或直接再按一次 Trigger 關閉。

**Animator 視窗一直噴 NullReference**  
多半是 Editor Animator 視窗已知問題，關掉 Animator 或重開 Unity 即可，通常不影響 Play。
