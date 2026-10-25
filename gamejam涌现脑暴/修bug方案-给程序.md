# 修 bug 方案（10.13 构建 · 版本 9）

> **来源**：对着 `testgame` 里 `Assembly-CSharp.dll`（10.13 18:56 编译）逐行读出来的。
> **每一条都在代码里核过**，标了行号。行号对应我反编译出来的那份，你自己在工程里搜函数名定位。
>
> **例外**：生育冷却那条里的 `FindPartner` **不修**（不做"家庭"结构，父亲是谁无所谓）。
> **死内容**：只统计不删，见《死内容统计.md》。

---

## 0. 一句话

```
三个必须先修的：  粮食被一个加法吃掉 / 自动存档每 30 秒改活状态 / 暂停锁漏了导致村子静默停摆
五个该修的：      多产词条失效 / 讣告被改写 / 产出显示错十倍 / 地形版本判断自相矛盾 / 水面边界错位
```

**前三条不修，游戏是"看起来在跑、实际上不对"。**

---

## 1. ★ 钳位函数把粮仓和威信砍到 99 ★

### 现状

```csharp
// LawEngine:4725
private static int Clamp(int value) => Math.Max(0, Math.Min(99, value));
```

它被用在**共享资源**上：

```csharp
// AddToScope:4219-4227
case LawScope.Granary:  d.granaryFood = Clamp(d.granaryFood + amount);
case LawScope.Prestige: d.prestige      = Clamp(d.prestige + amount);

// SetInScope:4244-4255
case LawScope.Granary:  d.granaryFood = Clamp(amount);
case LawScope.Prestige: d.prestige      = Clamp(amount);

// DrainFromSelf:4300
d.granaryFood = Clamp(d.granaryFood + num2);
```

**但粮仓的范围是 `0..granaryCapacity`（默认 500，开局 220），威信的范围是 `0..300`**（`SimulateTick:5385` 自己按 300 截断）。

### 后果

任何一条"给粮仓加粮"的法律，只要粮仓 > 99，就会把粮仓**直接改成 99**：

| 法条 | 效果 | 粮仓 220 时 |
|---|---|---|
| 法 39 童工 | `Add Granary +1` | 220 → **99** |
| 法 58 光棍 | `Add Granary +2` | 220 → **99** |
| 法 51 常平 | `Add Granary +10` | 400 → **99** |
| 法 37 立威 | `Add Prestige +3` | 威信 150 → **99** |
| 法 28 捐纳 | `Add Prestige +3` | 同上 |
| 法 24 王权税契 | `Add Prestige +2` | 同上 |

**一百多斤粮被一个加法吃掉，且不报错。**

同函数还被用在 `WriteField:4704` 的村民属性上——那里 `0..99` 是**对的**，但 `LawField.Food` 的上限应该是 **40**（`PrivateFoodCap`，`Normalize:6715` 也按 40 收），现在会被放到 99。

### 改法

**按字段分开钳位**，一个函数管一类：

```csharp
// 村民属性
private static int ClampField(LawField field, int value)
{
    int max = field switch
    {
        LawField.Hunger => 99,
        LawField.Fear   => 99,
        LawField.Gold   => 99,
        LawField.Food   => 40,      // PrivateFoodCap
        _               => 999999,
    };
    return Math.Max(0, Math.Min(max, value));
}

// 共享资源
private static int ClampGranary(VillageSaveData d, int v)
    => Math.Max(0, Math.Min(d.granaryCapacity, v));
private static int ClampPrestige(int v)  => Math.Max(0, Math.Min(300, v));
private static int ClampTreasury(int v)  => Math.Max(0, Math.Min(999999, v));
```

替换掉上面 **6 处** `Clamp(...)` 调用 + `WriteField` 里的 4 处。

> ⚠️ **这条修完粮食会更多**，所以必须和第 6 条（产出/消耗）一起调，否则粮仓会直接顶死在 500。

**验收**：粮仓 300 时触发「童工」，粮仓应变成 301，不是 99。

