# 《立法村》UI 贴图制作手册 — 给美术

> 你不需要会画画。你需要做的是：**一张一张地生成图片，然后按清单检查。**
>
> 这份手册里每张图都有一个**完整的提示词**，直接复制就能用。

---

## 0. 先看这三条（很重要）

### ① 你的工具没有「记忆」

**它不记得你上一张图写了什么。**
所以你生成的第二张图和第一张图**没有任何关系**，除非你在提示词里再写一遍。

> 👉 **每一张图的提示词，都要把「画风」和「颜色」重新写一遍。**
> 不要写「和上一张一样」「同上」—— 工具看不懂。

### ② 一次只做一张

**提示词里只描述一个东西。**
不要说「画一套按钮」，要说「画一个木质按钮」。

### ③ 每张图都从「空」开始

你没法让工具「在刚才那张的基础上改」。
想改，就把**新的提示词整段重新发一遍**。

---

## 1. 你要记住的画风（4 句话）

1. **明亮的像素游戏**（像《星露谷物语》），不是水墨、不是写实、不是 3D。
2. **木头做框，米黄色纸做面**。
3. **深色粗描边，边缘是硬的**（不模糊、不发虚）。
4. **颜色只用下面这些**。

### 颜色表

| 用在哪 | 颜色 | 色号 |
|---|---|---|
| 羊皮纸（面板里面） | 米黄 | `#F0C382` |
| 纸背 / 底色 | 深米黄 | `#E3BA8C` |
| 木框（深） | 棕 | `#7F490D` |
| 木框（浅，高光） | 浅棕 | `#B17247` |
| 字 / 描边 | 深褐 | `#3A2A1E` |
| 高光 | 奶白 | `#FFF6E0` |

### 法条卡的 9 个类别色（做徽章要用）

| 类别 | 色号 |
|---|---|
| 粮（民生） | `#8F2A1E` 红 |
| 钱（财政） | `#932D07` 橙 |
| 法（法度） | `#7F490D` 金棕 |
| 恐惧 | `#44205E` 紫 |
| 农事 | `#286321` 绿 |
| 秩序 | `#2E7FA8` 蓝 |
| 刑名 | `#74160E` 深红 |
| 生育 | `#FE8999` 粉 |
| 医疗 | `#005D6C` 青 |

> **把这 9 个颜色记在便签上。** 做徽章的时候照着抄。

### 你的参考图

**一直开着这几张图：**
- 法条卡那张（9 张卡）—— **这是我们的标准**
- 角色像素小人
- 宣传大图

新做的每一张，都要和法条卡**摆在一起看**。像不像同一个村子里的东西，一眼就知道。

---

## 2. 每张图怎么做（4 步）

```
第 1 步：复制这张图的「提示词」
第 2 步：生成 4 张，挑 1 张最像的
第 3 步：按「检查」那 3 条看一遍
第 4 步：不行 → 看「常见问题」怎么改，再生成 4 张
```

**不要追求完美。** 每张图挑一张"能用"的就行，后面还有程序会调大小和位置。

---

## 3. 三个名词解释

| 词 | 意思 |
|---|---|
| **像素画** | 用一个个小方块拼出来的画，边缘是硬邦邦的。提示词里写 `pixel art` |
| **九宫格** | 把一张图切成九块，四个角不动，中间随便拉伸。所以**边框要一样粗，四个角不要放花纹** |
| **透明背景** | 图片外面是透明的，不是白底。工具一般做不出来，所以**先用纯色背景生成，再抠掉** |

### 透明背景怎么做（只对徽章、按钮、头像框需要）

1. 提示词里写 `on a solid magenta background`（洋红色背景，`#FF00FF`）
2. 生成后，用 <https://www.remove.bg> 或 Photopea 把洋红色删掉
3. 保存成 PNG

> **面板（大块的方框）不需要透明**，因为它是方的，正好铺满。

---

## 4. 制作卡

> 每张卡的提示词都是**完整的**，直接复制，不用加别的东西。

---

### A. 面板（5 张）

#### A1 · `ui_panel_parchment` 主面板

- **用在哪**：游戏里左右两块信息面板
- **尺寸**：横着的长方形（生成时选 2:1），最终 512×256
- **画什么**：一个棕色木框，中间是米黄色的纸

**提示词：**

```
A rectangular game UI panel, carved warm brown wooden frame (#7F490D) with a lighter wood highlight (#B17247) along the top-left edges, filled with warm parchment paper (#F0C382) with a subtle paper texture, border thickness exactly the same on all four sides, simple rounded corners, plain corners with no decoration, bright 16-bit pixel art, JRPG style, saturated colors, thick dark outline, hard pixel edges, no blur, front view, centered, fills the whole frame, no text, no letters
```

**负面词：**

