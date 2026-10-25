// ═══════════════════════════════════════════════════════════════════
// Sim/World.cs —— 核心模拟。零 GC、纯整数、可哈希。
//
// 五条数值配平（前七轮实测得出，缺一即退化成一潭死水）：
//   ① 硬钳位 clamp(v,0,99) → 软回拉（越近边界拉力越大）
//   ② 性格标签必须调制动力学参数 ← 决定性的一刀（σ 3.7 → 27.7）
//   ③ 每个寄存器都要有受状态调制的随机项
//   ④ 恐惧衰减与持有量成比例，且源要封顶（否则饱和）
//   ⑤ 阈值设在实测的可用区
//
// 纪律：Tick 内不 new、不字符串、不用 LINQ/foreach(非数组)、不碰 float。
// ═══════════════════════════════════════════════════════════════════
using System;

namespace SimLit
{
    public class World
    {
        public const int N = 20;
        public const int SLOTS = 5;
        public const int MAXT = 2400;
        public const int CARDS = 20;
        public const int NUM_TARGETS = 1 + N + N * (N - 1);   // 401

        public const int AUTH_MAX = 10000;
        public const int UPKEEP_PER_LAW = 5;      // 厘点/tick
        public static int FATIGUE_K = 100;        // 阈值 = popPeak * K（非 const：便于扫参/调优）
        // 恐惧→饥饿的传导强度：每 tick 额外饥饿 = fear / FEAR_HUNGER_DIV。
        // 这是「恐惧有多致命」的总闸门：值越小越致命，<=0 则恐惧与存亡解耦。
        // 立法有效性的强度与村庄存活率在这里此消彼长，必须用曲线定值，不能拍脑袋。
        public static int FEAR_HUNGER_DIV = 45;

        // 恐惧的惯性（= 回落速率的分母）。分母越大，恐慌越难平复。
        // 【为什么必须调这个】原值 3 让被抢（+18）的冲击在一个 tick 内被抹平，
        //   恐惧没有任何累积能力 → 没有雪崩 → 恐惧类法只能测到噪声。
        // 【为什么用分母而不是直接改减法】保持「1 起步、随偏离量加速」的形状不变，
        //   只调时间常数，不会把远端钳成常数。
        public static int FEAR_DECAY_DIV = 3;

        // 恐慌传染：近邻平均恐惧超过自己这么多时，自己按概率被点燃。
        // 这是「少数人的恐慌引发雪崩」的机制本体 —— 必须有，否则恐惧只是个人情绪。
        public static int FEAR_CONTAGION_GAP = 12;
        public static int FEAR_CONTAGION_PCT = 25;

        // ══ 粮食承载力模型（全局最重要的一处结构，不是配平旋钮）══
        //
        // 【为什么必须是承载力，而不能是常数人均产出】
        //   如果产出与消耗都严格按人头走，收支比值就与人口无关：
        //     收支为正 → 永远盈余 → 粮仓顶满   → 20 人全活
        //     收支为负 → 永远赤字 → 粮仓掏空   → 0 人全灭
        //   人口下降既减少产出也减少消耗，**死人不解决粮食问题** → 中间态不稳定
        //   → 结局只能非 0 即 20（实测分布：0 人 38.8% / 20 人 47%，中段仅 14%）。
        //
        //   加上「有限的地」之后，人均产出随人口递减，平衡人口 P* 存在：
        //     人均产出(P) = PROD_BASE - P × CROWD_K  =  人均消耗 c
        //     P* = (PROD_BASE - c) / CROWD_K
        //   律法改变的是 PROD_BASE 或 c，也就是**改变 P*** —— 结局因此是一条连续谱。
        //
        // 单位：千分点（人均每 tick 产出的粮 ×1000）
        public static int PROD_BASE = 1650;   // 人均产出上限（P→0 时）1.85 粮/人/tick
        public static int CROWD_K  = 70;      // 每多一个人，人均产出降 0.07
        // 产出的伯努利抖动：让聚合量带上真实涨落。
        // 不加则「固定律法跨 40 seed 存活 σ = 0.00」—— 20 人的确定性求和把随机性平均掉了。
        public static int PROD_JITTER_PCT = 50;

        // 耕作法的生效概率。★ 关键：耕作必须是**概率性**的，不能是「人人每 tick 都 +mag」。
        //   人均消耗约 0.85 粮/tick，而「每人每 tick +2」= 2.0 —— 直接越过消耗线，
        //   一条耕作法就把 P* 推到无穷，全村锁死 20 人（实测 20 人档占 46%）。
        //   改成 30% 概率 × mag=1 → 人均 +0.30/tick，只**抬高**承载力而不会**取消**它。
        public static int FARM_PCT = 30;
        public const int GRANARY_CAP = 500;

