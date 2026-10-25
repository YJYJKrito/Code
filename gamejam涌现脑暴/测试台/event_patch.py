# -*- coding: utf-8 -*-
"""
在构建自带的 VillageEvents 上补两项规格：

  ① 对口法律：装了某条法 → 对应事件的效果乘一个百分比（多条相乘）
  ② 事件连锁：蝗灾/寒潮 之后 2 年内，民变权重 +60；疫病之后 1 年内，疫病权重 +30

外加：冷却 14600 → 8760（5 年 → 3 年）。

这些是**实验性补丁**：验证完写进给程序的方案，正式实现应由程序改工程源码。
由 setup.py 在生成 Sim.cs 之后调用 apply(sim)。
"""

import re

NL = chr(10)

COOLDOWN_OLD = "private const int CooldownTicks = 14600;"
COOLDOWN_NEW = "private const int CooldownTicks = 8760;"

WEIGHT_OLD = """			1 => 1 + num / 15,
			2 => 3,
			3 => 1 + (300 - data.prestige) / 12 + num3 / 3, """

WEIGHT_NEW = """			1 => 1 + num / 15 + ((data.tick < data.chainPlague) ? 30 : 0),
			2 => 3,
			3 => 1 + (300 - data.prestige) / 12 + num3 / 3 + ((data.tick < data.chainMobilize) ? 60 : 0), """

FIELDS_OLD = "		public int executions;"
FIELDS_NEW = """		public int executions;

		public int chainMobilize;

		public int chainPlague;

		public int[] omenEventIds = new int[0];

		public int[] omenBurstTicks = new int[0];"""

HELPERS = '''
		private static bool EvHasLaw(VillageSaveData data, int lawId)
		{
			if (data.prestigeSuspended || data.lawIds == null)
			{
				return false;
			}
			for (int i = 0; i < data.lawIds.Length; i++)
			{
				if (data.lawIds[i] == lawId)
				{
					return true;
				}
			}
			return false;
		}

		private static int Avec(VillageSaveData data, int pct, int lawId, int p)
		{
			if (!EvHasLaw(data, lawId))
			{
				return pct;
			}
			return pct * p / 100;
		}

		/// <summary>对口法律：装了哪些法 → 累乘出的百分比（100 = 没装）</summary>
		private static int LawPct(VillageSaveData data, int id)
		{
			int pct = 100;
			switch (id)
			{
			case 0:
				pct = Avec(data, pct, 5, 50); pct = Avec(data, pct, 71, 50); break;
			case 1:
				pct = Avec(data, pct, 12, 50); pct = Avec(data, pct, 13, 40); break;
			case 2:
				pct = Avec(data, pct, 15, 0); pct = Avec(data, pct, 48, 150); break;
			case 3:
				pct = Avec(data, pct, 6, 50); pct = Avec(data, pct, 1, 70); pct = Avec(data, pct, 16, 150); break;
			case 4:
				pct = Avec(data, pct, 14, 50); break;
			case 5:
				pct = Avec(data, pct, 30, 50); break;
			case 6:
				pct = Avec(data, pct, 10, 40); pct = Avec(data, pct, 25, 0); pct = Avec(data, pct, 2, 150); break;
			case 7:
				pct = Avec(data, pct, 5, 150); break;
			case 8:
				pct = Avec(data, pct, 29, 150); pct = Avec(data, pct, 31, 150); pct = Avec(data, pct, 14, 60); break;
			case 9:
				pct = Avec(data, pct, 28, 150); break;
			case 10:
				pct = Avec(data, pct, 17, 150); break;
			case 11:
				pct = Avec(data, pct, 12, 0); break;
			case 12:
				pct = Avec(data, pct, 35, 150); pct = Avec(data, pct, 6, 130); break;
			default:
				pct = Avec(data, pct, 8, 150); pct = Avec(data, pct, 9, 130); break;
			}
			return pct;
		}

		/// <summary>按百分比缩放；只要不是「全免」，至少保留 1</summary>
		private static int Sk(int v, int pct)
		{
			if (pct <= 0)
			{
				return 0;
			}
			int num = v * pct / 100;
			return (num < 1) ? 1 : num;
		}

'''