```
text, letters, watermark, numbers, gradient, glow, blur, anti-aliasing, 3d render, photo, realistic, dark, gothic, ink wash, ornate corner decorations, asymmetric border
```

**检查：**
- [ ] 四条边框**一样粗**
- [ ] 四个角**没有花纹**
- [ ] 中间是干净的米黄色

---

#### A2 · `ui_topbar_wood` 顶栏

- **用在哪**：屏幕最上面一条（放日期、资源数字）
- **尺寸**：很长的横条（生成时选 4:1），最终 1920×96
- **画什么**：一根横着的木头梁

**提示词：**

```
A long horizontal wooden beam for a game UI top bar, warm brown wood planks (#7F490D) with lighter wood grain highlights (#B17247), subtle carved edges, flat front view, bright 16-bit pixel art, JRPG style, saturated colors, thick dark outline, hard pixel edges, no blur, no text, no letters, no symbols
```

**负面词：**

```
text, letters, numbers, icons, glow, gradient, blur, 3d, photo, realistic, fantasy ornament, metal
```

**检查：**
- [ ] 是**横着的长条**，不是方的
- [ ] 木纹是横的
- [ ] 上面没有字、没有图标

---

#### A3 · `ui_card_slot_empty` 空法条卡

- **用在哪**：律法面板里「还没放法」的格子
- **尺寸**：竖着的长方形（生成时选 2:3），最终 280×450
- **画什么**：一个空的卡片框，虚线边框，中间是暗一点的纸

**提示词：**

```
An empty vertical game card slot, thin dashed wooden border (#7F490D) on a slightly darker parchment background (#E3BA8C), completely empty center, simple rounded corners, portrait orientation rectangle, bright 16-bit pixel art, JRPG style, thick dark outline, hard pixel edges, no blur, front view, centered, no text, no letters
```

**负面词：**

```
text, letters, illustration, picture, icon, glow, gradient, blur, 3d, photo, ornate, filled center
```

**检查：**
- [ ] 中间是**空的**（不能有画）
- [ ] 边框是**虚线**（让玩家知道能放东西）
- [ ] 是**竖着的**，不是横的

---

#### A4 · `ui_panel_scroll` 讣告卷轴

- **用在哪**：打开讣告列表时的面板
- **尺寸**：方形偏竖（生成时选 4:5），最终 800×600
- **画什么**：一张卷起来的旧纸，左边有木轴

**提示词：**

```
A vertical parchment scroll panel for a game UI, warm parchment paper (#F0C382) with aged edges, a wooden roller on the left side, subtle paper fibers and stains, mostly empty inside for text, bright 16-bit pixel art, JRPG style, saturated warm colors, thick dark outline, hard pixel edges, no blur, front view, centered, no text, no letters
```

**负面词：**

```
text, letters, calligraphy, symbols, glow, gradient, blur, 3d, photo, realistic, blood, horror, black
```

**检查：**
- [ ] 中间**大面积是空的**（要写字）
- [ ] 左边有木轴，右边没有（不对称是对的）
- [ ] 纸是**暖米色**，不是白纸

---

#### A5 · `ui_omen_banner` 兆头横幅（做 3 张）

- **用在哪**：屏幕下方「田里的蚂蚱比往年多」那条
- **尺寸**：长横条（生成时选 8:1），最终 1200×120
- **画什么**：一块横着的木牌，三种边框颜色

**提示词（普通版 · 棕色框）：**

```
A wide horizontal wooden signboard banner for a game UI, warm brown wooden frame (#7F490D), empty parchment center (#F0C382), simple flat shape, bright 16-bit pixel art, JRPG style, thick dark outline, hard pixel edges, no blur, front view, centered, no text, no letters
```

**提示词（危险版 · 深红框）：** 把上面提示词里的 `warm brown wooden frame (#7F490D)` 换成：

```
dark red wooden frame (#74160E)
```

**提示词（好兆版 · 绿框）：** 换成：

```
dark green wooden frame (#286321)
```

**检查：**
- [ ] 三张**形状一样**，只有边框颜色不同
- [ ] 中间是空的
- [ ] 是长条，不是方牌

---

### B. 徽章（6 张，**都要透明背景**）

> 全部是**圆形**：彩色圆盘 + 中间的白色小图案。
> 尺寸都生成正方形（1:1），最终 96×96。
>
> **六个提示词只有「颜色」和「图案」不一样，其余完全一样。**

#### B1 · `ui_badge_granary` 粮仓

```
A circular badge icon: a dark red disc (#8F2A1E) with a white wheat sheaf symbol in the center, thick dark outline, bright 16-bit pixel art, JRPG UI icon, hard pixel edges, no blur, centered, on a solid magenta background (#FF00FF), no text, no letters
```

#### B2 · `ui_badge_treasury` 税库