        // 个人存粮上限。公仓宽裕时村民会往家里搬粮；断粮时先吃自家的。
        // 这是把「集体命运」拆成 20 条独立时间线的机制 —— 见 Villager.food 的注释。
        public const int FOOD_CAP = 40;
        // 公仓高于这个水位，村民才开始往家里搬粮
        public const int STOCK_THRESHOLD = 40;

        public ulong seed;
        public Villager[] v;
        public Law[] laws;
        public LawRuntimeView[] view;
        public int[] rel;                          // N*N 有向关系
        public SimState st;

        // 卡状态（扁平数组，不进 Dictionary）
        // 【删掉的字段】原设计有两个 int[CARDS * NUM_TARGETS]（= int[8020]，各 32KB）：
        //   lastFired 和 triggerCount。它们被分配、但从**未被任何代码读取过**：
        //   每个 World 白扔 64KB。批量实验里一轮要造几千个 World（LegisEffect 约 4500 个），
        //   即几百 MB 的纯垃圾 —— 实测表现为跑几千局后在 World.Tick 里随机抛
        //   NullReferenceException 并伴随进程段错误（崩点逐次漂移：105 行、130 行、422 行）。
        //   真要做「同一张卡不连续触发」时再加回来，并且要真的读它。
        public int[] cardHits;
        public int totalCardsFired;

        // 预分配，保证 Tick 零 GC
        Villager[] _old;

        public int[] lawHits;
        public int deaths;
        public int fearDeaths;

        // ═══ 8 条预设法表 ═══
        // 每行 = {condType, condParam, effType, effParam, tagArg}
        // condType: 0饥饿>N 1金币<N 2恐惧>N 3金币>N 4有标签 5被抢过
        // effType : 0加钱 1扣钱 2取食 3加恐惧 4抢邻居 5打标签 6去标签 7减恐惧 8耕作
        // tagArg  : 该法要打的标签位（0 = 不打）。只在 effType 5 时是主效果，
        //           在 effType 1（扣钱）时是附带效果 —— 「税收」会顺手给纳税者打「税民」标。
        public static int[][] PRESETS = new int[PRESETS_COUNT][];
        public const int PRESETS_COUNT = 8;
        static World()
        {
            PRESETS[0] = new[] { 1, 99, 0, 8, 0 };                   // 福利：人人发 8 金币（→ 富者多耗粮，反成负担）
            PRESETS[1] = new[] { 3, 40, 1, 12, TagBits.Taxpayer };   // 税收：富者纳税 12 + 打「税民」标（税民有读者：卡12）
            PRESETS[2] = new[] { 0, 45, 4, 12, 0 };                  // 饥则抢：饿则抢邻居 12
            PRESETS[3] = new[] { 0, 40, 2, 30, 0 };                  // 济贫：饿则从粮仓取食 30
            PRESETS[4] = new[] { 1, 99, 8, 1, 0 };                   // 耕作：人均 +0.30 粮/tick（30% 概率 × mag=1）——只抬高承载力，不取消承载力
                                                                     // 【为什么从 20 降到 2】原值 +20/人/tick = +400/tick，
                                                                     // 是基础产量的 20 倍 —— 一条法直接锁死「全员存活」，
                                                                     // 变成必选法（presence effect 一度 +11.28，把其他法全压成噪声）。
            PRESETS[5] = new[] { 2, 28, 3, 22, 0 };                  // 恐惧蔓延：怕的人更怕。
                                                                     // 【阈值 20→28】恐惧分布实测上移到 20~55（峰值 30-34），
                                                                     // 原阈值 20 的触发率是 97% ≈ 恒真，等于没有条件。
            PRESETS[6] = new[] { 2, 30, 7, 25, 0 };                  // 安抚：安抚恐慌（阈值落在实测可用区）
            PRESETS[7] = new[] { 4, 0, 5, 0, TagBits.Sinner };       // 私刑倾向：给有伤疤的人打「罪人」标
        }