**工时 0.2 天**

---

## 2. ★ 自动存档会篡改正在运行的模拟状态 ★

### 现状

```csharp
// VillageSaveService.SaveSlot:6417
data.version = 9;
data.slot = slot;
Normalize(data);          // ★ data 就是 GameSession.CurrentSave 本体
```

`SaveCurrent:6047-6058` 传进去的是**同一个引用**。而 `VillageUiController:3244-3249` **每 30 秒自动存档一次**。

于是 `Normalize` 每 30 秒对**活着的世界**做一次"修复"：

```csharp
// Normalize:6711
data.villagerAgeTicks[j] = Mathf.Clamp(data.villagerAgeTicks[j], 0, 2400);
```

**而一年是 2920 / 2928 tick**（`TicksInYear = DaysInYear×8`，`VillageTime:7037-7049`；`AdvanceAge` 要涨到 `>= TicksInYear` 才长一岁）。

→ 合法值域是 `[0, 2927]`，**2400 比一年还小**。任何落在 `[2401, 2919]` 的村民，每 30 秒被砍回 2400。

**这直接破坏"确定性整数模拟"的前提**：同一个存档读回来，因为存盘时机不同，轨迹就不同。

同一处的连带伤害：

```csharp
// Normalize:6684-6686
data.lastProduction = 0;
data.lastConsumption = 0;
data.lastPrestigeDelta = 0;
```

→ HUD 上刚做的「产出 X / 刻」和「本刻 N」**每次自动存档后被抹成 0**，直到下一个 56/60 tick 边界才恢复。这三个字段实际上从来没被真正持久化过。

```csharp
// Normalize:6712 / 6717
data.villagerGold[j]     = Mathf.Clamp(..., 0, 99);
data.villagerChildren[j] = Mathf.Clamp(..., 0, 12);
```

→ 存盘瞬间切掉运行中的合法值。

### 改法

**把"读档修复"和"存盘规范化"彻底拆开：**

```csharp
// 只给读档用
private static void RepairLoaded(VillageSaveData data) { ...现有的全部逻辑... }

// TryReadFile 里：JsonUtility.FromJson 之后调 RepairLoaded

// SaveSlot 里：删掉 Normalize(data)，只留
data.version = 9;
data.slot = slot;
data.updatedAtUtc = ...;
```

`RepairLoaded` 里 `lastProduction/lastConsumption/lastPrestigeDelta` **赋 0 是对的**（派生量不该存），但那是**读档**时的行为，不再影响活状态。

**`villagerAgeTicks` 的上限**：如果一定要留钳位，改成 `TicksInYear(villagerAges[j]) - 1`，而不是写死 2400。

**验收**：
- 连存两次档，村民的 `villagerAgeTicks` 不变
- 暂停后看 HUD，「产出」在自动存档前后一致
- 存档往返：存 → 读 → 再跑 1000 tick，两边的村民年龄序列完全一致

**工时 0.25 天**

---

## 3. ★ 观察暂停的锁永不释放 → 跨场景后村子静默停摆 ★

### 现状

```csharp
// GameTimePause:841 —— static，跨场景存活
private static readonly List<object> Locks = new List<object>();

// VillageUiController:3118
public void ToggleInspectPause()
{
    if (_inspectPaused) { GameTimePause.Release(PauseToken.Inspect); _inspectPaused = false; }
    else                { GameTimePause.Request(PauseToken.Inspect); _inspectPaused = true;  }
}

// VillageUiController:3158
private void OnDisable()
{
    Time.timeScale = 1f;      // ★ 只复位时间缩放，不释放锁
}
```

其它四个暂停持有者**都在 `OnDisable` 里成对释放**：`GameplayMenuController:641`、`LawSelectionController:1199`、`ObituaryHistoryController:1992`、`KeyBindingPanelController:948`。

**只有 `PauseToken.Inspect` 漏了。**

### 后果

按空格进观察暂停 → 菜单点「保存并退回主菜单」→ 再进游戏：

