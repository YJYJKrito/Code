// ═══════════════════════════════════════════════════════════════════
// Diag/WorldStats.cs —— 统计与自检用的只读视图
//
// 【为什么这两个方法必须从 Sim/ 里搬出来】
//   它们用了 double / Math.Sqrt，而整条纪律是「Sim 层禁浮点」——
//   因为浮点是不可跨端复现的（x86 与 ARM64 对 float→int 越界转换的规范行为不同）。
//
//   Stat() 和 UsableConditions() 都不参与 Tick，永远不会反馈进状态，
//   所以它们本身是安全的。但只要它们留在 Sim/ 里，「Sim 层没有浮点」这条规则
//   就**无法用文本扫描来证明** —— 于是要么给测试开一个会腐烂的白名单，
//   要么这条规则形同虚设。
//
//   → 正确的做法是把诊断层单独拆出来。Unity 侧对应的是两个 asmdef：
//       Assets/Sim/   → 勾 noEngineReferences，禁浮点，纯确定性
//       Assets/Diag/  → 允许浮点、允许 UnityEngine，只读，不进构建产物
//
//   拆开之后 T08_01 可以是一条**绝对**断言，不需要任何例外。
// ═══════════════════════════════════════════════════════════════════
using System;

namespace SimLit
{
    public static class WorldStats
    {
        /// <summary>三个寄存器的均值与标准差（只读，可浮点）。</summary>
        public static (double mg, double mh, double mf, double sg, double sh, double sf) Stat(this World w)
        {
            int a = 0; double g = 0, hh = 0, f = 0;
            for (int i = 0; i < World.N; i++)
                if (w.v[i].alive) { a++; g += w.v[i].gold; hh += w.v[i].hunger; f += w.v[i].fear; }
            if (a == 0) return (0, 0, 0, 0, 0, 0);
            g /= a; hh /= a; f /= a;
            double vg = 0, vh = 0, vf = 0;
            for (int i = 0; i < World.N; i++)
                if (w.v[i].alive)
                {
                    vg += (w.v[i].gold - g) * (w.v[i].gold - g);
                    vh += (w.v[i].hunger - hh) * (w.v[i].hunger - hh);
                    vf += (w.v[i].fear - f) * (w.v[i].fear - f);
                }
            return (g, hh, f, Math.Sqrt(vg / a), Math.Sqrt(vh / a), Math.Sqrt(vf / a));
        }

        /// <summary>实测的「律法条件可用率」：10 个候选阈值里有几个落在 5%~95% 之间。
        ///
        /// 【阈值是怎么选的 —— 别再手写死】这三个带子由 DeepDiag 的 B2b 节实测得出
        /// （跑若干局后取经验分位数），当前值：
        ///     金币  N ∈ (2, 91)     饥饿  N ∈ (12, 49)     恐惧  N ∈ (20, 42)
        /// 下面 10 个探针全部落在带内。**一旦动力学改了，必须先重跑 --deep 看 B2b，
        /// 再回来改这里** —— 否则这台仪器会拿着带外的阈值去给一个不存在的世界打分，
        /// 然后报出一个看着很正常、其实毫无意义的「5/10」。这正是它以前干过的事
        /// （旧探针 f>15 恒真、f>60 死条件，两个早已失效却还在计分）。</summary>
        public static int UsableConditions(this World w)
        {
            int usable = 0;
            void P(Func<int, int, int, bool> f)
            {
                int hit = 0, tot = 0;
                for (int i = 0; i < World.N; i++)
                    if (w.v[i].alive) { tot++; if (f(w.v[i].gold, w.v[i].hunger, w.v[i].fear)) hit++; }
                double p = tot == 0 ? 0 : hit * 100.0 / tot;
                if (p >= 5 && p <= 95) usable++;
            }
            // 饥饿带 (12,49)
            P((g, h, f) => h > 20); P((g, h, f) => h > 30); P((g, h, f) => h > 40);
            // 金币带 (2,91)
            P((g, h, f) => g < 20); P((g, h, f) => g < 45); P((g, h, f) => g > 70);
            // 恐惧带 (20,42)
            P((g, h, f) => f > 24); P((g, h, f) => f > 30); P((g, h, f) => f > 36);
            P((g, h, f) => g > 20);
            return usable;
        }
    }
}