        /// <summary>lawset = 5 个预设法下标（0..7）。传 null 则按 seed 随机（旧行为）。
        /// presets: 覆盖 8 条预设法表（调参与对照实验用；null 则用内置表）。
        /// 每行 = {condType, condParam, effType, effParam, tagArg}。</summary>
        public World(ulong seed, int[] lawset = null, int[][] presets = null)
        {
            if (presets != null) PRESETS = presets;
            this.seed = seed;
            v = new Villager[N];
            _old = new Villager[N];
            laws = new Law[SLOTS];
            view = new LawRuntimeView[SLOTS];
            rel = new int[N * N];
            cardHits = new int[CARDS];
            lawHits = new int[SLOTS];
            st.authority = AUTH_MAX;
            st.popPeak = N;
            st.granary = 120;

            for (int i = 0; i < N; i++)
            {
                v[i].alive = true;
                v[i].gold = Rng.Range(seed, 0, Rng.CH_VILLAGER, i, 5, 45);
                v[i].hunger = Rng.Range(seed, 0, Rng.CH_VILLAGER, 100 + i, 5, 45);
                v[i].fear = Rng.Range(seed, 0, Rng.CH_VILLAGER, 200 + i, 0, 25);
                v[i].food = Rng.Range(seed, 0, Rng.CH_VILLAGER, 400 + i, 0, 12);   // 各家开局的余粮不同
                // 每人一个性格 —— 既是耦合点，也是系统里唯一的异质性来源
                v[i].tags = (ushort)(1 << Rng.Range(seed, 0, Rng.CH_VILLAGER, 300 + i, 0, 4));
                Diag.W(v[i].tags, Diag.SRC_INIT);
            }

            // 装填 5 条法：由 lawset 指定（无则按 seed 随机 —— 旧行为）
            for (int s = 0; s < SLOTS; s++)
            {
                laws[s].state = LawState.Enacted;
                int pi;
                if (lawset != null) pi = lawset[s];
                else pi = Rng.Range(seed, 0, Rng.CH_DEAL, s, 0, PRESETS_COUNT);
                if (pi < 0) { laws[s].state = LawState.Empty; continue; }   // 空槽 = 无法状态对照
                int[] p = PRESETS[pi];
                laws[s].condType = (byte)p[0];
                laws[s].condParam = p[1];
                laws[s].effType = (byte)p[2];
                laws[s].effParam = p[3];
                laws[s].tagArg = (ushort)p[4];   // 第 5 列现在是真的 tagArg
            }
        }

        // ── ① 软回拉：越靠近边界拉力越大，而不是硬切 ──
        static int Soft(int x, int lo, int hi)
        {
            if (x < lo) return lo;
            if (x > hi) return hi;
            return x;
        }

        bool Cond(int s, int i, Villager[] src)
        {
            ref Law L = ref src_law[s];
            ref Villager x = ref src[i];
            switch (L.condType)
            {
                case 0: return x.hunger > L.condParam;
                case 1: return x.gold < L.condParam;
                case 2: Diag.FR(Diag.SRC_LAW); return x.fear > L.condParam;
                case 3: return x.gold > L.condParam;
                case 4: Diag.RMask((ushort)(L.tagArg | TagBits.SCAR_MASK), Diag.SRC_LAW); return (x.tags & (L.tagArg | TagBits.SCAR_MASK)) != 0;
                case 5: return x.nRobbed > 0;          // 读历史 → 卡/法之间形成链
                default: return false;
            }
        }

        // Cond 需要一个能访问律法的上下文（避免 ref 传 struct 数组的麻烦）
        Law[] src_law;

        void Apply(int s, int i)
        {
            ref Law L = ref laws[s];
            ref Villager x = ref v[i];
            int mag = ((L.flags & LawFlag.Fatigued) != 0) ? L.effParam / 2 : L.effParam;
            if (mag < 0) mag = 0;

            switch (L.effType)
            {
                case 0: x.gold = Soft(x.gold + mag, 0, 99); break;
                case 1: // 扣钱。★ 附带效果：若该法声明了 tagArg，顺手给被扣钱的人打上标。
                    //   「税收」因此会打出「税民」，而「税民」有真读者（卡 12 稽查）。
                    //   这是让「法 → 标签 → 事件卡」形成链的关键一环。
                    x.gold = Soft(x.gold - mag, 0, 99); x.nTaxed++;
                    if (L.tagArg != 0) { x.tags |= L.tagArg; Diag.W(L.tagArg, Diag.SRC_LAW); }
                    break;
                case 2: // 从粮仓取食
                    if (st.granary >= 10) { st.granary -= 10; x.hunger = Soft(x.hunger - mag, 0, 99); x.nFed++; }
                    break;
                case 3: x.fear = Soft(x.fear + mag, 0, 99); Diag.FW(Diag.SRC_LAW); break;
                case 4: // 抢最近的邻居
                    {
                        int t = (i + 1) % N;
                        if (v[t].alive)
                        {
                            int take = mag; if (v[t].gold < take) take = v[t].gold;
                            v[t].gold -= take; x.gold = Soft(x.gold + take, 0, 99);
                            v[t].nRobbed++; x.nRobbed++;
                            v[t].fear = Soft(v[t].fear + 18, 0, 99); Diag.FW(Diag.SRC_LAW);   // 被抢 → 恐惧冲击
                            rel[i * N + t] = ClampRel(rel[i * N + t] - 15);
                            rel[t * N + i] = ClampRel(rel[t * N + i] - 30);
                            if (v[t].nRobbed >= 2) { v[t].tags |= TagBits.Bandit; Diag.W(TagBits.Bandit, Diag.SRC_LAW); }   // 伤疤
                        }
                    }
                    break;
                case 5: // 打标签：若打的是伤疤位，则终身不可清除
                    x.tags |= L.tagArg; Diag.W(L.tagArg, Diag.SRC_LAW);
                    break;
                case 6: TagBits.ApplyRemoveTag(ref x.tags, L.tagArg); Diag.W(L.tagArg, Diag.SRC_LAW); break;
                case 7: x.fear = Soft(x.fear - mag, 0, 99); Diag.FW(Diag.SRC_LAW); break;
                case 8: // 耕作：往粮仓加粮（概率性 —— 见 FARM_PCT 的注释）
                    if (Rng.Chance(seed, st.tick, Rng.CH_VILLAGER, 4000 + i, FARM_PCT))
                    { st.granary = Math.Min(st.granary + mag, GRANARY_CAP); x.nProduced++; }
                    break;
            }
        }