FIRE_NEW = '''		private static void Fire(VillageSaveData data, int id, int tick)
		{
			bool flag = Role(data, 1u);
			bool flag2 = Role(data, 2u);
			bool flag3 = Role(data, 4u);
			bool flag4 = Role(data, 8u);
			bool flag5 = Role(data, 16u);
			bool flag6 = Role(data, 32u);
			bool flag7 = Role(data, 64u);
			bool flag8 = Role(data, 128u);
			Cfg.EventFired[id]++;
			int lp = LawPct(data, id);
			switch (id)
			{
			case 0:
			{
				int dmg = Sk(data.granaryFood * (flag ? 30 : 60) / 100, lp);
				data.granaryFood = ClampGranary(data, data.granaryFood - dmg);
				Cfg.EventGranary[0] += dmg;
				data.chainMobilize = tick + 5840;
				break;
			}
			case 1:
				KillSome(data, Sk(flag2 ? 1 : 3, lp), infants: true);
				AddFear(data, (flag2 ? 8 : 15) * lp / 100);
				data.chainPlague = tick + 2920;
				break;
			case 2:
				if (!flag3)
				{
					if (data.treasuryGold >= 100)
					{
						data.treasuryGold = Math.Max(0, data.treasuryGold - Sk(100, lp));
						break;
					}
					KillSome(data, Sk(2, lp), infants: false);
					AddFear(data, 25 * lp / 100);
				}
				break;
			case 3:
				KillSome(data, Sk(flag4 ? 1 : 3, lp), infants: false);
				data.prestige = Math.Max(0, data.prestige - 30);
				StealFood(data, 30 * lp / 100);
				break;
			case 4:
				data.treasuryGold = Math.Max(0, data.treasuryGold - Sk(flag5 ? 75 : 150, lp));
				data.prestige = Math.Max(0, data.prestige - 25);
				break;
			case 5:
			{
				int dmg = Sk(data.granaryFood * (flag6 ? 25 : 50) / 100, lp);
				data.granaryFood = ClampGranary(data, data.granaryFood - dmg);
				Cfg.EventGranary[5] += dmg;
				StealFood(data, 20 * lp / 100);
				data.chainMobilize = tick + 5840;
				if (Cfg.ChainMode == 2)
				{
					for (int q = 0; q < 3; q++)
					{
						if (q < data.omenEventIds.Length && data.omenEventIds[q] < 0)
						{
							data.omenEventIds[q] = 3;
							data.omenBurstTicks[q] = tick + 2920;
							break;
						}
					}
				}
				break;
			}
			case 6:
			{
				if (flag3)
				{
					break;
				}
				for (int i = 0; i < data.villagerAlive.Length; i++)
				{
					if (data.villagerAlive[i] && data.villagerGold[i] > 0)
					{
						data.villagerGold[i] -= data.villagerGold[i] / 5 + 1;
						if (data.villagerGold[i] < 0)
						{
							data.villagerGold[i] = 0;
						}
					}
				}
				AddFear(data, 12 * lp / 100);
				StealFood(data, 50 * lp / 100);
				break;
			}
			case 7:
				data.granaryFood = ClampGranary(data, data.granaryFood + (flag ? 180 : 120) * lp / 100);
				FillFood(data, 4 * lp / 100);
				break;
			case 8:
				data.treasuryGold = Math.Max(0, data.treasuryGold - 50);
				data.granaryFood = ClampGranary(data, data.granaryFood + (110 + (flag7 ? 60 : 0)) * lp / 100);
				break;
			case 9:
				data.prestige = Math.Max(0, data.prestige - (flag4 ? 15 : 30));
				data.granaryFood = ClampGranary(data, data.granaryFood + 100 * lp / 100);
				break;
			case 10:
			{
				int num = (HasSettler(data) ? 6 : 4) * lp / 100;
				if (num < 1)
				{
					num = 1;
				}
				for (int j = 0; j < num; j++)
				{
					VillageLifecycleService.AppendImmigrant(data);
				}
				break;
			}
			case 11:
				if (!flag2)
				{
					data.treasuryGold = Math.Max(0, data.treasuryGold - 80);
				}
				AddFear(data, -10);
				break;
			case 12:
				AddFear(data, HasShaman(data) ? (-40) : (-30));
				data.prestige = Math.Max(0, data.prestige - 40);
				break;
			default:
				data.birthBoomUntilTick = tick + 5840;
				data.birthBoomPercent = (flag8 ? 200 : 150);
				AddFear(data, -10);
				break;
			}
		}
'''