- `GameTimePause.IsPaused == true` → `UpdateSimulation:3196` 直接 `return`，**tick 不再推进**
- 但 `Time.timeScale == 1`、村民照常走动
- `_inspectPaused` 是新实例的 `false` → **暂停徽标不亮、速率还显示「1x · 18.5 天/秒」**
- **玩家看不到任何提示，但村子已经死了**

要连按两次空格才能救回来。

### 改法

```csharp
private void OnDisable()
{
    if (_inspectPaused)
    {
        GameTimePause.Release(PauseToken.Inspect);
        _inspectPaused = false;
    }
    Time.timeScale = 1f;
}
```

> 💡 **顺带建议**：`GameTimePause` 加一个 `ResetAll()`，在场景加载时清空 `Locks`，当作兜底。比逐个补 `OnDisable` 更稳。

**验收**：观察暂停 → 退回主菜单 → 重新进游戏，tick 应正常推进。

**工时 0.1 天**

---

## 4. 多产词条对母亲冷却完全无效

### 现状

```csharp
// ResolveBirths:5777
data.villagerBirthCooldown[i] = (Traits.HasInnate(data, i, 256u) ? 480 : 480);
//                                                           ↑ 多产            ↑ 两个分支一样
```

`? 480 : 480` —— 字面量完全相同，是明显的写错。

且 `MotherCooldownTicks`(5215) / `FatherCooldownTicks`(5217) 两个常量**声明后从未使用**。

### 改法

```csharp
data.villagerBirthCooldown[i] = Traits.HasInnate(data, i, Innate.Prolific)
    ? MotherCooldownTicks / 2      // 240
    : MotherCooldownTicks;         // 480
```

> **`FindPartner` 那半边不修**（不做家庭结构）。

**验收**：同种子下，带「多产」的母亲两次生育间隔应约为普通母亲的一半。

**工时 0.05 天**

---

## 5. 讣告详情面板读活数组 → 历史会被改写

### 现状

```csharp
// VillagerStatusPanelController.BuildObituaryText:2587
... + save.lawNames[save.obituaryLawIndices[record]]
```

讣告列表用的 `obituaryTexts[]` 是**死亡当刻生成的字符串，是自包含的**；但**详情面板是现拼的**，`lawNames` 读的是活数组。

**触发**：某人死于「第 4 条：耕作」→ 之后把第 4 槽换成别的法 → 再翻这条讣告，**法条行显示的是新法律的名字**。

### 改法

和上次给词条加快照是同一套做法——**讣告再加一个快照字段**：

```csharp
// VillageSaveData
public string[] obituaryLawNames = new string[0];   // ★ 死亡当刻该槽位的法名

// AppendObituary
Array.Resize(ref data.obituaryLawNames, obituaryCount + 1);
data.obituaryLawNames[obituaryCount] =
    (lawIndex >= 0 && lawIndex < data.lawNames.Length) ? data.lawNames[lawIndex] : string.Empty;
```

`BuildObituaryText` 改成读 `obituaryLawNames[record]`。
`Normalize` 里按现有 `ResizeArray` 模式补齐（旧档补 `string.Empty`，显示成"（旧版本存档，无记录）"）。
存档版本 **9 → 10**。

> ⚠️ 注意和现有 `obituaryTexts` 的一致性：那段文本里已经嵌了当时的法名，两边应该一致。可以用 `obituaryTexts` 交叉验证一次。

**工时 0.2 天**

---

## 6. 「产出 / 刻」的消耗口径漏了最大的一项

### 现状

```csharp
// AdvanceGranary:5552-5585
int num4 = population * 4 + val;   // 产出
int num5 = population / 3;         // ★ 只算了这一项当消耗
data.lastProduction  = num4;
data.lastConsumption = num5;
```

但**真正的消耗是村民吃粮**——`AdvanceNeeds:5444-5455` 每人每 16 tick 吃 1，即约 **3.5 / 人 / 56 tick**，比 `population/3` 大**约 10 倍**。

→ HUD 上那个净额是错的。

### 改法