        static int ClampRel(int x) => x < -100 ? -100 : (x > 100 ? 100 : x);

        // ── 事件卡：门禁读历史/标签 → 卡与卡之间通过状态间接耦合 ──
        void RunStorylets()
        {
            for (int i = 0; i < N; i++)
            {
                ref Villager x = ref v[i];
                if (!x.alive) continue;

                // 卡 1：抢过东西的人更容易再怕（读历史）
                if (x.nRobbed >= 1 && Rng.Chance(seed, st.tick, Rng.CH_DEAL, 1000 + i, 30))
                { x.fear = Soft(x.fear + 5, 0, 99); Diag.FW(Diag.SRC_CARD); x.tags |= TagBits.Starving; Diag.W(TagBits.Starving, Diag.SRC_CARD); cardHits[0]++; totalCardsFired++; }

                // 卡 2：劫匪 + 抢过多次 → 再犯（读历史 + 读标签 → 卡→卡链）
                if ((x.tags & TagBits.Bandit) != 0 && x.nRobbed >= 2 && Rng.Chance(seed, st.tick, Rng.CH_DEAL, 1100 + i, 22))
                {
                    Diag.R(TagBits.Bandit, Diag.SRC_CARD);
                    int t = (i + 3) % N;
                    if (v[t].alive) { int mv = 10; if (v[t].gold < mv) mv = v[t].gold; v[t].gold -= mv; x.gold = Soft(x.gold + mv, 0, 99); v[t].nRobbed++; }
                    cardHits[1]++; totalCardsFired++;
                }

                // 卡 3：被反复抢 → 受审（读历史）
                if (x.nRobbed >= 3 && x.hunger > 50 && Rng.Chance(seed, st.tick, Rng.CH_DEAL, 1200 + i, 15))
                { x.tags |= TagBits.Accused; Diag.W(TagBits.Accused, Diag.SRC_CARD); cardHits[2]++; totalCardsFired++; }

                // 卡 4：被人喂过 → 变成信徒（读历史）
                if (x.nFed >= 2 && Rng.Chance(seed, st.tick, Rng.CH_DEAL, 1300 + i, 20))
                { x.tags |= TagBits.Believer; Diag.W(TagBits.Believer, Diag.SRC_CARD); x.fear = Soft(x.fear - 8, 0, 99); Diag.FW(Diag.SRC_CARD); cardHits[3]++; totalCardsFired++; }

                // 卡 5：饥民聚集（读标签）
                if ((x.tags & TagBits.Starving) != 0)
                {
                    Diag.R(TagBits.Starving, Diag.SRC_CARD);
                    if (Rng.Chance(seed, st.tick, Rng.CH_DEAL, 1400 + i, 12))
                    { int t = (i + 7) % N; if (v[t].alive) { v[t].tags |= TagBits.Starving; Diag.W(TagBits.Starving, Diag.SRC_CARD); } cardHits[4]++; totalCardsFired++; }
                }

                // ═══ 以下五张是本次修法新增：给恐惧与死信标签接上读者 ═══
                int avgFear = st.aliveCount == 0 ? 0 : st.sumFear / st.aliveCount;

                // 卡 6：恐慌逃难（★ 读恐惧）—— 极怕且饿 → 逃离（留下逃兵伤疤）
                if (x.fear > 70) { Diag.FR(Diag.SRC_CARD);
                if (x.hunger > 40 && Rng.Chance(seed, st.tick, Rng.CH_DEAL, 1600 + i, 25))
                {
                    x.tags |= TagBits.Deserter; Diag.W(TagBits.Deserter, Diag.SRC_CARD);
                    x.fear = Soft(x.fear - 25, 0, 99); Diag.FW(Diag.SRC_CARD);
                    x.hunger = Soft(x.hunger + 12, 0, 99);
                    cardHits[5]++; totalCardsFired++;
                } }

                // 卡 7：结社互保（★ 读恐惧）—— 高恐惧的人抱团，彼此安抚
                if (x.fear > 50) { Diag.FR(Diag.SRC_CARD);
                if (Rng.Chance(seed, st.tick, Rng.CH_DEAL, 1700 + i, 15))
                {
                    x.fear = Soft(x.fear - 14, 0, 99); Diag.FW(Diag.SRC_CARD);
                    x.tags |= TagBits.Believer; Diag.W(TagBits.Believer, Diag.SRC_CARD);
                    int t = (i + 5) % N;
                    if (v[t].alive) { v[t].fear = Soft(v[t].fear - 8, 0, 99); Diag.FW(Diag.SRC_CARD); }
                    cardHits[6]++; totalCardsFired++;
                } }

                // 卡 8：私刑（★★ 同时读「罪人」标签与群体恐惧）—— 死信标签的第一个真读者
                if ((x.tags & TagBits.Sinner) != 0 && avgFear > 40) { Diag.R(TagBits.Sinner, Diag.SRC_CARD); Diag.FR(Diag.SRC_CARD);
                if (Rng.Chance(seed, st.tick, Rng.CH_DEAL, 1800 + i, 20))
                {
                    x.alive = false; x.deathTick = st.tick; deaths++; fearDeaths++;
                    for (int j = 0; j < N; j++) if (v[j].alive) { v[j].fear = Soft(v[j].fear + 15, 0, 99); Diag.FW(Diag.SRC_CARD); }
                    cardHits[7]++; totalCardsFired++;
                    continue;
                } }

                // 卡 9：罪人被排斥（★ 读标签）—— 罪人的钱谁都能拿
                if ((x.tags & TagBits.Sinner) != 0) { Diag.R(TagBits.Sinner, Diag.SRC_CARD);
                if (Rng.Chance(seed, st.tick, Rng.CH_DEAL, 1900 + i, 12))
                {
                    if (x.gold > 0) { x.gold -= 1; }
                    x.fear = Soft(x.fear + 6, 0, 99); Diag.FW(Diag.SRC_CARD);
                    cardHits[8]++; totalCardsFired++;
                } }

                // 卡 10：守卫出现（★ 读群体恐惧）—— 恐慌到一定程度，村里会自己长出维持秩序的人
                if (avgFear > 45) { Diag.FR(Diag.SRC_CARD);
                if ((x.tags & TagBits.Guard) == 0) { Diag.R(TagBits.Guard, Diag.SRC_CARD);
                if (Rng.Chance(seed, st.tick, Rng.CH_DEAL, 2000 + i, 3))
                {
                    x.tags |= TagBits.Guard; Diag.W(TagBits.Guard, Diag.SRC_CARD);
                    x.fear = Soft(x.fear - 20, 0, 99); Diag.FW(Diag.SRC_CARD);
                    cardHits[9]++; totalCardsFired++;
                } } }

                // 卡 11：信徒布道（★ 读「信徒」标签）—— 有信仰的人安抚邻里。
                //   这是恐惧链上唯一的**负反馈**：恐慌催生信仰，信仰平抑恐慌。
                if ((x.tags & TagBits.Believer) != 0)
                {
                    Diag.R(TagBits.Believer, Diag.SRC_CARD);
                    if (Rng.Chance(seed, st.tick, Rng.CH_DEAL, 2100 + i, 20))
                    {
                        int t = (i + 2) % N;
                        if (v[t].alive) { v[t].fear = Soft(v[t].fear - 6, 0, 99); Diag.FW(Diag.SRC_CARD); }
                        cardHits[10]++; totalCardsFired++;
                    }
                }

                // 卡 12：稽查税民（★ 读「税民」标签）—— 被反复盘剥的人更怕
                if ((x.tags & TagBits.Taxpayer) != 0)
                {
                    Diag.R(TagBits.Taxpayer, Diag.SRC_CARD);
                    if (Rng.Chance(seed, st.tick, Rng.CH_DEAL, 2200 + i, 15))
                    {
                        if (x.gold > 0) x.gold -= 1;
                        x.fear = Soft(x.fear + 4, 0, 99); Diag.FW(Diag.SRC_CARD);
                        cardHits[11]++; totalCardsFired++;
                    }
                }

                // 卡 13-20：同类变体（真实项目里是 20 张手工卡；这里用参数化变体占位）
                for (int k = 12; k < CARDS; k++)
                {
                    int seq = 2100 + k * 100 + i;
                    if (Rng.Chance(seed, st.tick, Rng.CH_DEAL, seq, 3))
                    { cardHits[k]++; totalCardsFired++; }
                }
            }
        }

