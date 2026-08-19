# myvr1 — VR 場景流程說明

Unity VR 專案（URP / XR Interaction Toolkit）。目前 Build 流程：**S1 開場 → ForestBasic 森林關**。

建議 Unity 版本：**6000.5.x**（與 `ProjectSettings/ProjectVersion.txt` 一致）。

---

## Clone 後快速開始

```bash
git clone https://github.com/iannnnnnn/myvr1.git
cd myvr1
git checkout jiafu
```

1. 用 Unity **6000.5.x** 開啟專案
2. 等待 Package Manager 自動下載依賴
3. Play 從 **S1_TwoFutures** 開始（會直接進 **ForestBasic**）

repo 已含 `ForestAnimals`、`GreenForest`、UI 圖、地形與 XRI Sample。若仍有 Missing Prefab，見下方「本機可能仍需要的素材包」。

---

## 場景流程

```
S1_TwoFutures  →  ForestBasic
```

| 場景 | 路徑 | 說明 |
|------|------|------|
| S1 | `Assets/Scenes/S1_TwoFutures.unity` | 開場／兩種未來選擇 |
| 森林 | `Assets/Scenes/ForestBasic.unity` | 森林關（地形、互動、Level 1～4） |

Build Settings 已啟用：`S1`、`ForestBasic`。S1 的 `SceneFlowController.nextSceneName` 設為 `ForestBasic`。

其他場景（`S2_RegionSelect`、`City01` 等）在 repo 內，但不在目前 Build 流程中。

---

## S1 / ForestBasic 用到的 Package

### 共用（兩場景都要）

| Package | 版本 | 用途 |
|---------|------|------|
| Universal RP | `com.unity.render-pipelines.universal` 17.5.0 | 渲染管线 |
| Shader Graph | `com.unity.shadergraph` 17.5.0 | URP 依赖；地形草 Shader |
| UGUI | `com.unity.ugui` 2.5.0 | Canvas、Image、Button |
| Input System | `com.unity.inputsystem` 1.19.0 | XR 输入（XRI 依赖） |
| XR Interaction Toolkit | `com.unity.xr.interaction.toolkit` 3.5.1 | VR 互动核心 |
| XR Core Utils | `com.unity.xr.core-utils` 2.6.0 | XRI 依赖 |
| XR Management | `com.unity.xr.management` 4.5.4 | XR 插件管理 |
| OpenXR Plugin | `com.unity.xr.openxr` 1.17.1 | 头显 / OpenXR |

**TextMesh Pro**：S1 用到，来自项目内 `Assets/TextMesh Pro/`（非额外 UPM）。

**Repo 内 Sample**（由 XRI Package 导入后保存在 Assets）：

- `Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/` — XR Origin (XR Rig)
- `Assets/Samples/XR Interaction Toolkit/3.5.1/XR Interaction Simulator/` — Editor 模拟 VR（S1 使用）

### S1_TwoFutures

- UGUI + TextMesh Pro：开场 UI
- XRI `TrackedDeviceGraphicRaycaster`：XR 射线点 UI
- XR Interaction Simulator：Editor 内测试
- 自写脚本：`SceneFlowController`、`ObjectRotator`、`DelayedShow`

### ForestBasic

- **AI Navigation** `com.unity.ai.navigation` 2.0.14：`NavMeshSurface`、动物巡逻
- XRI `XRSimpleInteractable`：点选动物 / 物件
- XRI `XRGrabInteractable`：浇水壶、斧头等（Prefab 内）
- UGUI：InfoPopup、PauseMenu、关卡选择 UI
- Unity Terrain 模块：地形

### 打包 / 上机额外 Package（按平台）

| Package | 何时需要 |
|---------|----------|
| `com.unity.xr.meta-openxr` 2.5.0 | Quest / Meta 头显 |
| `com.unity.xr.androidxr-openxr` 1.3.1 | Android XR 设备 |

Editor 用 XR Interaction Simulator 测试时，不一定需要 Meta / Android XR Package。

### manifest 内有，但 S1 / ForestBasic 基本不用

HDRP、AR Foundation、XR Hands、ProBuilder、Timeline、Visual Scripting、IDE / Collab 等。

### 最小依赖清单（`Packages/manifest.json` 核心）

```json
"com.unity.render-pipelines.universal": "17.5.0",
"com.unity.shadergraph": "17.5.0",
"com.unity.ugui": "2.5.0",
"com.unity.inputsystem": "1.19.0",
"com.unity.xr.interaction.toolkit": "3.5.1",
"com.unity.xr.core-utils": "2.6.0",
"com.unity.xr.management": "4.5.4",
"com.unity.xr.openxr": "1.17.1",
"com.unity.ai.navigation": "2.0.14"
```

上 Quest 再加：`com.unity.xr.meta-openxr`

---

## 本機可能仍需要的素材包（未進 git）

`ForestAnimals`、`GreenForest` 已在 repo 內。若仍有 Missing Prefab／粉紅材質，可再補以下素材：

| 資料夾 | 用途 |
|--------|------|
| `Assets/Tree_Packs/` | URP 樹模型 |
| `Assets/ALP_Assets/` | 地形貼圖等 |
| `Assets/NatureManufacture Assets/` | 自然場景素材 |
| `Assets/Blue Polygon/` | 場景物件 |

`Grass And Flowers Pack 1` 等部分環境包已在 repo 內。

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
| `SceneFlowController` | `Assets/script/` | S1 進 ForestBasic |
| `UIManager` | `Assets/Programs/program/Scripts/UI/` | 共用 InfoPopup |
| `InteractableInfoPopup` | 同上 | 掛在可點物件上 |
| `InteractableGlowHint` | `Assets/script/` | 發光提示 |
| `AnimalWander` | `Assets/script/` | 動物巡邏 |

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
- repo 已含 `ForestAnimals`、`GreenForest`、UI、地形与 XRI Sample

```bash
git checkout jiafu
git pull origin jiafu
```

---

## 常見問題

**S1 进不了 ForestBasic**  
确认 Build Settings 有勾 `ForestBasic`，且 S1 的 `SceneFlowController.nextSceneName` 为 `ForestBasic`。

**熊點不到**  
檢查 Box Collider、XR Simple Interactable；Colliders 列表不要留空參考。

**InfoPopup 糊**  
提高 Dynamic Pixels Per Unit。

**Close 點不到**  
用雷射指到按鈕再按 Trigger；或直接再按一次 Trigger 關閉。

**Animator 視窗一直噴 NullReference**  
多半是 Editor Animator 視窗已知問題，關掉 Animator 或重開 Unity 即可，通常不影響 Play。