`AdvanceGranary` 的补粮循环里已经有真实扣减，把它累计起来：

```csharp
int fed = 0;
for (...) { while (...) { data.villagerFood[j]++; data.granaryFood--; fed++; } }
data.lastProduction  = num4;
data.lastConsumption = num5 + fed;     // ★ 记上真实吃掉的
```

> ⚠️ **这只是把账算对。** 收支本身现在容差极小（产出约 `4×人口`，真实消耗约 `3.83×人口`，净盈余约 4%），而耕地系数（`1000 + num7*1400`，钳 `[250,1600]`）摆动幅度达 ±60%——**任何波动都能把收支翻成负数**。第 1 条修完粮食会更多，这三件事要一起调。建议给这一块单开一次参数扫描。

**工时 0.1 天（记账）+ 0.5 天（调参）**

---

## 7. 地形版本判断自相矛盾

### 现状

```csharp
// Normalize:6651-6655
if (data.mapGenVersion <= 0) data.mapGenVersion = 4;          // 先把 0 改成 4
if (data.terrain == null || data.terrain.Length != 1536
    || data.mapGenVersion != 4)                                // 这个判断就永远不成立
{ ...重新生成地形... }
```

→ `mapGenVersion == 0` 的老档会**保留旧编码的地形，却被打上新标签 4**。
而 `mapGenVersion == 3` 的档反而会被重建。**两者行为应当一致。**

### 改法

先判断、后赋值：

```csharp
bool needRebuild = data.terrain == null
                || data.terrain.Length != VillageMap.TileCount
                || data.mapGenVersion != VillageMap.GeneratorVersion;
if (needRebuild) { ...重新生成...; data.mapGenVersion = VillageMap.GeneratorVersion; }
else            { ...逐格清洗... }
```

并把三处硬编码的 `4` / `1536` / `48` / `32` 换成 `VillageMap.GeneratorVersion` / `VillageMap.TileCount` / `VillageMap.Width` / `VillageMap.Height`（这几个常量已经声明了，现在是死的）。

> ⚠️ 顺带确认一件事：**重建地形会把玩家开垦出来的耕地全部丢掉**。如果老档的重建路径会被走到，至少给玩家一个提示。

**工时 0.1 天**

---

## 8. 水面边界的纵向邻居取反

### 现状

```csharp
// VillageMapRenderer.FindEdgeNeighbor:8658-8673
if (blockX == 0 && tileX > 0)       terrain[tileY*48 + tileX-1]   // 左 ✓
if (blockX == 3 && tileX < 47)      terrain[tileY*48 + tileX+1]   // 右 ✓
if (blockY == 0 && tileY < 31)      terrain[(tileY+1)*48 + tileX] // ★ 上边缘，却取下方
if (blockY == 3 && tileY > 0)       terrain[(tileY-1)*48 + tileX] // ★ 下边缘，却取上方
```

同一渲染器里的坐标约定是「`blockY = 0` 在格子顶部」——证据在 `Bake:8447-8462` 的逐像素光栅化（`num4 = (1023 - num3 - n) * 1536`，行号越大越靠上）。

横向是对的，**只有纵向写反了**。

### 后果

程序化像素回退路径里，每格的地形过渡色取自**对面**的邻格 → 水陆边界在垂直方向错位一格。

**触发条件**：走到 `PickPixel`→`FindEdgeNeighbor` 回退路径时。`GetSpritePixels:8608` 要求 `texture.isReadable`——**如果图集没开 Read/Write，每一格都会走这条路**，这个 bug 就成常态。

### 改法

```csharp
if (blockY == 0 && tileY > 0)       terrain[(tileY-1)*48 + tileX]
if (blockY == 3 && tileY < 31)      terrain[(tileY+1)*48 + tileX]
```

> **先确认图集开了 `Read/Write`。** 如果开了，这条不影响画面，但仍应修。

**工时 0.05 天**

---

## 9. 其余小项（有余力再动）

