# 10.19 包体 · 修 bug 方案 — 给程序


> ✅ **本文的 3 个问题已在 `testgame_fixed` 中处理**（MostLethal 只算致死、endKind 5、IsPunitive 死代码不再影响行为），并额外修复了模式选择、法改法、×人口、屠者、流民法、高压村力等 20 项。清单见 [当前机制真实规格-修复版.md](当前机制真实规格-修复版.md) 第 11 节。

> 新包体（157 KB，存档 v17）把**两种模式**和**讣告归因**都做进去了，整体是对的。
>
> 剩下 **3 个问题**，其中 1 个会**指错法律** —— 和你上次发现的是同一类。
> 全部在测试台里验证过。

| # | 问题 | 严重度 | 改动 |
|---|---|---|---|
| 1 | **「最致命的法」把「没装的法」算进去** | 🔴 指错法律 | 1 行 |
| 2 | 名存实亡和期满共用 `endKind = 4` | 🟠 结局显示错 | 1 行 + UI 1 行 |
| 3 | `LawAttribution.IsPunitive` 结果被丢弃 | ⚪ 死代码 | 删或接上 |

> **先更正我上一条消息里的一个错误判断**：我说「天灾讣告继承了上一次死亡的归因」——**看错了**。
> `KillSome()` 在杀之前调了 `ObitRules.AttributeEvent(data, eventId)`，天灾的缺位归因是**有意为之**（疫病→医药、民变→安抚……）。这一条不是 bug。

---

## Bug 1（🔴）「最致命的法」把没装的法算进去

### 位置

`结算面板.MostLethal(VillageSaveData save)`

### 现在

```csharp
for (int i = 0; i < save.obituaryCount; i++)
{
    string text2 = save.obituaryLawNames[i];
    if (string.IsNullOrEmpty(text2)) continue;
    // ★ 没有检查 obituaryLawKinds
    ...统计...
}
```

**`obituaryLawNames` 里现在同时装着两种东西**：
- `kind == 1` 致死法（玩家装了）
- `kind == 2` 缺位法（玩家**没装**）

`MostLethal` 把两种都算进去了。

### 实测

10 种子 × 6 玩法，**60 局**：

| 算法 | 把「没装的法」当成最致命 |
|---|---|
| 现在（统计所有法名） | **30 局 / 60（50%）** |
| 只统计 `kind == 1` | **0 局 / 60** |

例子（全部来自「默认三法」济贫·耕作·税收）：

```
最致命 = 『幼童口粮』，但它没装
最致命 = 『幼童口粮』，但它没装
最致命 = 『幼童口粮』，但它没装
```

**结算页会写「最致命的法『幼童口粮』（3475 人）」—— 指控一条玩家从来没立过的法。**

### 改成

```csharp
for (int i = 0; i < save.obituaryCount; i++)
{
    if (i >= save.obituaryLawKinds.Length) break;
    if (save.obituaryLawKinds[i] != ObitRules.KindCaused) continue;   // ★ 只算致死
    string text2 = save.obituaryLawNames[i];
    if (string.IsNullOrEmpty(text2)) continue;
    ...
}
```

### 验收

- [ ] 不装任何法跑一局，结算页**不出现**「最致命的法」
- [ ] 装「劳役」跑一局，结算页点名劳役
- [ ] 结算页点名的法**一定在 `lawIds` 里**

---

## Bug 2（🟠）名存实亡和期满共用一个 endKind

### 位置

`RunRules.Tick`

### 现在

```csharp
if (data.tinyStreak >= 24)
{
    data.endKind = 4;      // ← 和「期满」一样
    data.grade = 0;
    return;
}
...
if (tick >= 146104)
{
    data.endKind = 4;      // 期满
    data.grade = Grade(data);
}
```

`EndWord(4)` 一律返回 **「期满」**。

### 实测

- 90 局五十年模式里，**名存实亡触发 0 次**（先被 `num <= 2 → 绝户` 截住了）
- 所以这是**潜在 bug**：一旦触发，结算页会把一个「名存实亡」的村子写成「期满」

