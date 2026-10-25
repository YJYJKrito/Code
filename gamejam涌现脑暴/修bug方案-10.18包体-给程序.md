# 10.18 包体 · 修改方案 — 给程序

> **6 处改动，约 0.5 天。全部在测试台里实际改进去跑过，不是推演。**
>
> 改完之后的验收标准：**不变量审计 57,000+ 采样点 0 违规；幼童口粮灭村率 100% → 0%；积怨 0% → 60%。**
>
> 每条都写了：**在哪个方法、现在是什么、改成什么、怎么验。**

---

## 总览

| # | 问题 | 位置 | 改动 | 工时 |
|---|---|---|---|---|
| 1 | 幼童口粮抽干种子粮 → 100% 灭村 | `AdvanceNeeds` | 6 行 | 0.1 天 |
| 2 | 积怨职业永远拿不到 | `VillagerProfessions.Scan` | 1 行 | 0.01 天 |
| 3 | 威信会变成负数 | `SimulateTick` 威信段 | 1 行 | 0.01 天 |
| 4 | 耕者角色默认给 → 蝗灾职业修正失效 | `VillagerProfessions.Scan` | 2 行 | 0.02 天 |
| 5 | 可配置除数未防零 → 崩溃 | `SimulateTick` 税库段 | 2 行 | 0.01 天 |
| 6 | 劫匪几乎拿不到（1%） | `VillagerProfessions.Scan` | 1 行 | 0.01 天 |
| | **验收** | | | **0.3 天** |

**顺序：1 → 2 → 3 → 4 → 5 → 6 → 验收。**

---

## 1（🔴）幼童口粮会把村子抽干

### 位置

`VillageLifecycleService.AdvanceNeeds` —— 处理「缺粮时从公共粮仓取粮」的那一段。

### 现在

```csharp
bool num3  = HasLaw(data, 1) && data.villagerHunger[index] > 45;   // 济贫
bool flag  = HasLaw(data, 9) && data.villagerAges[index] < 8;      // 幼童口粮
bool flag2 = num3 | flag;
bool flag3 = data.villagerGold[index] > 0 && data.villagerHunger[index] > 30;

if (data.granaryFood > 0 && (flag2 | flag3))   // ★ 只要求 > 0
{
    data.granaryFood = Math.Max(0, data.granaryFood - 1);
    ...
}
```

### 改成

```csharp
// ★ 新增：只靠幼童口粮的人，不能动预留线以下的粮
int feedFloor = (flag2 && !num3 && !flag3) ? 40 : 0;

if (data.granaryFood > feedFloor && (flag2 | flag3))
```

### 为什么

装了幼童口粮之后，**每个饿着的小孩都能把粮仓挖到 0**（包括种子粮）。
实测粮仓常年卡在 3，全村进入「生 16 / 死 6」的 3 人循环。

**含义**：幼童口粮是「对**余粮**的优先权」，不是「对**种子粮**的优先权」。
同时装了济贫、或者手里有金币的人，**不受这条限制**。

> ⚠ **不要把 `> 0` 直接改成 `> 40`** —— 那会把**济贫和买粮也一起锁死**。
> 实测默认三法末人口会从 25 掉到 **7**。

### 验证

| 玩法 | 修前 | 修后 |
|---|---|---|
| 只装幼童口粮 | 0 / **100% 灭村** | **10 / 0%** |
| 生育系 (8+9+12) | 0 / **90% 灭村** | **9 / 0%** |
| 默认三法 | 25 / 0% | **25 / 0%（不变）** |

---

## 2（🟠）积怨职业永远拿不到

### 位置

`VillagerProfessions.Scan`。

### 现在

```csharp
if (num3 > 50) data.villagerHungryTicks[j]++;   // Scan 每 60 tick 跑一次，这里只 +1
...
if (data.villagerHungryTicks[j] >= 730)          // 730 次 = 43,800 tick = 15 年
```

### 改成

```csharp
if (num3 > 50) data.villagerHungryTicks[j] += 60;
```

### 为什么

规格要求「饥饿 > 50 **连续 730 tick（0.25 年）**」。
现在实际要求**连续挨饿 15 年**，所以积怨从来没出现过。

### 验证

| | 修前 | 修后 |
|---|---|---|
| 积怨出现率 | **0%** | **60%** |

---

## 3（🟠）威信会变成负数

### 位置

`SimulateTick` 里每 60 tick 的威信结算，**停摆分支**。

### 现在

```csharp
if (data.prestigeSuspended)
{
    data.prestige += num8;              // num8 可以是负数
    if (data.prestige >= 40) { data.prestigeSuspended = false; }
}
```

正常分支有 `if (prestige <= 0) { prestige = 0; ... }`，**停摆分支没有**。

### 改成

```csharp
if (data.prestigeSuspended)
{
    data.prestige = Math.Max(0, data.prestige + num8);
    if (data.prestige >= 40) { data.prestigeSuspended = false; }
}
```

### 为什么

不变量审计（57,000 个采样点）**只有这一条违规**：威信最低到 **−936**。
掉到负数之后，`>= 40` 更难满足，法律永久停摆。

### 验证

