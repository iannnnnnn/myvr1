# 可互動物件：發光提示 + 資訊框流程

之後要套到動物、樹木或其他模型，都照這份流程做。  
以熊（`Bear`）為範例，其他物件步驟相同。

---

## 完成後會有什麼效果

1. 物件**還沒被選取**時就會呼吸發光（提示可點）
2. 用雷射指向並按 **Trigger** 後，跳出小資訊框
3. 再按 Trigger，或按資訊框的 **Close**，可關閉

---

## 事前準備

- 場景建議用：`Assets/Scenes/Forest01.unity`
- 場景裡要有：`InfoPopup`  
  路徑：`Assets/Programs/program/Prefabs/UI/InfoPopup.prefab`  
  若 Hierarchy 沒有，從 Project 拖進場景即可
- 發光腳本：`InteractableGlowHint`
- 資訊框腳本：`InteractableInfoPopup`

---

## 步驟流程（每換一個模型就重做一次）

### 第 1 步：開場景

1. 開啟要放置的場景（例如 `Forest01`）
2. 確認 Hierarchy 有 `InfoPopup`

### 第 2 步：放入模型

1. 從 Project 找到模型 Prefab  
   例如熊：`Assets/ForestAnimals/URP/Bear/Prefab/Bear`
2. 拖進 Hierarchy / Scene
3. 放到玩家前方容易指到的位置
4. 建議改名，例如：`Bear Interactable`

### 第 3 步：加碰撞（沒有就點不到）

1. 選取物件
2. `Add Component` → **Box Collider**
3. 調整大小，讓框**包住整個模型**

> 之後換鹿、樹也一樣：Collider 一定要包住模型。

### 第 4 步：變成可點選

1. `Add Component`
2. 加上 **XR Simple Interactable**

> 只要「點一下開資訊」，用 **Simple**。  
> 不要用 Grab（Grab 是抓起來拿著）。

### 第 5 步：掛發光腳本

1. `Add Component` → **Interactable Glow Hint**
2. 建議設定：
   - **Emission Color**：橘黃或青綠
   - **Pulse Speed**：`2`
   - **Min Intensity**：`0.3`
   - **Max Intensity**：`2`
   - **Glowing**：勾選
3. **Target Renderers** 可留空  
   → 會自動抓子物件所有 Renderer

### 第 6 步：確認材質有開 Emission（很重要）

1. 選模型 → Inspector 找 **Mesh Renderer / Skinned Mesh Renderer**
2. 點開 Materials 裡的材質（例如 `Bear_URP`）
3. 找到 **Emissive / Emission**
4. 設定：
   - `UseEmissiveMap`：可先**不勾**（沒貼圖也沒關係）
   - **Emissive** 顏色：不要維持黑色，選一個有顏色的
   - HDR **Intensity**：拉到約 `1`～`2`
5. 存檔（Ctrl+S）

> 沒開 Emissive / Intensity 太低，腳本幾乎看不出發光。  
> 熊的材質是 `Universal Render Pipeline/Autodesk Interactive`，看的是 **Emissive** 欄位。

### 第 7 步：掛資訊框腳本

1. `Add Component` → **Interactable Info Popup**
2. 填入：
   - **Info Title**：例如 `熊`
   - **Info Content**：例如 `森林裡的熊，點擊可查看說明。`
   - **Info Image**：可選
   - **Info Audio Clip**：可選

### 第 8 步：測試

1. 按 Play
2. 用雷射指到物件 → 應看到呼吸發光
3. 按 Trigger → 應跳出 InfoPopup
4. 再按 Trigger 或按 Close → 關閉

---

## 做成 Prefab（方便之後重用）

測完一隻後建議存成 Prefab：

1. 把 Hierarchy 裡完成的物件  
   拖到：`Assets/Programs/program/Prefabs/Interactables/`
2. 命名例如：
   - `Bear Interactable`
   - `Deer Interactable`
   - `Tree Interactable`
3. 之後新場景直接拖 Prefab，不必重設元件

---

## 快速檢查清單

| 項目 | 有沒有 |
|---|---|
| 場景有 `InfoPopup` | ☐ |
| 有 `Box Collider` 且包住模型 | ☐ |
| 有 `XR Simple Interactable` | ☐ |
| 有 `Interactable Glow Hint` | ☐ |
| 材質 Emissive 已開、非黑色、Intensity > 0 | ☐ |
| 有 `Interactable Info Popup` 且有填標題/內容 | ☐ |
| Play 後能發光、能開資訊框 | ☐ |

---

## 常見問題

### 點不到物件
- 檢查有沒有 Collider
- Collider 是否太小、沒包到模型
- 是否有加 `XR Simple Interactable`

### 有掛發光腳本但看不見光
- 材質 Emissive 是否還是黑色
- Intensity 是否太低
- Renderer 是否在子物件上（Target Renderers 留空通常可自動抓到）

### 點了沒有資訊框
- 場景是否有 `InfoPopup`
- `UIManager` 是否存在（InfoPopup 上）
- `Interactable Info Popup` 是否有填內容
- Console 是否有警告訊息

### 想關掉發光
- 呼叫 `InteractableGlowHint.SetGlowing(false)`
- 或在 Inspector 取消勾選 **Glowing**

---

## 相關檔案路徑

- 發光腳本：`Assets/Script/InteractableGlowHint.cs`
- 資訊框腳本：`Assets/Programs/program/Scripts/UI/InteractableInfoPopup.cs`
- 彈窗管理：`Assets/Programs/program/Scripts/UI/UIManager.cs`
- 彈窗 Prefab：`Assets/Programs/program/Prefabs/UI/InfoPopup.prefab`
- 範本 Prefab：
  - `Assets/Programs/program/Prefabs/Interactables/Animal Interactable.prefab`
  - `Assets/Programs/program/Prefabs/Interactables/Tree Interactable.prefab`
- 熊模型：`Assets/ForestAnimals/URP/Bear/Prefab/Bear.prefab`
