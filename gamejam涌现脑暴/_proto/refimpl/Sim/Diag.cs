// ═══════════════════════════════════════════════════════════════════
// Sim/Diag.cs —— 只读遥测计数器。
//
// 存在的理由：DeepDiag 里曾经用**写死的字符串**报告「哪些标签是死信」
// 和「恐惧被谁读取」。修完代码之后那些字符串没跟着改，于是诊断仪开始撒谎。
// 结论：凡是关于「谁读了谁」的判断，必须是运行时数出来的，不能是人写的。
//
// 纪律：
//   · 计数器只被读，绝不参与模拟 → 开了也不影响确定性与结局
//   · On=false 时全部方法只剩一次静态 bool 判断（JIT 可预测分支）
//   · 无锁、无分配；一次 run 结束调 Reset 再开
// ═══════════════════════════════════════════════════════════════════
using System;

namespace SimLit
{
    public static class Diag
    {
        public const int NTAG = 13;
        // 标签下标（与 TagBits 的位一一对应）
        public const int T_COWARD = 0, T_GREEDY = 1, T_DILIGENT = 2, T_SICKLY = 3,
                         T_BANDIT = 4, T_DEBTOR = 5, T_DESERTER = 6, T_ACCUSED = 7,
                         T_TAXPAYER = 8, T_BELIEVER = 9, T_STARVING = 10, T_GUARD = 11, T_SINNER = 12;

        public static readonly string[] TAG_NAME =
        { "怯懦", "贪婪", "勤勉", "病弱", "劫匪", "负债", "逃兵", "受审", "税民", "信徒", "饥民", "守卫", "罪人" };

        // 来源分类（决定「谁在读」）
        public const int SRC_INIT = 0, SRC_LAW = 1, SRC_CARD = 2, SRC_PHYS = 3, NSRC = 4;
        public static readonly string[] SRC_NAME = { "初始化", "律法", "事件卡", "物理层" };

        public static bool On;
        public static readonly long[,] tagRead = new long[NTAG, NSRC];
        public static readonly long[,] tagWrite = new long[NTAG, NSRC];

        // 恐惧的读/写，同样按来源分类
        public static readonly long[] fearRead = new long[NSRC];
        public static readonly long[] fearWrite = new long[NSRC];

        public static void Reset()
        {
            Array.Clear(tagRead, 0, tagRead.Length);
            Array.Clear(tagWrite, 0, tagWrite.Length);
            Array.Clear(fearRead, 0, NSRC);
            Array.Clear(fearWrite, 0, NSRC);
        }

        // 位 → 下标
        public static int Idx(ushort bit)
        {
            switch (bit)
            {
                case TagBits.Coward: return T_COWARD;
                case TagBits.Greedy: return T_GREEDY;
                case TagBits.Diligent: return T_DILIGENT;
                case TagBits.Sickly: return T_SICKLY;
                case TagBits.Bandit: return T_BANDIT;
                case TagBits.Debtor: return T_DEBTOR;
                case TagBits.Deserter: return T_DESERTER;
                case TagBits.Accused: return T_ACCUSED;
                case TagBits.Taxpayer: return T_TAXPAYER;
                case TagBits.Believer: return T_BELIEVER;
                case TagBits.Starving: return T_STARVING;
                case TagBits.Guard: return T_GUARD;
                case TagBits.Sinner: return T_SINNER;
                default: return -1;
            }
        }

        public static ushort BitOf(int k)
        {
            switch (k)
            {
                case T_COWARD: return TagBits.Coward;
                case T_GREEDY: return TagBits.Greedy;
                case T_DILIGENT: return TagBits.Diligent;
                case T_SICKLY: return TagBits.Sickly;
                case T_BANDIT: return TagBits.Bandit;
                case T_DEBTOR: return TagBits.Debtor;
                case T_DESERTER: return TagBits.Deserter;
                case T_ACCUSED: return TagBits.Accused;
                case T_TAXPAYER: return TagBits.Taxpayer;
                case T_BELIEVER: return TagBits.Believer;
                case T_STARVING: return TagBits.Starving;
                case T_GUARD: return TagBits.Guard;
                case T_SINNER: return TagBits.Sinner;
                default: return 0;
            }
        }

        public static void R(ushort bit, int src) { if (On) { int k = Idx(bit); if (k >= 0) tagRead[k, src]++; } }
        public static void W(ushort bit, int src) { if (On) { int k = Idx(bit); if (k >= 0) tagWrite[k, src]++; } }
        public static void RMask(ushort mask, int src)
        {
            if (!On) return;
            for (int k = 0; k < NTAG; k++) if ((mask & BitOf(k)) != 0) tagRead[k, src]++;
        }
        public static void FR(int src) { if (On) fearRead[src]++; }
        public static void FW(int src) { if (On) fearWrite[src]++; }

        public static long TagReadTotal(int k) { long s = 0; for (int j = 0; j < NSRC; j++) s += tagRead[k, j]; return s; }
        public static long TagWriteTotal(int k) { long s = 0; for (int j = 0; j < NSRC; j++) s += tagWrite[k, j]; return s; }
        public static long FearReadTotal() { long s = 0; for (int j = 0; j < NSRC; j++) s += fearRead[j]; return s; }
        public static long FearWriteTotal() { long s = 0; for (int j = 0; j < NSRC; j++) s += fearWrite[j]; return s; }
    }
}