```
A circular badge icon: an orange disc (#932D07) with a white money pouch symbol in the center, thick dark outline, bright 16-bit pixel art, JRPG UI icon, hard pixel edges, no blur, centered, on a solid magenta background (#FF00FF), no text, no letters
```

#### B3 · `ui_badge_prestige` 威信

```
A circular badge icon: a golden brown disc (#7F490D) with a white balance scale symbol in the center, thick dark outline, bright 16-bit pixel art, JRPG UI icon, hard pixel edges, no blur, centered, on a solid magenta background (#FF00FF), no text, no letters
```

#### B4 · `ui_badge_population` 人口

```
A circular badge icon: a blue disc (#2E7FA8) with two small white human figures in the center, thick dark outline, bright 16-bit pixel art, JRPG UI icon, hard pixel edges, no blur, centered, on a solid magenta background (#FF00FF), no text, no letters
```

#### B5 · `ui_badge_fear` 恐惧

```
A circular badge icon: a purple disc (#44205E) with a white open eye symbol in the center, thick dark outline, bright 16-bit pixel art, JRPG UI icon, hard pixel edges, no blur, centered, on a solid magenta background (#FF00FF), no text, no letters
```

#### B6 · `ui_badge_obituary` 讣告

```
A circular badge icon: a dark red disc (#74160E) with a white rolled paper scroll symbol in the center, thick dark outline, bright 16-bit pixel art, JRPG UI icon, hard pixel edges, no blur, centered, on a solid magenta background (#FF00FF), no text, no letters
```

**统一负面词：**

```
text, letters, numbers, realistic, photo, 3d, gradient, glow, blur, square badge, rectangle, multiple icons, white background
```

**检查（六张都要）：**
- [ ] 是**圆形**
- [ ] 中间是**白色图案**，不是彩色
- [ ] 背景是**洋红色**（之后要抠掉）

---

### C. 按钮（2 张）

#### C1 · `ui_btn_wood` 木牌按钮

- **用在哪**：主菜单、所有按钮
- **尺寸**：横着的长方形（选 3:1），最终 240×80
- **画什么**：一块横着的木牌，圆角

**提示词：**

```
A wooden signboard button for a game UI, warm brown wood (#7F490D) with a lighter top-left highlight (#B17247), rounded rectangle, thick dark outline, empty center, no text, bright 16-bit pixel art, JRPG style, hard pixel edges, no blur, front view, centered, on a solid magenta background (#FF00FF), no letters
```

**负面词：**

```
text, letters, icons, nails, ropes, glow, gradient, blur, 3d, photo, realistic, square
```

**检查：**
- [ ] 是**圆角**的横木牌
- [ ] 空白的，没有字
- [ ] 背景洋红，能抠

> **按下和悬停状态不用单独生成。**
> 生成一张正常的，然后在修图软件里把整张调暗 15% = 按下；调亮 10% = 悬停。

---

#### C2 · `ui_btn_icon` 方形小按钮

- **用在哪**：暂停、关闭、加速
- **尺寸**：正方形（1:1），最终 96×96
- **画什么**：一个小方木块，圆角

**提示词：**

```
A small square wooden icon button for a game UI, warm brown wood (#7F490D) with a lighter top-left highlight (#B17247), rounded corners, thick dark outline, empty center, no symbol, no text, bright 16-bit pixel art, JRPG style, hard pixel edges, no blur, front view, centered, on a solid magenta background (#FF00FF)
```

**负面词：**

```
text, letters, icons, arrows, symbols, glow, gradient, blur, 3d, photo, circle
```

**检查：**
- [ ] 正方形、圆角
- [ ] 中间是空的（图标程序另外叠上去）
- [ ] 背景洋红

---

### D. 其它（4 张）

#### D1 · `ui_portrait_frame` 头像框

- **用在哪**：点开村民时，人物头像外面那一圈
- **尺寸**：正方形（1:1），最终 128×128
- **画什么**：一个方木框，中间空着（程序会把人物放进去）

**提示词：**

```
A square wooden portrait frame for a game UI, warm brown wood (#7F490D) with lighter highlights (#B17247), thick even border, empty parchment center (#F0C382), simple corners with no ornaments, bright 16-bit pixel art, JRPG style, thick dark outline, hard pixel edges, no blur, front view, centered, no text, no letters
```

**负面词：**

```
text, letters, face, person, portrait, glow, gradient, blur, 3d, photo, ornate corners
```

**检查：**
- [ ] 中间**是空的**（不能有脸）
- [ ] 四条边一样粗
- [ ] 四个角**没有花纹**

---

#### D2 · `ui_bar_frame` + `ui_bar_fill` 村力条（做 2 张）

**框（长的空槽）：**