def _find_method(text, sig):
    """从签名行开始，按大括号配平找到方法结束。"""
    i = text.index(sig)
    j = text.index('{', i)
    depth = 0
    k = j
    while k < len(text):
        if text[k] == '{':
            depth += 1
        elif text[k] == '}':
            depth -= 1
            if depth == 0:
                return i, k + 1
        k += 1
    raise RuntimeError('unbalanced')



ENSURE_NEW = """		private const int MaxOmens = 3;

		public static void Ensure(VillageSaveData data)
		{
			if (data == null)
			{
				return;
			}
			if (data.eventLastFire == null)
			{
				data.eventLastFire = new int[14];
			}
			if (data.eventLastFire.Length != 14)
			{
				int num = data.eventLastFire.Length;
				Array.Resize(ref data.eventLastFire, 14);
				for (int i = num; i < 14; i++)
				{
					data.eventLastFire[i] = 0;
				}
			}
			if (data.omenEventIds == null || data.omenEventIds.Length != MaxOmens)
			{
				int num2 = ((data.omenEventIds == null) ? 0 : data.omenEventIds.Length);
				Array.Resize(ref data.omenEventIds, MaxOmens);
				Array.Resize(ref data.omenBurstTicks, MaxOmens);
				for (int j = num2; j < MaxOmens; j++)
				{
					data.omenEventIds[j] = -1;
					data.omenBurstTicks[j] = 0;
				}
			}
		}
"""

TICK_NEW = """		public static int Tick(VillageSaveData data, int tick, ref string eventText)
		{
			if (data == null || tick <= 0)
			{
				return 0;
			}
			Ensure(data);
			int num = 0;
			for (int i = 0; i < MaxOmens; i++)
			{
				if (data.omenEventIds[i] >= 0 && tick >= data.omenBurstTicks[i])
				{
					int num2 = data.omenEventIds[i];
					data.omenEventIds[i] = -1;
					data.omenBurstTicks[i] = 0;
					data.eventLastFire[num2] = tick;
					Fire(data, num2, tick);
					eventText = Bursts[num2];
					num++;
				}
			}
			if (num > 0)
			{
				return 1;
			}
			if (tick % 1800 != 0)
			{
				return 0;
			}
			int num3 = 0;
			for (int j = 0; j < MaxOmens; j++)
			{
				if (data.omenEventIds[j] >= 0)
				{
					num3++;
				}
			}
			if (num3 >= MaxOmens)
			{
				return 0;
			}
			if (Rng.Range(data.rngSeed, tick, 62, 0, 0, 100) >= 45)
			{
				return 0;
			}
			int num4 = Pick(data, tick);
			if (Cfg.ForceEvent >= 0)
			{
				num4 = Cfg.ForceEvent;
			}
			if (num4 < 0)
			{
				return 0;
			}
			for (int k = 0; k < MaxOmens; k++)
			{
				if (data.omenEventIds[k] < 0)
				{
					data.omenEventIds[k] = num4;
					data.omenBurstTicks[k] = tick + 2920;
					break;
				}
			}
			eventText = Omens[num4];
			return 0;
		}
"""

PICK_NEW = """		private static int CooldownFor(int id)
		{
			switch (id)
			{
			case 1:
				return 1460;
			case 3:
				return 2920;
			default:
				return CooldownTicks;
			}
		}

		private static int Pick(VillageSaveData data, int tick)
		{
			int[] array = new int[14];
			int num = 0;
			for (int i = 0; i < 14; i++)
			{
				bool flag = false;
				for (int k = 0; k < MaxOmens; k++)
				{
					if (data.omenEventIds[k] == i)
					{
						flag = true;
					}
				}
				if (flag)
				{
					continue;
				}
				if (tick - data.eventLastFire[i] >= CooldownFor(i))
				{
					int num2 = Weight(data, i);
					if (num2 < 1)
					{
						num2 = 1;
					}
					array[i] = num2;
					num += num2;
				}
			}
			if (num <= 0)
			{
				return -1;
			}
			int num3 = Rng.Range(data.rngSeed, tick, 61, 0, 0, num);
			for (int j = 0; j < 14; j++)
			{
				if (array[j] > 0)
				{
					if (num3 < array[j])
					{
						return j;
					}
					num3 -= array[j];
				}
			}
			return -1;
		}
"""