审计里「威信为负」：**1209 次 → 0**。

---

## 4（🟠）耕者角色是默认给的

### 位置

`VillagerProfessions.Scan` —— 授予 `RoleFarmer` 的那一行。

### 现在

```csharp
if (num2 >= 16 && num2 < 60 && !HasMark(1) && !HasMark(2))
    num6 |= 1;      // 16–60 岁的健康成年人自动成为耕者
```

### 改成

```csharp
if (num2 >= 16 && num2 < 60 && !HasMark(1) && !HasMark(2)
    && Traits.HasInnate(data, j, 16u))     // ★ 要求「勤勉」词条
    num6 |= 1;
```

### 为什么

实测 **86.4% 的成年村民自动是耕者** → 事件里的 `flag = Role(Farmer)` 永远是 true：

```csharp
case 0: granary - (flag ? 40 : 80);   // 蝗灾永远只掉 40
case 7: granary + (flag ? 180 : 120); // 丰年永远 +180
```

**「村里有没有耕者」这条职业修正等于不存在**，和设计原则
「职业修正必须是玩家通过法律决定的」直接冲突。
（设计文档里耕者的来源本来就是**勤勉**词条。）

### 验证

| | 耕者持有率 | 蝗灾粮损（无对口法） | 装耕作 |
|---|---|---|---|
| 现在 | **86.4%** | 147 | 71 |
| 改后 | **8.5%** | **206** | **100** |

改完之后 **「装不装耕作」才第一次真的影响蝗灾**。

---

## 5（🟠）可配置除数要防零

### 位置

`SimulateTick` 税库进项/俸禄那一段。

### 现在

```csharp
data.treasuryGold = Math.Min(999999, data.treasuryGold + CountAlive(data) / TreasuryPopDiv);
data.treasuryGold = Math.Max(0, data.treasuryGold - CountAlive(data) / TreasuryUpkeepDiv);
```

### 改成

```csharp
data.treasuryGold = Math.Min(999999, data.treasuryGold
    + ((TreasuryPopDiv > 0) ? (CountAlive(data) / TreasuryPopDiv) : 0));
data.treasuryGold = Math.Max(0, data.treasuryGold
    - ((TreasuryUpkeepDiv > 0) ? (CountAlive(data) / TreasuryUpkeepDiv) : 0));
```

### 为什么

当前是常量 3 / 4，**不会崩**。但只要有人想把俸禄关掉（设 0）就会
`DivideByZeroException`，整个模拟挂掉 —— 测试台已经踩到了。

> **规则**：任何除数，只要它来自可配置项，就必须防零。

---

## 6（⚠）劫匪几乎拿不到

### 位置

`VillagerProfessions.Scan` —— 授予 `Mark.Bandit` 的那一行。

### 现在

```csharp
if (HasLaw(data, 2) && num3 > 55) Traits.AddMark(data, j, 2u);
```

### 改成

```csharp
if (HasLaw(data, 2) && num3 > 45) Traits.AddMark(data, j, 2u);
```

### 为什么

劳动力经济下饥饿 p90 只有 40，**> 55 几乎达不到** —— 实测劫匪出现率 **1%**。
和之前那批「门槛落在参数区间外」是同一类问题。

### 验证

| 阈值 | 劫匪出现率 | 持有率 |
|---|---|---|
| 55（现在） | **1%** | 0.2% |
| **45** | **7%** | 2.0% |

---

## 验收清单

改完按顺序跑：

```bash
cd 测试台/pscan

# ① 不变量：应该 0 违规
dotnet run -c Release -- audit 20

# ② 幼童口粮：应该不再灭村
dotnet run -c Release -- lawsets 20

# ③ 积怨 / 劫匪 / 耕者：看 native 的表
dotnet run -c Release -- native 10
```

**期望值：**

- [ ] 不变量审计 **0 违规**
- [ ] 「只装幼童口粮」末人口 **≥ 9**，灭村率 **0%**
- [ ] 积怨出现率 **≥ 30%**
- [ ] 劫匪出现率 **≥ 5%**
- [ ] 耕者持有率 **≤ 15%**
- [ ] 蝗灾在**没装耕作**时粮损 **≈ 200**（不是 147）
- [ ] 默认三法末人口**没有变**（25 上下）
- [ ] 同一种子跑两遍结果完全一致

---

## 不要这样改

| 错误改法 | 后果 |
|---|---|
| 把幼童口粮的 `> 0` 直接改成 `> 40` | 济贫和买粮一起锁死，默认玩法 25 → **7** |
| 给 10 岁以下加产出来解决幼童口粮 | **没用**，六个档位下仍然是 9–10 人 |
| 用「提高饥荒强度」来解决积怨 | 没用，那是计数器单位错，不是阈值问题 |
| 调 `PrestigePerLaw` 来压威信负数 | 没用，负数来自停摆分支缺下限 |

---

## 一句话

> **六处都是「默认值/边界」问题，不是设计问题：**
> 种子粮被挖空、计数器单位错、停摆分支没下限、角色默认给、除数没防零、阈值在区间外。
>
> **改完游戏的手感会明显不同** —— 第一次，职业修正和法条选择真的会分开。
