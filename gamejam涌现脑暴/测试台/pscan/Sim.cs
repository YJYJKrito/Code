using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

	public sealed class VillageSaveData
	{
		public const int CurrentVersion = 17;

		public const int LawSlotCount = 12;

		public const int ActiveLawSlotCount = 6;

		public int version = 17;

		public int slot;

		public string villageName = "立法村";

		public string createdAtUtc = string.Empty;

		public string updatedAtUtc = string.Empty;

		public int tick;

		public int prestige = 100;

		public int rngSeed;

		public int mapSeed;

		public int mapGenVersion;

		public byte[] terrain = new byte[0];

		public int terrainRevision;

		public int arableBaseline;

		public int speedMultiplier = 1;

		public bool tyrantMode;

		public bool prestigeSuspended;

		public int granaryFood = 220;

		public int granaryCapacity = 500;

		public int treasuryGold;

		public int totalBirths;

		public int totalDeaths;

		public int purchasedLawCount;

		public int[] purchasedLawIds = new int[0];

		public string lastEventText = string.Empty;

		public int lastProduction;

		public int lastConsumption;

		public int lastPrestigeDelta;

		public string[] lawNames = new string[6];

		public int[] lawIds = new int[6];

		public int[] lawStrength = new int[6];

		public int[] lawCooldown = new int[6];

		public string[] villagerNames = new string[0];

		public int[] villagerGenders = new int[0];

		public int[] villagerAges = new int[0];

		public int[] villagerAgeTicks = new int[0];

		public bool[] villagerAlive = new bool[0];

		public int[] villagerGold = new int[0];

		public int[] villagerHunger = new int[0];

		public int[] villagerFear = new int[0];

		public int[] villagerFood = new int[0];

		public int[] villagerBirthCooldown = new int[0];

		public int[] villagerChildren = new int[0];

		public uint[] villagerInnate = new uint[0];

		public uint[] villagerMarks = new uint[0];

		public uint[] villagerRoles = new uint[0];

		public int eventOmenId = -1;

		public int eventOmenTick;

		public int[] eventLastFire = new int[0];

		public int birthBoomUntilTick;

		public int birthBoomPercent = 100;

		public int[] omenEventIds = new int[0];

		public int[] omenBurstTicks = new int[0];

		public int chainMobilize;

		public int chainPlague;

		public int executions;

		public int[] villagerNeighborDeaths = new int[0];

		public int[] villagerDonations = new int[0];

		public int[] villagerChildrenAlive = new int[0];

		public int[] villagerMother = new int[0];

		public int[] villagerFather = new int[0];

		public int[] villagerWealthPeak = new int[0];

		public int[] villagerHungryTicks = new int[0];

		public int obituaryCount;

		public int[] obituaryTicks = new int[0];

		public int[] obituaryAges = new int[0];

		public int[] obituaryLawIndices = new int[0];

		public int[] obituaryVillagerIndices = new int[0];

		public bool[] obituaryInfants = new bool[0];

		public string[] obituaryNames = new string[0];

		public string[] obituaryCauses = new string[0];

		public string[] obituaryTexts = new string[0];

		public int[] obituaryGenders = new int[0];

		public uint[] obituaryInnate = new uint[0];

		public uint[] obituaryMarks = new uint[0];

		public string[] obituaryLawNames = new string[0];

		public byte[] obituaryLawKinds = new byte[0];

		public int runMode = -1;

		public int personMonths;

		public int famineMonths;

		public long hungerSum;

		public int monthSamples;

		public int vStreak;

		public int lowStreak;

		public int tinyStreak;

		public int clearMonth;

		public int endKind;

		public int grade;

		public int legacyObituaryCount;
	}

	public struct LifecycleReport
	{
		public int births;

		public int deaths;

		public int immigrants;

		public int events;

		public string eventText;

		public bool showBanner;
	}

	public struct LawTickResult
	{
		public int deaths;

		public string eventText;
	}

	public static class VillageLifecycleService
	{
		private const int TicksPerDay = 8;

		private const int EatIntervalTicks = 16;

		private const int HungerIntervalAdultTicks = 32;

		private const int HungerIntervalInfantTicks = 16;

		private const int FearIntervalTicks = 32;

		private const int GranaryIntervalTicks = 56;

		private const int InfantMortalityIntervalTicks = 120;

		private const int OldAgeOnset = 60;

		private const int OldAgeDiv = 3500;

		private const int OldAgeCap = 20;

		private const int ChannelOldAge = 5400;

		private const int PrivateFoodCap = 40;

		private const int GranaryReserve = 40;

		private const int TreasuryPeriodTicks = 2400;

		private const int TreasuryPopDiv = 3;

		private const int TreasuryUpkeepDiv = 4;

		private const int ImmigrantPeriodTicks = 2400;

		private const int ImmigrantThreshold = 25;

		private const int ImmigrantGranaryFloor = 20;

		private const int ImmigrantBatch = 2;

		private const int PrestigeUpkeepPerLaw = 4;

		private const int PrestigeResumeLevel = 40;

		private const int PrestigeCap = 300;

		private const int PrestigeSoftCap = 200;

		public const int PrestigeLawCost = 30;

		public const int MapInfluence = 1400;

		public const int MapInfluenceFloor = 250;

		private const int PrivateFoodFloor = 8;

		private static int PrivateFoodFloorAdult => Cfg.FoodCapAdult;

		private static int PrivateFoodFloorChild => Cfg.FoodCapChild;

		private const int TaxIntervalTicks = 240;

		private const int BirthIntervalTicks = 240;

		private const int MotherCooldownTicks = 480;

		private const int FatherCooldownTicks = 360;

		public static LifecycleReport Advance(VillageSaveData data, int ticks)
		{
			LifecycleReport report = default;
			if (data == null || ticks <= 0)
			{
				return report;
			}
			EnsureLifecycleArrays(data);
			for (int i = 0; i < ticks; i++)
			{
				SimulateTick(data, ref report);
			}
			if (!string.IsNullOrEmpty(report.eventText))
			{
				data.lastEventText = report.eventText;
			}
			return report;
		}

		private static bool ShouldShowBanner(VillageSaveData data, int obituariesAtTickStart, int deathsThisTick)
		{
			if (deathsThisTick >= 3)
			{
				return true;
			}
			for (int i = obituariesAtTickStart; i < data.obituaryCount; i++)
			{
				if (i < data.obituaryLawIndices.Length && data.obituaryLawIndices[i] >= 0)
				{
					return true;
				}
				if (i < data.obituaryAges.Length && data.obituaryAges[i] < 3)
				{
					return true;
				}
			}
			return false;
		}

		public static bool KillByLaw(VillageSaveData data, int index, string cause, int lawSlot)
		{
			if (data == null || index < 0 || index >= data.villagerAlive.Length || !data.villagerAlive[index])
			{
				return false;
			}
			KillVillager(data, index, cause, lawSlot);
			return true;
		}

		public static int CountAlive(VillageSaveData data)
		{
			int num = 0;
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i])
				{
					num++;
				}
			}
			return num;
		}

		private static void SimulateTick(VillageSaveData data, ref LifecycleReport report)
		{
			int tick = data.tick;
			int num = CountAlive(data);
			int num2 = 0;
			int obituaryCount = data.obituaryCount;
			LawEngine.BeginTick(data);
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i])
				{
					bool flag = AdvanceAge(data, i);
					if (data.villagerHunger[i] >= 99)
					{
						KillVillager(data, i, "饥饿", ObitRules.AttributeSlot(data, "饥饿"));
						report.deaths++;
						num2++;
					}
					else if (data.villagerFear[i] >= 99)
					{
						KillVillager(data, i, "惊吓", ObitRules.AttributeSlot(data, "惊吓"));
						report.deaths++;
						num2++;
					}
					else if (flag && RollOldAge(data, i, tick))
					{
						report.deaths++;
						num2++;
					}
					else
					{
						AdvanceNeeds(data, i, tick, num);
					}
				}
			}
			if (tick > 0 && tick % 56 == 0)
			{
				AdvanceGranary(data, CountAlive(data));
			}
			if (tick > 0 && tick % 120 == 0)
			{
				int num3 = ResolveInfantMortality(data, tick);
				report.deaths += num3;
				num2 += num3;
			}
			if (tick > 0 && tick % 60 == 0)
			{
				Traits.SurvivorScan(data, tick);
				VillagerProfessions.Scan(data, tick);
			}
			if (tick > 0 && tick % 240 == 0)
			{
				int num4 = ResolveBirths(data, tick, ref report);
				report.births += num4;
			}
			if (tick > 0 && tick % 2400 == 0 && Cfg.Immigration > 0 && CountAlive(data) < Cfg.ImmThreshold && data.granaryFood > Cfg.ImmGranary)
			{
				for (int j = 0; j < Cfg.Immigration; j++)
				{
					if (CountAlive(data) >= Cfg.ImmThreshold)
					{
						break;
					}
					AppendImmigrant(data);
					report.immigrants++;
				}
				report.eventText = "有流民在村口停下了。";
			}
			if (tick > 0 && tick % 2400 == 0)
			{
				data.treasuryGold = Math.Min(999999, data.treasuryGold + ((Cfg.TreasuryPopDiv > 0) ? (CountAlive(data) / Cfg.TreasuryPopDiv) : 0));
				if (Cfg.TreasuryPopDiv > 0) Cfg.TreasuryIn += CountAlive(data) / Cfg.TreasuryPopDiv;
				data.treasuryGold = Math.Max(0, data.treasuryGold - ((Cfg.TreasuryUpkeepDiv > 0) ? (CountAlive(data) / Cfg.TreasuryUpkeepDiv) : 0));
				if (Cfg.TreasuryUpkeepDiv > 0) Cfg.TreasuryOut += CountAlive(data) / Cfg.TreasuryUpkeepDiv;
			}
			string eventText = null;
			report.events += VillageEvents.Tick(data, tick, ref eventText);
			if (!string.IsNullOrEmpty(eventText))
			{
				report.eventText = eventText;
			}
			if (tick > 0 && tick % 240 == 0)
			{
				RunRules.Tick(data, tick);
			}
			Farmland.Tick(data, tick);
			LawTickResult lawTickResult = (data.prestigeSuspended ? default(LawTickResult) : LawEngine.Tick(data, tick, num2));
			report.deaths += lawTickResult.deaths;
			num2 += lawTickResult.deaths;
			if (num2 > 0)
			{
				report.showBanner = ShouldShowBanner(data, obituaryCount, num2);
			}
			if (!string.IsNullOrEmpty(lawTickResult.eventText))
			{
				report.eventText = lawTickResult.eventText;
			}
			if (tick > 0 && tick % 60 == 0)
			{
				int prestige = data.prestige;
				int num5 = 0;
				for (int k = 0; k < data.lawIds.Length; k++)
				{
					if (data.lawIds[k] > 0)
					{
						num5++;
					}
				}
				int num6 = 0;
				for (int l = 0; l < data.villagerAlive.Length; l++)
				{
					if (data.villagerAlive[l])
					{
						num6 += data.villagerFear[l];
					}
				}
				int num7 = ((num != 0) ? (num6 / num) : 0);
				int num8 = num / 4 + (12 - num7) / 2;
				if (data.prestige > 200)
				{
					num8 = num8 * (300 - data.prestige) / 100;
				}
				if (data.prestigeSuspended)
				{
					data.prestige = Math.Max(0, data.prestige + num8);
					if (data.prestige >= 40)
					{
						data.prestigeSuspended = false;
					}
				}
				else
				{
					data.prestige = data.prestige - 4 * num5 + num8;
					if (data.prestige <= 0)
					{
						data.prestige = 0;
						data.prestigeSuspended = true;
					}
				}
				if (data.prestige > 300)
				{
					data.prestige = 300;
				}
				data.lastPrestigeDelta = data.prestige - prestige;
			}
			data.tick = tick + 1;
			if (report.deaths > 0 || report.births > 0)
			{
				string eventText2;
				if (report.births > 0 && report.deaths > 0)
				{
					eventText2 = "村庄新增 " + report.births + " 人，同时有 " + report.deaths + " 人死亡。";
				}
				else
				{
					eventText2 = ((report.births > 0) ? ("村庄新增 " + report.births + " 名新生儿。") : ("有 " + report.deaths + " 人死亡，讣告已记入历史。"));
				}
				report.eventText = eventText2;
			}
		}

		private static bool AdvanceAge(VillageSaveData data, int index)
		{
			bool result = false;
			data.villagerAgeTicks[index]++;
			int num = VillageTime.TicksInYear(VillageTime.ToYear(data.tick));
			if (data.villagerAgeTicks[index] >= num)
			{
				data.villagerAgeTicks[index] -= num;
				data.villagerAges[index]++;
				result = true;
			}
			if (data.villagerBirthCooldown[index] > 0)
			{
				data.villagerBirthCooldown[index]--;
			}
			return result;
		}

		private static void AdvanceNeeds(VillageSaveData data, int index, int tick, int population)
		{
			int num = ((data.villagerAges[index] < 3) ? 16 : 32);
			if (Traits.HasInnate(data, index, 64u))
			{
				num -= 6;
			}
			if (Traits.HasInnate(data, index, 128u))
			{
				num += 4;
			}
			if (num < 1)
			{
				num = 1;
			}
			if (tick % num == 0)
			{
				data.villagerHunger[index]++;
			}
			if (tick % 16 == 0)
			{
				if (data.villagerFood[index] > 0)
				{
					int num2 = ((!Traits.HasInnate(data, index, 2048u)) ? 1 : 2);
					if (Traits.HasInnate(data, index, 1024u) && tick / 16 % 3 == 0)
					{
						num2 = 0;
					}
					data.villagerFood[index] = Math.Max(0, data.villagerFood[index] - num2);
					data.villagerHunger[index] = Math.Max(0, data.villagerHunger[index] - 5);
				}
				else
				{
					bool flag = HasLaw(data, 1) && data.villagerHunger[index] > 45;
					bool flag2 = HasLaw(data, 9) && data.villagerAges[index] < 8;
					bool flag3 = flag | flag2;
					bool flag4 = data.villagerGold[index] > 0 && data.villagerHunger[index] > 30;
					int num3 = ((flag2 && !flag && !flag4) ? 40 : 0);
					if (data.granaryFood > num3 && (flag3 | flag4))
					{
						data.granaryFood = Math.Max(0, data.granaryFood - 1);
						if (!flag3 && (!Traits.HasInnate(data, index, 4u) || Rng.Chance(data.rngSeed, tick, 5200, index, 70)))
						{
							data.villagerGold[index] = Math.Max(0, data.villagerGold[index] - 1);
						}
						data.villagerHunger[index] = Math.Max(0, data.villagerHunger[index] - 4);
					}
					else if (flag4)
					{
						data.villagerGold[index] = Math.Max(0, data.villagerGold[index] - 1);
						data.villagerHunger[index] = Math.Max(0, data.villagerHunger[index] - 4);
					}
				}
			}
			if (tick % 32 == 0)
			{
				int num4 = 0;
				if (HasLaw(data, 4) && data.villagerFear[index] > 45)
				{
					num4++;
				}
				if (HasLaw(data, 6) && data.villagerFear[index] > 40)
				{
					num4 -= 2;
				}
				if (HasLaw(data, 10))
				{
					num4--;
				}
				if (num4 == 0)
				{
					int num5 = 12;
					if (Traits.HasInnate(data, index, 1u))
					{
						num5 += 18;
					}
					if (Traits.HasInnate(data, index, 2u))
					{
						num5 -= 7;
					}
					if (Traits.HasMark(data, index, 512u))
					{
						num5 += 10;
					}
					if (num5 < 0)
					{
						num5 = 0;
					}
					if (data.villagerFear[index] > num5)
					{
						num4 = -1;
					}
				}
				if (num4 != 0)
				{
					data.villagerFear[index] = Math.Max(0, Math.Min(99, data.villagerFear[index] + num4));
				}
			}
			if (tick % 240 == 0 && HasLaw(data, 3) && data.villagerGold[index] > 60)
			{
				data.villagerGold[index] -= 12;
				data.granaryFood = Math.Min(data.granaryCapacity, data.granaryFood + 3);
				data.treasuryGold += 3;
				Cfg.TreasuryIn += 3;
			}
			if (tick % 240 == 0 && HasLaw(data, 11) && data.villagerAges[index] >= 14)
			{
				data.villagerHunger[index] = Math.Min(99, data.villagerHunger[index] + 2);
			}
		}

		private static int LaborOutput(VillageSaveData data)
		{
			bool flag = HasLaw(data, 39);
			long num = 0L;
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i])
				{
					int num2 = data.villagerAges[i];
					int num3;
					if (num2 < 10)
					{
						num3 = (flag ? 50 : 0);
					}
					else if (num2 < 16)
					{
						num3 = 50;
					}
					else
					{
						num3 = ((num2 < 60) ? 100 : 50);
					}
					int num4 = data.villagerHunger[i] / 2;
					int val = num3 * (100 - num4) / 100;
					int val2 = num3 * 10 / 100;
					num += Math.Max(val, val2);
				}
			}
			return (int)(num * 5 / 100);
		}

		private static void AdvanceGranary(VillageSaveData data, int population)
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i])
				{
					if (Traits.HasInnate(data, i, 16u))
					{
						num++;
					}
					if (Traits.HasInnate(data, i, 32u))
					{
						num2++;
					}
				}
			}
			int val = (num - num2) * 2;
			int num3 = population / 3;
			val = Math.Max(-num3, Math.Min(num3, val));
			int num4 = Cfg.Output(data, population) + val;
			int num5 = population / 3;
			if (HasLaw(data, 5))
			{
				num4 += Math.Max(1, population / 4);
			}
			if (HasLaw(data, 11))
			{
				num4 += Math.Max(1, population / 5);
			}
			if (HasLaw(data, 1))
			{
				num5 += Math.Max(1, population / 8);
			}
			int num6 = Farmland.CountFarmland(data);
			if (data.arableBaseline > 0 && num6 > 0)
			{
				long num7 = (long)(num6 - data.arableBaseline) * 1000L / data.arableBaseline;
				long num8 = 1000 + num7 * Cfg.ArableCoef / 1000;
				if (num8 < 250)
				{
					num8 = 250L;
				}
				if (num8 > 1600)
				{
					num8 = 1600L;
				}
				num4 = (int)(num4 * num8 / 1000);
			}
			data.lastProduction = num4;
			data.granaryFood += num4 - num5;
			data.granaryFood = Math.Max(0, Math.Min(data.granaryCapacity, data.granaryFood));
			int num9 = 0;
			int reserve = Cfg.Reserve(data);
			for (int j = 0; j < data.villagerAlive.Length; j++)
			{
				if (data.granaryFood <= reserve)
				{
					break;
				}
				if (data.villagerAlive[j])
				{
					int num10 = ((data.villagerAges[j] < 3) ? 10 : 20);
					while (data.villagerFood[j] < num10 && data.granaryFood > reserve)
					{
						data.villagerFood[j]++;
						data.granaryFood--;
						num9++;
					}
				}
			}
			data.lastConsumption = num5 + num9;
		}

		private static int ResolveInfantMortality(VillageSaveData data, int tick)
		{
			int num = 0;
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i] && data.villagerAges[i] < 3)
				{
					int num2 = 2;
					if (data.villagerHunger[i] > 50)
					{
						num2 += (data.villagerHunger[i] - 50) / 3;
					}
					if (data.villagerFear[i] > 55)
					{
						num2 += (data.villagerFear[i] - 55) / 4;
					}
					if (data.villagerFood[i] < 2)
					{
						num2 += 2;
					}
					if (data.granaryFood < 20)
					{
						num2 += 3;
					}
					if (HasLaw(data, 9))
					{
						num2 -= 4;
					}
					if (HasLaw(data, 12))
					{
						num2 -= 3;
					}
					if (Traits.HasInnate(data, i, 64u))
					{
						num2 += 2;
					}
					if (Traits.HasInnate(data, i, 128u))
					{
						num2 -= 2;
					}
					if (Traits.HasMark(data, i, 256u))
					{
						num2 += 2;
					}
					if (Traits.HasMark(data, i, 2097152u))
					{
						num2 -= 3;
					}
					num2 = Math.Max(1, Math.Min(30, num2));
					if (Rng.Chance(data.rngSeed, tick, 1000 + i, num2))
					{
						KillVillager(data, i, "夭折", ObitRules.AttributeSlot(data, "夭折"));
						num++;
					}
				}
			}
			return num;
		}

		public static int OldAgePercent(int age)
		{
			if (age < 60)
			{
				return 0;
			}
			int num = age - 60;
			int num2 = num * num * 100 / 3500;
			if (num2 <= 20)
			{
				return num2;
			}
			return 20;
		}

		private static bool RollOldAge(VillageSaveData data, int i, int tick)
		{
			int num = data.villagerAges[i];
			if (Traits.HasMark(data, i, 1024u))
			{
				num += 7;
			}
			int num2 = OldAgePercent(num);
			if (num2 <= 0)
			{
				return false;
			}
			if (Traits.HasInnate(data, i, 64u))
			{
				num2 = num2 * 115 / 100;
			}
			if (Traits.HasInnate(data, i, 128u))
			{
				num2 = num2 * 85 / 100;
			}
			if (HasLaw(data, 12))
			{
				num2 -= 5;
			}
			if (num2 < 1)
			{
				num2 = 1;
			}
			if (num2 > 20)
			{
				num2 = 20;
			}
			if (!Rng.Chance(data.rngSeed, tick, 5400 + i, num2))
			{
				return false;
			}
			KillVillager(data, i, "寿终", -1);
			return true;
		}

		private static int ResolveBirths(VillageSaveData data, int tick, ref LifecycleReport report)
		{
			int num = 0;
			int num2 = CountAlive(data);
			if (data.granaryFood < 30 || num2 >= 120)
			{
				return 0;
			}
			int num3 = 40;
			if (HasLaw(data, 8))
			{
				num3 += 35;
			}
			if (HasLaw(data, 10))
			{
				num3 -= 15;
			}
			if (data.granaryFood > 180 && HasLaw(data, 8))
			{
				num3 = 100;
			}
			else if (data.granaryFood > 150)
			{
				num3 += 12;
			}
			if (data.granaryFood < 70)
			{
				num3 -= 25;
			}
			if (data.birthBoomUntilTick > tick && data.birthBoomPercent > 100)
			{
				num3 = num3 * data.birthBoomPercent / 100;
			}
			num3 = Math.Max(5, Math.Min(100, num3));
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (!CanGiveBirth(data, i))
				{
					continue;
				}
				int num4 = FindPartner(data, i);
				if (num4 >= 0)
				{
					int num5 = num3;
					if (Traits.HasInnate(data, i, 256u))
					{
						num5 += 20;
					}
					if (Traits.HasMark(data, i, 256u))
					{
						num5 -= 20;
					}
					if (Traits.HasMark(data, i, 134217728u))
					{
						num5 -= 15;
					}
					num5 = Math.Max(5, Math.Min(100, num5));
					if (Rng.Chance(data.rngSeed, tick, 3000 + i, num5))
					{
						AppendChild(data, i, num4);
						num++;
						data.granaryFood = Math.Max(0, data.granaryFood - 12);
						data.villagerBirthCooldown[i] = (Traits.HasInnate(data, i, 256u) ? 240 : 480);
						data.villagerBirthCooldown[num4] = Math.Max(data.villagerBirthCooldown[num4], 360);
						report.eventText = data.villagerNames[i] + "生下了一名新生儿。";
					}
				}
			}
			return num;
		}

		private static bool CanGiveBirth(VillageSaveData data, int index)
		{
			if (Traits.HasInnate(data, index, 512u))
			{
				return false;
			}
			if (data.villagerAlive[index] && data.villagerGenders[index] == 0 && data.villagerAges[index] >= 18 && data.villagerAges[index] <= 42 && data.villagerBirthCooldown[index] <= 0 && (Cfg.BirthChildCap >= 999 || data.villagerChildren[index] < Cfg.BirthChildCap) && data.villagerHunger[index] < 60 && data.villagerFear[index] < 70)
			{
				if (data.villagerFood[index] < 2)
				{
					return data.granaryFood >= 6;
				}
				return true;
			}
			return false;
		}

		private static int FindPartner(VillageSaveData data, int mother)
		{
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i] && data.villagerGenders[i] == 1 && data.villagerAges[i] >= 18 && data.villagerAges[i] <= 55 && data.villagerHunger[i] < 75 && data.villagerFear[i] < 80)
				{
					return i;
				}
			}
			return -1;
		}

		private static int FindFreeVillagerSlot(VillageSaveData data)
		{
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (!data.villagerAlive[i])
				{
					return i;
				}
			}
			return data.villagerAlive.Length;
		}

		private static void AppendChild(VillageSaveData data, int mother, int father)
		{
			int num = FindFreeVillagerSlot(data);
			if (num >= data.villagerAlive.Length)
			{
				Array.Resize(ref data.villagerNames, num + 1);
				Array.Resize(ref data.villagerGenders, num + 1);
				Array.Resize(ref data.villagerAges, num + 1);
				Array.Resize(ref data.villagerAgeTicks, num + 1);
				Array.Resize(ref data.villagerAlive, num + 1);
				Array.Resize(ref data.villagerGold, num + 1);
				Array.Resize(ref data.villagerHunger, num + 1);
				Array.Resize(ref data.villagerFear, num + 1);
				Array.Resize(ref data.villagerFood, num + 1);
				Array.Resize(ref data.villagerBirthCooldown, num + 1);
				Array.Resize(ref data.villagerChildren, num + 1);
				Array.Resize(ref data.villagerInnate, num + 1);
				Array.Resize(ref data.villagerMarks, num + 1);
			}
			data.villagerNames[num] = Bootstrap.GetVillagerName(data.rngSeed, 24 + data.totalBirths);
			data.villagerGenders[num] = Rng.Range(data.rngSeed, data.tick, 4000 + num, 0, 2);
			data.villagerAges[num] = 0;
			data.villagerAgeTicks[num] = 0;
			data.villagerAlive[num] = true;
			data.villagerGold[num] = 0;
			data.villagerHunger[num] = 5;
			data.villagerFear[num] = 5;
			data.villagerFood[num] = 2;
			data.villagerBirthCooldown[num] = 0;
			data.villagerChildren[num] = 0;
			data.villagerInnate[num] = Traits.RollInheritedInnate(data, num, mother, father);
			data.villagerMarks[num] = 0u;
			VillagerProfessions.ResetSlot(data, num);
			if (Traits.HasMark(data, mother, 256u))
			{
				Traits.AddMark(data, num, 256u);
			}
			data.villagerChildren[mother]++;
			data.villagerChildren[father]++;
			VillagerProfessions.OnBirth(data, num, mother, father);
			data.totalBirths++;
		}

		public static void AppendImmigrant(VillageSaveData data)
		{
			int num = FindFreeVillagerSlot(data);
			if (num >= data.villagerAlive.Length)
			{
				Array.Resize(ref data.villagerNames, num + 1);
				Array.Resize(ref data.villagerGenders, num + 1);
				Array.Resize(ref data.villagerAges, num + 1);
				Array.Resize(ref data.villagerAgeTicks, num + 1);
				Array.Resize(ref data.villagerAlive, num + 1);
				Array.Resize(ref data.villagerGold, num + 1);
				Array.Resize(ref data.villagerHunger, num + 1);
				Array.Resize(ref data.villagerFear, num + 1);
				Array.Resize(ref data.villagerFood, num + 1);
				Array.Resize(ref data.villagerBirthCooldown, num + 1);
				Array.Resize(ref data.villagerChildren, num + 1);
				Array.Resize(ref data.villagerInnate, num + 1);
				Array.Resize(ref data.villagerMarks, num + 1);
			}
			data.villagerNames[num] = Bootstrap.GetVillagerName(data.rngSeed, 5000 + data.tick + num);
			data.villagerGenders[num] = Rng.Range(data.rngSeed, data.tick, 4300 + num, 0, 2);
			data.villagerAges[num] = 16 + Rng.Range(data.rngSeed, data.tick, 4400 + num, 0, 24);
			data.villagerAgeTicks[num] = 0;
			data.villagerAlive[num] = true;
			data.villagerGold[num] = 10 + Rng.Range(data.rngSeed, data.tick, 4500 + num, 0, 30);
			data.villagerHunger[num] = 10;
			data.villagerFear[num] = 10;
			data.villagerFood[num] = 4;
			data.villagerBirthCooldown[num] = 0;
			data.villagerChildren[num] = 0;
			data.villagerInnate[num] = Traits.RollInnate(data.rngSeed + num, num);
			data.villagerMarks[num] = 0u;
			VillagerProfessions.OnImmigrant(data, num);
		}

		private static void KillVillager(VillageSaveData data, int index, string cause, int lawIndex)
		{
			if (data.villagerAlive[index])
			{
				data.villagerAlive[index] = false;
				VillagerProfessions.OnDeath(data, index, cause, lawIndex);
				AppendObituary(data, index, cause, lawIndex);
				LawEngine.MarkDeath(index);
				LawEngine.RunDeathEffects(data, index);
			}
		}

		private static void AppendObituary(VillageSaveData data, int villagerIndex, string cause, int lawIndex)
		{
			int obituaryCount = data.obituaryCount;
			Array.Resize(ref data.obituaryTicks, obituaryCount + 1);
			Array.Resize(ref data.obituaryAges, obituaryCount + 1);
			Array.Resize(ref data.obituaryLawIndices, obituaryCount + 1);
			Array.Resize(ref data.obituaryVillagerIndices, obituaryCount + 1);
			Array.Resize(ref data.obituaryInfants, obituaryCount + 1);
			Array.Resize(ref data.obituaryNames, obituaryCount + 1);
			Array.Resize(ref data.obituaryCauses, obituaryCount + 1);
			Array.Resize(ref data.obituaryTexts, obituaryCount + 1);
			Array.Resize(ref data.obituaryGenders, obituaryCount + 1);
			Array.Resize(ref data.obituaryInnate, obituaryCount + 1);
			Array.Resize(ref data.obituaryMarks, obituaryCount + 1);
			Array.Resize(ref data.obituaryLawNames, obituaryCount + 1);
			Array.Resize(ref data.obituaryLawKinds, obituaryCount + 1);
			data.obituaryTicks[obituaryCount] = data.tick;
			data.obituaryAges[obituaryCount] = data.villagerAges[villagerIndex];
			if (lawIndex >= 0 && lawIndex < data.lawIds.Length)
			{
				LawAttribution.IsPunitive(data.lawIds[lawIndex]);
			}
			data.obituaryLawIndices[obituaryCount] = lawIndex;
			data.obituaryVillagerIndices[obituaryCount] = villagerIndex;
			data.obituaryInfants[obituaryCount] = data.villagerAges[villagerIndex] < 3;
			data.obituaryNames[obituaryCount] = data.villagerNames[villagerIndex];
			data.obituaryCauses[obituaryCount] = cause;
			bool flag = cause == "天灾";
			bool flag2 = (cause == "饥饿" || cause == "惊吓" || cause == "夭折") | flag;
			byte b = (byte)((cause == "律法") ? 1 : (flag2 ? ObitRules.LastKind : 0));
			string text;
			if (lawIndex >= 0 && lawIndex < data.lawNames.Length)
			{
				text = data.lawNames[lawIndex];
			}
			else
			{
				text = ((b == 2) ? ObitRules.LastName : string.Empty);
			}
			data.obituaryLawKinds[obituaryCount] = b;
			data.obituaryLawNames[obituaryCount] = text;
			data.obituaryTexts[obituaryCount] = ObitRules.Build(data, villagerIndex, cause, lawIndex, b, text);
			data.obituaryGenders[obituaryCount] = data.villagerGenders[villagerIndex];
			data.obituaryInnate[obituaryCount] = data.villagerInnate[villagerIndex];
			data.obituaryMarks[obituaryCount] = data.villagerMarks[villagerIndex];
			data.obituaryCount++;
			data.totalDeaths++;
		}

		private static string BuildObituaryText(VillageSaveData data, int villagerIndex, string cause, int lawIndex)
		{
			string text = data.villagerNames[villagerIndex];
			int num = data.villagerAges[villagerIndex];
			string text2 = ((lawIndex >= 0 && lawIndex < data.lawNames.Length && !string.IsNullOrWhiteSpace(data.lawNames[lawIndex])) ? ("他死于你写的第 " + (lawIndex + 1) + " 条法。") : "没有一条明确的法律能替他负责。");
			return text + "，" + num + " 岁。\n他死于" + VillageTime.FormatDay(data.tick) + "的" + cause + "。\n" + text2;
		}

		private static int PreferredLaw(VillageSaveData data, params int[] lawIds)
		{
			for (int i = 0; i < lawIds.Length; i++)
			{
				int num = FindLawSlot(data, lawIds[i]);
				if (num >= 0)
				{
					return num;
				}
			}
			return -1;
		}

		private static int FindLawSlot(VillageSaveData data, int lawId)
		{
			for (int i = 0; i < data.lawIds.Length; i++)
			{
				if (data.lawIds[i] == lawId)
				{
					return i;
				}
			}
			return -1;
		}

		private static bool HasLaw(VillageSaveData data, int lawId)
		{
			if (data.prestigeSuspended)
			{
				return false;
			}
			return FindLawSlot(data, lawId) >= 0;
		}

		private static void EnsureLifecycleArrays(VillageSaveData data)
		{
			int length = Math.Max(1, data.villagerAlive.Length);
			EnsureLength(ref data.villagerNames, length, "村民");
			EnsureLength(ref data.villagerGenders, length, 0);
			EnsureLength(ref data.villagerAges, length, 18);
			EnsureLength(ref data.villagerAgeTicks, length, 0);
			EnsureLength(ref data.villagerAlive, length, fallback: true);
			EnsureLength(ref data.villagerGold, length, 0);
			EnsureLength(ref data.villagerHunger, length, 0);
			EnsureLength(ref data.villagerFear, length, 0);
			EnsureLength(ref data.villagerFood, length, 0);
			EnsureLength(ref data.villagerBirthCooldown, length, 0);
			EnsureLength(ref data.villagerChildren, length, 0);
			EnsureLength(ref data.villagerInnate, length, 0u);
			EnsureLength(ref data.villagerMarks, length, 0u);
			LawEngine.EnsureRuntimeArrays(data);
		}

		private static void EnsureLength<T>(ref T[] array, int length, T fallback)
		{
			if (array == null)
			{
				array = new T[length];
				for (int i = 0; i < length; i++)
				{
					array[i] = fallback;
				}
			}
			else if (array.Length < length)
			{
				int num = array.Length;
				Array.Resize(ref array, length);
				for (int j = num; j < length; j++)
				{
					array[j] = fallback;
				}
			}
		}
	}

	public static class LawEngine
	{
		public const int MaxLawStrength = 100;

		private static bool[] _deathMarks;

		private static bool _runningDeathEffects;

		public static LawTickResult Tick(VillageSaveData data, int tick, int deathsThisTick)
		{
			LawTickResult result = default;
			if (data == null || data.lawIds == null)
			{
				return result;
			}
			EnsureRuntimeArrays(data);
			int aliveCount = VillageLifecycleService.CountAlive(data);
			EnsureDeathMarks(data);
			int num = Math.Min(data.lawIds.Length, data.lawCooldown.Length);
			for (int i = 0; i < num; i++)
			{
				int num2 = data.lawIds[i];
				if (num2 <= 0)
				{
					continue;
				}
				LawDefinition lawDefinition = LawCatalog.FindById(num2);
				if (lawDefinition == null || lawDefinition.effects == null || lawDefinition.effects.Length == 0 || HasOnDeathTrigger(lawDefinition))
				{
					continue;
				}
				if (data.lawCooldown[i] > 0)
				{
					data.lawCooldown[i]--;
					continue;
				}
				bool flag = false;
				for (int j = 0; j < data.villagerAlive.Length; j++)
				{
					if (data.villagerAlive[j] && AllCondsPass(data, j, i, lawDefinition, tick, aliveCount, deathsThisTick))
					{
						ApplyAll(data, j, i, lawDefinition, tick, !flag, ref result);
						VillagerProfessions.OnLawFired(data, j, lawDefinition.id);
						flag = true;
					}
				}
				if (flag && lawDefinition.cooldown > 0)
				{
					data.lawCooldown[i] = lawDefinition.cooldown;
				}
			}
			return result;
		}

		public static void BeginTick(VillageSaveData data)
		{
			EnsureDeathMarks(data);
			for (int i = 0; i < _deathMarks.Length; i++)
			{
				_deathMarks[i] = false;
			}
		}

		public static void MarkDeath(int index)
		{
			if (_deathMarks != null && index >= 0 && index < _deathMarks.Length)
			{
				_deathMarks[index] = true;
			}
		}

		public static void RunDeathEffects(VillageSaveData data, int deadIndex)
		{
			if (data == null || _runningDeathEffects || data.lawIds == null)
			{
				return;
			}
			_runningDeathEffects = true;
			try
			{
				int aliveCount = VillageLifecycleService.CountAlive(data);
				for (int i = 0; i < data.lawIds.Length; i++)
				{
					int num = data.lawIds[i];
					if (num > 0)
					{
						LawDefinition lawDefinition = LawCatalog.FindById(num);
						if (lawDefinition != null && lawDefinition.effects != null && lawDefinition.effects.Length != 0 && HasOnDeathTrigger(lawDefinition) && AllCondsPass(data, deadIndex, i, lawDefinition, data.tick, aliveCount, 0))
						{
							LawTickResult result = default;
							ApplyAll(data, deadIndex, i, lawDefinition, data.tick, firstTrigger: true, ref result);
						}
					}
				}
			}
			finally
			{
				_runningDeathEffects = false;
			}
		}

		private static void EnsureDeathMarks(VillageSaveData data)
		{
			int num = ((data.villagerAlive != null) ? data.villagerAlive.Length : 0);
			if (_deathMarks == null)
			{
				_deathMarks = new bool[num];
			}
			else if (_deathMarks.Length < num)
			{
				bool[] array = new bool[num];
				Array.Copy(_deathMarks, array, _deathMarks.Length);
				_deathMarks = array;
			}
		}

		private static bool NeighborDied(VillageSaveData d, int i)
		{
			if (_deathMarks == null)
			{
				return false;
			}
			int num = NeighborNext(d, i);
			int num2 = NeighborPrev(d, i);
			if (num < 0 || num >= _deathMarks.Length || !_deathMarks[num])
			{
				if (num2 >= 0 && num2 < _deathMarks.Length)
				{
					return _deathMarks[num2];
				}
				return false;
			}
			return true;
		}

		private static bool HasOnDeathTrigger(LawDefinition def)
		{
			if (def.conds == null)
			{
				return false;
			}
			for (int i = 0; i < def.conds.Length; i++)
			{
				if (def.conds[i].kind == 20)
				{
					return true;
				}
			}
			return false;
		}

		public static void EnsureRuntimeArrays(VillageSaveData data)
		{
			int num = ((data.lawIds != null) ? data.lawIds.Length : 6);
			if (data.lawNames == null || data.lawNames.Length != num)
			{
				string[] array = new string[num];
				if (data.lawNames != null)
				{
					Array.Copy(data.lawNames, array, Math.Min(num, data.lawNames.Length));
				}
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i] == null)
					{
						array[i] = string.Empty;
					}
				}
				data.lawNames = array;
			}
			if (data.lawStrength == null || data.lawStrength.Length != num)
			{
				int[] array2 = new int[num];
				if (data.lawStrength != null)
				{
					Array.Copy(data.lawStrength, array2, Math.Min(num, data.lawStrength.Length));
				}
				data.lawStrength = array2;
			}
			if (data.lawCooldown == null || data.lawCooldown.Length != num)
			{
				int[] array3 = new int[num];
				if (data.lawCooldown != null)
				{
					Array.Copy(data.lawCooldown, array3, Math.Min(num, data.lawCooldown.Length));
				}
				data.lawCooldown = array3;
			}
		}

		private static bool AllCondsPass(VillageSaveData d, int i, int slot, LawDefinition def, int tick, int aliveCount, int deathsThisTick)
		{
			if (def.conds == null || def.conds.Length == 0)
			{
				return true;
			}
			for (int j = 0; j < def.conds.Length; j++)
			{
				if (!EvalCond(d, i, slot, def.conds[j], tick, aliveCount, deathsThisTick))
				{
					return false;
				}
			}
			return true;
		}

		private static bool EvalCond(VillageSaveData d, int i, int slot, LawCond cond, int tick, int aliveCount, int deathsThisTick)
		{
			LawCondKind kind = (LawCondKind)cond.kind;
			LawField field = (LawField)cond.field;
			if (field == LawField.Fear && Cfg.FearThScale != 100) cond.a = cond.a * Cfg.FearThScale / 100;
			switch (kind)
			{
			case LawCondKind.Always:
				return true;
			case LawCondKind.SelfGt:
				return ReadField(d, i, field) > cond.a;
			case LawCondKind.SelfLt:
				return ReadField(d, i, field) < cond.a;
			case LawCondKind.SelfEq:
				return ReadField(d, i, field) == cond.a;
			case LawCondKind.NeighborGt:
			{
				int num4 = NeighborNext(d, i);
				int num5 = NeighborPrev(d, i);
				if (num4 < 0 || ReadField(d, num4, field) <= cond.a)
				{
					if (num5 >= 0)
					{
						return ReadField(d, num5, field) > cond.a;
					}
					return false;
				}
				return true;
			}
			case LawCondKind.NeighborRelative:
			{
				int num = ReadField(d, i, field);
				int num2 = NeighborNext(d, i);
				int num3 = NeighborPrev(d, i);
				if (num2 < 0 || ReadField(d, num2, field) <= num + cond.a)
				{
					if (num3 >= 0)
					{
						return ReadField(d, num3, field) > num + cond.a;
					}
					return false;
				}
				return true;
			}
			case LawCondKind.AliveGt:
				return aliveCount > cond.a;
			case LawCondKind.AliveLt:
				return aliveCount < cond.a;
			case LawCondKind.CountGt:
				return CountAbove(d, field, cond.a) > cond.b;
			case LawCondKind.CountLt:
				return CountBelow(d, field, cond.a) > cond.b;
			case LawCondKind.ResLt:
				return ReadShared(d, field) < cond.a;
			case LawCondKind.AvgGt:
				return AverageField(d, field) > cond.a;
			case LawCondKind.ResLtPopTimes:
				return ReadShared(d, field) < aliveCount * cond.a;
			case LawCondKind.ResGt:
				return ReadShared(d, field) > cond.a;
			case LawCondKind.Periodic:
				if (cond.a > 0)
				{
					return tick % cond.a == 0;
				}
				return false;
			case LawCondKind.DeathsThisTick:
				return deathsThisTick >= Math.Max(1, cond.a);
			case LawCondKind.HasLaw:
				return HasLaw(d, cond.a);
			case LawCondKind.TotalGt:
				return ReadTotal(d, field) > cond.a;
			case LawCondKind.ObituaryCountGt:
				return CountObituaries(d, cond.a != 0) > cond.b;
			case LawCondKind.OnDeath:
				return true;
			case LawCondKind.NeighborDiedThisTick:
				return NeighborDied(d, i);
			default:
				return false;
			}
		}

		private static void ApplyAll(VillageSaveData d, int i, int slot, LawDefinition def, int tick, bool firstTrigger, ref LawTickResult result)
		{
			for (int j = 0; j < def.effects.Length; j++)
			{
				ApplyEffect(d, i, slot, def.effects[j], tick, firstTrigger, ref result);
			}
		}

		private static void ApplyEffect(VillageSaveData d, int i, int slot, LawEffect e, int tick, bool firstTrigger, ref LawTickResult result)
		{
			if ((e.chance < 100 && !Rng.Chance(d.rngSeed, tick, 7000 + slot * 97 + e.kind, e.chance)) || !GuardPasses(d, i, e))
			{
				return;
			}
			LawEffectKind kind = (LawEffectKind)e.kind;
			LawScope scope = (LawScope)e.scope;
			LawField field = (LawField)e.field;
			int num = Scale(d, slot, e.amount);
			switch (kind)
			{
			case LawEffectKind.Add:
				if (firstTrigger || !IsSharedScope(scope))
				{
					int num3 = ((d.lawIds != null && slot >= 0 && slot < d.lawIds.Length) ? d.lawIds[slot] : 0);
					AddToScope(d, i, scope, field, num, num3 != 4);
				}
				break;
			case LawEffectKind.SetField:
				SetInScope(d, i, scope, field, num);
				break;
			case LawEffectKind.Move:
				MoveToSelf(d, i, scope, field, (Math.Abs(num) == 0) ? 1 : Math.Abs(num));
				break;
			case LawEffectKind.Drain:
				DrainFromSelf(d, i, scope, field, (Math.Abs(num) == 0) ? 1 : Math.Abs(num));
				break;
			case LawEffectKind.Steal:
				StealFromNeighbors(d, i, field, num);
				break;
			case LawEffectKind.FillTo:
				FillTo(d, i, scope, field, Math.Abs(num), e.extra);
				break;
			case LawEffectKind.ModifyBirthCd:
				ModifyBirthCooldown(d, i, scope, (num == 0) ? 1 : num);
				break;
			case LawEffectKind.BroadcastFrom:
				Broadcast(d, i, field, num);
				break;
			case LawEffectKind.KillSelf:
				if (VillageLifecycleService.KillByLaw(d, i, "律法", slot))
				{
					result.deaths++;
				}
				break;
			case LawEffectKind.KillMaxOf:
			{
				int num2 = FindMaxAlive(d, field);
				if (VillageLifecycleService.KillByLaw(d, num2, "律法", slot))
				{
					result.deaths++;
					result.eventText = d.villagerNames[num2] + " 被律法处决。";
				}
				break;
			}
			case LawEffectKind.KillYoungest:
			{
				int index = FindYoungestAlive(d);
				if (VillageLifecycleService.KillByLaw(d, index, "律法", slot))
				{
					result.deaths++;
				}
				break;
			}
			case LawEffectKind.ModifyLawStrength:
				ModifyStrengthByLawId(d, (int)field, e.amount);
				break;
			case LawEffectKind.DisableLaw:
				ModifyStrengthByLawId(d, (int)field, -100);
				break;
			}
		}

		private static void AddToScope(VillageSaveData d, int i, LawScope scope, LawField field, int amount, bool scaleFear)
		{
			switch (scope)
			{
			case LawScope.Granary:
				d.granaryFood = ClampGranary(d, d.granaryFood + amount);
				return;
			case LawScope.Treasury:
				d.treasuryGold = Math.Max(0, d.treasuryGold + amount);
				Cfg.Flow(amount);
				return;
			case LawScope.Prestige:
				d.prestige = ClampPrestige(d.prestige + amount);
				return;
			}
			ForEachTarget(d, i, scope, (int target) =>
			{
				if (scaleFear && field == LawField.Fear && amount > 0)
				{
					Traits.AddFear(d, target, amount);
				}
				else
				{
					WriteField(d, target, field, ReadField(d, target, field) + amount);
				}
			});
		}

		private static void SetInScope(VillageSaveData d, int i, LawScope scope, LawField field, int amount)
		{
			switch (scope)
			{
			case LawScope.Granary:
				d.granaryFood = ClampGranary(d, amount);
				return;
			case LawScope.Treasury:
				d.treasuryGold = Math.Max(0, amount);
				return;
			case LawScope.Prestige:
				d.prestige = ClampPrestige(amount);
				return;
			}
			ForEachTarget(d, i, scope, (int target) =>
			{
				WriteField(d, target, field, amount);
			});
		}

		private static void MoveToSelf(VillageSaveData d, int i, LawScope source, LawField field, int amount)
		{
			int num = Math.Min(source switch
			{
				LawScope.Granary => d.granaryFood, 
				LawScope.Treasury => d.treasuryGold, 
				_ => NeighborMaxField(d, i, field), 
			}, amount);
			if (num > 0)
			{
				switch (source)
				{
				case LawScope.Granary:
					d.granaryFood -= num;
					break;
				case LawScope.Treasury:
					d.treasuryGold -= num;
					break;
				default:
					TakeFromNeighbors(d, i, field, num);
					break;
				}
				WriteField(d, i, field, ReadField(d, i, field) + num);
			}
		}

		private static void DrainFromSelf(VillageSaveData d, int i, LawScope target, LawField field, int amount)
		{
			int num = ReadField(d, i, field);
			int num2 = Math.Min(num, amount);
			if (num2 <= 0)
			{
				return;
			}
			WriteField(d, i, field, num - num2);
			switch (target)
			{
			case LawScope.Granary:
				d.granaryFood = ClampGranary(d, d.granaryFood + num2);
				break;
			case LawScope.Treasury:
				d.treasuryGold += num2;
				break;
			case LawScope.Neighbors:
			{
				int num3 = NeighborNext(d, i);
				if (num3 >= 0)
				{
					WriteField(d, num3, field, ReadField(d, num3, field) + num2);
				}
				break;
			}
			case LawScope.All:
				break;
			}
		}

		private static void StealFromNeighbors(VillageSaveData d, int i, LawField field, int amount)
		{
			int num = Math.Abs(amount);
			int index = NeighborNext(d, i);
			int index2 = NeighborPrev(d, i);
			int num2 = TakeFrom(d, index, field, num);
			num2 += TakeFrom(d, index2, field, num - num2);
			if (num2 > 0)
			{
				WriteField(d, i, field, ReadField(d, i, field) + num2);
			}
		}

		private static void FillTo(VillageSaveData d, int i, LawScope scope, LawField field, int amount, int cap)
		{
			if (scope == LawScope.Granary || scope == LawScope.Treasury || scope == LawScope.Prestige)
			{
				int num = ReadShared(d, field);
				int val = ((cap > 0) ? cap : amount);
				SetInScope(d, i, scope, field, Math.Min(val, num + amount));
				return;
			}
			ForEachTarget(d, i, scope, (int target) =>
			{
				int num2 = ReadField(d, target, field);
				int val2 = ((cap > 0) ? cap : (num2 + amount));
				WriteField(d, target, field, Math.Min(val2, num2 + amount));
			});
		}

		private static void ModifyBirthCooldown(VillageSaveData d, int i, LawScope scope, int amount)
		{
			ForEachTarget(d, i, scope, (int target) =>
			{
				d.villagerBirthCooldown[target] = Math.Max(0, d.villagerBirthCooldown[target] + amount);
			});
		}

		private static void Broadcast(VillageSaveData d, int i, LawField field, int amount)
		{
			ForEachTarget(d, i, LawScope.All, (int target) =>
			{
				WriteField(d, target, field, ReadField(d, target, field) + amount);
			});
		}

		private static void ModifyStrengthByLawId(VillageSaveData d, int lawId, int amount)
		{
			if (d.lawIds == null)
			{
				return;
			}
			for (int i = 0; i < d.lawIds.Length; i++)
			{
				if (d.lawIds[i] == lawId)
				{
					ModifyStrength(d, i, amount);
					break;
				}
			}
		}

		private static int CountBelow(VillageSaveData d, LawField field, int threshold)
		{
			int num = 0;
			for (int i = 0; i < d.villagerAlive.Length; i++)
			{
				if (d.villagerAlive[i] && ReadField(d, i, field) < threshold)
				{
					num++;
				}
			}
			return num;
		}

		private static void ModifyStrength(VillageSaveData d, int slot, int amount)
		{
			if (d.lawStrength != null && slot >= 0 && slot < d.lawStrength.Length)
			{
				d.lawStrength[slot] = Math.Max(-100, Math.Min(100, d.lawStrength[slot] + amount));
			}
		}

		private static int Scale(VillageSaveData d, int slot, int amount)
		{
			if (amount == 0)
			{
				return 0;
			}
			int val = ((d.lawStrength != null && slot >= 0 && slot < d.lawStrength.Length) ? d.lawStrength[slot] : 0);
			int num = 100 + Math.Max(-100, Math.Min(100, val));
			return amount * num / 100;
		}

		private static bool GuardPasses(VillageSaveData d, int i, LawEffect e)
		{
			if (e.guardField == byte.MaxValue)
			{
				return true;
			}
			int num = ReadField(d, i, (LawField)e.guardField);
			if (e.guardOp != 0)
			{
				return num < e.guardValue;
			}
			return num >= e.guardValue;
		}

		private static bool IsSharedScope(LawScope scope)
		{
			if (scope != LawScope.Granary && scope != LawScope.Treasury)
			{
				return scope == LawScope.Prestige;
			}
			return true;
		}

		private static void ForEachTarget(VillageSaveData d, int i, LawScope scope, Action<int> action)
		{
			switch (scope)
			{
			case LawScope.Self:
				action(i);
				break;
			case LawScope.Neighbors:
			{
				int num = NeighborNext(d, i);
				int num2 = NeighborPrev(d, i);
				if (num >= 0 && num != i)
				{
					action(num);
				}
				if (num2 >= 0 && num2 != i && num2 != num)
				{
					action(num2);
				}
				break;
			}
			case LawScope.All:
			{
				for (int j = 0; j < d.villagerAlive.Length; j++)
				{
					if (d.villagerAlive[j])
					{
						action(j);
					}
				}
				break;
			}
			}
		}

		private static int TakeFrom(VillageSaveData d, int index, LawField field, int amount)
		{
			if (index < 0 || amount <= 0)
			{
				return 0;
			}
			int num = ReadField(d, index, field);
			int num2 = Math.Min(num, amount);
			if (num2 > 0)
			{
				WriteField(d, index, field, num - num2);
			}
			return num2;
		}

		private static void TakeFromNeighbors(VillageSaveData d, int i, LawField field, int amount)
		{
			int index = NeighborNext(d, i);
			int num = TakeFrom(d, index, field, amount);
			TakeFrom(d, NeighborPrev(d, i), field, amount - num);
		}

		private static int NeighborMaxField(VillageSaveData d, int i, LawField field)
		{
			int num = NeighborNext(d, i);
			int num2 = NeighborPrev(d, i);
			int num3 = 0;
			if (num >= 0)
			{
				num3 = Math.Max(num3, ReadField(d, num, field));
			}
			if (num2 >= 0)
			{
				num3 = Math.Max(num3, ReadField(d, num2, field));
			}
			return num3;
		}

		private static int NeighborNext(VillageSaveData d, int i)
		{
			int num = d.villagerAlive.Length;
			if (num <= 0)
			{
				return -1;
			}
			int num2 = (i + 1) % num;
			if (num2 != i)
			{
				return num2;
			}
			return -1;
		}

		private static int NeighborPrev(VillageSaveData d, int i)
		{
			int num = d.villagerAlive.Length;
			if (num <= 0)
			{
				return -1;
			}
			int num2 = (i - 1 + num) % num;
			if (num2 != i)
			{
				return num2;
			}
			return -1;
		}

		private static int FindMaxAlive(VillageSaveData d, LawField field)
		{
			int result = -1;
			int num = int.MinValue;
			for (int i = 0; i < d.villagerAlive.Length; i++)
			{
				if (d.villagerAlive[i])
				{
					int num2 = ReadField(d, i, field);
					if (num2 > num)
					{
						num = num2;
						result = i;
					}
				}
			}
			return result;
		}

		private static int FindYoungestAlive(VillageSaveData d)
		{
			int result = -1;
			int num = int.MaxValue;
			for (int i = 0; i < d.villagerAlive.Length; i++)
			{
				if (d.villagerAlive[i] && d.villagerAges[i] < num)
				{
					num = d.villagerAges[i];
					result = i;
				}
			}
			return result;
		}

		private static int CountAbove(VillageSaveData d, LawField field, int threshold)
		{
			int num = 0;
			for (int i = 0; i < d.villagerAlive.Length; i++)
			{
				if (d.villagerAlive[i] && ReadField(d, i, field) > threshold)
				{
					num++;
				}
			}
			return num;
		}

		private static int AverageField(VillageSaveData d, LawField field)
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < d.villagerAlive.Length; i++)
			{
				if (d.villagerAlive[i])
				{
					num2 += ReadField(d, i, field);
					num++;
				}
			}
			if (num != 0)
			{
				return num2 / num;
			}
			return 0;
		}

		private static int CountObituaries(VillageSaveData d, bool infantOnly)
		{
			int num = 0;
			for (int i = 0; i < d.obituaryCount && i < d.obituaryInfants.Length; i++)
			{
				if (!infantOnly || d.obituaryInfants[i])
				{
					num++;
				}
			}
			return num;
		}

		private static int ReadShared(VillageSaveData d, LawField field)
		{
			return field switch
			{
				LawField.Granary => d.granaryFood, 
				LawField.Treasury => d.treasuryGold, 
				LawField.Prestige => d.prestige, 
				_ => 0, 
			};
		}

		private static int ReadTotal(VillageSaveData d, LawField field)
		{
			return field switch
			{
				LawField.TotalDeaths => d.totalDeaths, 
				LawField.TotalBirths => d.totalBirths, 
				LawField.InfantDeaths => CountObituaries(d, infantOnly: true), 
				LawField.ObituaryCount => d.obituaryCount, 
				_ => ReadShared(d, field), 
			};
		}

		private static bool HasLaw(VillageSaveData d, int lawId)
		{
			for (int i = 0; i < d.lawIds.Length; i++)
			{
				if (d.lawIds[i] == lawId)
				{
					return true;
				}
			}
			return false;
		}

		private static int ReadField(VillageSaveData d, int i, LawField field)
		{
			if (i < 0 || i >= d.villagerAlive.Length)
			{
				return ReadShared(d, field);
			}
			switch (field)
			{
			case LawField.Hunger:
				return d.villagerHunger[i];
			case LawField.Fear:
				return d.villagerFear[i];
			case LawField.Gold:
				return d.villagerGold[i];
			case LawField.Food:
				return d.villagerFood[i];
			case LawField.Children:
				return d.villagerChildren[i];
			case LawField.Age:
				return d.villagerAges[i];
			case LawField.Gender:
				return d.villagerGenders[i];
			case LawField.BirthCooldown:
				return d.villagerBirthCooldown[i];
			case LawField.Alive:
				if (!d.villagerAlive[i])
				{
					return 0;
				}
				return 1;
			default:
				return ReadTotal(d, field);
			}
		}

		private static void WriteField(VillageSaveData d, int i, LawField field, int value)
		{
			if (i >= 0 && i < d.villagerAlive.Length)
			{
				switch (field)
				{
				case LawField.Hunger:
					d.villagerHunger[i] = ClampField(LawField.Hunger, value);
					break;
				case LawField.Fear:
					d.villagerFear[i] = ClampField(LawField.Fear, value);
					break;
				case LawField.Gold:
					d.villagerGold[i] = ClampField(LawField.Gold, value);
					break;
				case LawField.Food:
					d.villagerFood[i] = ClampField(LawField.Food, value);
					break;
				case LawField.Children:
					d.villagerChildren[i] = Math.Max(0, value);
					break;
				case LawField.Age:
					d.villagerAges[i] = Math.Max(0, value);
					break;
				case LawField.BirthCooldown:
					d.villagerBirthCooldown[i] = Math.Max(0, value);
					break;
				case LawField.Gender:
				case LawField.Granary:
				case LawField.Treasury:
				case LawField.Prestige:
				case LawField.Alive:
					break;
				}
			}
		}

		private static int ClampField(LawField field, int value)
		{
			return Math.Max(0, Math.Min(field switch
			{
				LawField.Hunger => 99, 
				LawField.Fear => 99, 
				LawField.Gold => 99, 
				LawField.Food => 40, 
				_ => 999999, 
			}, value));
		}

		private static int ClampGranary(VillageSaveData d, int value)
		{
			return Math.Max(0, Math.Min(d.granaryCapacity, value));
		}

		private static int ClampPrestige(int value)
		{
			return Math.Max(0, Math.Min(300, value));
		}
	}

	public static class Condition
	{
		public static bool IsStarving(VillageSaveData d, int i)
		{
			return Hunger(d, i) > 70;
		}

		public static bool IsTerrified(VillageSaveData d, int i)
		{
			return Fear(d, i) > 70;
		}

		public static bool IsDestitute(VillageSaveData d, int i)
		{
			return Gold(d, i) < 10;
		}

		public static bool IsWealthy(VillageSaveData d, int i)
		{
			return Gold(d, i) > 60;
		}

		public static bool HasNoStores(VillageSaveData d, int i)
		{
			return Food(d, i) == 0;
		}

		public static bool IsHoarding(VillageSaveData d, int i)
		{
			return Food(d, i) >= 35;
		}

		public static bool IsInfant(VillageSaveData d, int i)
		{
			return Age(d, i) < 3;
		}

		public static bool IsChild(VillageSaveData d, int i)
		{
			if (Age(d, i) >= 3)
			{
				return Age(d, i) < 14;
			}
			return false;
		}

		public static bool IsAdult(VillageSaveData d, int i)
		{
			if (Age(d, i) >= 14)
			{
				return Age(d, i) < 50;
			}
			return false;
		}

		public static bool IsElder(VillageSaveData d, int i)
		{
			return Age(d, i) >= 65;
		}

		public static bool IsFertile(VillageSaveData d, int i)
		{
			if (Gender(d, i) == 0 && Age(d, i) >= 18)
			{
				return Age(d, i) <= 42;
			}
			return false;
		}

		public static bool IsChildless(VillageSaveData d, int i)
		{
			return Children(d, i) == 0;
		}

		public static bool HasManyChildren(VillageSaveData d, int i)
		{
			return Children(d, i) >= 3;
		}

		public static bool IsDying(VillageSaveData d, int i)
		{
			if (Hunger(d, i) < 95)
			{
				return Fear(d, i) >= 95;
			}
			return true;
		}

		private static bool Valid(VillageSaveData d, int i)
		{
			if (d != null && i >= 0)
			{
				return i < d.villagerAlive.Length;
			}
			return false;
		}

		private static int Hunger(VillageSaveData d, int i)
		{
			if (!Valid(d, i))
			{
				return 0;
			}
			return d.villagerHunger[i];
		}

		private static int Fear(VillageSaveData d, int i)
		{
			if (!Valid(d, i))
			{
				return 0;
			}
			return d.villagerFear[i];
		}

		private static int Gold(VillageSaveData d, int i)
		{
			if (!Valid(d, i))
			{
				return 0;
			}
			return d.villagerGold[i];
		}

		private static int Food(VillageSaveData d, int i)
		{
			if (!Valid(d, i))
			{
				return 0;
			}
			return d.villagerFood[i];
		}

		private static int Age(VillageSaveData d, int i)
		{
			if (!Valid(d, i))
			{
				return 0;
			}
			return d.villagerAges[i];
		}

		private static int Gender(VillageSaveData d, int i)
		{
			if (!Valid(d, i))
			{
				return 0;
			}
			return d.villagerGenders[i];
		}

		private static int Children(VillageSaveData d, int i)
		{
			if (!Valid(d, i))
			{
				return 0;
			}
			return d.villagerChildren[i];
		}
	}

	public static class LawCatalog
	{
		public static readonly int[] ScriptedLawIds = new int[11]
		{
			1, 3, 4, 5, 6, 8, 9, 10, 11, 12,
			71
		};

		private static readonly LawDefinition[] Definitions = new LawDefinition[71]
		{
			Law(1, "济贫", "饥饿 > 45", "从公共粮仓取粮", "救急，但会让所有人都不再省着吃。", 0),
			Law(2, "饥则抢", "饥饿 > 55", "抢邻居 12 金币", "制造伤疤，也破坏邻里关系。", 2, new LawCond[1] { C(LawCondKind.SelfGt, LawField.Hunger, 55) }, new LawEffect[2]
			{
				E(LawEffectKind.Steal, LawScope.Self, LawField.Gold, 12),
				E(LawEffectKind.Add, LawScope.Self, LawField.Fear, 3)
			}),
			Law(3, "税收", "金币 > 60", "交纳 12 金币", "税款进入执政税库，可用于购买稀有法律。", 1),
			Law(4, "恐惧蔓延", "恐惧 > 45", "恐惧继续增加", "正反馈会从一个人扩散到整片村庄。", 3),
			Law(5, "耕作", "所有人", "公共粮仓产出提高", "提高承载力，但普惠法最容易疲劳。", 0),
			Law(6, "安抚", "恐惧 > 40", "减少恐惧", "目前最稳定的恐惧压制法律。", 3),
			Law(7, "私刑倾向", "恐惧 > 60", "打上罪人标签", "恐惧高的人会先指认邻居。", 2, new LawCond[1] { C(LawCondKind.SelfGt, LawField.Fear, 42) }, new LawEffect[2]
			{
				E(LawEffectKind.Add, LawScope.Self, LawField.Fear, 2),
				E(LawEffectKind.Add, LawScope.Neighbors, LawField.Fear, 2)
			}),
			Law(8, "婚育补贴", "已婚且饥饿 < 55", "提高生育意愿", "稳定人口，但长期增加粮食压力。", 4),
			Law(9, "幼童口粮", "年龄 < 8", "优先获得存粮", "显著降低幼儿夭折概率。", 4),
			Law(10, "宵禁", "所有人", "夜间恐惧下降、社交减少", "更安全，但出生率也会下降。", 3),
			Law(11, "劳役", "成年人", "粮仓产出增加，饥饿上升", "用身体换粮，病弱者风险更高。", 1),
			Law(12, "医药", "病弱或年龄 < 5", "降低夭折和病死概率", "昂贵，但能托住人口底线。", 4),
			Law(13, "公共卫生", "每 240 tick", "降低疾病与夭折", "清洁水源与集中处置尸体。", 4, new LawCond[1] { C(LawCondKind.Periodic, LawField.Hunger, 240) }, new LawEffect[2]
			{
				E(LawEffectKind.Add, LawScope.All, LawField.Hunger, -2),
				E(LawEffectKind.Add, LawScope.All, LawField.Fear, -1)
			}),
			Law(14, "盐铁专卖", "每 240 tick", "税库收入提高", "稳定财政，但会压低村民收入。", 1, new LawCond[1] { C(LawCondKind.Periodic, LawField.Treasury, 240) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.Treasury, LawField.Treasury, 6) }),
			Law(15, "兵役征召", "成年男性", "提高秩序，降低出生", "年轻人被编入守卫队。", 2, new LawCond[3]
			{
				C(LawCondKind.Periodic, LawField.Gender, 120),
				C(LawCondKind.SelfEq, LawField.Gender, 1),
				C(LawCondKind.SelfGt, LawField.Age, 17)
			}, new LawEffect[3]
			{
				E(LawEffectKind.Add, LawScope.Treasury, LawField.Treasury, 1),
				E(LawEffectKind.Add, LawScope.Self, LawField.Fear, -1),
				E(LawEffectKind.ModifyBirthCd, LawScope.Self, LawField.BirthCooldown, 20)
			}),
			Law(16, "连坐", "邻居本 tick 死亡", "自己恐惧增加", "一人犯罪，邻居共同受罚。", 2, new LawCond[1] { C(LawCondKind.NeighborDiedThisTick, LawField.Alive, 1) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.Self, LawField.Fear, 6) }),
			HiddenLaw(17, "流民法", "人口尚可接纳", "允许外来者加入", "死亡积累到一定规模后出现。", 1, 1, 5, "累计死亡 5 人后解锁", new LawCond[2]
			{
				C(LawCondKind.AliveLt, LawField.Hunger, 60),
				C(LawCondKind.Periodic, LawField.Hunger, 240)
			}, new LawEffect[1] { E(LawEffectKind.Add, LawScope.All, LawField.Hunger, -4) }),
			HiddenLaw(18, "起义豁免", "恐惧极高", "释放罪人并降低恐惧", "连续死亡后出现的临时妥协。", 3, 1, 8, "累计死亡 8 人后解锁", new LawCond[1] { C(LawCondKind.CountGt, LawField.Fear, 49, 3) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.All, LawField.Fear, -12) }, 240),
			HiddenLaw(19, "安葬礼法", "有幼儿夭折", "降低全村恐惧", "村庄开始认真面对死亡。", 4, 6, 3, "幼儿夭折 3 人后解锁", new LawCond[1] { C(LawCondKind.Periodic, LawField.Fear, 240) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.All, LawField.Fear, -3) }),
			HiddenLaw(20, "丰年祭", "公共粮仓充足", "提高生育和士气", "只有粮仓充盈时才会出现。", 0, 4, 400, "公共粮仓达到 400 后解锁", new LawCond[2]
			{
				C(LawCondKind.ResGt, LawField.Granary, 200),
				C(LawCondKind.Periodic, LawField.Hunger, 240)
			}, new LawEffect[1] { E(LawEffectKind.Add, LawScope.All, LawField.Hunger, -6) }),
			HiddenLaw(21, "长老议事", "人口达到 30", "降低法律疲劳", "人口形成规模后，长老会介入。", 1, 3, 30, "存活人口达到 30 后解锁", new LawCond[2]
			{
				C(LawCondKind.AliveGt, LawField.Age, 29),
				C(LawCondKind.Periodic, LawField.BirthCooldown, 240)
			}, new LawEffect[1] { E(LawEffectKind.ModifyBirthCd, LawScope.All, LawField.BirthCooldown, -20) }),
			HiddenLaw(22, "瘟疫隔离", "多人恐惧 > 80", "隔离高危村民", "恐惧扩散后出现的紧急法律。", 3, 7, 3, "3 名村民恐惧超过 80 后解锁", new LawCond[1] { C(LawCondKind.CountGt, LawField.Fear, 56, 2) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.All, LawField.Fear, -10) }, 240),
			HiddenLaw(23, "互助会", "多人贫穷", "富人向穷人转移粮食", "贫困人口形成规模后出现。", 0, 8, 8, "8 名村民金币低于 10 后解锁", new LawCond[1] { C(LawCondKind.CountLt, LawField.Gold, 10, 7) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.All, LawField.Gold, 2) }),
			RareLaw(24, "王权税契", "税库金币 >= 60", "威信恢复加快", "用税库金币购买的王权法律。", 1, 60, "税库达到 60 金币后可购买", new LawCond[1] { C(LawCondKind.Periodic, LawField.Prestige, 120) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.Prestige, LawField.Prestige, 2) }),
			RareLaw(25, "雇佣护卫", "税库金币 >= 40", "降低私刑与抢劫", "用税库金币维持一支护卫队。", 2, 40, "税库达到 40 金币后可购买", new LawCond[1] { C(LawCondKind.Periodic, LawField.Fear, 120) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.All, LawField.Fear, -4) }),
			Law(26, "买粮", "金币 > 30，饥饿 > 30", "用金币从税库买粮", "把私财换成口粮，税库同时得到进项。", 0, new LawCond[2]
			{
				C(LawCondKind.SelfGt, LawField.Gold, 30),
				C(LawCondKind.SelfGt, LawField.Hunger, 30)
			}, new LawEffect[4]
			{
				E(LawEffectKind.Add, LawScope.Self, LawField.Gold, -30),
				E(LawEffectKind.Add, LawScope.Treasury, LawField.Treasury, 30),
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, -10),
				E(LawEffectKind.FillTo, LawScope.Self, LawField.Food, 10, 40)
			}),
			Law(27, "丁税", "子女 > 2，金币 > 0", "按子女数缴税", "人丁越多，负担越重。", 1, new LawCond[2]
			{
				C(LawCondKind.SelfGt, LawField.Children, 2),
				C(LawCondKind.SelfGt, LawField.Gold, 0)
			}, new LawEffect[2]
			{
				E(LawEffectKind.Add, LawScope.Self, LawField.Gold, -8),
				E(LawEffectKind.Add, LawScope.Treasury, LawField.Treasury, 4)
			}),
			Law(28, "捐纳", "金币 > 50", "捐钱换威信", "花钱买名声，税库也受益。", 1, new LawCond[1] { C(LawCondKind.SelfGt, LawField.Gold, 50) }, new LawEffect[3]
			{
				E(LawEffectKind.Add, LawScope.Self, LawField.Gold, -50),
				E(LawEffectKind.Add, LawScope.Treasury, LawField.Treasury, 20),
				E(LawEffectKind.Add, LawScope.Prestige, LawField.Prestige, 3)
			}),
			Law(29, "平粜", "粮仓紧张，税库充足", "税库换粮入仓", "荒年用财政撑住粮价。", 1, new LawCond[2]
			{
				C(LawCondKind.ResLtPopTimes, LawField.Granary, 2),
				C(LawCondKind.ResGt, LawField.Treasury, 40)
			}, new LawEffect[2]
			{
				E(LawEffectKind.Add, LawScope.Treasury, LawField.Treasury, -40),
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, 25)
			}),
			Law(30, "屯粮", "有子女，粮仓充裕", "把公粮搬进自家", "给自己留后路，公仓少一点。", 0, new LawCond[2]
			{
				C(LawCondKind.SelfGt, LawField.Children, 2),
				C(LawCondKind.ResGt, LawField.Granary, 60)
			}, new LawEffect[2]
			{
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, -2),
				E(LawEffectKind.FillTo, LawScope.Self, LawField.Food, 2, 40)
			}),
			Law(31, "售粮", "金币 > 50，粮仓 > 120", "私下卖粮换钱", "绕过公仓的灰色交易。", 1, new LawCond[2]
			{
				C(LawCondKind.SelfGt, LawField.Gold, 50),
				C(LawCondKind.ResGt, LawField.Granary, 120)
			}, new LawEffect[4]
			{
				E(LawEffectKind.Add, LawScope.Self, LawField.Gold, -4),
				E(LawEffectKind.Add, LawScope.Treasury, LawField.Treasury, 4),
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, -6),
				E(LawEffectKind.FillTo, LawScope.Self, LawField.Food, 6, 40)
			}),
			Law(32, "觊觎", "饥饿 > 70", "偷邻居的存粮", "饿到极处，邻里就是粮袋。", 2, new LawCond[1] { C(LawCondKind.SelfGt, LawField.Hunger, 70) }, new LawEffect[2]
			{
				E(LawEffectKind.Steal, LawScope.Self, LawField.Food, 6),
				E(LawEffectKind.Add, LawScope.Neighbors, LawField.Fear, 4)
			}),
			Law(33, "征粮", "存粮 > 15", "余粮上交公共粮仓", "超出的部分归全村。", 0, new LawCond[1] { C(LawCondKind.SelfGt, LawField.Food, 15) }, new LawEffect[1] { E(LawEffectKind.Drain, LawScope.Granary, LawField.Food, 5) }),
			Law(34, "诛心", "3 人恐惧 > 65", "让恐惧者更恐惧", "恐惧本身成为刑罚。", 3, new LawCond[1] { C(LawCondKind.CountGt, LawField.Fear, 45, 2) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.All, LawField.Fear, 3, 0, 100, 1, 0, 25) }, 30),
			Law(35, "祭祀", "粮仓 > 5，3 人恐惧 > 50", "耗粮安抚人心", "用粮食换取一夜安宁。", 3, new LawCond[2]
			{
				C(LawCondKind.ResGt, LawField.Granary, 5),
				C(LawCondKind.CountGt, LawField.Fear, 35, 2)
			}, new LawEffect[2]
			{
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, -5),
				E(LawEffectKind.Add, LawScope.All, LawField.Fear, -8, 0, 100, 1, 0, 50)
			}, 60),
			Law(36, "逐客", "有人恐惧 > 85", "处决最恐惧者", "把最不安的人赶出村子。", 3, new LawCond[1] { C(LawCondKind.CountGt, LawField.Fear, 59, 1) }, new LawEffect[2]
			{
				E(LawEffectKind.KillMaxOf, LawScope.Self, LawField.Fear, 0),
				E(LawEffectKind.Add, LawScope.All, LawField.Fear, -6)
			}, 120),
			Law(37, "立威", "威信 < 12，3 人恐惧 > 60", "以饥荒换取威信", "让所有人记住谁在执政。", 3, new LawCond[2]
			{
				C(LawCondKind.SelfLt, LawField.Prestige, 12),
				C(LawCondKind.CountGt, LawField.Fear, 42, 2)
			}, new LawEffect[3]
			{
				E(LawEffectKind.Add, LawScope.All, LawField.Hunger, 4),
				E(LawEffectKind.Add, LawScope.Prestige, LawField.Prestige, 3),
				E(LawEffectKind.Add, LawScope.All, LawField.Fear, -5, 0, 100, 1, 0, 60)
			}),
			Law(38, "威望", "威信 > 12", "用威信压住恐惧", "名声就是最好的镇静剂。", 3, new LawCond[1] { C(LawCondKind.SelfGt, LawField.Prestige, 12) }, new LawEffect[2]
			{
				E(LawEffectKind.Add, LawScope.All, LawField.Fear, -3, 0, 100, 1, 0, 40),
				E(LawEffectKind.Add, LawScope.Prestige, LawField.Prestige, -2)
			}, 60),
			Law(39, "童工", "年龄 < 14", "孩子也下地", "多一双小手，多一口加餐。", 4, new LawCond[1] { C(LawCondKind.SelfLt, LawField.Age, 14) }, new LawEffect[3]
			{
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, 1),
				E(LawEffectKind.Add, LawScope.Self, LawField.Food, -1),
				E(LawEffectKind.Add, LawScope.Self, LawField.Hunger, 1)
			}),
			Law(40, "弃老", "年龄 > 60", "老人交出全部存粮", "把最后的口粮留给年轻人。", 4, new LawCond[1] { C(LawCondKind.SelfGt, LawField.Age, 60) }, new LawEffect[1] { E(LawEffectKind.Drain, LawScope.Granary, LawField.Food, 99) }),
			Law(41, "丁役", "成年男性", "服役换秩序", "以劳役和金钱换取秩序。", 2, new LawCond[3]
			{
				C(LawCondKind.SelfEq, LawField.Gender, 1),
				C(LawCondKind.SelfGt, LawField.Age, 17),
				C(LawCondKind.SelfLt, LawField.Age, 46)
			}, new LawEffect[4]
			{
				E(LawEffectKind.Add, LawScope.Self, LawField.Fear, -2),
				E(LawEffectKind.Add, LawScope.Self, LawField.Gold, -1),
				E(LawEffectKind.Add, LawScope.Treasury, LawField.Treasury, 1),
				E(LawEffectKind.Add, LawScope.Self, LawField.Hunger, 1)
			}),
			Law(42, "老权", "威信 > 40，年龄 > 50", "老者优先取粮", "资历换口粮，威信换服从。", 4, new LawCond[2]
			{
				C(LawCondKind.SelfGt, LawField.Prestige, 40),
				C(LawCondKind.SelfGt, LawField.Age, 50)
			}, new LawEffect[3]
			{
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, -2),
				E(LawEffectKind.FillTo, LawScope.Self, LawField.Food, 2, 40),
				E(LawEffectKind.Add, LawScope.Self, LawField.Fear, -1)
			}),
			Law(43, "晚婚", "年轻女性", "推迟生育", "把生育往后压，先活过这几年。", 4, new LawCond[2]
			{
				C(LawCondKind.SelfEq, LawField.Gender, 0),
				C(LawCondKind.SelfLt, LawField.Age, 22)
			}, new LawEffect[1] { E(LawEffectKind.ModifyBirthCd, LawScope.Self, LawField.BirthCooldown, 30) }),
			Law(44, "峻法", "威信 < 40，已装劳役", "劳役效果增强", "用更重的劳役维持秩序。", 2, new LawCond[2]
			{
				C(LawCondKind.SelfLt, LawField.Prestige, 40),
				C(LawCondKind.HasLaw, LawField.Prestige, 11)
			}, new LawEffect[2]
			{
				E(LawEffectKind.ModifyLawStrength, LawScope.Self, LawField.BirthCooldown, 2),
				E(LawEffectKind.Add, LawScope.Self, LawField.Prestige, 1)
			}, 240),
			Law(45, "苛敛", "税库 < 20，已装税收", "税收增强、耕作减弱", "加税的钱从耕作里出。", 1, new LawCond[2]
			{
				C(LawCondKind.ResLt, LawField.Treasury, 20),
				C(LawCondKind.HasLaw, LawField.Treasury, 3)
			}, new LawEffect[2]
			{
				E(LawEffectKind.ModifyLawStrength, LawScope.Self, LawField.Food, 2),
				E(LawEffectKind.ModifyLawStrength, LawScope.Self, LawField.Age, -1)
			}, 240),
			Law(46, "缓征", "5 人饥饿 > 70", "税收减弱、济贫增强", "太多人饿着，税就收不上来。", 0, new LawCond[1] { C(LawCondKind.CountGt, LawField.Hunger, 70, 4) }, new LawEffect[2]
			{
				E(LawEffectKind.ModifyLawStrength, LawScope.Self, LawField.Food, -2),
				E(LawEffectKind.ModifyLawStrength, LawScope.Self, LawField.Fear, 2)
			}, 240),
			Law(47, "整肃", "6 人恐惧 > 50", "医药减弱、劳役增强", "乱世先整人，再谈治病。", 2, new LawCond[1] { C(LawCondKind.CountGt, LawField.Fear, 35, 5) }, new LawEffect[2]
			{
				E(LawEffectKind.ModifyLawStrength, LawScope.Self, LawField.TotalDeaths, -3),
				E(LawEffectKind.ModifyLawStrength, LawScope.Self, LawField.BirthCooldown, 2)
			}, 240),
			Law(48, "威权", "威信 > 80", "以威信换财政", "威权本身就是一种税收。", 2, new LawCond[1] { C(LawCondKind.SelfGt, LawField.Prestige, 80) }, new LawEffect[3]
			{
				E(LawEffectKind.Add, LawScope.Prestige, LawField.Prestige, -5),
				E(LawEffectKind.Add, LawScope.Treasury, LawField.Treasury, -8),
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, -2)
			}, 60),
			Law(49, "赦令", "威信 > 90", "废除一条法", "大赦天下，威信为代价。", 1, new LawCond[1] { C(LawCondKind.SelfGt, LawField.Prestige, 90) }, new LawEffect[2]
			{
				E(LawEffectKind.DisableLaw, LawScope.Self, LawField.BirthCooldown, 11),
				E(LawEffectKind.Add, LawScope.Prestige, LawField.Prestige, -10)
			}, 240),
			Law(50, "示众", "3 人恐惧 > 80", "重新分配恐惧", "恐惧高的人被公开处置。", 3, new LawCond[1] { C(LawCondKind.CountGt, LawField.Fear, 56, 2) }, new LawEffect[2]
			{
				E(LawEffectKind.Add, LawScope.All, LawField.Fear, -20, 0, 100, 1, 0, 80),
				E(LawEffectKind.Add, LawScope.All, LawField.Fear, 2, 0, 100, 1, 1, 80)
			}, 120),
			Law(51, "常平", "粮仓 < 人口 × 3", "税库买粮平抑物价", "财政兜底，粮价不至于失控。", 1, new LawCond[1] { C(LawCondKind.ResLtPopTimes, LawField.Granary, 3) }, new LawEffect[3]
			{
				E(LawEffectKind.Add, LawScope.Treasury, LawField.Treasury, -6, 0, 100, 8, 0, 6),
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, 10, 0, 100, 8, 0, 6),
				E(LawEffectKind.Add, LawScope.All, LawField.Hunger, 2, 0, 100, 8, 1, 6)
			}, 60),
			Law(52, "抚孤", "幼儿夭折者众", "抚恤孤儿", "村庄为失去孩子的家庭兜底。", 0, new LawCond[1] { C(LawCondKind.ObituaryCountGt, LawField.InfantDeaths, 1, 2) }, new LawEffect[3]
			{
				E(LawEffectKind.Add, LawScope.All, LawField.Hunger, -4),
				E(LawEffectKind.Add, LawScope.All, LawField.Fear, -4),
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, -10)
			}, 240),
			Law(53, "丁税宏观", "人口 > 11", "有钱人交税，穷人得粮", "按财产分档征收。", 1, new LawCond[1] { C(LawCondKind.AliveGt, LawField.Age, 11) }, new LawEffect[3]
			{
				E(LawEffectKind.Add, LawScope.All, LawField.Gold, -2, 0, 100, 2, 0, 2),
				E(LawEffectKind.Add, LawScope.Treasury, LawField.Treasury, 2, 0, 100, 2, 0, 2),
				E(LawEffectKind.Add, LawScope.All, LawField.Hunger, 3, 0, 100, 2, 1, 2)
			}, 240),
			Law(54, "减口", "人口 > 17 且粮仓紧张", "减少幼儿", "灾年里的残酷选择。", 4, new LawCond[2]
			{
				C(LawCondKind.AliveGt, LawField.Age, 17),
				C(LawCondKind.ResLtPopTimes, LawField.Granary, 3)
			}, new LawEffect[1] { E(LawEffectKind.KillYoungest, LawScope.Self, LawField.Age, 0, 0, 15) }, 120),
			Law(55, "绝户", "无子女且年龄 > 49", "遗产充公", "没有后代的人，财产归公。", 1, new LawCond[2]
			{
				C(LawCondKind.SelfLt, LawField.Children, 1),
				C(LawCondKind.SelfGt, LawField.Age, 49)
			}, new LawEffect[2]
			{
				E(LawEffectKind.Drain, LawScope.Treasury, LawField.Gold, 99),
				E(LawEffectKind.Add, LawScope.Self, LawField.Hunger, 2)
			}),
			Law(56, "恤寡", "年长有子女的女性且饥饿 > 45", "优先领粮", "寡妇与母亲优先。", 4, new LawCond[4]
			{
				C(LawCondKind.SelfEq, LawField.Gender, 0),
				C(LawCondKind.SelfGt, LawField.Children, 0),
				C(LawCondKind.SelfGt, LawField.Age, 39),
				C(LawCondKind.SelfGt, LawField.Hunger, 45)
			}, new LawEffect[2]
			{
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, -10),
				E(LawEffectKind.Add, LawScope.Self, LawField.Hunger, -35)
			}),
			Law(57, "遗产", "有人死亡时", "死者财产入公", "死者带不走的东西归村子。", 1, new LawCond[1] { C(LawCondKind.OnDeath, LawField.Alive, 1) }, new LawEffect[2]
			{
				E(LawEffectKind.Drain, LawScope.Granary, LawField.Food, 99),
				E(LawEffectKind.Drain, LawScope.Treasury, LawField.Gold, 99)
			}),
			Law(58, "光棍", "成年无子女男性", "多分一份口粮", "没有人替他说话，法律替他说话。", 4, new LawCond[3]
			{
				C(LawCondKind.SelfEq, LawField.Gender, 1),
				C(LawCondKind.SelfLt, LawField.Children, 1),
				C(LawCondKind.SelfGt, LawField.Age, 24)
			}, new LawEffect[2]
			{
				E(LawEffectKind.Add, LawScope.Self, LawField.Hunger, 3),
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, 2)
			}),
			Law(59, "多子", "子女 > 2", "子女多者上交余粮", "孩子多的家庭先把余粮交出来。", 4, new LawCond[1] { C(LawCondKind.SelfGt, LawField.Children, 2) }, new LawEffect[2]
			{
				E(LawEffectKind.Drain, LawScope.Granary, LawField.Food, 6),
				E(LawEffectKind.Add, LawScope.Self, LawField.Hunger, -6)
			}),
			HiddenLaw(60, "哭丧", "本 tick 有人死亡", "全村恐惧上升", "哭声会传染。", 3, 1, 1, "累计死亡 1 人后解锁", new LawCond[1] { C(LawCondKind.DeathsThisTick, LawField.Fear, 1) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.All, LawField.Fear, 4) }, 120),
			HiddenLaw(61, "厚葬", "本 tick 有人死亡且粮仓充足", "以粮换安宁", "用一场体面的葬礼换回一点平静。", 4, 1, 3, "累计死亡 3 人后解锁", new LawCond[2]
			{
				C(LawCondKind.DeathsThisTick, LawField.Fear, 1),
				C(LawCondKind.ResGt, LawField.Granary, 10)
			}, new LawEffect[2]
			{
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, -8),
				E(LawEffectKind.Add, LawScope.All, LawField.Fear, -3)
			}),
			HiddenLaw(62, "阴气", "累计死亡 >= 20", "阴气积累", "死的人多了，村子自己会变冷。", 3, 1, 20, "累计死亡 20 人后解锁", new LawCond[1] { C(LawCondKind.TotalGt, LawField.TotalDeaths, 20) }, new LawEffect[2]
			{
				E(LawEffectKind.Add, LawScope.All, LawField.Fear, 1),
				E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, -1)
			}, 60),
			HiddenLaw(63, "香火", "累计死亡 >= 1", "以粮祭奠", "每一次死亡都要消耗一点粮食。", 4, 1, 1, "累计死亡 1 人后解锁", new LawCond[1] { C(LawCondKind.TotalGt, LawField.TotalDeaths, 1) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.Granary, LawField.Granary, -1) }, 60),
			HiddenLaw(64, "刑戮", "4 人恐惧 > 60", "处决最恐惧者", "恐惧到极点时，杀人是唯一的手段。", 2, 7, 3, "3 名村民恐惧超过 80 后解锁", new LawCond[1] { C(LawCondKind.CountGt, LawField.Fear, 42, 3) }, new LawEffect[2]
			{
				E(LawEffectKind.KillMaxOf, LawScope.Self, LawField.Fear, 0),
				E(LawEffectKind.Add, LawScope.All, LawField.Fear, -8)
			}, 120),
			HiddenLaw(65, "夭殇", "有幼儿夭折", "生育冷却拉长", "孩子的死让所有人不敢再生。", 4, 6, 1, "幼儿夭折 1 人后解锁", new LawCond[1] { C(LawCondKind.TotalGt, LawField.InfantDeaths, 0) }, new LawEffect[1] { E(LawEffectKind.ModifyBirthCd, LawScope.All, LawField.BirthCooldown, 1) }, 30),
			Law(66, "比邻", "邻居比自己更恐惧", "自己也跟着恐惧", "隔壁的恐惧会爬过墙。", 3, new LawCond[1] { C(LawCondKind.NeighborRelative, LawField.Fear, 15) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.Self, LawField.Fear, 3) }),
			Law(67, "阋墙", "自己和邻居都饥饿 > 60", "抢邻居的钱", "饥饿面前没有邻居。", 2, new LawCond[2]
			{
				C(LawCondKind.SelfGt, LawField.Hunger, 60),
				C(LawCondKind.NeighborGt, LawField.Hunger, 60)
			}, new LawEffect[3]
			{
				E(LawEffectKind.Steal, LawScope.Self, LawField.Gold, 6),
				E(LawEffectKind.Add, LawScope.Self, LawField.Fear, 5),
				E(LawEffectKind.Add, LawScope.Neighbors, LawField.Fear, 5)
			}),
			Law(68, "株连", "邻居本 tick 死亡", "恐惧扩散", "一人的死会牵连邻里。", 2, new LawCond[1] { C(LawCondKind.NeighborDiedThisTick, LawField.Alive, 1) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.Self, LawField.Fear, 8) }),
			Law(69, "守望", "自己有存粮且邻居饥饿 > 70", "接济邻居", "有余粮的人要管邻居的饭。", 0, new LawCond[2]
			{
				C(LawCondKind.SelfGt, LawField.Food, 10),
				C(LawCondKind.NeighborGt, LawField.Hunger, 70)
			}, new LawEffect[2]
			{
				E(LawEffectKind.Drain, LawScope.Neighbors, LawField.Food, 3),
				E(LawEffectKind.Add, LawScope.Self, LawField.Fear, -2)
			}),
			Law(70, "共井", "相邻者存活", "共用一口井", "邻里还在，日子就还能过。", 0, new LawCond[1] { C(LawCondKind.NeighborGt, LawField.Alive, 0) }, new LawEffect[1] { E(LawEffectKind.Add, LawScope.Self, LawField.Hunger, -1) }),
			Law(71, "开垦", "粮仓 < 150", "花 15 粮，把一格草地或荒地变成耕地", "开了荒才有地种。但粮仓里的粮食是今年要吃的。", 0)
		};

		public static int Count => Definitions.Length;

		public static LawDefinition Get(int index)
		{
			if (index < 0 || index >= Definitions.Length)
			{
				return null;
			}
			return Definitions[index];
		}

		public static LawDefinition FindById(int id)
		{
			for (int i = 0; i < Definitions.Length; i++)
			{
				if (Definitions[i].id == id)
				{
					return Definitions[i];
				}
			}
			return null;
		}

		public static int IndexOfId(int id)
		{
			for (int i = 0; i < Definitions.Length; i++)
			{
				if (Definitions[i].id == id)
				{
					return i;
				}
			}
			return -1;
		}

		public static bool IsScripted(int id)
		{
			for (int i = 0; i < ScriptedLawIds.Length; i++)
			{
				if (ScriptedLawIds[i] == id)
				{
					return true;
				}
			}
			return false;
		}

		public static bool IsUnlocked(VillageSaveData save, LawDefinition law)
		{
			if (law == null || save == null)
			{
				return false;
			}
			if (!law.hidden)
			{
				return true;
			}
			return law.unlockType switch
			{
				1 => save.totalDeaths >= law.unlockThreshold, 
				2 => save.totalBirths >= law.unlockThreshold, 
				3 => CountAlive(save) >= law.unlockThreshold, 
				4 => save.granaryFood >= law.unlockThreshold, 
				5 => save.treasuryGold >= law.unlockThreshold, 
				6 => CountInfantDeaths(save) >= law.unlockThreshold, 
				7 => CountFearAbove(save, 80) >= law.unlockThreshold, 
				8 => CountGoldBelow(save, 10) >= law.unlockThreshold, 
				9 => save.tyrantMode, 
				_ => true, 
			};
		}

		public static bool IsPurchased(VillageSaveData save, LawDefinition law)
		{
			if (law == null || law.treasuryCost <= 0)
			{
				return true;
			}
			for (int i = 0; i < save.purchasedLawCount && i < save.purchasedLawIds.Length; i++)
			{
				if (save.purchasedLawIds[i] == law.id)
				{
					return true;
				}
			}
			return false;
		}

		public static void UnlockProgress(VillageSaveData save, LawDefinition law, out int current, out int need)
		{
			current = 0;
			need = law?.unlockThreshold ?? 0;
			if (save != null && law != null)
			{
				switch (law.unlockType)
				{
				case 1:
					current = save.totalDeaths;
					break;
				case 2:
					current = save.totalBirths;
					break;
				case 3:
					current = CountAlive(save);
					break;
				case 4:
					current = save.granaryFood;
					break;
				case 5:
					current = save.treasuryGold;
					break;
				case 6:
					current = CountInfantDeaths(save);
					break;
				case 7:
					current = CountFearAbove(save, 80);
					break;
				case 8:
					current = CountGoldBelow(save, 10);
					break;
				default:
					current = (save.tyrantMode ? need : 0);
					break;
				}
			}
		}

		public static int CountAlive(VillageSaveData save)
		{
			int num = 0;
			for (int i = 0; i < save.villagerAlive.Length; i++)
			{
				if (save.villagerAlive[i])
				{
					num++;
				}
			}
			return num;
		}

		private static int CountInfantDeaths(VillageSaveData save)
		{
			int num = 0;
			for (int i = 0; i < save.obituaryCount; i++)
			{
				if (save.obituaryInfants[i])
				{
					num++;
				}
			}
			return num;
		}

		private static int CountFearAbove(VillageSaveData save, int threshold)
		{
			int num = 0;
			for (int i = 0; i < save.villagerFear.Length; i++)
			{
				if (save.villagerAlive[i] && save.villagerFear[i] > threshold)
				{
					num++;
				}
			}
			return num;
		}

		private static int CountGoldBelow(VillageSaveData save, int threshold)
		{
			int num = 0;
			for (int i = 0; i < save.villagerGold.Length; i++)
			{
				if (save.villagerAlive[i] && save.villagerGold[i] < threshold)
				{
					num++;
				}
			}
			return num;
		}

		private static LawDefinition Law(int id, string name, string condition, string effect, string description, int category)
		{
			return Create(id, name, condition, effect, description, category, hidden: false, 0, 0, 0, string.Empty, null, null, 0);
		}

		private static LawDefinition Law(int id, string name, string condition, string effect, string description, int category, LawCond[] conds, LawEffect[] effects, int cooldown = 0)
		{
			return Create(id, name, condition, effect, description, category, hidden: false, 0, 0, 0, string.Empty, conds, effects, cooldown);
		}

		private static LawDefinition HiddenLaw(int id, string name, string condition, string effect, string description, int category, int unlockType, int threshold, string hint, LawCond[] conds, LawEffect[] effects, int cooldown = 0)
		{
			return Create(id, name, condition, effect, description, category, hidden: true, unlockType, threshold, 0, hint, conds, effects, cooldown);
		}

		private static LawDefinition RareLaw(int id, string name, string condition, string effect, string description, int category, int cost, string hint, LawCond[] conds, LawEffect[] effects, int cooldown = 0)
		{
			return Create(id, name, condition, effect, description, category, hidden: false, 0, 0, cost, hint, conds, effects, cooldown);
		}

		private static LawDefinition Create(int id, string name, string condition, string effect, string description, int category, bool hidden, int unlockType, int threshold, int cost, string hint, LawCond[] conds, LawEffect[] effects, int cooldown)
		{
			return new LawDefinition
			{
				id = id,
				name = name,
				condition = condition,
				effect = effect,
				description = description,
				category = category,
				hidden = hidden,
				unlockType = unlockType,
				unlockThreshold = threshold,
				treasuryCost = cost,
				unlockHint = hint,
				conds = conds,
				effects = effects,
				cooldown = cooldown
			};
		}

		private static LawCond C(LawCondKind kind, LawField field, int a, int b = 0)
		{
			return new LawCond
			{
				kind = (byte)kind,
				field = (byte)field,
				a = a,
				b = b
			};
		}

		private static LawEffect E(LawEffectKind kind, LawScope scope, LawField field, int amount, int extra = 0, int chance = 100, int guardField = 255, int guardOp = 0, int guardValue = 0)
		{
			return new LawEffect
			{
				kind = (byte)kind,
				scope = (byte)scope,
				field = (byte)field,
				amount = amount,
				extra = extra,
				chance = chance,
				guardField = (byte)Mathf.Clamp(guardField, 0, 255),
				guardOp = (byte)guardOp,
				guardValue = guardValue
			};
		}
	}

	public sealed class LawDefinition
	{
		public int id;

		public string name;

		public string condition;

		public string effect;

		public string description;

		public int category;

		public bool hidden;

		public int unlockType;

		public int unlockThreshold;

		public int treasuryCost;

		public string unlockHint;

		public LawCond[] conds;

		public LawEffect[] effects;

		public int cooldown;
	}

	public struct LawCond
	{
		public byte kind;

		public byte field;

		public int a;

		public int b;
	}

	public struct LawEffect
	{
		public byte kind;

		public byte scope;

		public byte field;

		public int amount;

		public int extra;

		public int chance;

		public byte guardField;

		public byte guardOp;

		public int guardValue;
	}

	public enum LawField
	{
		Hunger,
		Fear,
		Gold,
		Food,
		Children,
		Age,
		Gender,
		Granary,
		Treasury,
		Prestige,
		Alive,
		BirthCooldown,
		TotalDeaths,
		TotalBirths,
		InfantDeaths,
		ObituaryCount
	}

	public enum LawCondKind
	{
		SelfGt,
		SelfLt,
		SelfEq,
		NeighborGt,
		AliveGt,
		AliveLt,
		CountGt,
		AvgGt,
		ResLtPopTimes,
		Periodic,
		DeathsThisTick,
		HasLaw,
		ResGt,
		TotalGt,
		NeighborRelative,
		NeighborDiedThisTick,
		ObituaryCountGt,
		Always,
		CountLt,
		ResLt,
		OnDeath
	}

	public enum LawScope
	{
		Self,
		Neighbors,
		All,
		Granary,
		Treasury,
		Prestige
	}

	public enum LawEffectKind
	{
		Add,
		Move,
		Drain,
		Steal,
		KillSelf,
		KillMaxOf,
		KillYoungest,
		ModifyLawStrength,
		DisableLaw,
		ModifyBirthCd,
		SetField,
		BroadcastFrom,
		FillTo
	}

	public static class Rng
	{
		public static uint For(int seed, int tick, int channel)
		{
			return For(seed, tick, channel, 0);
		}

		public static uint For(int seed, int tick, int channel, int seq)
		{
			int num = seed ^ (tick * 374761393) ^ (channel * 668265263) ^ (int)(seq * 2246822519u);
			int num2 = (num ^ (num >>> 13)) * 1274126177;
			int num3 = (num2 ^ (num2 >>> 16)) * -2048144777;
			return (uint)(num3 ^ (num3 >>> 13));
		}

		public static int Range(int seed, int tick, int channel, int minimumInclusive, int maximumExclusive)
		{
			return Range(seed, tick, channel, 0, minimumInclusive, maximumExclusive);
		}

		public static int Range(int seed, int tick, int channel, int seq, int minimumInclusive, int maximumExclusive)
		{
			if (maximumExclusive <= minimumInclusive)
			{
				return minimumInclusive;
			}
			uint num = (uint)(maximumExclusive - minimumInclusive);
			return minimumInclusive + (int)(For(seed, tick, channel, seq) % num);
		}

		public static bool Chance(int seed, int tick, int channel, int percent)
		{
			return Chance(seed, tick, channel, 0, percent);
		}

		public static bool Chance(int seed, int tick, int channel, int seq, int percent)
		{
			if (percent <= 0)
			{
				return false;
			}
			if (percent >= 100)
			{
				return true;
			}
			return For(seed, tick, channel, seq) % 100 < (uint)percent;
		}
	}

	public static class Innate
	{
		public const uint Coward = 1u;

		public const uint Steadfast = 2u;

		public const uint Greedy = 4u;

		public const uint Generous = 8u;

		public const uint Diligent = 16u;

		public const uint Idle = 32u;

		public const uint Frail = 64u;

		public const uint Robust = 128u;

		public const uint Prolific = 256u;

		public const uint Barren = 512u;

		public const uint Frugal = 1024u;

		public const uint Wasteful = 2048u;

		public const uint Gregarious = 4096u;

		public const uint Solitary = 8192u;

		public const uint Tough = 16384u;

		public const uint Brittle = 32768u;

		public static readonly uint[][] Axes = new uint[8][]
		{
			new uint[2] { 1u, 2u },
			new uint[2] { 4u, 8u },
			new uint[2] { 16u, 32u },
			new uint[2] { 64u, 128u },
			new uint[2] { 256u, 512u },
			new uint[2] { 1024u, 2048u },
			new uint[2] { 4096u, 8192u },
			new uint[2] { 16384u, 32768u }
		};
	}

	public static class Mark
	{
		public const uint Sinner = 1u;

		public const uint Bandit = 2u;

		public const uint Deserter = 4u;

		public const uint LastHeir = 8u;

		public const uint Widow = 16u;

		public const uint Bachelor = 32u;

		public const uint Debtor = 64u;

		public const uint Settler = 128u;

		public const uint Starved = 256u;

		public const uint Haunted = 512u;

		public const uint Worn = 1024u;

		public const uint Bitter = 2048u;

		public const uint Butcher = 4096u;

		public const uint Mourner = 8192u;

		public const uint Patron = 16384u;

		public const uint Shaman = 32768u;

		public const uint PermanentMask = 65535u;

		public const uint ClearableMask = 1073676288u;

		public const uint Corvee = 262144u;

		public const uint OnMeds = 2097152u;

		public const uint Mourning = 134217728u;
	}

	public static class Traits
	{
		private const int ChannelInnate = 5000;

		private const int ChannelInherit = 5100;

		private const int ChannelMark = 5200;

		private const int ChannelScar = 5300;

		public static bool HasInnate(VillageSaveData d, int i, uint bit)
		{
			if (d != null && d.villagerInnate != null && i >= 0 && i < d.villagerInnate.Length)
			{
				return (d.villagerInnate[i] & bit) != 0;
			}
			return false;
		}

		public static bool HasMark(VillageSaveData d, int i, uint bit)
		{
			if (d != null && d.villagerMarks != null && i >= 0 && i < d.villagerMarks.Length)
			{
				return (d.villagerMarks[i] & bit) != 0;
			}
			return false;
		}

		public static bool HasAnyMark(VillageSaveData d, int i)
		{
			if (d != null && d.villagerMarks != null && i >= 0 && i < d.villagerMarks.Length)
			{
				return d.villagerMarks[i] != 0;
			}
			return false;
		}

		public static void AddMark(VillageSaveData d, int i, uint bit)
		{
			if (d != null && d.villagerMarks != null && i >= 0 && i < d.villagerMarks.Length)
			{
				d.villagerMarks[i] |= bit;
			}
		}

		public static void RemoveMark(VillageSaveData d, int i, uint bit)
		{
			if (d != null && d.villagerMarks != null && i >= 0 && i < d.villagerMarks.Length)
			{
				d.villagerMarks[i] &= ~(bit & 0x3FFF0000);
			}
		}

		public static void AddFear(VillageSaveData d, int i, int amount)
		{
			if (d == null || i < 0 || i >= d.villagerFear.Length)
			{
				return;
			}
			int num = amount;
			if (amount > 0)
			{
				if (HasInnate(d, i, 16384u))
				{
					num = Math.Max(1, amount * 7 / 10);
				}
				if (HasInnate(d, i, 32768u))
				{
					num = amount * 3 / 2;
				}
			}
			d.villagerFear[i] = Math.Max(0, Math.Min(99, d.villagerFear[i] + num));
		}

		public static uint RollInnate(int seed, int index)
		{
			uint num = 0u;
			int num2 = 1 + Rng.Range(seed, 0, 5000, index * 10, 0, 2);
			int num3 = Rng.Range(seed, 0, 5000, index * 10 + 1, 0, 8);
			num |= PickFromAxis(seed, index, num3);
			if (num2 >= 2)
			{
				int num4 = Rng.Range(seed, 0, 5000, index * 10 + 2, 0, 7);
				if (num4 >= num3)
				{
					num4++;
				}
				num |= PickFromAxis(seed, index, num4);
			}
			return num;
		}

		public static uint RollInheritedInnate(VillageSaveData d, int childIndex, int mother, int father)
		{
			uint bits = ((mother >= 0 && mother < d.villagerInnate.Length) ? d.villagerInnate[mother] : 0u);
			uint bits2 = ((father >= 0 && father < d.villagerInnate.Length) ? d.villagerInnate[father] : 0u);
			uint num = 0u;
			int num2 = Rng.Range(d.rngSeed, d.tick, 5100, childIndex * 10, 0, 10);
			uint num3;
			if (num2 < 4)
			{
				num3 = PickOneBit(bits, d, childIndex, 0);
			}
			else
			{
				num3 = ((num2 >= 8) ? PickFromAxis(d.rngSeed, childIndex, Rng.Range(d.rngSeed, d.tick, 5100, childIndex * 10 + 2, 0, 8)) : PickOneBit(bits2, d, childIndex, 1));
			}
			int num4 = AxisOf(num3);
			num |= num3;
			if (Rng.Range(d.rngSeed, d.tick, 5100, childIndex * 10 + 3, 0, 10) < 6)
			{
				int num5 = Rng.Range(d.rngSeed, d.tick, 5100, childIndex * 10 + 4, 0, 7);
				if (num5 >= num4)
				{
					num5++;
				}
				num |= PickFromAxis(d.rngSeed, childIndex, num5);
			}
			return num;
		}

		public static void SurvivorScan(VillageSaveData d, int tick)
		{
			if (d == null || tick <= 0)
			{
				return;
			}
			for (int i = 0; i < d.villagerAlive.Length; i++)
			{
				if (d.villagerAlive[i])
				{
					if (d.villagerHunger[i] >= 90 && !HasMark(d, i, 256u))
					{
						AddMark(d, i, 256u);
					}
					if (d.villagerFear[i] >= 90 && !HasMark(d, i, 512u))
					{
						AddMark(d, i, 512u);
					}
					if (HasMark(d, i, 262144u) && !HasMark(d, i, 1024u) && Rng.Chance(d.rngSeed, tick, 5300, i, 10))
					{
						AddMark(d, i, 1024u);
					}
				}
			}
		}

		public static int PortraitIndex(VillageSaveData d, int i)
		{
			if (d == null || i < 0 || i >= d.villagerAlive.Length)
			{
				return 1;
			}
			uint marks = ((d.villagerMarks != null && i < d.villagerMarks.Length) ? d.villagerMarks[i] : 0u);
			return PortraitIndex(d.villagerGenders[i] == 0, marks);
		}

		public static int PortraitIndex(bool female, uint marks)
		{
			if (female)
			{
				if ((marks & 0x8000) != 0)
				{
					return 4;
				}
				if ((marks & 0x1000) != 0)
				{
					return 2;
				}
				if ((marks & 0x4000) != 0)
				{
					return 3;
				}
				return 5;
			}
			if ((marks & 2) != 0)
			{
				return 0;
			}
			return 1;
		}

		public static string DescribeInnate(VillageSaveData d, int i)
		{
			return DescribeInnate((d != null && d.villagerInnate != null && i >= 0 && i < d.villagerInnate.Length) ? d.villagerInnate[i] : 0u);
		}

		public static string DescribeInnate(uint bits)
		{
			string empty = string.Empty;
			empty += Append(bits, 1u, "怯懦");
			empty += Append(bits, 2u, "果决");
			empty += Append(bits, 4u, "贪婪");
			empty += Append(bits, 8u, "慷慨");
			empty += Append(bits, 16u, "勤勉");
			empty += Append(bits, 32u, "懒散");
			empty += Append(bits, 64u, "病弱");
			empty += Append(bits, 128u, "强健");
			empty += Append(bits, 256u, "多产");
			empty += Append(bits, 512u, "难育");
			empty += Append(bits, 1024u, "节俭");
			empty += Append(bits, 2048u, "挥霍");
			empty += Append(bits, 4096u, "合群");
			empty += Append(bits, 8192u, "孤僻");
			empty += Append(bits, 16384u, "坚韧");
			empty += Append(bits, 32768u, "脆弱");
			if (!string.IsNullOrEmpty(empty))
			{
				return empty;
			}
			return "无";
		}

		public static string DescribeMarks(VillageSaveData d, int i)
		{
			return DescribeMarks((d != null && d.villagerMarks != null && i >= 0 && i < d.villagerMarks.Length) ? d.villagerMarks[i] : 0u);
		}

		public static string DescribeMarks(uint bits)
		{
			string empty = string.Empty;
			empty += Append(bits, 1u, "罪人");
			empty += Append(bits, 2u, "劫匪");
			empty += Append(bits, 4u, "逃兵");
			empty += Append(bits, 8u, "绝户");
			empty += Append(bits, 16u, "守寡");
			empty += Append(bits, 32u, "光棍");
			empty += Append(bits, 64u, "债户");
			empty += Append(bits, 128u, "徙民");
			empty += Append(bits, 256u, "绝粮");
			empty += Append(bits, 512u, "惊魂");
			empty += Append(bits, 1024u, "劳损");
			empty += Append(bits, 2048u, "积怨");
			empty += Append(bits, 4096u, "屠者");
			empty += Append(bits, 8192u, "尸亲");
			empty += Append(bits, 16384u, "恩主");
			empty += Append(bits, 32768u, "巫祝");
			if (!string.IsNullOrEmpty(empty))
			{
				return empty;
			}
			return "无";
		}

		public static Color TintColor(VillageSaveData d, int i)
		{
			if (HasMark(d, i, 1u) || HasMark(d, i, 4096u))
			{
				return new Color(0.78f, 0.28f, 0.24f, 1f);
			}
			if (HasMark(d, i, 2u))
			{
				return new Color(0.42f, 0.42f, 0.44f, 1f);
			}
			if (HasMark(d, i, 16u))
			{
				return new Color(0.88f, 0.88f, 0.84f, 1f);
			}
			if (HasMark(d, i, 256u) || HasMark(d, i, 1024u))
			{
				return new Color(0.72f, 0.6f, 0.32f, 1f);
			}
			if (HasAnyMark(d, i))
			{
				return new Color(0.82f, 0.72f, 0.52f, 1f);
			}
			return Color.white;
		}

		private static uint PickFromAxis(int seed, int index, int axis)
		{
			uint[] obj = Innate.Axes[axis];
			int num = Rng.Range(seed, 0, 5000, index * 10 + 100 + axis, 0, 2);
			return obj[num];
		}

		private static uint PickOneBit(uint bits, VillageSaveData d, int childIndex, int salt)
		{
			if (bits == 0)
			{
				return PickFromAxis(d.rngSeed, childIndex, Rng.Range(d.rngSeed, d.tick, 5100, childIndex * 10 + 5 + salt, 0, 8));
			}
			int num = 0;
			for (int i = 0; i < 16; i++)
			{
				if ((bits & (uint)(1 << i)) != 0)
				{
					num++;
				}
			}
			int num2 = Rng.Range(d.rngSeed, d.tick, 5100, childIndex * 10 + 6 + salt, 0, num);
			for (int j = 0; j < 16; j++)
			{
				if ((bits & (uint)(1 << j)) != 0)
				{
					if (num2 == 0)
					{
						return (uint)(1 << j);
					}
					num2--;
				}
			}
			return bits & 0xFFFF;
		}

		private static int AxisOf(uint bit)
		{
			for (int i = 0; i < Innate.Axes.Length; i++)
			{
				if ((Innate.Axes[i][0] & bit) != 0 || (Innate.Axes[i][1] & bit) != 0)
				{
					return i;
				}
			}
			return 0;
		}

		private static string Append(uint bits, uint bit, string name)
		{
			if ((bits & bit) != 0)
			{
				return " · " + name;
			}
			return string.Empty;
		}
	}

	public static class VillageTime
	{
		public const int TicksPerDay = 8;

		public const int MonthsPerYear = 12;

		public const float DaysPerSecond = 18.5f;

		public const float TicksPerSecond = 148f;

		public const int MaxStepTicksPerFrame = 200;

		public const int CadenceDays = 30;

		public const int CadenceTicks = 240;

		private static readonly int[] MonthDays = new int[12]
		{
			31, 28, 31, 30, 31, 30, 31, 31, 30, 31,
			30, 31
		};

		public static bool IsLeapYear(int year)
		{
			if (year % 4 == 0)
			{
				if (year % 100 == 0)
				{
					return year % 400 == 0;
				}
				return true;
			}
			return false;
		}

		public static int DaysInMonth(int year, int month)
		{
			switch (month)
			{
			default:
				return 30;
			case 2:
				if (IsLeapYear(year))
				{
					return 29;
				}
				break;
			case 1:
			case 3:
			case 4:
			case 5:
			case 6:
			case 7:
			case 8:
			case 9:
			case 10:
			case 11:
			case 12:
				break;
			}
			return MonthDays[month - 1];
		}

		public static int DaysInYear(int year)
		{
			if (!IsLeapYear(year))
			{
				return 365;
			}
			return 366;
		}

		public static int TicksInYear(int year)
		{
			return DaysInYear(year) * 8;
		}

		public static void ToDate(int tick, out int year, out int month, out int day)
		{
			long num = ((tick < 0) ? 0 : (tick / 8));
			int i = (int)((num > 0) ? (num * 400 / 146097) : 0);
			long num2;
			for (num2 = DaysBeforeYear(i); num >= num2 + DaysInYear(i); i++)
			{
				num2 += DaysInYear(i);
			}
			while (num < num2 && i > 0)
			{
				i--;
				num2 -= DaysInYear(i);
			}
			long num3 = num - num2;
			int j;
			for (j = 1; j < 12 && num3 >= DaysInMonth(i, j); j++)
			{
				num3 -= DaysInMonth(i, j);
			}
			year = i;
			month = j;
			day = (int)num3 + 1;
		}

		public static int ToYear(int tick)
		{
			ToDate(tick, out var year, out var _, out var _);
			return year;
		}

		public static int ToMonth(int tick)
		{
			ToDate(tick, out var _, out var month, out var _);
			return month;
		}

		public static int ToDayOfMonth(int tick)
		{
			ToDate(tick, out var _, out var _, out var day);
			return day;
		}

		public static int ToDay(int tick)
		{
			if (tick >= 0)
			{
				return tick / 8 + 1;
			}
			return 1;
		}

		public static string FormatYearMonth(int tick)
		{
			ToDate(tick, out var year, out var month, out var _);
			return year + "年" + month + "月";
		}

		public static string FormatFull(int tick)
		{
			ToDate(tick, out var year, out var month, out var day);
			return year + "年" + month + "月" + day + "日";
		}

		public static string FormatDay(int tick)
		{
			return FormatYearMonth(tick);
		}

		public static string FormatDayTime(int tick)
		{
			return FormatYearMonth(tick);
		}

		public static long DaysBeforeYear(int year)
		{
			if (year <= 0)
			{
				return 0L;
			}
			long num = (long)year - 1L;
			return 365L * (long)year + (num / 4 - num / 100 + num / 400) + 1;
		}
	}

	public static class Farmland
	{
		public const int LawId = 71;

		public const int TillCost = 15;

		public const int TillCostGrass = 15;

		public const int TillCostWaste = 30;

		public const int TillGoldGrass = 2;

		public const int TillGoldWaste = 4;

		public const int TillArableMult = 3;

		public const int TillPeriodTicks = 120;

		public const int TillTrigger = 150;

		public const int DegradePeriodTicks = 2400;

		public const int DegradePercent = 2;

		public const int DegradeWastelandPercent = 35;

		private const int ChannelDegrade = 31;

		private const int ChannelDegradeKind = 32;

		public static void Tick(VillageSaveData data, int tick)
		{
			if (data != null && data.terrain != null && data.terrain.Length != 0 && tick > 0)
			{
				if (tick % 120 == 0)
				{
					TryTill(data);
				}
				if (tick % 2400 == 0)
				{
					Degrade(data, tick);
				}
			}
		}

		public static bool TryTill(VillageSaveData data)
		{
			if (!HasLaw(data, 71))
			{
				return false;
			}
			if (data.granaryFood >= 150)
			{
				return false;
			}
			byte b = 4;
			int num = FindFrontierTile(data, b);
			if (num < 0)
			{
				b = 1;
				num = FindFrontierTile(data, b);
			}
			if (num < 0)
			{
				return false;
			}
			int num2 = ((b == 4) ? 30 : 15);
			int num3 = ((b == 4) ? 4 : 2);
			if (data.granaryFood < num2)
			{
				return false;
			}
			if (data.treasuryGold < num3)
			{
				return false;
			}
			if (data.arableBaseline > 0 && CountFarmland(data) >= data.arableBaseline * 3)
			{
				return false;
			}
			data.terrain[num] = 2;
			data.granaryFood -= num2;
			data.treasuryGold -= num3;
			Cfg.TreasuryOut += num3;
			data.terrainRevision++;
			return true;
		}

		private static int FindFrontierTile(VillageSaveData data, byte kind)
		{
			int num = 96;
			int num2 = 64;
			for (int i = 0; i < data.terrain.Length; i++)
			{
				if (data.terrain[i] == kind)
				{
					int num3 = i % num;
					int num4 = i / num;
					if (num3 > 0 && data.terrain[i - 1] == 2)
					{
						return i;
					}
					if (num3 < num - 1 && data.terrain[i + 1] == 2)
					{
						return i;
					}
					if (num4 > 0 && data.terrain[i - num] == 2)
					{
						return i;
					}
					if (num4 < num2 - 1 && data.terrain[i + num] == 2)
					{
						return i;
					}
				}
			}
			return -1;
		}

		public static int Degrade(VillageSaveData data, int tick)
		{
			int num = 0;
			for (int i = 0; i < data.terrain.Length; i++)
			{
				if (data.terrain[i] == 2)
				{
					int percent = ((data.runMode >= 0) ? 1 : 2);
					if (Rng.Chance(data.rngSeed, tick, 31, i, percent))
					{
						data.terrain[i] = (byte)((!Rng.Chance(data.rngSeed, tick, 32, i, Cfg.DegradeWastePct)) ? 1 : 4);
						num++;
					}
				}
			}
			if (num > 0)
			{
				data.terrainRevision++;
			}
			return num;
		}

		public static int CountFarmland(VillageSaveData data)
		{
			if (data == null || data.terrain == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < data.terrain.Length; i++)
			{
				if (data.terrain[i] == 2)
				{
					num++;
				}
			}
			return num;
		}

		private static bool HasLaw(VillageSaveData data, int lawId)
		{
			if (data.prestigeSuspended)
			{
				return false;
			}
			if (data.lawIds == null)
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
	}

	public static class TerrainType
	{
		public const byte Water = 0;

		public const byte Grass = 1;

		public const byte Farmland = 2;

		public const byte Desert = 3;

		public const byte Wasteland = 4;

		public const byte Snow = 5;

		public const byte Ice = 6;

		public const int Count = 7;

		public static bool Habitable(byte terrain)
		{
			if (terrain != 0)
			{
				return terrain != 6;
			}
			return false;
		}

		public static bool Arable(byte terrain)
		{
			return terrain == 2;
		}

		public static bool Tillable(byte terrain)
		{
			if (terrain != 1)
			{
				return terrain == 4;
			}
			return true;
		}

		public static bool Liquid(byte terrain)
		{
			if (terrain != 0)
			{
				return terrain == 6;
			}
			return true;
		}

		public static bool TreeGround(byte terrain)
		{
			return terrain == 1;
		}

		public static bool SnowTreeGround(byte terrain)
		{
			return terrain == 5;
		}

		public static bool Buildable(byte terrain)
		{
			return terrain == 1;
		}
	}

	public sealed class VillageMap
	{
		public const int Width = 96;

		public const int Height = 64;

		public const int TileCount = 6144;

		public const int SpawnCount = 80;

		public const int GeneratorVersion = 5;

		public int seed;

		public int version;

		public byte[] terrain;

		public int[] spawnTiles;

		public int arableCount;

		public int habitableCount;

		public int waterCount;
	}

	public static class VillageMapGenerator
	{
		private const int ChannelElevation = 8;

		private const int ChannelMoisture = 9;

		private const int ChannelRiver = 11;

		private const int ChannelTemperature = 12;

		private const int WaterLevel = 60;

		private const int ChannelDesert = 13;

		private const int ChannelWaste = 14;

		private const int MinWaterThickness = 3;

		private const int WastelandPatchTarget = 110;

		private const int ColdBand = 10;

		private const int PolarFringe = 4;

		private const int IceFringeExtra = 3;

		private const int SnowThreshold = 190;

		private const int SnowThresholdPerRow = 20;

		private const int VillageClearingRadius = 10;

		private const int FarmPlotCount = 4;

		private const int FarmPlotRadius = 2;

		public const int MinArableTiles = 60;

		public static VillageMap Generate(int seed)
		{
			if (seed == 0)
			{
				seed = 1;
			}
			VillageMap villageMap = new VillageMap
			{
				seed = seed,
				version = 5,
				terrain = new byte[6144],
				spawnTiles = new int[80]
			};
			int[] array = new int[6144];
			int[] array2 = new int[6144];
			int[] array3 = new int[6144];
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 96; j++)
				{
					int num = i * 96 + j;
					array[num] = (BuildNoise(seed, 8, j, i, 24, 16, 2) + BuildNoise(seed, 8, j, i, 48, 32, 1) + BuildNoise(seed, 8, j, i, 96, 64, 1)) / 4;
					array2[num] = (BuildNoise(seed, 9, j, i, 24, 16, 2) + BuildNoise(seed, 9, j, i, 48, 32, 1) + BuildNoise(seed, 9, j, i, 96, 64, 1)) / 4;
					array3[num] = (BuildNoise(seed, 12, j, i, 24, 16, 2) + BuildNoise(seed, 12, j, i, 48, 32, 1) + BuildNoise(seed, 12, j, i, 96, 64, 1)) / 4;
				}
			}
			NormalizeSpread(array, 127, 45);
			NormalizeSpread(array2, 127, 45);
			NormalizeSpread(array3, 127, 45);
			bool[] array4 = new bool[6144];
			for (int k = 0; k < 64; k++)
			{
				for (int l = 0; l < 96; l++)
				{
					int num2 = k * 96 + l;
					array4[num2] = IsCold(k, array3[num2]);
				}
			}
			for (int m = 0; m < 6144; m++)
			{
				byte b = (byte)((array[m] >= 60) ? ((!array4[m]) ? 1 : 5) : 0);
				villageMap.terrain[m] = b;
			}
			RefineWater(villageMap, 4);
			CarveSideRiver(villageMap, seed);
			ClearVillageArea(villageMap, array4);
			EnforceWaterThickness(villageMap, array4);
			FillIsolatedWater(villageMap, array4);
			EnsureWaterCap(villageMap, seed, array4);
			EnsureGrassCoverage(villageMap, seed);
			PlaceSpawns(villageMap, seed);
			PlaceFarmland(villageMap);
			PlaceDesert(villageMap, seed, array4);
			PlaceWasteland(villageMap, seed, array4);
			EnsureArableFloor(villageMap, seed);
			ApplyIce(villageMap, array4);
			CountTerrain(villageMap);
			return villageMap;
		}

		private static void PlaceFarmland(VillageMap map)
		{
			if (map.spawnTiles == null || map.spawnTiles.Length == 0)
			{
				return;
			}
			int num = map.spawnTiles.Length / 4;
			if (num < 1)
			{
				num = 1;
			}
			for (int i = 0; i < 4; i++)
			{
				int num2 = (i * num + num / 2) % map.spawnTiles.Length;
				int num3 = map.spawnTiles[num2];
				int num4 = num3 % 96;
				int num5 = num3 / 96;
				for (int j = num5 - 2; j <= num5 + 2; j++)
				{
					if (j < 0 || j >= 64)
					{
						continue;
					}
					for (int k = num4 - 2; k <= num4 + 2; k++)
					{
						if (k >= 0 && k < 96)
						{
							int num6 = j * 96 + k;
							if (map.terrain[num6] == 1)
							{
								map.terrain[num6] = 2;
							}
						}
					}
				}
			}
		}

		private static void EnsureArableFloor(VillageMap map, int seed)
		{
			int num = Count(map, 2);
			for (int i = 0; i < 6144; i++)
			{
				if (num >= 60)
				{
					break;
				}
				int num2 = (i * 61 + seed * 7) & 0x7FFFFFFF;
				num2 %= 6144;
				if (TerrainType.Tillable(map.terrain[num2]))
				{
					map.terrain[num2] = 2;
					num++;
				}
			}
		}

		private static void ComputeSpawnBounds(VillageMap map, out int minX, out int maxX, out int minY, out int maxY)
		{
			minX = int.MaxValue;
			maxX = int.MinValue;
			minY = int.MaxValue;
			maxY = int.MinValue;
			for (int i = 0; i < map.spawnTiles.Length; i++)
			{
				int num = map.spawnTiles[i] % 96;
				int num2 = map.spawnTiles[i] / 96;
				if (num < minX)
				{
					minX = num;
				}
				if (num > maxX)
				{
					maxX = num;
				}
				if (num2 < minY)
				{
					minY = num2;
				}
				if (num2 > maxY)
				{
					maxY = num2;
				}
			}
			if (minX > maxX)
			{
				minX = 0;
				maxX = -1;
				minY = 0;
				maxY = -1;
			}
		}

		private static void NormalizeSpread(int[] values, int targetMean, int targetSigma)
		{
			long num = 0L;
			for (int i = 0; i < values.Length; i++)
			{
				num += values[i];
			}
			int num2 = (int)(num / values.Length);
			long num3 = 0L;
			for (int j = 0; j < values.Length; j++)
			{
				long num4 = values[j] - num2;
				num3 += num4 * num4;
			}
			int num5 = (int)Math.Sqrt((double)num3 / (double)values.Length);
			if (num5 <= 0)
			{
				num5 = 1;
			}
			for (int k = 0; k < values.Length; k++)
			{
				int val = targetMean + (values[k] - num2) * targetSigma / num5;
				values[k] = Math.Max(0, Math.Min(255, val));
			}
		}

		private static int BuildNoise(int seed, int channel, int x, int y, int gridWidth, int gridHeight, int weight)
		{
			int num = x * (gridWidth - 1) * 256 / 95;
			int num2 = y * (gridHeight - 1) * 256 / 63;
			int num3 = num >> 8;
			int num4 = num2 >> 8;
			int num5 = num & 0xFF;
			int num6 = num2 & 0xFF;
			int num7 = Math.Min(gridWidth - 1, num3 + 1);
			int num8 = Math.Min(gridHeight - 1, num4 + 1);
			int num9 = Rng.Range(seed, 0, channel, num4 * gridWidth + num3, 0, 256);
			int num10 = Rng.Range(seed, 0, channel, num4 * gridWidth + num7, 0, 256);
			int num11 = Rng.Range(seed, 0, channel, num8 * gridWidth + num3, 0, 256);
			int num12 = Rng.Range(seed, 0, channel, num8 * gridWidth + num7, 0, 256);
			int num13 = num9 * (256 - num5) + num10 * num5 >> 8;
			int num14 = num11 * (256 - num5) + num12 * num5 >> 8;
			return (num13 * (256 - num6) + num14 * num6 >> 8) * weight;
		}

		private static void RefineWater(VillageMap map, int iterations)
		{
			byte[] array = map.terrain;
			byte[] array2 = new byte[array.Length];
			for (int i = 0; i < iterations; i++)
			{
				for (int j = 0; j < 64; j++)
				{
					for (int k = 0; k < 96; k++)
					{
						int num = j * 96 + k;
						int num2 = 0;
						for (int l = -1; l <= 1; l++)
						{
							for (int m = -1; m <= 1; m++)
							{
								if (m != 0 || l != 0)
								{
									int num3 = k + m;
									int num4 = j + l;
									if (num3 >= 0 && num4 >= 0 && num3 < 96 && num4 < 64 && array[num4 * 96 + num3] == 0)
									{
										num2++;
									}
								}
							}
						}
						if (num2 >= 5)
						{
							array2[num] = 0;
						}
						else if (num2 <= 1 && array[num] == 0)
						{
							array2[num] = 1;
						}
						else
						{
							array2[num] = array[num];
						}
					}
				}
				byte[] array3 = array;
				array = array2;
				array2 = array3;
			}
			if (array != map.terrain)
			{
				Array.Copy(array, map.terrain, array.Length);
			}
		}

		private static void CarveSideRiver(VillageMap map, int seed)
		{
			int num = 70 + Rng.Range(seed, 0, 11, 0, 0, 15);
			int num2 = Rng.Range(seed, 0, 11, 300, 0, 13);
			int num3 = 32 + Rng.Range(seed, 0, 11, 301, 0, 21);
			int num4 = 0;
			for (int i = 0; i < num3; i++)
			{
				int num5 = num2 + i;
				if (num5 >= 64)
				{
					break;
				}
				num4 += Rng.Range(seed, 0, 11, 400 + i, -1, 2);
				if (num4 > 8)
				{
					num4 = 8;
				}
				if (num4 < -8)
				{
					num4 = -8;
				}
				int num6 = num + num4;
				for (int j = -1; j <= 1; j++)
				{
					int num7 = num6 + j;
					if (num7 >= 0 && num7 < 96)
					{
						map.terrain[num5 * 96 + num7] = 0;
					}
				}
			}
		}

		private static int LiquidRun(byte[] terrain, int x, int y, int dx, int dy)
		{
			int num = 1;
			for (int i = -1; i <= 1; i += 2)
			{
				int num2 = x + dx * i;
				int num3 = y + dy * i;
				while (num2 >= 0 && num2 < 96 && num3 >= 0 && num3 < 64 && TerrainType.Liquid(terrain[num3 * 96 + num2]))
				{
					num++;
					num2 += dx * i;
					num3 += dy * i;
				}
			}
			return num;
		}

		private static void EnforceWaterThickness(VillageMap map, bool[] cold)
		{
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 96; j++)
				{
					int num = i * 96 + j;
					if (TerrainType.Liquid(map.terrain[num]) && (LiquidRun(map.terrain, j, i, 1, 0) < 3 || LiquidRun(map.terrain, j, i, 0, 1) < 3))
					{
						map.terrain[num] = (byte)((!cold[num]) ? 1 : 5);
					}
				}
			}
		}

		private static void PlaceDesert(VillageMap map, int seed, bool[] cold)
		{
			bool flag = Rng.Range(seed, 0, 13, 0, 0, 2) == 1;
			int num = 14 + Rng.Range(seed, 0, 13, 1, 0, 9);
			int num2 = 12 + Rng.Range(seed, 0, 13, 2, 0, 20);
			int num3 = 14 + Rng.Range(seed, 0, 13, 3, 0, 15);
			for (int i = num2; i < num2 + num3 && i < 64; i++)
			{
				int num4 = num - Rng.Range(seed, 0, 13, 100 + i, 0, 6);
				for (int j = 0; j < num4; j++)
				{
					int num5 = (flag ? (95 - j) : j);
					if (num5 >= 0 && num5 < 96)
					{
						int num6 = i * 96 + num5;
						byte b = map.terrain[num6];
						if (b != 0 && b != 6 && b != 5 && b != 2 && !cold[num6])
						{
							map.terrain[num6] = 3;
						}
					}
				}
			}
		}

		private static void PlaceWasteland(VillageMap map, int seed, bool[] cold)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 6600;
			while (num < 110 && num2 < num3)
			{
				num2++;
				int num4 = 2 + Rng.Range(seed, 0, 14, 100000 + num2, 0, 2);
				int num5 = 2 + Rng.Range(seed, 0, 14, 200000 + num2, 0, 2);
				int num6 = Rng.Range(seed, 0, 14, num2, 0, 96 - num4);
				int num7 = Rng.Range(seed, 0, 14, 300000 + num2, 0, 64 - num5);
				bool flag = true;
				for (int i = num7; (i < num7 + num5) & flag; i++)
				{
					for (int j = num6; j < num6 + num4; j++)
					{
						int num8 = i * 96 + j;
						if (map.terrain[num8] != 1 || cold[num8])
						{
							flag = false;
							break;
						}
					}
				}
				if (!flag)
				{
					continue;
				}
				for (int k = num7; k < num7 + num5; k++)
				{
					for (int l = num6; l < num6 + num4; l++)
					{
						map.terrain[k * 96 + l] = 4;
					}
				}
				num++;
			}
		}

		private static bool IsCold(int y, int temperature)
		{
			int num = Math.Min(y, 63 - y);
			if (num > 10)
			{
				return false;
			}
			if (num <= 4)
			{
				return true;
			}
			return temperature < 190 - num * 20;
		}

		private static void ApplyIce(VillageMap map, bool[] cold)
		{
			for (int i = 0; i < map.terrain.Length; i++)
			{
				if (map.terrain[i] == 0)
				{
					int num = Math.Min(i / 96, 63 - i / 96);
					if (cold[i] || num <= 13)
					{
						map.terrain[i] = 6;
					}
				}
			}
		}

		private static void ClearVillageArea(VillageMap map, bool[] cold)
		{
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 96; j++)
				{
					int num = i * 96 + j;
					if (map.terrain[num] == 0 && Math.Abs(j - 23) + Math.Abs(i - 15) <= 10)
					{
						map.terrain[num] = (byte)((!cold[num]) ? 1 : 5);
					}
				}
			}
		}

		private static void FillIsolatedWater(VillageMap map, bool[] cold)
		{
			bool[] array = new bool[map.terrain.Length];
			int[] array2 = new int[map.terrain.Length];
			int[] array3 = new int[64];
			for (int i = 0; i < map.terrain.Length; i++)
			{
				if (array[i] || map.terrain[i] != 0)
				{
					continue;
				}
				int num = 0;
				bool flag = false;
				int num2 = 0;
				array2[num2++] = i;
				array[i] = true;
				while (num2 > 0)
				{
					int num3 = array2[--num2];
					if (num < array3.Length)
					{
						array3[num] = num3;
					}
					num++;
					int num4 = num3 % 96;
					int num5 = num3 / 96;
					if (num4 == 0 || num5 == 0 || num4 == 95 || num5 == 63)
					{
						flag = true;
					}
					for (int j = 0; j < 4; j++)
					{
						int num6 = num4 + j switch
						{
							1 => 1, 
							0 => -1, 
							_ => 0, 
						};
						int num7 = num5 + j switch
						{
							3 => 1, 
							2 => -1, 
							_ => 0, 
						};
						if (num6 >= 0 && num7 >= 0 && num6 < 96 && num7 < 64)
						{
							int num8 = num7 * 96 + num6;
							if (!array[num8] && map.terrain[num8] == 0)
							{
								array[num8] = true;
								array2[num2++] = num8;
							}
						}
					}
				}
				if (num < 4 && !flag && num <= array3.Length)
				{
					for (int k = 0; k < num; k++)
					{
						int num9 = array3[k];
						map.terrain[num9] = (byte)((!cold[num9]) ? 1 : 5);
					}
				}
			}
		}

		private static void EnsureGrassCoverage(VillageMap map, int seed)
		{
			int num = 1228;
			int num2 = Count(map, 1);
			for (int i = 0; i < 6144; i++)
			{
				if (num2 >= num)
				{
					break;
				}
				int num3 = (i * 73 + seed) & 0x7FFFFFFF;
				num3 %= 6144;
				if (map.terrain[num3] == 3)
				{
					map.terrain[num3] = 1;
					num2++;
				}
			}
		}

		private static void EnsureWaterCap(VillageMap map, int seed, bool[] cold)
		{
			int num = 1536;
			int num2 = Count(map, 0);
			for (int i = 0; i < 6144; i++)
			{
				if (num2 <= num)
				{
					break;
				}
				int num3 = (i * 47 + seed * 3) & 0x7FFFFFFF;
				num3 %= 6144;
				if (map.terrain[num3] == 0)
				{
					map.terrain[num3] = (byte)((!cold[num3]) ? 1 : 5);
					num2--;
				}
			}
		}

		private static void PlaceSpawns(VillageMap map, int seed)
		{
			int num = 47;
			int num2 = 31;
			for (int i = 0; i < 8; i++)
			{
				for (int j = 0; j < 10; j++)
				{
					int num3 = i * 10 + j;
					int num4 = Math.Max(0, Math.Min(95, num - 18 + j * 4));
					int tile = Math.Max(0, Math.Min(63, num2 - 14 + i * 4)) * 96 + num4;
					map.spawnTiles[num3] = FindNearestHabitable(map, tile);
				}
			}
			for (int k = 0; k < map.spawnTiles.Length; k++)
			{
				for (int l = k + 1; l < map.spawnTiles.Length; l++)
				{
					if (map.spawnTiles[k] == map.spawnTiles[l])
					{
						map.spawnTiles[l] = FindNearestFreeHabitable(map, map.spawnTiles[l], map.spawnTiles, l);
					}
				}
			}
		}

		private static int FindNearestHabitable(VillageMap map, int tile)
		{
			if (TerrainType.Habitable(map.terrain[tile]))
			{
				return tile;
			}
			int num = tile % 96;
			int num2 = tile / 96;
			for (int i = 1; i < 96; i++)
			{
				for (int j = Math.Max(0, num2 - i); j <= Math.Min(63, num2 + i); j++)
				{
					for (int k = Math.Max(0, num - i); k <= Math.Min(95, num + i); k++)
					{
						int num3 = j * 96 + k;
						if (TerrainType.Habitable(map.terrain[num3]))
						{
							return num3;
						}
					}
				}
			}
			return tile;
		}

		private static int FindNearestFreeHabitable(VillageMap map, int tile, int[] used, int usedCount)
		{
			int num = tile % 96;
			int num2 = tile / 96;
			for (int i = 1; i < 96; i++)
			{
				for (int j = Math.Max(0, num2 - i); j <= Math.Min(63, num2 + i); j++)
				{
					for (int k = Math.Max(0, num - i); k <= Math.Min(95, num + i); k++)
					{
						int num3 = j * 96 + k;
						if (!TerrainType.Habitable(map.terrain[num3]))
						{
							continue;
						}
						bool flag = false;
						for (int l = 0; l < usedCount; l++)
						{
							if (used[l] == num3)
							{
								flag = true;
								break;
							}
						}
						if (!flag)
						{
							return num3;
						}
					}
				}
			}
			return tile;
		}

		private static void CountTerrain(VillageMap map)
		{
			map.arableCount = 0;
			map.habitableCount = 0;
			map.waterCount = 0;
			for (int i = 0; i < map.terrain.Length; i++)
			{
				if (TerrainType.Arable(map.terrain[i]))
				{
					map.arableCount++;
				}
				if (TerrainType.Habitable(map.terrain[i]))
				{
					map.habitableCount++;
				}
				if (TerrainType.Liquid(map.terrain[i]))
				{
					map.waterCount++;
				}
			}
		}

		private static int Count(VillageMap map, byte terrain)
		{
			int num = 0;
			for (int i = 0; i < map.terrain.Length; i++)
			{
				if (map.terrain[i] == terrain)
				{
					num++;
				}
			}
			return num;
		}
	}

	public static class VillageEvents
	{
		private const int EventCount = 14;

		private const int CheckPeriod = 1800;

		private const int TriggerPercent = 45;

		private const int OmenTicks = 2920;

		private const int CooldownTicks = 8760;

		private const int OmenSlots = 3;

		private const int ChannelPick = 61;

		private const int ChannelRoll = 62;

		private static readonly string[] Names = new string[14]
		{
			"蝗灾", "疫病", "边患", "民变", "贪墨", "寒潮", "匪患", "丰年", "商队", "乡绅",
			"人口回流", "名医", "大赦", "新生儿潮"
		};

		private static readonly string[] Omens = new string[14]
		{
			"田里的蚂蚱比往年多。", "村口有人咳了整夜。", "边境的消息传了回来。", "有人在夜里聚众说话。", "账册上的数目对不上。", "北边的风一天比一天冷。", "附近有生人打探村子的虚实。", "今年的雨水来得正好。", "远处的商队朝这边来了。", "有乡绅递了帖子。",
			"逃出去的人开始往回看。", "有位游方郎中路过。", "牢里的人在等一句话。", "村里多了几件小衣裳。"
		};

		private static readonly string[] Bursts = new string[14]
		{
			"蝗群落下，半年的粮没了。", "疫病带走了几个孩子。", "边患一起，村里死了人。", "民变被压下去，威信大跌。", "贪墨案发，税库少了一截。", "寒潮压境，粮仓见底。", "匪患掠过，村民的金币被抢。", "丰年，粮仓满了。", "商队成交，粮与钱都动了。", "乡绅捐了粮，也要了名声。",
			"流民回村，人多了起来。", "名医坐诊，村里安稳了。", "大赦令下，恐惧退了些。", "村里迎来一波新生儿。"
		};

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
			if (data.eventOmenId < -1 || data.eventOmenId >= 14)
			{
				data.eventOmenId = -1;
			}
			if (data.omenEventIds == null || data.omenEventIds.Length != 3)
			{
				int num2 = ((data.omenEventIds != null) ? data.omenEventIds.Length : 0);
				Array.Resize(ref data.omenEventIds, 3);
				for (int j = num2; j < 3; j++)
				{
					data.omenEventIds[j] = -1;
				}
			}
			if (data.omenBurstTicks == null || data.omenBurstTicks.Length != 3)
			{
				int num3 = ((data.omenBurstTicks != null) ? data.omenBurstTicks.Length : 0);
				Array.Resize(ref data.omenBurstTicks, 3);
				for (int k = num3; k < 3; k++)
				{
					data.omenBurstTicks[k] = 0;
				}
			}
			for (int l = 0; l < 3; l++)
			{
				if (data.omenEventIds[l] < 0 || data.omenEventIds[l] >= 14)
				{
					data.omenEventIds[l] = -1;
				}
			}
		}

		public static int Tick(VillageSaveData data, int tick, ref string eventText)
		{
			if (data == null || tick <= 0)
			{
				return 0;
			}
			Ensure(data);
			int num = 0;
			for (int i = 0; i < 3; i++)
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
				return num;
			}
			if (tick % 1800 != 0)
			{
				return 0;
			}
			if (FreeSlot(data) < 0)
			{
				return 0;
			}
			if (Cfg.NativeEventChance <= 0 || Rng.Range(data.rngSeed, tick, 62, 0, 0, 100) >= Cfg.NativeEventChance)
			{
				return 0;
			}
			int num3 = (Cfg.ForceEvent >= 0) ? Cfg.ForceEvent : Pick(data, tick);
			if (num3 < 0)
			{
				return 0;
			}
			int num4 = FreeSlot(data);
			if (num4 < 0)
			{
				return 0;
			}
			data.omenEventIds[num4] = num3;
			data.omenBurstTicks[num4] = tick + 2920;
			eventText = Omens[num3];
			return 0;
		}

		private static int Pick(VillageSaveData data, int tick)
		{
			int[] array = new int[14];
			int num = 0;
			for (int i = 0; i < 14; i++)
			{
				if (tick - data.eventLastFire[i] >= CooldownFor(i))
				{
					int num2 = Weight(data, i, tick);
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

		private static int CooldownFor(int id)
		{
			return id switch
			{
				1 => 1460, 
				3 => 2920, 
				_ => 8760, 
			};
		}

		private static int Weight(VillageSaveData data, int id, int tick)
		{
			int num = VillageLifecycleService.CountAlive(data);
			int num2 = AverageHunger(data);
			int num3 = AverageFear(data);
			return id switch
			{
				0 => 1 + num2 / 4, 
				1 => 1 + num / 15 + ((Cfg.ChainMode == 1 && tick < data.chainPlague) ? 30 : 0), 
				2 => 3, 
				3 => 1 + (300 - data.prestige) / 12 + num3 / 3 + ((Cfg.ChainMode == 1 && tick < data.chainMobilize) ? 60 : 0), 
				4 => 1 + data.treasuryGold / 100, 
				5 => 3, 
				6 => 1 + PoorCount(data) / 5, 
				7 => 1 + Math.Max(0, 40 - num2) / 4, 
				8 => 1 + data.treasuryGold / 60, 
				9 => 1 + data.prestige / 100, 
				10 => 1 + Math.Max(0, 50 - num) / 10, 
				11 => 1 + data.treasuryGold / 80, 
				12 => 1 + data.prestige / 120 + num3 / 3, 
				_ => 1 + Math.Max(0, 15 - num3) + data.granaryFood / 200, 
			};
		}

		private static void Fire(VillageSaveData data, int id, int tick)
		{
			Cfg.EventFired[id]++;
			bool flag = Role(data, 1u);
			bool flag2 = Role(data, 2u);
			bool flag3 = Role(data, 4u);
			bool flag4 = Role(data, 8u);
			bool flag5 = Role(data, 16u);
			bool flag6 = Role(data, 32u);
			bool flag7 = Role(data, 64u);
			bool flag8 = Role(data, 128u);
			int pct = LawPct(data, id);
			switch (id)
			{
			case 0:
				int _g0 = data.granaryFood;
				data.granaryFood = ClampGranary(data, data.granaryFood - Sk(data.granaryFood * (flag ? 30 : 60) / 100, pct));
				Cfg.EventGranary[0] += _g0 - data.granaryFood;
				break;
			case 1:
				KillSome(data, Sk(flag2 ? 1 : 3, pct), infants: true, id);
				AddFear(data, Sk(flag2 ? 8 : 15, pct));
				break;
			case 2:
				if (!flag3)
				{
					if (data.treasuryGold >= 100)
					{
						data.treasuryGold -= Sk(100, pct);
						break;
					}
					KillSome(data, Sk(2, pct), infants: false, id);
					AddFear(data, Sk(25, pct));
				}
				break;
			case 3:
				KillSome(data, Sk(flag4 ? 1 : 3, pct), infants: false, id);
				data.prestige = Math.Max(0, data.prestige - Sk(30, pct));
				StealFood(data, 30);
				break;
			case 4:
				data.treasuryGold = Math.Max(0, data.treasuryGold - Sk(flag5 ? 75 : 150, pct));
				data.prestige = Math.Max(0, data.prestige - Sk(25, pct));
				break;
			case 5:
				int _g5 = data.granaryFood;
				data.granaryFood = ClampGranary(data, data.granaryFood - Sk(data.granaryFood * (flag6 ? 10 : 20) / 100, pct));
				Cfg.EventGranary[5] += _g5 - data.granaryFood;
				StealFood(data, 20);
				break;
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
				AddFear(data, Sk(12, pct));
				StealFood(data, Sk(50, pct));
				break;
			}
			case 7:
				data.granaryFood = ClampGranary(data, data.granaryFood + Sk(flag ? 180 : 120, pct));
				FillFood(data, 4);
				break;
			case 8:
				data.treasuryGold = Math.Max(0, data.treasuryGold - Sk(50, pct));
				data.granaryFood = ClampGranary(data, data.granaryFood + Sk(110 + (flag7 ? 60 : 0), pct));
				break;
			case 9:
				data.prestige = Math.Max(0, data.prestige - Sk(flag4 ? 15 : 30, pct));
				data.granaryFood = ClampGranary(data, data.granaryFood + Sk(100, pct));
				break;
			case 10:
			{
				for (int j = 0; j < Sk(HasSettler(data) ? 6 : 4, pct); j++)
				{
					VillageLifecycleService.AppendImmigrant(data);
				}
				break;
			}
			case 11:
				if (!flag2)
				{
					data.treasuryGold = Math.Max(0, data.treasuryGold - Sk(80, pct));
				}
				AddFear(data, SkSigned(-10, pct));
				break;
			case 12:
				AddFear(data, SkSigned(HasShaman(data) ? (-40) : (-30), pct));
				data.prestige = Math.Max(0, data.prestige - 40);
				break;
			default:
				data.birthBoomUntilTick = tick + 5840;
				data.birthBoomPercent = (flag8 ? 200 : 150);
				AddFear(data, SkSigned(-10, pct));
				break;
			}
			if (id == 0 || id == 5)
			{
				data.chainMobilize = tick + 5840;
				QueueOmen(data, 3, tick + 2920);
			}
			if (id == 1)
			{
				data.chainPlague = tick + 2920;
			}
		}

		private static int FreeSlot(VillageSaveData data)
		{
			for (int i = 0; i < 3; i++)
			{
				if (data.omenEventIds[i] < 0)
				{
					return i;
				}
			}
			return -1;
		}

		private static void QueueOmen(VillageSaveData data, int id, int burstTick)
		{
			int num = FreeSlot(data);
			if (num >= 0)
			{
				data.omenEventIds[num] = id;
				data.omenBurstTicks[num] = burstTick;
			}
		}

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

		private static int LawPct(VillageSaveData data, int id)
		{
			int pct = 100;
			switch (id)
			{
			case 0:
				pct = Avec(data, pct, 5, 50);
				return Avec(data, pct, 71, 50);
			case 1:
				pct = Avec(data, pct, 12, 50);
				return Avec(data, pct, 13, 40);
			case 2:
				pct = Avec(data, pct, 15, 0);
				return Avec(data, pct, 48, 150);
			case 3:
				pct = Avec(data, pct, 6, 50);
				pct = Avec(data, pct, 1, 70);
				return Avec(data, pct, 16, 150);
			case 4:
				return Avec(data, pct, 14, 50);
			case 5:
				return Avec(data, pct, 30, 50);
			case 6:
				pct = Avec(data, pct, 10, 40);
				pct = Avec(data, pct, 25, 0);
				return Avec(data, pct, 2, 150);
			case 7:
				return Avec(data, pct, 5, 150);
			case 8:
				pct = Avec(data, pct, 29, 150);
				pct = Avec(data, pct, 31, 150);
				return Avec(data, pct, 14, 60);
			case 9:
				return Avec(data, pct, 28, 150);
			case 10:
				return Avec(data, pct, 17, 150);
			case 11:
				return Avec(data, pct, 12, 0);
			case 12:
				pct = Avec(data, pct, 35, 150);
				return Avec(data, pct, 6, 130);
			default:
				pct = Avec(data, pct, 8, 150);
				return Avec(data, pct, 9, 130);
			}
		}

		private static int SkSigned(int value, int pct)
		{
			return value * pct / 100;
		}

		private static int Sk(int value, int pct)
		{
			if (pct <= 0)
			{
				return 0;
			}
			int num = value * pct / 100;
			if (num >= 1)
			{
				return num;
			}
			return 1;
		}

		private static bool Role(VillageSaveData data, uint role)
		{
			return VillagerProfessions.RoleCount(data, role) > 0;
		}

		private static bool HasShaman(VillageSaveData data)
		{
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i] && Traits.HasMark(data, i, 32768u))
				{
					return true;
				}
			}
			return false;
		}

		private static bool HasSettler(VillageSaveData data)
		{
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i] && Traits.HasMark(data, i, 128u))
				{
					return true;
				}
			}
			return false;
		}

		private static void StealFood(VillageSaveData data, int pct)
		{
			if (pct <= 0)
			{
				return;
			}
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i])
				{
					data.villagerFood[i] -= data.villagerFood[i] * pct / 100;
					if (data.villagerFood[i] < 0)
					{
						data.villagerFood[i] = 0;
					}
				}
			}
		}

		private static void FillFood(VillageSaveData data, int add)
		{
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i])
				{
					data.villagerFood[i] = Math.Min(40, data.villagerFood[i] + add);
				}
			}
		}

		private static void KillSome(VillageSaveData data, int count, bool infants, int eventId)
		{
			ObitRules.AttributeEvent(data, eventId);
			for (int i = 0; i < count; i++)
			{
				int num = -1;
				for (int j = 0; j < data.villagerAlive.Length; j++)
				{
					if (!data.villagerAlive[j])
					{
						continue;
					}
					bool num2;
					if (!infants)
					{
						if (data.villagerAges[j] < 18)
						{
							continue;
						}
						num2 = data.villagerAges[j] < 60;
					}
					else
					{
						num2 = data.villagerAges[j] < 3;
					}
					if (num2 && (num < 0 || data.villagerHunger[j] > data.villagerHunger[num]))
					{
						num = j;
					}
				}
				if (num >= 0)
				{
					VillageLifecycleService.KillByLaw(data, num, "天灾", -1);
					continue;
				}
				break;
			}
		}

		private static void AddFear(VillageSaveData data, int delta)
		{
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i])
				{
					data.villagerFear[i] = Math.Max(0, Math.Min(99, data.villagerFear[i] + delta));
				}
			}
		}

		private static int ClampGranary(VillageSaveData data, int value)
		{
			return Math.Max(0, Math.Min(data.granaryCapacity, value));
		}

		private static int AverageHunger(VillageSaveData data)
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i])
				{
					num += data.villagerHunger[i];
					num2++;
				}
			}
			if (num2 <= 0)
			{
				return 0;
			}
			return num / num2;
		}

		private static int AverageFear(VillageSaveData data)
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i])
				{
					num += data.villagerFear[i];
					num2++;
				}
			}
			if (num2 <= 0)
			{
				return 0;
			}
			return num / num2;
		}

		private static int PoorCount(VillageSaveData data)
		{
			int num = 0;
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i] && data.villagerGold[i] < 10)
				{
					num++;
				}
			}
			return num;
		}
	}

	public static class VillagerProfessions
	{
		private const int BitterTicks = 730;

		public const uint RoleFarmer = 1u;

		public const uint RoleDoctor = 2u;

		public const uint RoleRanger = 4u;

		public const uint RoleElder = 8u;

		public const uint RoleClerk = 16u;

		public const uint RoleLaborer = 32u;

		public const uint RoleMerchant = 64u;

		public const uint RoleMidwife = 128u;

		public static int RoleCount(VillageSaveData data, uint role)
		{
			Ensure(data);
			int num = 0;
			for (int i = 0; i < data.villagerAlive.Length && i < data.villagerRoles.Length; i++)
			{
				if (data.villagerAlive[i] && (data.villagerRoles[i] & role) != 0)
				{
					num++;
				}
			}
			return num;
		}

		public static void Ensure(VillageSaveData data)
		{
			int length = ((data.villagerAlive != null) ? data.villagerAlive.Length : 0);
			EnsureArray(ref data.villagerNeighborDeaths, length, 0);
			EnsureArray(ref data.villagerDonations, length, 0);
			EnsureArray(ref data.villagerChildrenAlive, length, 0);
			EnsureArray(ref data.villagerMother, length, -1);
			EnsureArray(ref data.villagerFather, length, -1);
			EnsureArray(ref data.villagerWealthPeak, length, 0);
			EnsureArray(ref data.villagerHungryTicks, length, 0);
			EnsureArray(ref data.villagerRoles, length, 0u);
		}

		private static void EnsureArray(ref uint[] values, int length, uint fill)
		{
			if (values == null)
			{
				values = new uint[length];
				for (int i = 0; i < length; i++)
				{
					values[i] = fill;
				}
			}
			else if (values.Length != length)
			{
				int num = values.Length;
				Array.Resize(ref values, length);
				for (int j = num; j < length; j++)
				{
					values[j] = fill;
				}
			}
		}

		private static void EnsureArray(ref int[] values, int length, int fill)
		{
			if (values == null)
			{
				values = new int[length];
				for (int i = 0; i < length; i++)
				{
					values[i] = fill;
				}
			}
			else if (values.Length != length)
			{
				int num = values.Length;
				Array.Resize(ref values, length);
				for (int j = num; j < length; j++)
				{
					values[j] = fill;
				}
			}
		}

		public static void ResetSlot(VillageSaveData data, int index)
		{
			if (data != null && index >= 0)
			{
				Ensure(data);
				if (index < data.villagerNeighborDeaths.Length)
				{
					data.villagerNeighborDeaths[index] = 0;
					data.villagerDonations[index] = 0;
					data.villagerChildrenAlive[index] = 0;
					data.villagerMother[index] = -1;
					data.villagerFather[index] = -1;
					data.villagerWealthPeak[index] = 0;
					data.villagerHungryTicks[index] = 0;
					data.villagerRoles[index] = 0u;
				}
			}
		}

		public static void OnBirth(VillageSaveData data, int child, int mother, int father)
		{
			Ensure(data);
			if (child >= 0 && child < data.villagerMother.Length)
			{
				data.villagerMother[child] = mother;
				data.villagerFather[child] = father;
				if (mother >= 0 && mother < data.villagerChildrenAlive.Length)
				{
					data.villagerChildrenAlive[mother]++;
				}
				if (father >= 0 && father < data.villagerChildrenAlive.Length)
				{
					data.villagerChildrenAlive[father]++;
				}
			}
		}

		public static void OnImmigrant(VillageSaveData data, int index)
		{
			if (data != null && index >= 0 && index < data.villagerAlive.Length && data.villagerAlive[index])
			{
				ResetSlot(data, index);
				Traits.AddMark(data, index, 128u);
			}
		}

		public static void OnDeath(VillageSaveData data, int index, string cause, int lawIndex)
		{
			if (data == null)
			{
				return;
			}
			Ensure(data);
			for (int i = -1; i <= 1; i += 2)
			{
				int num = index + i;
				if (num >= 0 && num < data.villagerAlive.Length && data.villagerAlive[num])
				{
					data.villagerNeighborDeaths[num]++;
				}
			}
			if (index >= 0 && index < data.villagerMother.Length)
			{
				int num2 = data.villagerMother[index];
				int num3 = data.villagerFather[index];
				if (num2 >= 0 && num2 < data.villagerChildrenAlive.Length && data.villagerChildrenAlive[num2] > 0)
				{
					data.villagerChildrenAlive[num2]--;
				}
				if (num3 >= 0 && num3 < data.villagerChildrenAlive.Length && data.villagerChildrenAlive[num3] > 0)
				{
					data.villagerChildrenAlive[num3]--;
				}
			}
			if (lawIndex >= 0 || cause == "律法")
			{
				data.executions++;
			}
		}

		public static void OnLawFired(VillageSaveData data, int index, int lawId)
		{
			if (data != null && lawId == 28)
			{
				Ensure(data);
				if (index >= 0 && index < data.villagerDonations.Length)
				{
					data.villagerDonations[index]++;
				}
			}
		}

		public static void Scan(VillageSaveData data, int tick)
		{
			if (data == null || data.villagerAlive == null)
			{
				return;
			}
			Ensure(data);
			int num = 0;
			for (int i = 0; i < data.villagerDonations.Length && i < data.villagerAlive.Length; i++)
			{
				num += data.villagerDonations[i];
			}
			for (int j = 0; j < data.villagerAlive.Length; j++)
			{
				if (data.villagerAlive[j])
				{
					int num2 = data.villagerAges[j];
					int num3 = data.villagerHunger[j];
					int num4 = data.villagerFear[j];
					int num5 = data.villagerGold[j];
					if (num5 > data.villagerWealthPeak[j])
					{
						data.villagerWealthPeak[j] = num5;
					}
					if (num3 > 50)
					{
						data.villagerHungryTicks[j] += 60;
					}
					else
					{
						data.villagerHungryTicks[j] = 0;
					}
					if (HasLaw(data, 7) && num4 > 40)
					{
						Traits.AddMark(data, j, 1u);
					}
					if (HasLaw(data, 2) && num3 > Cfg.ProfBanditHunger)
					{
						Traits.AddMark(data, j, 2u);
					}
					if (HasLaw(data, 15) && num2 >= 14 && num2 <= 40 && num4 > 25)
					{
						Traits.AddMark(data, j, 4u);
					}
					if (HasLaw(data, 11) && num2 >= 40 && num3 > 56)
					{
						Traits.AddMark(data, j, 1024u);
					}
					if (HasLaw(data, 35) && num4 > 30)
					{
						Traits.AddMark(data, j, 32768u);
					}
					if ((HasLaw(data, 26) || HasLaw(data, 27)) && num2 >= 25 && num5 <= 0 && data.villagerWealthPeak[j] >= 30)
					{
						Traits.AddMark(data, j, 64u);
					}
					if (num >= 3 && data.villagerDonations[j] > 0)
					{
						Traits.AddMark(data, j, 16384u);
					}
					if (num2 >= 45 && data.villagerChildren[j] > 0 && data.villagerChildrenAlive[j] == 0)
					{
						Traits.AddMark(data, j, 8u);
					}
					if (num2 >= 50 && data.villagerChildren[j] == 0)
					{
						Traits.AddMark(data, j, 32u);
					}
					if (data.villagerHungryTicks[j] >= 730)
					{
						Traits.AddMark(data, j, 2048u);
					}
					if (data.villagerNeighborDeaths[j] >= 15)
					{
						Traits.AddMark(data, j, 8192u);
					}
					uint num6 = data.villagerRoles[j];
					if (num2 >= 16 && num2 < 60 && !Traits.HasMark(data, j, 1u) && !Traits.HasMark(data, j, 2u) && (Cfg.FarmerMode == 0 || Traits.HasInnate(data, j, 16u)))
					{
						num6 |= 1;
					}
					if (HasLaw(data, 12) && num2 >= 25)
					{
						num6 |= 2;
					}
					if (HasLaw(data, 15) && num2 >= 18)
					{
						num6 |= 4;
					}
					if (HasLaw(data, 3) && num2 >= 25)
					{
						num6 |= 0x10;
					}
					if (HasLaw(data, 11) && num2 >= 18 && num2 <= 50)
					{
						num6 |= 0x20;
					}
					if (HasLaw(data, 51) && num2 >= 25)
					{
						num6 |= 0x40;
					}
					if (HasLaw(data, 8) && data.villagerGenders[j] == 0 && num2 >= 30)
					{
						num6 |= 0x80;
					}
					if (Traits.HasMark(data, j, 16384u) && num2 >= 40)
					{
						num6 |= 8;
					}
					data.villagerRoles[j] = num6;
				}
			}
			if (data.executions <= 0)
			{
				return;
			}
			bool flag = false;
			for (int k = 0; k < data.villagerAlive.Length; k++)
			{
				if (data.villagerAlive[k] && Traits.HasMark(data, k, 4096u))
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				return;
			}
			int num7 = -1;
			int num8 = int.MaxValue;
			for (int l = 0; l < data.villagerAlive.Length; l++)
			{
				if (data.villagerAlive[l] && data.villagerAges[l] >= 18 && data.villagerFear[l] < num8)
				{
					num8 = data.villagerFear[l];
					num7 = l;
				}
			}
			if (num7 >= 0)
			{
				Traits.AddMark(data, num7, 4096u);
			}
		}

		private static bool HasLaw(VillageSaveData data, int lawId)
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
	}

	public static class ObitRules
	{
		public const byte KindNone = 0;

		public const byte KindCaused = 1;

		public const byte KindMissing = 2;

		public static byte LastKind;

		public static string LastName = string.Empty;

		private static readonly int[] HungerHarm = new int[11]
		{
			11, 27, 31, 37, 39, 44, 45, 48, 61, 63,
			3
		};

		private static readonly int[] HungerHelp = new int[8] { 1, 26, 29, 46, 51, 52, 71, 5 };

		private static readonly int[] FearHarm = new int[8] { 4, 7, 16, 34, 50, 60, 62, 2 };

		private static readonly int[] FearHelp = new int[8] { 6, 10, 13, 18, 22, 23, 25, 35 };

		private static readonly int[] InfantHarm = new int[4] { 54, 47, 65, 39 };

		private static readonly int[] InfantHelp = new int[5] { 9, 12, 13, 8, 52 };

		private static readonly int[] PlagueHelp = new int[2] { 12, 13 };

		private static readonly int[] BorderHelp = new int[2] { 15, 25 };

		private static readonly int[] RiotHelp = new int[2] { 6, 1 };

		private static readonly int[] BanditHelp = new int[2] { 10, 25 };

		public static int AttributeSlot(VillageSaveData data, string cause)
		{
			LastKind = 0;
			LastName = string.Empty;
			return cause switch
			{
				"饥饿" => Scan(data, HungerHarm, HungerHelp), 
				"惊吓" => Scan(data, FearHarm, FearHelp), 
				"夭折" => Scan(data, InfantHarm, InfantHelp), 
				_ => -1, 
			};
		}

		public static void AttributeEvent(VillageSaveData data, int eventId)
		{
			LastKind = 0;
			LastName = string.Empty;
			int[] array;
			switch (eventId)
			{
			default:
				return;
			case 1:
				array = PlagueHelp;
				break;
			case 2:
				array = BorderHelp;
				break;
			case 3:
				array = RiotHelp;
				break;
			case 6:
				array = BanditHelp;
				break;
			case 4:
			case 5:
				return;
			}
			for (int i = 0; i < array.Length; i++)
			{
				if (SlotOf(data, array[i]) < 0)
				{
					LawDefinition lawDefinition = LawCatalog.FindById(array[i]);
					LastKind = 2;
					LastName = ((lawDefinition != null) ? lawDefinition.name : string.Empty);
					break;
				}
			}
		}

		private static int Scan(VillageSaveData data, int[] harm, int[] help)
		{
			for (int i = 0; i < harm.Length; i++)
			{
				int num = SlotOf(data, harm[i]);
				if (num >= 0)
				{
					LastKind = 1;
					LastName = data.lawNames[num];
					return num;
				}
			}
			for (int j = 0; j < help.Length; j++)
			{
				if (SlotOf(data, help[j]) < 0)
				{
					LawDefinition lawDefinition = LawCatalog.FindById(help[j]);
					LastKind = 2;
					LastName = ((lawDefinition != null) ? lawDefinition.name : string.Empty);
					return -1;
				}
			}
			return -1;
		}

		private static int SlotOf(VillageSaveData data, int lawId)
		{
			if (data.prestigeSuspended || data.lawIds == null)
			{
				return -1;
			}
			for (int i = 0; i < data.lawIds.Length; i++)
			{
				if (data.lawIds[i] == lawId)
				{
					return i;
				}
			}
			return -1;
		}

		public static string Build(VillageSaveData data, int villagerIndex, string cause, int lawIndex, byte kind, string lawName)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(data.villagerNames[villagerIndex]).Append("，").Append(data.villagerAges[villagerIndex])
				.Append(" 岁。\n");
			int num = VillageTime.ToYear(data.tick) + 1;
			int num2 = VillageTime.ToMonth(data.tick);
			string value = num + "年" + num2 + "月";
			if (cause == "夭折")
			{
				stringBuilder.Append("死于").Append(value).Append("。还没满三岁。\n");
			}
			else if (cause == "寿终")
			{
				stringBuilder.Append("死于").Append(value).Append("，寿终。\n");
			}
			else
			{
				stringBuilder.Append("死于").Append(value).Append("，")
					.Append(cause)
					.Append("。\n");
			}
			if (kind == 1 && !string.IsNullOrEmpty(lawName))
			{
				string[] array = new string[3]
				{
					"是你写的『" + lawName + "』。",
					"第 " + (lawIndex + 1) + " 条法『" + lawName + "』，是他没熬过去的原因。",
					"他死在你立的『" + lawName + "』之下。"
				};
				stringBuilder.Append("<color=#C94F43>").Append(array[(villagerIndex * 31 + data.tick) % array.Length]).Append("</color>\n");
			}
			else if (kind == 2 && !string.IsNullOrEmpty(lawName))
			{
				string[] array2 = new string[3]
				{
					"村里没有『" + lawName + "』。",
					"如果能早一点立『" + lawName + "』……",
					"没有『" + lawName + "』，这样的孩子撑不过冬天。"
				};
				stringBuilder.Append("<color=#7C9BB8>").Append(array2[(villagerIndex * 31 + data.tick) % array2.Length]).Append("</color>\n");
			}
			else
			{
				string[] array3 = new string[2] { "没有一条法能替他负责。", "他死于没有法的地方。" };
				stringBuilder.Append("<color=#9A9388>").Append(array3[(villagerIndex * 31 + data.tick) % array3.Length]).Append("</color>");
			}
			string value2 = PersonalLine(data, villagerIndex);
			if (!string.IsNullOrEmpty(value2))
			{
				stringBuilder.Append('\n').Append(value2);
			}
			return stringBuilder.ToString();
		}

		private static string PersonalLine(VillageSaveData data, int i)
		{
			if (i >= data.villagerChildrenAlive.Length)
			{
				return string.Empty;
			}
			if (data.villagerChildren[i] > 0 && data.villagerChildrenAlive[i] == 0)
			{
				return "他没能留下一个孩子。";
			}
			if (data.villagerNeighborDeaths[i] >= 15)
			{
				return "他送走过 " + data.villagerNeighborDeaths[i] + " 个邻居。";
			}
			if (data.villagerDonations[i] >= 3)
			{
				return "他给村里捐过 " + data.villagerDonations[i] + " 次。";
			}
			if (data.villagerHungryTicks[i] >= 730)
			{
				return "他饿了很久。";
			}
			if (Traits.HasMark(data, i, 2u))
			{
				return "他抢过别人的粮。";
			}
			if (Traits.HasMark(data, i, 8192u))
			{
				return "他替别人送过葬。";
			}
			if (Traits.HasMark(data, i, 128u))
			{
				return "他是从外面来的。";
			}
			if (Traits.HasMark(data, i, 4096u))
			{
				return "他动过手。";
			}
			if (Traits.HasMark(data, i, 32768u))
			{
				return "他替村子求过神。";
			}
			return string.Empty;
		}
	}

	public static class RunRules
	{
		public const int ModeFifty = 0;

		public const int ModePressure = 1;

		public const int EndRunning = 0;

		public const int EndClear = 1;

		public const int EndExtinct = 2;

		public const int EndDismissed = 3;

		public const int EndTimeout = 4;

		public const int MonthTicks = 240;

		public const int FiftyYearTicks = 146104;

		public const int PressureTarget = 45;

		public const int PressureHoldMonths = 240;

		public const int PressureKill = 20;

		public const int PressureKillMonths = 6;

		public static void Tick(VillageSaveData data, int tick)
		{
			if (data == null || data.endKind != 0)
			{
				return;
			}
			if (VillageLifecycleService.CountAlive(data) <= 0)
			{
				data.endKind = 2;
				data.grade = 0;
			}
			else
			{
				if (data.runMode < 0)
				{
					return;
				}
				int num = VillageLifecycleService.CountAlive(data);
				int num2 = AverageHunger(data);
				data.personMonths += num;
				data.hungerSum += num2;
				data.monthSamples++;
				if (num2 >= 40)
				{
					data.famineMonths++;
				}
				if (data.runMode == 1)
				{
					int num3 = HealthyPop(data) * 2;
					data.vStreak = ((num3 >= 45) ? (data.vStreak + 1) : 0);
					data.lowStreak = ((num3 < 20) ? (data.lowStreak + 1) : 0);
					if (data.vStreak >= 240)
					{
						data.endKind = 1;
						data.clearMonth = tick;
						data.grade = Grade(data);
						return;
					}
					if (data.lowStreak >= 6)
					{
						data.endKind = 3;
						data.grade = 0;
						return;
					}
				}
				else
				{
					data.tinyStreak = ((num < 5) ? (data.tinyStreak + 1) : 0);
					if (num <= 2)
					{
						data.endKind = 2;
						data.grade = 0;
						return;
					}
					if (data.tinyStreak >= 24)
					{
						data.endKind = 5;
						data.grade = 0;
						return;
					}
				}
				if (tick >= 146104)
				{
					data.endKind = 4;
					data.grade = Grade(data);
				}
			}
		}

		public static int TotalScore(VillageSaveData data)
		{
			int num = Clamp(data.personMonths * 100 / 24000, 0, 100);
			int num2 = (int)((data.monthSamples > 0) ? (data.hungerSum / data.monthSamples) : 0);
			int num3 = Clamp(100 - num2 * 2 - data.famineMonths / 2, 0, 100);
			return (num * 70 + num3 * 30) / 100;
		}

		public static int Grade(VillageSaveData data)
		{
			int num = TotalScore(data);
			if (num >= 85)
			{
				return 5;
			}
			if (num >= 67)
			{
				return 4;
			}
			if (num >= 58)
			{
				return 3;
			}
			if (num >= 40)
			{
				return 2;
			}
			return 1;
		}

		public static string GradeLetter(int grade)
		{
			return grade switch
			{
				5 => "S", 
				4 => "A", 
				3 => "B", 
				2 => "C", 
				1 => "D", 
				_ => "F", 
			};
		}

		public static int HealthyPop(VillageSaveData data)
		{
			int num = 0;
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i] && data.villagerHunger[i] < 50 && data.villagerFear[i] < 50)
				{
					num++;
				}
			}
			return num;
		}

		private static int AverageHunger(VillageSaveData data)
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < data.villagerAlive.Length; i++)
			{
				if (data.villagerAlive[i])
				{
					num += data.villagerHunger[i];
					num2++;
				}
			}
			if (num2 <= 0)
			{
				return 0;
			}
			return num / num2;
		}

		private static int Clamp(int value, int min, int max)
		{
			if (value >= min)
			{
				if (value <= max)
				{
					return value;
				}
				return max;
			}
			return min;
		}
	}

	public static class LawAttribution
	{
		public static bool IsPunitive(int lawId)
		{
			switch (lawId)
			{
			case 2:
			case 4:
			case 7:
			case 10:
			case 11:
			case 15:
			case 16:
			case 32:
			case 34:
			case 36:
			case 37:
			case 40:
			case 41:
			case 44:
			case 45:
			case 47:
			case 48:
			case 50:
			case 53:
			case 54:
			case 60:
			case 62:
			case 63:
			case 64:
			case 65:
			case 66:
			case 67:
			case 68:
				return true;
			default:
				return false;
			}
		}
	}