| # | 问题 | 位置 | 改法 | 工时 |
|---|---|---|---|---|
| 1 | **`.bak` 备份等于当前文件** | `AtomicWrite:6804-6833` | `File.Replace` 之后那行 `File.Copy(path, text2)` **删掉**（设置文件那版就没有，可对照）| 0.03 |
| 2 | 删除按钮不置回 false | `RefreshSlots:1857-1874` | 给 `deleteButton` 加 `else { SetActive(false); }`，和 `primaryButton` 对称 | 0.03 |
| 3 | 村民面板不暂停、不刷新 | `VillagerStatusPanelController.Open:2498` | 加 `GameTimePause.Request`；`title/meta/stats` 补 null 检查（`OpenObituary` 已经做了，抄过来）| 0.1 |
| 4 | 地图生成里的浮点 | `NormalizeSpread:7968` | `(int)Math.Sqrt(...)` 换整数牛顿迭代或查表 | 0.1 |
| 5 | 通道 27 被两种用途共用 | `8481` / `8560` | 给修复草换个没用过的通道号 | 0.02 |
| 6 | 水/冰变体写死 `seed = 0` | `8508` / `8550` | 改成 `map.seed`（除非是有意的）| 0.02 |
| 7 | 一格变化就重烘整张图 | `7373-7378` / `8429` | 只重绘受影响的那一格；`SpritePixelCache.Clear()` 也别每次清 | 0.3 |
| 8 | `SimulateTick` 用了陈旧人数 | `5284` | 循环里有死亡后重算一次，或把 `AdvanceGranary` 挪到循环之后取新的 `CountAlive` | 0.05 |
| 9 | 主菜单文案 | 预制体 | 「只写**五**条法律」和实际法槽数对不上，改文案 | 0.02 |

---

## 10. 还要确认两件事

**① 粮仓精灵从来没有被摆到地图上。**
`WorldDecorBinder` 只认 `Tree` 和 `House` 两种子物体（`Collect:8786-8806`），`SetSprites` 只收 `trees / snowTrees / houses`。全工程**没有任何粮仓精灵字段，也没有 `"Granary"` 节点**。美术画的 2 张粮仓图目前用不上。
如果你说的"粮仓/房子是玩家建造的"是**以后**的功能，那这条不算 bug，只是**现在粮仓在地图上看不见**——确认一下是有意还是漏了。

**② `byte[] terrain` 能不能被 `JsonUtility` 往返？**
理论上作为**字段**是支持的（会序列化成数字数组）。但如果它其实存不进去，每次读档都会因为 `terrain.Length != 1536` 重新生成地图，**玩家的开垦成果全丢**。
验证方法：新开一局 → 等 30 秒自动存档 → 打开 `village_slot_001.json`，看有没有 `"terrain": [...]`。**1 分钟的事。**

---

## 11. 工时与顺序

| 序 | 做什么 | 工时 |
|---|---|---|
| 1 | **钳位分成三档**（§1） | 0.2 |
| 2 | **存档不再改活状态**（§2） | 0.25 |
| 3 | **暂停锁释放**（§3） | 0.1 |
| 4 | 多产冷却（§4） | 0.05 |
| 5 | 讣告法名快照（§5） | 0.2 |
| 6 | 产出记账 + 调参（§6） | 0.6 |
| 7 | 地形版本判断（§7） | 0.1 |
| 8 | 水面边界（§8） | 0.05 |
| 9 | 小项（§9） | 0.4 |
| | **合计** | **≈1.95 天** |

**顺序：1 → 2 → 3 → 6（必做），然后 4/5/7/8，最后 9。**

> **§1 和 §6 必须连着做**：修完钳位粮食会变多，不重调收支，粮仓会顶死在 500。

**如果只剩一天**：做 1、2、3、6 四条。

---

## 12. 一句话

> **第一条是"加法把粮仓改成 99"，第二条是"存档顺手把村民的年龄进度抹了"，第三条是"暂停锁漏了，村子静默停摆"。**
> **三条都不报错、不崩溃，只是安静地把游戏变错。**