### 改成

```csharp
if (data.tinyStreak >= 24)
{
    data.endKind = 5;      // ★ 名存实亡
    data.grade = 0;
    return;
}
```

UI 补一个 case：

```csharp
private static string EndWord(int kind)
{
    return kind switch
    {
        1 => "通关",
        2 => "绝户",
        3 => "罢黜",
        4 => "期满",
        5 => "名存实亡",   // ★
        _ => "结束",
    };
}
```

> 高压模式的标题用的是 `endKind == 1 ? 通关 : 罢黜`，不用改；
> 五十年模式用的是 `"五十年模式 · " + EndWord(endKind)`，加 case 就够。

### 验证

单元测试（测试台 `tinytest`）：

```
人口=4 tinyStreak=24 endKind=5
✅ 名存实亡 = endKind 5
```

### 顺带一个可选调整

`tinyStreak >= 24` 在实测 90 局里**一次都没触发** —— 因为人口 ≤2 会先判绝户，
而 3–4 人的村子很少能撑满 24 个月。
**如果你想让「名存实亡」真的出现**，把 `24` 改成 **12**（连续一年 <5 人）。

---

## Bug 3（⚪）一行死代码

### 位置

`VillageLifecycleService.AppendObituary`

```csharp
if (lawIndex >= 0 && lawIndex < data.lawIds.Length)
{
    LawAttribution.IsPunitive(data.lawIds[lawIndex]);   // 结果被丢掉了
}
```

看起来是想用「这条法是不是惩罚性」来覆盖归因，但返回值没赋值，**什么也没发生**。

**处理**：要么删掉，要么接上——比如：

```csharp
if (lawIndex >= 0 && lawIndex < data.lawIds.Length
    && LawAttribution.IsPunitive(data.lawIds[lawIndex]))
{
    b = ObitRules.KindCaused;
}
```

> 目前 `cause == "律法"` 已经强制 `kind = 1`，所以**删掉也不会改变行为**。建议删。

---

## 附：新包体的健康数据

**30 年回归**

| 检查 | 结果 |
|---|---|
| 13 种玩法 | **13 / 13 存活，0% 灭村** |
| 33 条单法 | **33 / 33 有效** |
| 不变量审计 | **0 违规**（27,495 采样点） |
| 事件 | 14 个全出现，**22.4 次/局** |
| 讣告归因 | **0 篇保护性法律被当成死因** ✅ |

**五十年模式（runMode 0，10 种子 × 9 玩法）**

| 结束方式 | 局数 |
|---|---|
| 期满 | 65 / 90 |
| 绝户 | 25 / 90 |
| 名存实亡 | 0 / 90 |

| 玩法 | 人年 | 总评 |
|---|---|---|
| 满槽6条 | 2082 | 85 |
| 流民法 | 1720 | 87 |
| 默认三法 | 1424 | 59 |
| 苛政 | 82 | 32 |

**高压模式（runMode 1）**

| 结束方式 | 局数 |
|---|---|
| 通关 | 16 / 90 |
| 罢黜 | 32 / 90 |
| 绝户 | 12 / 90 |
| 50 年未达标 | 30 / 90 |

- 满槽 6/10 通关、流民 9/10 通关
- **通关用时中位 ≈ 29 年 ≈ 9.5 分钟 @1x**

---

## 验收清单

- [ ] 不装法的一局，结算页**没有**「最致命的法」
- [ ] 结算页点名的法**一定在法槽里**
- [ ] 名存实亡显示为「名存实亡」，不是「期满」
- [ ] 五十年期满仍显示「期满」
- [ ] 高压通关/罢黜显示不变
- [ ] 同一种子跑两遍，结算完全一致

---

## 一句话

> **`obituaryLawNames` 现在有两种语义（致死 / 缺位），
> 所有读它的地方都必须先看 `obituaryLawKinds`。**
>
> 结算页看漏了一次 —— 这就是玩家看到「最致命的法是幼童口粮」的原因。