def apply(sim):
    log = []

    if COOLDOWN_OLD in sim:
        sim = sim.replace(COOLDOWN_OLD, COOLDOWN_NEW, 1)
        log.append(("冷却 5 年 → 3 年", "OK"))
    else:
        log.append(("冷却 5 年 → 3 年", "SKIP 已改或找不到"))

    w1_old = "1 => 1 + num / 15,"
    w1_new = "1 => 1 + num / 15 + ((Cfg.ChainMode == 1 && data.tick < data.chainPlague) ? 30 : 0),"
    w3_old = "3 => 1 + (300 - data.prestige) / 12 + num3 / 3,"
    w3_new = "3 => 1 + (300 - data.prestige) / 12 + num3 / 3 + ((Cfg.ChainMode == 1 && data.tick < data.chainMobilize) ? Cfg.ChainBoost : 0),"
    if w1_old in sim and w3_old in sim:
        sim = sim.replace(w1_old, w1_new, 1).replace(w3_old, w3_new, 1)
        log.append(("事件连锁（权重）", "OK"))
    else:
        log.append(("事件连锁（权重）", "SKIP 已改或找不到"))

    if "public int chainMobilize;" not in sim:
        sim = sim.replace(FIELDS_OLD, FIELDS_NEW, 1)
        log.append(("连锁字段", "OK"))
    else:
        log.append(("连锁字段", "SKIP 已存在"))

    pick_old = "int num3 = Pick(data, tick);"
    pick_new = "int num3 = (Cfg.ForceEvent >= 0) ? Cfg.ForceEvent : Pick(data, tick);"
    if pick_old in sim and pick_new not in sim:
        sim = sim.replace(pick_old, pick_new, 1)
        log.append(("强制事件开关", "OK"))
    else:
        log.append(("强制事件开关", "SKIP"))

    if "private static int LawPct(" not in sim:
        # 插在 Fire 方法之前
        idx = sim.index("		private static void Fire(VillageSaveData data, int id, int tick)")
        sim = sim[:idx] + HELPERS + sim[idx:]
        log.append(("对口法律工具函数", "OK"))
    else:
        log.append(("对口法律工具函数", "SKIP 已存在"))

    a, b = _find_method(sim, "		private static void Fire(VillageSaveData data, int id, int tick)")
    body = sim[a:b]
    if "LawPct(data, id)" not in body:
        sim = sim[:a] + FIRE_NEW + sim[b:]
        log.append(("Fire 重写（含对口法律）", "OK"))
    else:
        log.append(("Fire 重写（含对口法律）", "SKIP 已改"))

    # ── 兆头槽：单数 → 3 格 ──
    if "MaxOmens" not in sim and "omenEventIds.Length != 3" not in sim:
        for sig, new in [
            ("\t\tpublic static void Ensure(VillageSaveData data)", ENSURE_NEW),
            ("\t\tpublic static int Tick(VillageSaveData data, int tick, ref string eventText)", TICK_NEW),
            ("\t\tprivate static int Pick(VillageSaveData data, int tick)", PICK_NEW),
        ]:
            a, b = _find_method(sim, sig)
            sim = sim[:a] + new + sim[b:]
        log.append(("兆头槽 1 → 3", "OK"))
    else:
        log.append(("兆头槽 1 → 3", "SKIP 已改"))

    return sim, log


if __name__ == "__main__":
    import io
    p = r"E:\CODE\gamejam涌现脑暴\测试台\pscan\Sim.cs"
    s = io.open(p, encoding="utf-8").read()
    s2, log = apply(s)
    for n, r in log:
        print("   [%-4s] %s" % (r.split()[0], n))
    io.open(p, "w", encoding="utf-8").write(s2)
    print("   Sim.cs 已更新")