```
A horizontal progress bar frame for a game UI, dark wooden frame (#7F490D) with empty dark parchment inside, long thin rectangle with rounded ends, thick dark outline, bright 16-bit pixel art, JRPG style, hard pixel edges, no blur, front view, centered, no text, no letters, on a solid magenta background (#FF00FF)
```

**填充条（绿色长条）：**

```
A horizontal progress bar fill strip, bright green gradient-free flat color (#4FA84A) with a lighter top highlight (#8FD87A), long thin rectangle, thick dark outline, bright 16-bit pixel art, JRPG style, hard pixel edges, no blur, flat front view, no text, no letters, on a solid magenta background (#FF00FF)
```

**检查：**
- [ ] 框是**空的**
- [ ] 填充条是**纯绿色**（不要渐变，程序会拉长它）
- [ ] 两张的高度一样

---

#### D3 · `ui_seal_year` 年份印章

- **用在哪**：顶栏最左边，盖着年份
- **尺寸**：正方形（1:1），最终 96×96
- **画什么**：一个红色的方印章，中间空着（字由程序写）

**提示词：**

```
A square red seal stamp for a game UI, cinnabar red (#A8322A), weathered carved edges, blank center, bright 16-bit pixel art, JRPG style, thick dark outline, hard pixel edges, no blur, front view, centered, on a solid magenta background (#FF00FF), no text, no letters
```

**负面词：**

```
text, letters, chinese characters, symbols, glow, gradient, blur, 3d, photo, realistic
```

**检查：**
- [ ] 中间是空的（不要有字！）
- [ ] 红色、方、边缘有点旧

---

#### D4 · `ui_paper_texture` 纸纹

- **用在哪**：铺在面板上增加质感
- **尺寸**：正方形（1:1），最终 256×256
- **画什么**：一张**可以无接缝平铺**的米色纸

**提示词：**

```
A seamless tileable parchment paper texture, warm beige (#E3BA8C) with subtle fibers and small stains, flat even lighting, no shadows, no objects, bright 16-bit pixel art style texture, hard pixel edges, no blur, top-down view, fills the whole square, no text, no letters
```

**负面词：**

```
objects, text, letters, border, frame, shadows, gradient, vignette, blur, 3d, photo, realistic paper photo
```

**检查：**
- [ ] 整张**颜色均匀**（不能中间亮四周暗）
- [ ] 没有边框、没有图案
- [ ] 看起来能**左右上下拼起来**

---

## 5. 生成顺序（先做这些）

### 第一批（最重要，先做 3 张）

- [ ] A1 主面板
- [ ] C1 木牌按钮
- [ ] B1 粮仓徽章

> **做完这三张，先发我看一眼。** 风格对不对，这时候改成本最低。

### 第二批

- [ ] A2 顶栏
- [ ] A3 空卡槽
- [ ] B2–B6 另外 5 个徽章
- [ ] C2 方形小按钮

### 第三批

- [ ] A4 讣告卷轴
- [ ] A5 兆头横幅 ×3
- [ ] D1 头像框
- [ ] D2 村力条 ×2
- [ ] D3 年份印章
- [ ] D4 纸纹

---

## 6. 交付

| 项 | 要求 |
|---|---|
| 格式 | PNG |
| 尺寸 | 每张生成后缩放到卡片里写的「最终尺寸」 |
| 背景 | 徽章/按钮/头像框 = 透明；面板 = 不用透明 |
| 命名 | 卡片编号，例如 `ui_badge_granary.png` |
| 2 倍图 | 有的话再给一份 `_2x`，没有就算了 |

---

## 7. 常见问题

### 「生成出来的不是像素画，是普通插画」

在提示词最前面加一句：

```
pixel art, 16-bit, chunky pixels, hard edges
```

负面词里确保有 `realistic, photo, 3d, smooth, blur`。

### 「边框一边粗一边细」

重新生成。提示词里已经有 `border thickness exactly the same on all four sides`，
但工具有时候不听。挑一张**四条边一样粗**的。

### 「四角有花纹，拉伸会变形」

重新生成。提示词里已经有 `plain corners with no decoration`。
**九宫格拉伸会把角保原样、中间拉长，角上有花纹就会很奇怪。**

### 「颜色不像」

把法条卡那张图放在旁边，对比着看。
然后在提示词里**把色号写得更靠前**（工具会优先注意前面的词）。

### 「生成的中文乱码」

**所有图里都不要有文字**。如果有字，重新生成，并在负面词里加重：

```
text, letters, characters, watermark, signature
```

### 「透明背景抠不干净」

- 洋红色背景要**纯色**（不要让工具有渐变、阴影）
- 抠图时把容差调大一点
- 边缘有残留的话，缩到最终尺寸（96×96）后基本看不出来

---

## 8. 一句话

> **记住四件事：明亮像素、木框纸面、粗描边、只用这九个颜色。**
>
> **每张图都重新写一遍画风 —— 工具不会记得。**