        // ══════════ 一个 tick ══════════
        public void Tick()
        {
            src_law = laws;

            // 1. 派生量
            st.aliveCount = 0; st.sumFear = 0;
            for (int i = 0; i < N; i++) if (v[i].alive) { st.aliveCount++; st.sumFear += v[i].fear; }
            if (st.aliveCount > st.popPeak) st.popPeak = st.aliveCount;
            int avgFear = st.aliveCount == 0 ? 0 : st.sumFear / st.aliveCount;

            // 2. 威信结算（含停摆与迟滞恢复）
            int active = 0;
            for (int s = 0; s < SLOTS; s++)
                if (laws[s].state == LawState.Enacted && (laws[s].flags & LawFlag.Suspended) == 0) active++;
            int regen = (st.aliveCount * 2 / 5) + Math.Max(0, 30 - avgFear) / 5;   // 群体恐惧越高，威信恢复越慢
            Diag.FR(Diag.SRC_PHYS);
            if (!st.suspendedAll)
            {
                st.authority = st.authority - UPKEEP_PER_LAW * active + regen;
                if (st.authority <= 0) { st.authority = 0; st.suspendedAll = true; }
            }
            else
            {
                st.authority += regen;
                if (st.authority >= 1000) st.suspendedAll = false;   // 迟滞，防抖
            }
            for (int s = 0; s < SLOTS; s++)
            {
                if (st.suspendedAll && (laws[s].flags & LawFlag.Constitutional) == 0)
                    laws[s].flags |= LawFlag.Suspended;
                else laws[s].flags &= (byte)(~LawFlag.Suspended & 0xFF);
            }

            // 3. 律法（双缓冲：读 _old，写 v）
            for (int i = 0; i < N; i++) _old[i] = v[i];
            for (int s = 0; s < SLOTS; s++) view[s].hitsThisTick = 0;

            if (!st.suspendedAll)
            {
                for (int s = 0; s < SLOTS; s++)
                {
                    if (laws[s].state != LawState.Enacted) continue;
                    for (int i = 0; i < N; i++)
                    {
                        if (!v[i].alive) continue;
                        if (!Cond(s, i, _old)) continue;
                        Apply(s, i);
                        view[s].hitsThisTick++;
                        lawHits[s]++;
                    }
                    laws[s].hitsTotal += view[s].hitsThisTick;
                    if ((laws[s].flags & LawFlag.Fatigued) == 0 && laws[s].hitsTotal > st.popPeak * FATIGUE_K)
                        laws[s].flags |= LawFlag.Fatigued;
                }
            }

            // 4. 事件卡
            RunStorylets();

            // 4.5 粮食生产：★ 承载力模型（见 PROD_BASE/CROWD_K 的注释）
            //   人均产出随人口递减 → 存在平衡人口 P* → 结局是一条连续谱，而不是非 0 即 20。
            //   确定性部分（均值）+ 伯努利抖动（打破「跨 seed 存活 σ = 0.00」）
            int perCapita = PROD_BASE - st.aliveCount * CROWD_K;
            if (perCapita < 0) perCapita = 0;
            st.granary += st.aliveCount * perCapita / 1000;
            for (int i = 0; i < N; i++)
                if (v[i].alive && Rng.Chance(seed, st.tick, Rng.CH_VILLAGER, 8000 + i, PROD_JITTER_PCT))
                    st.granary += 1;
            if (st.granary > GRANARY_CAP) st.granary = GRANARY_CAP;
           

            // 5. 基础物理（②③④：性格调制 + 受状态调制的随机 + 比例衰减 + 软回拉）
            for (int i = 0; i < N; i++)
            {
                if (!v[i].alive) continue;

                // ② 性格调制每个村民的动力学参数 —— 异质性的唯一来源
                int prodBias = 0, drainBias = 0, fearBias = 0, eatBias = 0;
                if ((v[i].tags & TagBits.Greedy) != 0) { prodBias = 15; drainBias = -5; }
                if ((v[i].tags & TagBits.Diligent) != 0) { prodBias += 10; eatBias = -1; }
                if ((v[i].tags & TagBits.Coward) != 0) fearBias = 12;
                if ((v[i].tags & TagBits.Sickly) != 0) { prodBias -= 10; eatBias = +1; }
                if (Diag.On)   // 这 4 个性格位每 tick 每人都被物理层读一次
                { Diag.R(TagBits.Greedy, Diag.SRC_PHYS); Diag.R(TagBits.Diligent, Diag.SRC_PHYS); Diag.R(TagBits.Coward, Diag.SRC_PHYS); Diag.R(TagBits.Sickly, Diag.SRC_PHYS); }

                // 饥饿：★ 恐惧会加剧饥饿（怕的人吃不下、睡不着的整数近似）
                //   这一条把恐惧接进了「饥饿 → 粮仓 → 死亡」的主链，
                //   恐惧从此不再是自己玩自己，而是能真的饿死人
                v[i].hunger = Soft(v[i].hunger + 3 + eatBias + (FEAR_HUNGER_DIV > 0 ? v[i].fear / FEAR_HUNGER_DIV : 0), 0, 99);
                // ★ 个人存粮：公仓宽裕时往自家搬（贪婪的人搬得更凶）
                //   公仓 ↔ 个人存粮之间只发生转移，凭空不生粮 —— 配平不受影响。
                if (st.granary > STOCK_THRESHOLD && v[i].food < FOOD_CAP)
                {
                    int stockPct = 55 + ((v[i].tags & TagBits.Greedy) != 0 ? 30 : 0);
                    if (Rng.Chance(seed, st.tick, Rng.CH_VILLAGER, 9500 + i, stockPct))
                    { st.granary -= 1; v[i].food += 1; }
                }
                // 进食：先吃自家存粮，自家吃完了才去掏公仓 ——
                //   这一步把「20 个人共一口锅」拆成「20 个米缸」，
                //   断炊的时间点因此各人不同，死亡率才能落在 0 和 20 之间。
                if (v[i].hunger > 50)
                {
                    if (v[i].food >= 10) { v[i].food -= 10; v[i].hunger = Soft(v[i].hunger - 40, 0, 99); }
                    else if (st.granary >= 10) { st.granary -= 10; v[i].hunger = Soft(v[i].hunger - 40, 0, 99); }
                }

                // ③ 金币：随机生产/消耗 + ① 软回拉
                if (Rng.Chance(seed, st.tick, Rng.CH_VILLAGER, 5000 + i, 40 + prodBias)) v[i].gold = Soft(v[i].gold + 1, 0, 99);
                if (Rng.Chance(seed, st.tick, Rng.CH_VILLAGER, 6000 + i, 34 + drainBias)) v[i].gold = Soft(v[i].gold - 1, 0, 99);
                if (v[i].gold > 80) v[i].gold -= (v[i].gold - 80) / 3 + 1;   // 软回拉，不硬切
                if (v[i].gold < 5) v[i].gold += 1;

                // ★ 金币接进粮食经济：富者多耗粮（财富带来消费）
                //   这一条让金币不再是可以无限囤积的死钱 ——
                //   福利法（发钱）从此会加剧粮食压力，税收法（收钱）反而省粮，
                //   两条法第一次有了相反的、可测量的效果
                if (v[i].gold > 40 && Rng.Chance(seed, st.tick, Rng.CH_VILLAGER, 9000 + i, 15))
                { v[i].gold -= 2; st.granary -= 3; if (st.granary < 0) st.granary = 0; }

                // ④ 恐惧：源封顶 + 比例衰减 + 自举（缺一即饱和或归零）
                // ④ 恐惧：向「性格基线」回归 + 压力抬升 + 邻居传染
                //    基线是稳定内点的来源；事件冲击把它顶上去，再由回归拉回来 ——
                //    这样既不会饱和（第四次踩的坑），也不会归零（第一次踩的坑）
                int setpoint = 8 + fearBias;                                  // 怯懦的人基线更高
                if (v[i].hunger > 45) setpoint += 6;                          // 饿会怕
                if (v[i].gold < 8) setpoint += 4;                             // 穷会怕
                // ★ 个人历史写进基线 —— 这是同一时刻村民之间差异的来源，
                //   也是「路径依赖」：被抢过的人一辈子更难安心，被善待过的人更容易安心
                int hist = v[i].nRobbed * 3 + v[i].nTaxed - v[i].nFed * 2;
                if (hist > 14) hist = 14;
                if (hist < -8) hist = -8;
                setpoint += hist;
                int nb = 0;
                for (int k = 1; k <= 4; k++) { int j = (i + k) % N; if (v[j].alive && v[j].fear > setpoint + 15) nb++; }
                setpoint += nb * 2;                                           // 邻居更怕，自己也更怕
                // ★ 修恐惧-A：抬高点恐惧可达上限。原 cap=60 让「恐惧>35」几乎稳定越不过去，
                //   恐惧类法的条件全挤在死区（实测可用条件里恐惧阈值长期是死条件）。
                if (setpoint > 80) setpoint = 80;

                // ★ 修恐惧-D：恐慌传染（雪崩的燃料）
                //   原来邻居只通过 setpoint 间接影响自己，且 setpoint 有 80 的上限 ——
                //   恐惧再高也传不出「集体情绪」。这里让恐惧本身能直接点燃邻居：
                //   近邻平均恐惧高出自己一截 → 自己被传染 +2。
                //   这条与 FEAR_DECAY_DIV 一起，才可能造出「一人恐慌 → 一片恐慌」。
                {
                    int nbs = v[(i + 1) % N].alive ? v[(i + 1) % N].fear : 0;
                    int nbAvg;
                    if (v[(i + N - 1) % N].alive) { nbs += v[(i + N - 1) % N].fear; nbAvg = nbs / 2; }
                    else nbAvg = nbs;
                    if (nbAvg > v[i].fear + FEAR_CONTAGION_GAP &&
                        Rng.Chance(seed, st.tick, Rng.CH_VILLAGER, 7000 + i, FEAR_CONTAGION_PCT))
                    { v[i].fear = Soft(v[i].fear + 2, 0, 99); Diag.FW(Diag.SRC_PHYS); }
                }

                // ★ 修恐惧-B：恐慌有惯性。回落速率由 FEAR_DECAY_DIV 控制（原写死为 3）。
                //   分母小 → 冲击一个 tick 就被抹平 → 恐惧永远攒不起来 → 没有雪崩。
                if (v[i].fear < setpoint) v[i].fear += 1 + (Rng.Chance(seed, st.tick, Rng.CH_VILLAGER, 7000 + i, 60) ? 1 : 0);
                else if (v[i].fear > setpoint)
                {
                    int pull = (v[i].fear - setpoint) / FEAR_DECAY_DIV;
                    if (pull < 1) pull = 1;
                    v[i].fear -= pull;
                }
                v[i].fear = Soft(v[i].fear, 0, 99);
                Diag.FR(Diag.SRC_PHYS);

                // 死亡判定
                if (v[i].hunger >= 99)
                {
                    v[i].alive = false; v[i].deathTick = st.tick; deaths++;
                    // ★ 修恐惧-C：死亡冲击改为「近邻局域化 + 距离衰减」，打破全民同步的共同模式。
                    //   原实现给所有幸存者 +12 → 恐惧成了村庄级振荡，同一时刻村民之间拉不开差距。
                    for (int k = 1; k <= 4; k++)
                    {
                        int jp = (i + k) % N;      if (v[jp].alive) { v[jp].fear = Soft(v[jp].fear + (22 - 4 * k), 0, 99); Diag.FW(Diag.SRC_PHYS); }
                        int jm = (i - k + N) % N;  if (v[jm].alive) { v[jm].fear = Soft(v[jm].fear + (22 - 4 * k), 0, 99); Diag.FW(Diag.SRC_PHYS); }
                    }
                }
            }

            // 关系回归（整数）
            for (int i = 0; i < N * N; i++) rel[i] = rel[i] * 98 / 100;

            st.tick++;
        }

