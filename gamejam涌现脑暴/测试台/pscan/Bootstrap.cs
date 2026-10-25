using System;
using System.Globalization;

public static class Bootstrap {
private static readonly string[] GeneratedSurnames = new string[31]
		{
			"沈", "阿", "林", "秦", "孟", "赵", "周", "吴", "陶", "陈",
			"孙", "李", "韩", "顾", "严", "谢", "马", "叶", "罗", "丁",
			"郑", "王", "冯", "褚", "卫", "蒋", "潘", "杜", "戴", "夏",
			"钟"
		};

private static readonly string[] GeneratedGivens = new string[41]
		{
			"石", "禾", "娘", "土", "秋", "慎", "朴", "犁", "安", "盐",
			"渠", "井", "麦", "仓", "火", "雨", "桥", "灯", "桑", "槐",
			"砺", "竹", "岩", "溪", "舟", "陌", "葵", "岑", "柏", "泉",
			"野", "岭", "川", "汀", "槲", "芜", "苕", "柘", "榆", "桐",
			"芷"
		};

public static string GetVillagerName(int seed, int serial)
		{
			int num = GeneratedSurnames.Length * GeneratedGivens.Length;
			if (serial < 0)
			{
				serial = 0;
			}
			if (num <= 0)
			{
				return "村民";
			}
			int num2 = (int)((uint)seed % (uint)num);
			int i = seed / 7 % num;
			if (i < 0)
			{
				i = -i;
			}
			for (; i % GeneratedSurnames.Length == 0 || i % GeneratedGivens.Length == 0; i++)
			{
			}
			if (i <= 0)
			{
				i = 1;
			}
			int num3 = (int)((num2 + (long)serial * (long)i) % num);
			if (num3 < 0)
			{
				num3 += num;
			}
			return GeneratedSurnames[num3 % GeneratedSurnames.Length] + GeneratedGivens[num3 / GeneratedSurnames.Length];
		}

public static VillageSaveData CreateNewGame(int slot, int seed)
		{
			int slot2 = Mathf.Max(0, slot);
			string text = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
			VillageSaveData villageSaveData = new VillageSaveData
			{
				version = 17,
				slot = slot2,
				villageName = "立法村",
				createdAtUtc = text,
				updatedAtUtc = text,
				tick = 0,
				prestige = 100,
				rngSeed = seed,
				speedMultiplier = 1,
				tyrantMode = false,
				lastEventText = "开局已建立。你只写法律，村民自行决定如何活下去。"
			};
			int num = 24;
			villageSaveData.villagerNames = new string[num];
			villageSaveData.villagerGenders = new int[num];
			villageSaveData.villagerAges = new int[num];
			villageSaveData.villagerAgeTicks = new int[num];
			villageSaveData.villagerAlive = new bool[num];
			villageSaveData.villagerGold = new int[num];
			villageSaveData.villagerHunger = new int[num];
			villageSaveData.villagerFear = new int[num];
			villageSaveData.villagerFood = new int[num];
			villageSaveData.villagerBirthCooldown = new int[num];
			villageSaveData.villagerChildren = new int[num];
			villageSaveData.villagerInnate = new uint[num];
			villageSaveData.villagerMarks = new uint[num];
			for (int i = 0; i < num; i++)
			{
				villageSaveData.villagerNames[i] = GetVillagerName(seed, i);
				villageSaveData.villagerGenders[i] = i % 2;
								villageSaveData.villagerAges[i] = Cfg.InitAge(i, seed);
				villageSaveData.villagerAlive[i] = true;
				villageSaveData.villagerGold[i] = 8 + (i * 17 + 23) % 90;
				villageSaveData.villagerHunger[i] = 4 + (i * 13 + 31) % 46;
				villageSaveData.villagerFear[i] = 3 + (i * 7 + 12) % 52;
				villageSaveData.villagerFood[i] = 8 + (i * 11 + 7) % 33;
				villageSaveData.villagerInnate[i] = Traits.RollInnate(seed, i);
				villageSaveData.villagerMarks[i] = 0u;
			}
			VillageMap villageMap = VillageMapGenerator.Generate(seed);
			villageSaveData.mapSeed = seed;
			villageSaveData.mapGenVersion = 5;
			villageSaveData.terrain = (byte[])villageMap.terrain.Clone();
			villageSaveData.terrainRevision = 0;
			villageSaveData.arableBaseline = villageMap.arableCount;
			villageSaveData.lawNames[0] = "济贫";
			villageSaveData.lawNames[1] = "耕作";
			villageSaveData.lawNames[2] = "税收";
			villageSaveData.lawIds[0] = 1;
			villageSaveData.lawIds[1] = 5;
			villageSaveData.lawIds[2] = 3;
			return villageSaveData;
		}
}
