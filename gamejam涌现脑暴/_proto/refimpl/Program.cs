// 参考实现入口：跑全部自检 + 打印一个样例村庄的轨迹
using System;
using System.Text;
using SimLit;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length > 0 && args[0] == "--trace")
        {
            Trace(7);
            return;
        }

        if (args.Length > 0 && args[0] == "--deep")
        {
            Console.WriteLine(DeepDiag.Run());
            return;
        }

        if (args.Length > 0 && args[0] == "--legis")
        {
            Console.WriteLine(LegisEffect.Run());
            return;
        }

        if (args.Length > 0 && args[0] == "--percept")
        {
            Console.WriteLine(Percept.Run());
            return;
        }

        if (args.Length > 0 && args[0] == "--rig")
        {
            Console.WriteLine(LegisRigorous.Run(args));
            return;
        }

        if (args.Length > 0 && args[0] == "--calib")
        {
            Console.WriteLine(Calib.Run());
            return;
        }

        if (args.Length > 0 && args[0] == "--probe") { Console.WriteLine(Probe.Run()); return; }

        if (args.Length > 0 && args[0] == "--calib2")
        {
            Console.WriteLine(Calib.RunFine());
            return;
        }

        Console.WriteLine(SelfTest.RunAll());

        Console.WriteLine("\n══════ 样例村庄轨迹（seed=7）══════");
        Trace(7, brief: true);
    }

    static void Trace(ulong seed, bool brief = false)
    {
        var w = new World(seed);
        Console.WriteLine("  tick | 存活 | 威信 | 死亡 | 触发卡 | 均金币(σ) | 均饥饿(σ) | 均恐惧(σ) | 可用条件");
        for (int t = 0; t < World.MAXT; t++)
        {
            w.Tick();
            if (t % 400 == 0 || t == World.MAXT - 1 || w.AliveCount() == 0)
            {
                var s = w.Stat();
                Console.WriteLine($"  {w.st.tick,4} | {w.AliveCount(),3}/20 | {w.st.authority / 100.0,5:F1} | {w.deaths,3} | {w.totalCardsFired,5} | "
                    + $"{s.mg,4:F0}({s.sg,4:F1}) | {s.mh,4:F0}({s.sh,4:F1}) | {s.mf,4:F0}({s.sf,4:F1}) |   {w.UsableConditions(),2}/10");
            }
            if (w.AliveCount() == 0) break;
        }
        if (!brief)
        {
            Console.WriteLine("\n  存活村民的性格与伤疤：");
            for (int i = 0; i < World.N; i++)
                if (w.v[i].alive)
                    Console.WriteLine($"    #{i,2} {TagBits.Describe(w.v[i].tags),-22} 金币 {w.v[i].gold,2} 饥饿 {w.v[i].hunger,2} 恐惧 {w.v[i].fear,2} 被抢 {w.v[i].nRobbed} 被喂 {w.v[i].nFed}");
        }
    }
}