        // ══════════ 状态哈希（只吃模拟字段，逐字段显式混合）══════════
        public ulong HashState()
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                h = Rng.Mix64(h ^ (ulong)(uint)st.tick);
                h = Rng.Mix64(h ^ (ulong)(uint)st.aliveCount);
                h = Rng.Mix64(h ^ (ulong)(uint)st.authority);
                h = Rng.Mix64(h ^ (ulong)(uint)st.popPeak);
                h = Rng.Mix64(h ^ (ulong)(uint)deaths);
                for (int i = 0; i < N; i++)
                {
                    h = Rng.Mix64(h ^ (ulong)(uint)v[i].gold);
                    h = Rng.Mix64(h ^ (ulong)(uint)v[i].hunger);
                    h = Rng.Mix64(h ^ (ulong)(uint)v[i].fear);
                    h = Rng.Mix64(h ^ v[i].tags);
                    h = Rng.Mix64(h ^ (v[i].alive ? 1UL : 0UL));
                    h = Rng.Mix64(h ^ (ulong)(uint)v[i].nRobbed);
                    h = Rng.Mix64(h ^ (ulong)(uint)v[i].food);
                }
                for (int s = 0; s < SLOTS; s++) { h = Rng.Mix64(h ^ (ulong)(uint)laws[s].hitsTotal); h = Rng.Mix64(h ^ laws[s].flags); }
                return h;
            }
        }

        public int AliveCount() { int a = 0; for (int i = 0; i < N; i++) if (v[i].alive) a++; return a; }

    }
}
